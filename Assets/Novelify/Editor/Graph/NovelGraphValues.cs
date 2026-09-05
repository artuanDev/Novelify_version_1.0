using System.Collections.Generic;
using Unity.GraphToolkit.Editor;

namespace Novelify.Editor
{
    /// <summary>Shared import/preview resolution, including portals and character pass-through ports.</summary>
    internal static class NovelGraphValues
    {
        public static T Resolve<T>(NovelGraph graph, IPort input) => Resolve<T>(graph, input, new HashSet<IPort>());

        private static T Resolve<T>(NovelGraph graph, IPort input, HashSet<IPort> visited)
        {
            if (input == null || !visited.Add(input)) return default;
            var connected = new List<IPort>();
            input.GetConnectedPorts(connected);
            foreach (IPort source in connected)
                if (TryResolveSource(graph, source, visited, out T value)) return value;
            return input.TryGetValue(out T fallback) ? fallback : default;
        }

        private static bool TryResolveSource<T>(NovelGraph graph, IPort source, HashSet<IPort> visited, out T value)
        {
            value = default;
            if (source == null) return false;
            INode node = source.GetNode();
            if (node is IVariableNode variable) return variable.Variable.TryGetDefaultValue(out value);
            string inputName = node is DialogueNode && source.Name == "Current Speaker" ? "Speaker" :
                node is CharacterActionNode && source.Name == "Character" ? "Character" : null;
            if (inputName == null) return false;
            value = Resolve<T>(graph, node.GetInputPortByName(inputName), visited);
            return true;
        }

        public static INode FlowDestination(NovelGraph graph, IPort output)
        {
            if (output == null) return null;
            return output.FirstConnectedPort?.GetNode();
        }
    }
}
