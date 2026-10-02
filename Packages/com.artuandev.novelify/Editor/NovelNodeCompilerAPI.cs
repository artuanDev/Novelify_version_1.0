using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace Novelify.Editor
{
    /// <summary>Compile an editor-only flow node to serializable runtime data.</summary>
    /// <remarks>Implement in an Editor assembly with a public parameterless constructor. Novelify discovers implementations on domain reload.</remarks>
    public interface INovelFlowNodeCompiler
    {
        bool CanCompile(INode node);
        RuntimeNode Compile(INode node, NovelNodeCompileContext context);
    }

    /// <summary>Compile an editor-only value node's output port to a runtime expression.</summary>
    public interface INovelValueNodeCompiler
    {
        bool CanCompile(INode node);
        RuntimeValueExpression Compile(IPort output, NovelNodeCompileContext context);
    }

    /// <summary>Services available to third-party node compilers during graph import.</summary>
    public sealed class NovelNodeCompileContext
    {
        private readonly Func<IPort, RuntimeValueExpression> _expression;
        private readonly Func<IPort, string> _destination;
        private readonly Graph _graph;
        private readonly AssetImportContext _importContext;

        internal NovelNodeCompileContext(
            AssetImportContext importContext,
            Graph graph,
            Func<IPort, RuntimeValueExpression> expression,
            Func<IPort, string> destination)
        {
            _importContext = importContext;
            _graph = graph;
            _expression = expression;
            _destination = destination;
        }

        public RuntimeValueExpression Expression(IPort port) => _expression(port);
        public Graph Graph => _graph;
        public string AssetPath => _importContext?.assetPath ?? string.Empty;
        public string NodeID(INode node) => node?.ID.ToString() ?? string.Empty;
        public RuntimeValueExpression Expression(INode node, string inputName) =>
            Expression(node?.GetInputPortByName(inputName));
        public string Destination(IPort output) => _destination(output);
        public string Destination(INode node, string outputName = "out") =>
            Destination(node?.GetOutputPortByName(outputName));

        public T PortValue<T>(INode node, string inputName) =>
            PortValue<T>(node?.GetInputPortByName(inputName));

        public T PortValue<T>(IPort port)
        {
            if (port == null) return default;
            T value = NovelGraphValues.Resolve<T>(_graph, port);
            if (value is UnityEngine.Object asset && asset != null)
                DependsOn(AssetDatabase.GetAssetPath(asset));
            return value;
        }

        public T OptionValue<T>(INode node, string optionName, T fallback = default)
        {
            INodeOption option = node?.GetNodeOptionByName(optionName);
            return option != null && option.TryGetValue(out T value) ? value : fallback;
        }

        public void Warning(string message) => _importContext?.LogImportWarning(message);
        public void Error(string message) => _importContext?.LogImportError(message);
        public void DependsOn(string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath)) _importContext?.DependsOnSourceAsset(assetPath);
        }
    }

    internal static class NovelNodeCompilerCatalog
    {
        private static readonly INovelFlowNodeCompiler[] Flow = Discover<INovelFlowNodeCompiler>();
        private static readonly INovelValueNodeCompiler[] Values = Discover<INovelValueNodeCompiler>();

        private static T[] Discover<T>()
        {
            var result = new List<T>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<T>()
                         .Where(type => !type.IsAbstract && !type.IsInterface &&
                             type.GetConstructor(Type.EmptyTypes) != null)
                         .OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                try { result.Add((T)Activator.CreateInstance(type)); }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogError($"Novelify could not create compiler {type.FullName}: {exception}");
                }
            }
            return result.ToArray();
        }

        internal static bool IsValueNode(INode node)
        {
            foreach (INovelValueNodeCompiler compiler in Values)
            {
                try { if (compiler.CanCompile(node)) return true; }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogError($"Novelify value compiler {compiler.GetType().FullName} failed: {exception}");
                }
            }
            return false;
        }

        internal static bool TryCompileFlow(INode node, NovelNodeCompileContext context, out RuntimeNode result)
        {
            result = null;
            INovelFlowNodeCompiler selected = null;
            foreach (INovelFlowNodeCompiler compiler in Flow)
            {
                bool claims;
                try { claims = compiler.CanCompile(node); }
                catch (Exception exception)
                {
                    context.Error($"{compiler.GetType().FullName} failed: {exception}");
                    continue;
                }
                if (!claims) continue;
                if (selected != null)
                {
                    context.Error($"Multiple Novelify flow compilers claim {node.GetType().FullName}.");
                    return true;
                }
                selected = compiler;
            }
            if (selected == null) return false;
            try { result = selected.Compile(node, context); }
            catch (Exception exception) { context.Error($"{selected.GetType().FullName} failed: {exception}"); }
            if (result == null) context.Error($"{selected.GetType().FullName} returned no runtime node.");
            return true;
        }

        internal static bool TryCompileValue(IPort output, NovelNodeCompileContext context, out RuntimeValueExpression result)
        {
            result = null;
            INode node = output.GetNode();
            INovelValueNodeCompiler selected = null;
            foreach (INovelValueNodeCompiler compiler in Values)
            {
                bool claims;
                try { claims = compiler.CanCompile(node); }
                catch (Exception exception)
                {
                    context.Error($"{compiler.GetType().FullName} failed: {exception}");
                    continue;
                }
                if (!claims) continue;
                if (selected != null)
                {
                    context.Error($"Multiple Novelify value compilers claim {node.GetType().FullName}.");
                    return true;
                }
                selected = compiler;
            }
            if (selected == null) return false;
            try { result = selected.Compile(output, context); }
            catch (Exception exception) { context.Error($"{selected.GetType().FullName} failed: {exception}"); }
            if (result == null) context.Error($"{selected.GetType().FullName} returned no expression.");
            return true;
        }
    }
}
