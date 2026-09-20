using System;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Novelify.Editor
{
    /// <summary>
    /// Applies Novelify's visual language to Graph Toolkit without changing graph data.
    /// Story graphs use cool editorial blues; reusable function graphs use warm amber.
    /// </summary>
    [InitializeOnLoad]
    internal static class NovelGraphThemeBootstrap
    {
        private const string ThemePath =
            "Assets/Novelify/Editor/Graph/Styles/NovelGraphTheme.uss";
        private const string IdentityName = "novelify-graph-identity";
        private const double ScanIntervalSeconds = 0.75d;

        private static readonly string[] CategoryClasses =
        {
            "novelify-node-start", "novelify-node-end", "novelify-node-story",
            "novelify-node-choice", "novelify-node-character",
            "novelify-node-presentation", "novelify-node-audio",
            "novelify-node-transition", "novelify-node-logic", "novelify-node-flow",
            "novelify-node-utility"
        };

        private static StyleSheet _theme;
        private static double _nextScanTime;

        static NovelGraphThemeBootstrap()
        {
            EditorApplication.update += ThemeOpenNovelGraphWindows;
            EditorApplication.delayCall += ThemeOpenNovelGraphWindows;
        }

        [MenuItem("Window/Novelify/Reapply Graph Theme", false, 2100)]
        private static void ReapplyTheme()
        {
            _theme = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemePath);
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (TryGetNovelGraph(window, out Graph graph))
                    AttachTheme(window.rootVisualElement, graph, true);
        }

        private static void ThemeOpenNovelGraphWindows()
        {
            if (EditorApplication.timeSinceStartup < _nextScanTime) return;
            _nextScanTime = EditorApplication.timeSinceStartup + ScanIntervalSeconds;
            _theme ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemePath);
            if (_theme == null) return;

            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (TryGetNovelGraph(window, out Graph graph))
                    AttachTheme(window.rootVisualElement, graph, false);
        }

        private static bool TryGetNovelGraph(EditorWindow window, out Graph graph)
        {
            graph = null;
            if (window is not IGraphWindow graphWindow) return false;
            if (graphWindow.Graph is not NovelGraph && graphWindow.Graph is not NovelFunctionGraph)
                return false;
            graph = graphWindow.Graph;
            return true;
        }

        private static void AttachTheme(VisualElement root, Graph graph, bool forceRefresh)
        {
            bool functionGraph = graph is NovelFunctionGraph;
            root.AddToClassList("novelify-graph-window");
            root.EnableInClassList("novelify-story-graph", !functionGraph);
            root.EnableInClassList("novelify-function-graph", functionGraph);

            if (forceRefresh && root.styleSheets.Contains(_theme)) root.styleSheets.Remove(_theme);
            if (!root.styleSheets.Contains(_theme)) root.styleSheets.Add(_theme);

            EnsureIdentity(root, functionGraph);
            ThemeNodeViews(root, graph);
        }

        private static void EnsureIdentity(VisualElement root, bool functionGraph)
        {
            VisualElement identity = root.Q<VisualElement>(IdentityName);
            if (identity == null)
            {
                identity = new VisualElement { name = IdentityName, pickingMode = PickingMode.Ignore };
                identity.AddToClassList("novelify-graph-identity");
                identity.Add(new Label { name = "novelify-graph-eyebrow", pickingMode = PickingMode.Ignore });
                identity.Add(new Label { name = "novelify-graph-title", pickingMode = PickingMode.Ignore });
                root.Add(identity);
            }

            identity.EnableInClassList("is-function", functionGraph);
            identity.Q<Label>("novelify-graph-eyebrow").text =
                functionGraph ? "REUSABLE SEQUENCE" : "INTERACTIVE STORY";
            identity.Q<Label>("novelify-graph-title").text =
                functionGraph ? "FUNCTION CANVAS" : "NOVEL CANVAS";
            identity.BringToFront();
        }

        private static void ThemeNodeViews(VisualElement element, Graph graph)
        {
            if (IsViewType(element, "NodeView"))
            {
                object nodeModel = ReadMember(element, "NodeModel");
                INode node = FindPublicNode(graph, nodeModel);
                foreach (string categoryClass in CategoryClasses)
                    element.RemoveFromClassList(categoryClass);
                element.AddToClassList(CategoryFor(node));
            }

            for (int i = 0; i < element.hierarchy.childCount; i++)
                ThemeNodeViews(element.hierarchy[i], graph);
        }

        private static string CategoryFor(INode node)
        {
            if (node == null) return "novelify-node-utility";
            if (node is StartNode) return "novelify-node-start";
            if (node is EndNode) return "novelify-node-end";
            if (node is ChoiceNode) return "novelify-node-choice";
            if (node is SpeechBubbleNode || node is SimpleDialogueNode)
                return "novelify-node-story";
            if (node is CharacterActionNode || node is SplitNovelCharacterNode ||
                node is MakeNovelCharacterReferenceNode || node is SplitNovelCharacterReferenceNode)
                return "novelify-node-character";
            if (node is SpeechBubblePresentationNode || node is CreateDialogueBoxNode ||
                node is CreateDialogueSpeakerBoxNode || node is ChangeDialogueBackgroundStyleNode ||
                node is ResetDialogueStyleNode)
                return "novelify-node-presentation";
            if (node is PlaySoundNode || node is PlayMusicNode || node is StopSoundNode ||
                node is StopAudioChannelNode)
                return "novelify-node-audio";
            if (node is FadeAuthoringNode) return "novelify-node-transition";

            string name = node.GetType().Name;
            if (name.Contains("Variable", StringComparison.Ordinal) ||
                name.Contains("Value", StringComparison.Ordinal) ||
                name.Contains("Random", StringComparison.Ordinal) ||
                name.Contains("Float", StringComparison.Ordinal) ||
                name.Contains("Vector", StringComparison.Ordinal) ||
                name.StartsWith("And", StringComparison.Ordinal) ||
                name.StartsWith("Or", StringComparison.Ordinal) ||
                name.StartsWith("Not", StringComparison.Ordinal))
                return "novelify-node-logic";
            if (node is LabelNode || node is JumpNode ||
                node is BranchNovelNode || node is WaitNode || node is CheckpointNode ||
                node is CallNovelPageNode)
                return "novelify-node-flow";
            return "novelify-node-utility";
        }

        private static INode FindPublicNode(Graph graph, object nodeModel)
        {
            if (graph == null || nodeModel == null) return null;
            foreach (INode node in graph.GetNodes())
            {
                object publicModel = ReadMember(node, "NodeModel") ?? ReadMember(node, "m_Implementation");
                if (ReferenceEquals(publicModel, nodeModel) ||
                    ReferenceEquals(ReadMember(publicModel, "NodeModel"), nodeModel))
                    return node;
            }
            return null;
        }

        private static bool IsViewType(VisualElement view, string typeName)
        {
            Type type = view.GetType();
            while (type != null)
            {
                if (type.Name == typeName && type.Namespace == "Unity.GraphToolkit.Editor") return true;
                type = type.BaseType;
            }
            return false;
        }

        private static object ReadMember(object source, string name)
        {
            if (source == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = source.GetType();
            while (type != null)
            {
                try
                {
                    foreach (PropertyInfo property in type.GetProperties(flags | BindingFlags.DeclaredOnly))
                        if (property.GetIndexParameters().Length == 0 &&
                            (property.Name == name || property.Name.EndsWith("." + name, StringComparison.Ordinal)))
                            return property.GetValue(source);
                    FieldInfo field = type.GetField(name, flags | BindingFlags.DeclaredOnly);
                    if (field != null) return field.GetValue(source);
                }
                catch { /* Views can be detached during a scan. */ }
                type = type.BaseType;
            }
            return null;
        }
    }

    [DataTypeStyleMapper(typeof(NovelGraph))]
    internal sealed class NovelGraphDataTypeStyles : DataTypeStyleMapper
    {
        public NovelGraphDataTypeStyles() =>
            Register(typeof(NovelCharacter), null, new Color32(45, 212, 191, 255));
    }
}
