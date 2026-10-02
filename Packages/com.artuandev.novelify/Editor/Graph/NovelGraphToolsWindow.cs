using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Novelify.Editor
{
    internal sealed class NovelGraphToolsWindow : EditorWindow
    {
        private EditorWindow _source;
        private string _search = string.Empty;
        private bool _bookmarksOnly;
        private Vector2 _scroll;
        private List<NovelGraphCheck> _checks = new();
        private bool _hasChecked;

        [MenuItem("Window/Novelify/Graph Tools", false, 2101)]
        private static void OpenFromMenu() => Open(NovelQualityOfLifeBridge.ResolveWindow());

        internal static void Open(EditorWindow source)
        {
            var window = GetWindow<NovelGraphToolsWindow>("Novelify Graph Tools");
            window._source = source;
            window.minSize = new Vector2(460f, 340f);
            window._checks.Clear();
            window._hasChecked = false;
            window.Show();
        }

        private static string BookmarkKey(Graph graph, INode node) =>
            "Novelify.Bookmark." + Application.dataPath + "." + graph.AssetGuid + "." + node.ID;

        internal static bool IsBookmarked(Graph graph, INode node) => EditorPrefs.GetBool(BookmarkKey(graph, node), false);
        internal static void ToggleBookmark(Graph graph, INode node) =>
            EditorPrefs.SetBool(BookmarkKey(graph, node), !IsBookmarked(graph, node));

        private void OnGUI()
        {
            if (!NovelQualityOfLifeBridge.IsNovelWindow(_source))
            {
                EditorGUILayout.HelpBox("Open a Novel Graph or Novel Function, then choose Graph Tools " +
                    "or press Ctrl/Cmd+Shift+F in the graph.", MessageType.Info);
                if (GUILayout.Button("Use Open Graph")) _source = NovelQualityOfLifeBridge.ResolveWindow();
                return;
            }
            Graph graph = ((IGraphWindow)_source).Graph;
            string path = AssetDatabase.GUIDToAssetPath(graph.AssetGuid.ToString());
            EditorGUILayout.LabelField(string.IsNullOrEmpty(path) ? _source.titleContent.text : path, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Search titles, dialogue, labels, character names, and node IDs. " +
                "Bookmarks stay in your local editor preferences. Checks report suggestions without changing the graph.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                _bookmarksOnly = GUILayout.Toggle(_bookmarksOnly, "Bookmarks", EditorStyles.toolbarButton, GUILayout.Width(85f));
                if (GUILayout.Button("Check Graph", EditorStyles.toolbarButton, GUILayout.Width(95f)))
                { _checks = NovelGraphChecks.Analyze(graph); _hasChecked = true; }
                if (GUILayout.Button("Use Active", EditorStyles.toolbarButton, GUILayout.Width(85f)))
                { _source = NovelQualityOfLifeBridge.ResolveWindow(); _checks.Clear(); _hasChecked = false; }
            }
            List<INode> nodes = graph.GetNodes().Where(node => Matches(graph, node, _search) &&
                (!_bookmarksOnly || IsBookmarked(graph, node))).ToList();
            EditorGUILayout.LabelField($"{nodes.Count} matching nodes");
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (INode node in nodes)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(IsBookmarked(graph, node) ? "★" : "☆", GUILayout.Width(28f))) ToggleBookmark(graph, node);
                    if (GUILayout.Button(new GUIContent(node.Title, SearchableText(graph, node)), GUILayout.MinWidth(150f))) Locate(node);
                    if (node is CharacterAnimationNode animation && GUILayout.Button("Preview", GUILayout.Width(65f)))
                        NovelAnimationPreviewWindow.Open((IGraphWindow)_source, animation);
                    if (GUILayout.Button("Copy ID", GUILayout.Width(65f))) EditorGUIUtility.systemCopyBuffer = node.ID.ToString();
                }
            }
            if (_checks.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Graph check results", EditorStyles.boldLabel);
                foreach (NovelGraphCheck check in _checks)
                {
                    EditorGUILayout.HelpBox(check.Message, MessageType.Warning);
                    if (check.Node != null && GUILayout.Button("Locate " + check.Node.Title)) Locate(check.Node);
                }
            }
            EditorGUILayout.EndScrollView();
            if (_hasChecked && _checks.Count == 0)
                EditorGUILayout.HelpBox("No common authoring issues found.", MessageType.Info);
        }

        internal static bool Matches(Graph graph, INode node, string search) =>
            string.IsNullOrWhiteSpace(search) || SearchableText(graph, node).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

        private static string SearchableText(Graph graph, INode node)
        {
            var parts = new List<string> { node.Title, node.GetType().Name, node.ID.ToString() };
            foreach (IPort port in node.GetInputPorts())
            {
                if (port.DataType == typeof(string) && port.TryGetValue(out string value)) parts.Add(value);
                if (port.DataType == typeof(NovelCharacter))
                {
                    NovelCharacter character = NovelGraphValues.Resolve<NovelCharacter>(graph, port);
                    if (character != null) { parts.Add(character.name); parts.Add(character.SpeakerName); }
                }
            }
            foreach (INodeOption option in node.NodeOptions)
            {
                if (option.TryGetValue(out string text)) parts.Add(text);
                if (option.TryGetValue(out RichDialogueText dialogue)) parts.Add(dialogue.Text);
            }
            return string.Join(" ", parts.Where(part => !string.IsNullOrEmpty(part)));
        }

        private void Locate(INode node)
        {
            if (!((IGraphWindow)_source).Graph.GetNodes().Any(candidate => candidate.ID == node.ID)) return;
            if (!FrameNode(_source, node))
                ShowNotification(new GUIContent("This Graph Toolkit version could not frame the node."));
        }

        internal static bool FrameNode(EditorWindow window, INode node)
        {
            // Graph Toolkit's model API is public, but its canvas view is internal.
            // Keep the adapter optional and isolated from graph authoring/runtime logic.
            VisualElement graphView = FindView(window.rootVisualElement, "GraphView");
            if (graphView == null) return false;
            object model = NovelQualityOfLifeBridge.Read(node, "NodeModel") ?? NovelQualityOfLifeBridge.Read(node, "m_Implementation");
            model = NovelQualityOfLifeBridge.Read(model, "NodeModel") ?? model;
            VisualElement nodeView = FindNodeView(window.rootVisualElement, model);
            if (nodeView == null) return false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo update = graphView.GetType().GetMethod("UpdateViewTransform", flags, null,
                new[] { typeof(Vector3), typeof(Vector3) }, null);
            if (update == null) return false;
            Vector2 size = nodeView.layout.size;
            if (!float.IsFinite(size.x) || !float.IsFinite(size.y) || size.x <= 0f || size.y <= 0f) return false;
            Rect viewport = graphView.contentRect;
            float zoom = Mathf.Min(1f, Mathf.Min(viewport.width * 0.85f / size.x, viewport.height * 0.85f / size.y));
            Vector2 position = viewport.center - (node.Position + size * 0.5f) * zoom;
            try
            {
                update.Invoke(graphView, new object[] { new Vector3(position.x, position.y, 0f), new Vector3(zoom, zoom, 1f) });
                window.Focus();
                nodeView.Focus();
                nodeView.style.borderTopColor = nodeView.style.borderBottomColor =
                    nodeView.style.borderLeftColor = nodeView.style.borderRightColor = new Color(1f, 0.75f, 0.2f);
                nodeView.schedule.Execute(() =>
                {
                    nodeView.style.borderTopColor = nodeView.style.borderBottomColor =
                        nodeView.style.borderLeftColor = nodeView.style.borderRightColor = StyleKeyword.Null;
                }).StartingIn(1500);
                return true;
            }
            catch (TargetInvocationException) { return false; }
        }

        private static VisualElement FindView(VisualElement root, string name)
        {
            if (NovelQualityOfLifeBridge.IsView(root, name)) return root;
            foreach (VisualElement child in root.Children())
            {
                VisualElement result = FindView(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private static VisualElement FindNodeView(VisualElement root, object model)
        {
            if (NovelQualityOfLifeBridge.IsView(root, "NodeView") &&
                ReferenceEquals(NovelQualityOfLifeBridge.Read(root, "NodeModel"), model)) return root;
            foreach (VisualElement child in root.Children())
            {
                VisualElement result = FindNodeView(child, model);
                if (result != null) return result;
            }
            return null;
        }
    }

    internal readonly struct NovelGraphCheck
    {
        internal readonly INode Node;
        internal readonly string Message;
        internal NovelGraphCheck(INode node, string message) { Node = node; Message = message; }
    }

    internal static class NovelGraphChecks
    {
        internal static List<NovelGraphCheck> Analyze(Graph graph)
        {
            var checks = new List<NovelGraphCheck>();
            List<INode> nodes = graph.GetNodes().ToList();
            List<INode> starts = nodes.Where(node => node is StartNode).ToList();
            if (graph is NovelGraph && starts.Count != 1)
                checks.Add(new NovelGraphCheck(starts.FirstOrDefault(), $"Story graph has {starts.Count} Start nodes; expected one."));
            var labels = nodes.OfType<LabelNode>().GroupBy(node =>
                NovelGraphValues.Resolve<string>(graph, node.GetInputPortByName("Label")) ?? string.Empty);
            foreach (var group in labels)
                if (string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
                    foreach (INode label in group)
                        checks.Add(new NovelGraphCheck(label, string.IsNullOrWhiteSpace(group.Key) ? "Label name is empty." : "Duplicate Label: " + group.Key));
            foreach (INode node in nodes)
            {
                IPort input = node.GetInputPortByName("in");
                if (input != null && !input.IsConnected && node is not LabelNode)
                    checks.Add(new NovelGraphCheck(node, node.Title + ": Enter has no connection."));
                foreach (IPort output in node.GetOutputPorts())
                    if ((output.Name == "out" || output.Name == "True" || output.Name == "False" ||
                        node is ChoiceNode && output.Name.StartsWith("Choice ", StringComparison.Ordinal)) && !output.IsConnected)
                        checks.Add(new NovelGraphCheck(node, node.Title + ": " + output.Name + " has no continuation."));
                if (node is CharacterActionNode && node is not StopCharacterAnimationNode)
                {
                    IPort character = node.GetInputPortByName("Character");
                    IPort reference = node.GetInputPortByName("Character Reference");
                    if (character?.IsConnected != true && reference?.IsConnected != true &&
                        NovelGraphValues.Resolve<NovelCharacter>(graph, character) == null &&
                        NovelGraphValues.Resolve<NovelCharacterReference>(graph, reference).Character == null)
                        checks.Add(new NovelGraphCheck(node, node.Title + ": assign a Character or Character Reference."));
                }
                if (node is CharacterAnimationNode)
                {
                    foreach (string name in new[] { "Amplitude", "Frequency", "Duration" })
                    {
                        IPort port = node.GetInputPortByName(name);
                        if (port?.IsConnected == true) continue;
                        float value = NovelGraphValues.Resolve<float>(graph, port);
                        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || name != "Duration" && value == 0f)
                            checks.Add(new NovelGraphCheck(node, node.Title + ": " + name + " prevents a visible animation or is invalid."));
                    }
                }
                if (node is JumpNode)
                {
                    IPort label = node.GetInputPortByName("Label");
                    if (label?.IsConnected == true) continue;
                    string name = NovelGraphValues.Resolve<string>(graph, label);
                    if (string.IsNullOrWhiteSpace(name) || !nodes.OfType<LabelNode>().Any(candidate =>
                        NovelGraphValues.Resolve<string>(graph, candidate.GetInputPortByName("Label")) == name))
                        checks.Add(new NovelGraphCheck(node, "Jump has no matching Label: " + name));
                }
            }
            return checks;
        }
    }
}
