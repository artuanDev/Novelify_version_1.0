using System;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Novelify.Editor
{
    [InitializeOnLoad]
    internal static class NovelQualityOfLifeBridge
    {
        private const string Hook = "novelify-qol-hook";
        private static double _nextScan;
        internal static EditorWindow LastGraphWindow;

        static NovelQualityOfLifeBridge() => EditorApplication.update += Scan;

        internal static bool IsNovelWindow(EditorWindow window) => window != null && window is IGraphWindow graphWindow &&
            (graphWindow.Graph is NovelGraph || graphWindow.Graph is NovelFunctionGraph);

        private static void Scan()
        {
            if (EditorApplication.timeSinceStartup < _nextScan) return;
            _nextScan = EditorApplication.timeSinceStartup + 0.5d;
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (!IsNovelWindow(window) || window.rootVisualElement.panel == null) continue;
                VisualElement root = window.rootVisualElement;
                if (root.ClassListContains(Hook)) continue;
                root.AddToClassList(Hook);
                INode lastClick = null;
                double lastTime = 0d;
                Vector2 lastPosition = Vector2.zero;
                root.RegisterCallback<PointerDownEvent>(evt =>
                {
                    LastGraphWindow = window;
                    if (evt.button != 0) return;
                    INode node = NodeAt((IGraphWindow)window, evt.target as VisualElement);
                    if (node is not CharacterAnimationNode animation) { lastClick = null; return; }
                    // Input fields retain their ordinary double-click text selection.
                    for (VisualElement target = evt.target as VisualElement;
                         target != null && !IsView(target, "NodeView"); target = target.parent)
                        if (target is TextField || target is FloatField || target is ObjectField ||
                            target is Button || IsView(target, "Port")) { lastClick = null; return; }
                    double now = EditorApplication.timeSinceStartup;
                    bool doubleClick = evt.clickCount == 2 || ReferenceEquals(lastClick, node) &&
                        now - lastTime <= 0.4d && Vector2.Distance(lastPosition, (Vector2)evt.position) <= 6f;
                    lastClick = node;
                    lastTime = now;
                    lastPosition = evt.position;
                    if (!doubleClick) return;
                    lastClick = null;
                    NovelAnimationPreviewWindow.Open((IGraphWindow)window, animation);
                    evt.StopImmediatePropagation();
                }, TrickleDown.TrickleDown);
                root.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if ((evt.ctrlKey || evt.commandKey) && evt.shiftKey && evt.keyCode == KeyCode.F)
                    {
                        LastGraphWindow = window;
                        NovelGraphToolsWindow.Open(window);
                        evt.StopPropagation();
                    }
                }, TrickleDown.TrickleDown);
                root.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    INode node = NodeAt((IGraphWindow)window, evt.target as VisualElement);
                    if (node == null) return;
                    if (node is CharacterAnimationNode animation)
                        evt.menu.AppendAction("Novelify/Preview Animation", _ =>
                            NovelAnimationPreviewWindow.Open((IGraphWindow)window, animation));
                    evt.menu.AppendAction("Novelify/Toggle Bookmark", _ =>
                    {
                        NovelGraphToolsWindow.ToggleBookmark(((IGraphWindow)window).Graph, node);
                        NovelGraphToolsWindow.Open(window);
                    });
                    evt.menu.AppendAction("Novelify/Copy Node ID", _ => EditorGUIUtility.systemCopyBuffer = node.ID.ToString());
                    evt.menu.AppendAction("Novelify/Find Nodes and Check Graph", _ => NovelGraphToolsWindow.Open(window));
                }));
            }
        }

        internal static EditorWindow ResolveWindow()
        {
            if (IsNovelWindow(EditorWindow.focusedWindow)) return EditorWindow.focusedWindow;
            if (IsNovelWindow(LastGraphWindow)) return LastGraphWindow;
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (IsNovelWindow(window)) return window;
            return null;
        }

        internal static INode NodeAt(IGraphWindow window, VisualElement element)
        {
            for (; element != null; element = element.parent)
                if (IsView(element, "NodeView")) return FindNode(window.Graph, Read(element, "NodeModel"));
            return null;
        }

        internal static INode FindNode(Graph graph, object model)
        {
            if (model == null || graph == null) return null;
            foreach (INode node in graph.GetNodes())
            {
                object implementation = Read(node, "NodeModel") ?? Read(node, "m_Implementation");
                if (ReferenceEquals(implementation, model) || ReferenceEquals(Read(implementation, "NodeModel"), model))
                    return node;
            }
            return null;
        }

        internal static bool IsView(VisualElement view, string name)
        {
            for (Type type = view.GetType(); type != null; type = type.BaseType)
                if (type.Name == name && type.Namespace == "Unity.GraphToolkit.Editor") return true;
            return false;
        }

        internal static object Read(object source, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (Type type = source?.GetType(); type != null; type = type.BaseType)
            {
                try
                {
                    foreach (PropertyInfo property in type.GetProperties(flags))
                        if (property.GetIndexParameters().Length == 0 &&
                            (property.Name == name || property.Name.EndsWith("." + name, StringComparison.Ordinal)))
                            return property.GetValue(source);
                    FieldInfo field = type.GetField(name, flags);
                    if (field != null) return field.GetValue(source);
                }
                catch (TargetInvocationException) { return null; }
            }
            return null;
        }
    }
}
