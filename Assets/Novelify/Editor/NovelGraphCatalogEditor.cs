using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Novelify.Editor
{
    [CustomEditor(typeof(NovelGraphCatalog))]
    public sealed class NovelGraphCatalogInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            NovelGraphCatalog catalog = (NovelGraphCatalog)target;
            EditorGUILayout.HelpBox(
                "This generated catalogue maps persistent graph IDs to runtime graph assets in player builds. " +
                "Use the catalogue window to inspect it; edit the source .novelgraph files rather than this asset.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Catalogue Editor", GUILayout.Height(28f)))
                    NovelGraphCatalogWindow.Open(catalog);
                if (GUILayout.Button("Rebuild", GUILayout.Height(28f)))
                {
                    NovelGraphCatalog rebuilt = NovelGraphCatalogBuilder.RebuildCatalog(catalog);
                    EditorGUIUtility.PingObject(rebuilt);
                }
            }

            int graphCount = catalog.Graphs?.Count(entry => entry?.Graph != null && entry.Graph is not RuntimeNovelFunction) ?? 0;
            int functionCount = catalog.Graphs?.Count(entry => entry?.Graph is RuntimeNovelFunction) ?? 0;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Contents", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Story graphs", graphCount.ToString());
            EditorGUILayout.LabelField("Novel functions", functionCount.ToString());
            EditorGUILayout.LabelField("Generated (UTC)", string.IsNullOrEmpty(catalog.GeneratedAtUtc) ? "Unknown" : catalog.GeneratedAtUtc);
            EditorGUILayout.LabelField("Asset path", AssetDatabase.GetAssetPath(catalog));

            if (!NovelGraphCatalogLocation.IsDefault(catalog))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(
                    "This catalogue is outside the Resources folder, so NovelGraphCatalog.LoadDefault() cannot load it in a built game.",
                    MessageType.Warning);
                if (GUILayout.Button(NovelGraphCatalogLocation.ActionLabel()))
                    NovelGraphCatalogLocation.UseOrOpenDefault(catalog, null);
            }
        }
    }

    internal static class NovelGraphCatalogLocation
    {
        public static bool IsDefault(NovelGraphCatalog catalog) =>
            catalog != null && string.Equals(
                AssetDatabase.GetAssetPath(catalog),
                NovelGraphCatalogBuilder.CatalogPath,
                StringComparison.OrdinalIgnoreCase);

        public static string ActionLabel() =>
            AssetDatabase.LoadAssetAtPath<NovelGraphCatalog>(NovelGraphCatalogBuilder.CatalogPath) != null
                ? "Open Default Catalogue"
                : "Move This Catalogue to the Default Location";

        public static NovelGraphCatalog UseOrOpenDefault(
            NovelGraphCatalog current,
            NovelGraphCatalogWindow window)
        {
            NovelGraphCatalog existing = AssetDatabase.LoadAssetAtPath<NovelGraphCatalog>(NovelGraphCatalogBuilder.CatalogPath);
            if (existing != null)
            {
                if (window != null) window.SelectCatalog(existing);
                else NovelGraphCatalogWindow.Open(existing);
                EditorGUIUtility.PingObject(existing);
                return existing;
            }

            string currentPath = AssetDatabase.GetAssetPath(current);
            if (string.IsNullOrEmpty(currentPath)) return current;
            NovelGraphCatalogBuilder.EnsureDefaultFolder();
            string error = AssetDatabase.MoveAsset(currentPath, NovelGraphCatalogBuilder.CatalogPath);
            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Could not move catalogue", error, "OK");
                return current;
            }

            AssetDatabase.SaveAssets();
            NovelGraphCatalog moved = AssetDatabase.LoadAssetAtPath<NovelGraphCatalog>(NovelGraphCatalogBuilder.CatalogPath);
            NovelGraphCatalogBuilder.RebuildCatalog(moved);
            if (window != null) window.SelectCatalog(moved);
            EditorGUIUtility.PingObject(moved);
            return moved;
        }
    }

    public sealed class NovelGraphCatalogWindow : EditorWindow
    {
        private enum EntryFilter { All, StoryGraphs, Functions, Problems }

        [SerializeField] private NovelGraphCatalog _catalog;
        [SerializeField] private string _search = string.Empty;
        [SerializeField] private EntryFilter _filter;
        [SerializeField] private string _selectedGraphID;
        [SerializeField] private string _selectedNodeID;
        [SerializeField] private Vector2 _listScroll;
        [SerializeField] private Vector2 _detailScroll;

        private GUIStyle _entryStyle;
        private GUIStyle _selectedEntryStyle;
        private GUIStyle _nodeStyle;
        private GUIStyle _selectedNodeStyle;
        private GUIStyle _wrappedMiniLabel;

        [MenuItem("Window/Novelify/Graph Catalogue")]
        public static void OpenWindow() => Open(
            AssetDatabase.LoadAssetAtPath<NovelGraphCatalog>(NovelGraphCatalogBuilder.CatalogPath));

        public static void Open(NovelGraphCatalog catalog)
        {
            NovelGraphCatalogWindow window = GetWindow<NovelGraphCatalogWindow>();
            window.titleContent = new GUIContent("Graph Catalogue", EditorGUIUtility.IconContent("d_UnityEditor.Graphs.AnimatorControllerTool").image);
            window.minSize = new Vector2(840f, 520f);
            window._catalog = catalog != null ? catalog : NovelGraphCatalogBuilder.RebuildCatalog();
            window.EnsureSelection();
            window.Show();
            window.Focus();
        }

        [OnOpenAsset(0)]
        private static bool OnOpenAsset(EntityId entityID, int line)
        {
            if (EditorUtility.EntityIdToObject(entityID) is not NovelGraphCatalog catalog) return false;
            Open(catalog);
            return true;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Graph Catalogue");
            if (_catalog == null)
                _catalog = AssetDatabase.LoadAssetAtPath<NovelGraphCatalog>(NovelGraphCatalogBuilder.CatalogPath);
            EditorApplication.projectChanged += OnProjectChanged;
            EnsureSelection();
        }

        private void OnDisable() => EditorApplication.projectChanged -= OnProjectChanged;

        private void OnProjectChanged()
        {
            if (_catalog == null)
                _catalog = AssetDatabase.LoadAssetAtPath<NovelGraphCatalog>(NovelGraphCatalogBuilder.CatalogPath);
            Repaint();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();

            if (_catalog == null)
            {
                EditorGUILayout.HelpBox("The runtime graph catalogue has not been generated yet.", MessageType.Info);
                if (GUILayout.Button("Create Runtime Graph Catalogue", GUILayout.Height(30f))) Rebuild();
                return;
            }

            DrawSummary();
            if (!NovelGraphCatalogLocation.IsDefault(_catalog)) DrawDefaultLocationPrompt();
            float leftWidth = Mathf.Clamp(position.width * 0.31f, 250f, 370f);
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(leftWidth), GUILayout.ExpandHeight(true)))
                    DrawEntryList();

                Rect divider = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
                EditorGUI.DrawRect(divider, new Color(0f, 0f, 0f, 0.38f));

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                    DrawSelectedGraph();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent(" Rebuild", EditorGUIUtility.IconContent("Refresh").image), EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    Rebuild();
                if (GUILayout.Button("Open Source", EditorStyles.toolbarButton, GUILayout.Width(85f))) OpenSelectedGraph();
                if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(45f))) PingSelectedGraph();
                GUILayout.Space(8f);
                _filter = (EntryFilter)EditorGUILayout.EnumPopup(_filter, EditorStyles.toolbarPopup, GUILayout.Width(105f));
                GUILayout.FlexibleSpace();
                _search = GUILayout.TextField(_search ?? string.Empty, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.Width(230f));
                if (!string.IsNullOrEmpty(_search) && GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(22f)))
                {
                    _search = string.Empty;
                    GUI.FocusControl(null);
                }
            }
        }

        private void DrawSummary()
        {
            List<NovelGraphCatalog.Entry> entries = ValidEntries().ToList();
            int stories = entries.Count(entry => entry.Graph is not RuntimeNovelFunction);
            int functions = entries.Count - stories;
            int problems = entries.Count(entry => Analyze(entry).Count > 0) + NullEntryCount();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"{entries.Count} compiled assets", EditorStyles.boldLabel);
                GUILayout.Space(12f);
                GUILayout.Label($"{stories} stories", EditorStyles.miniLabel);
                GUILayout.Label($"{functions} functions", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUIStyle status = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    normal = { textColor = problems == 0 ? new Color(0.35f, 0.75f, 0.45f) : new Color(1f, 0.62f, 0.25f) }
                };
                GUILayout.Label(problems == 0 ? "Catalogue healthy" : $"{problems} issue(s)", status);
            }
        }

        private void DrawDefaultLocationPrompt()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(
                    "This catalogue is outside Resources and cannot be loaded as the player default.",
                    new GUIStyle(EditorStyles.wordWrappedMiniLabel),
                    GUILayout.ExpandWidth(true));
                if (GUILayout.Button(NovelGraphCatalogLocation.ActionLabel(), GUILayout.Width(210f), GUILayout.Height(30f)))
                    NovelGraphCatalogLocation.UseOrOpenDefault(_catalog, this);
            }
        }

        internal void SelectCatalog(NovelGraphCatalog catalog)
        {
            _catalog = catalog;
            _selectedGraphID = null;
            _selectedNodeID = null;
            EnsureSelection();
            Repaint();
        }

        private void DrawEntryList()
        {
            GUILayout.Space(4f);
            GUILayout.Label("COMPILED GRAPHS", EditorStyles.miniBoldLabel);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            foreach (NovelGraphCatalog.Entry entry in FilteredEntries()) DrawEntry(entry);
            if (!FilteredEntries().Any())
                EditorGUILayout.HelpBox("No catalogue entries match the current search and filter.", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void DrawEntry(NovelGraphCatalog.Entry entry)
        {
            RuntimeNovelGraph graph = entry.Graph;
            string path = AssetDatabase.GetAssetPath(graph);
            string title = string.IsNullOrEmpty(path) ? graph.name : System.IO.Path.GetFileNameWithoutExtension(path);
            string kind = graph is RuntimeNovelFunction ? "FUNCTION" : "STORY";
            int issues = Analyze(entry).Count;
            string subtitle = $"{kind}  ·  {graph.AllNodes?.Count ?? 0} nodes" + (issues > 0 ? $"  ·  {issues} issue(s)" : string.Empty);
            bool selected = string.Equals(_selectedGraphID, entry.GraphID, StringComparison.Ordinal);
            GUIContent content = new GUIContent(title + "\n" + subtitle, GraphIcon(graph), path);
            Rect row = GUILayoutUtility.GetRect(content, selected ? _selectedEntryStyle : _entryStyle, GUILayout.Height(48f), GUILayout.ExpandWidth(true));
            if (GUI.Button(row, content, selected ? _selectedEntryStyle : _entryStyle))
            {
                if (selected && Event.current.clickCount == 2) OpenGraph(graph);
                _selectedGraphID = entry.GraphID;
                _selectedNodeID = null;
                GUI.FocusControl(null);
                Repaint();
            }
        }

        private void DrawSelectedGraph()
        {
            NovelGraphCatalog.Entry entry = SelectedEntry();
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.ExpandHeight(true));
            if (entry?.Graph == null)
            {
                EditorGUILayout.HelpBox("Select a graph to inspect its compiled runtime data.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            RuntimeNovelGraph graph = entry.Graph;
            string path = AssetDatabase.GetAssetPath(graph);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10f);
                GUILayout.Label(GraphIcon(graph), GUILayout.Width(48f), GUILayout.Height(48f));
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(System.IO.Path.GetFileNameWithoutExtension(path), new GUIStyle(EditorStyles.largeLabel) { fontStyle = FontStyle.Bold });
                    GUILayout.Label(graph is RuntimeNovelFunction ? "Reusable Novel Function" : "Novel Story Graph", EditorStyles.miniLabel);
                    GUILayout.Label(path, _wrappedMiniLabel);
                }
                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(6f);
            DrawMetadata(entry);
            DrawDiagnostics(entry);
            if (graph is RuntimeNovelFunction function) DrawFunctionInterface(function);
            DrawGraphPreview(graph);
            DrawSelectedNodeDetails(graph);
            EditorGUILayout.EndScrollView();
        }

        private void DrawMetadata(NovelGraphCatalog.Entry entry)
        {
            RuntimeNovelGraph graph = entry.Graph;
            EditorGUILayout.LabelField("Runtime metadata", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawSelectable("Graph ID", graph.GraphID);
                DrawSelectable("Content version", graph.ContentVersion);
                EditorGUILayout.LabelField("Schema version", graph.SchemaVersion.ToString());
                DrawSelectable("Entry node", graph.EntryNodeID);
            }
        }

        private static void DrawSelectable(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private void DrawDiagnostics(NovelGraphCatalog.Entry entry)
        {
            List<string> issues = Analyze(entry);
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("Stable IDs, entry point and node destinations are valid.", MessageType.Info);
                return;
            }
            foreach (string issue in issues) EditorGUILayout.HelpBox(issue, MessageType.Warning);
        }

        private static void DrawFunctionInterface(RuntimeNovelFunction function)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Function interface", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string inputs = function.Inputs == null || function.Inputs.Count == 0
                    ? "None"
                    : string.Join(", ", function.Inputs.Select(input => input.Name));
                string outputs = function.Outputs == null || function.Outputs.Count == 0
                    ? "None"
                    : string.Join(", ", function.Outputs.Select(output => output.Name));
                EditorGUILayout.LabelField("Inputs", inputs);
                EditorGUILayout.LabelField("Outputs", outputs);
            }
        }

        private void DrawGraphPreview(RuntimeNovelGraph graph)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Flow preview", EditorStyles.boldLabel);
            List<RuntimeNode> ordered = FlowOrder(graph);
            if (ordered.Count == 0)
            {
                EditorGUILayout.HelpBox("This graph has no reachable runtime nodes.", MessageType.Warning);
                return;
            }

            HashSet<string> reachable = new HashSet<string>(ordered.Select(node => node.NodeID), StringComparer.Ordinal);
            foreach (RuntimeNode node in ordered) DrawNodeCard(node, true);

            List<RuntimeNode> unreachable = (graph.AllNodes ?? new List<RuntimeNode>())
                .Where(node => node != null && !reachable.Contains(node.NodeID ?? string.Empty)).ToList();
            if (unreachable.Count > 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label($"UNREACHABLE ({unreachable.Count})", EditorStyles.miniBoldLabel);
                foreach (RuntimeNode node in unreachable) DrawNodeCard(node, false);
            }
        }

        private void DrawNodeCard(RuntimeNode node, bool reachable)
        {
            bool selected = string.Equals(_selectedNodeID, node.NodeID, StringComparison.Ordinal);
            string preview = NodePreview(node);
            GUIContent content = new GUIContent(NodeTitle(node) + "\n" + preview, NodeIcon(node));
            GUIStyle style = selected ? _selectedNodeStyle : _nodeStyle;
            Rect row = GUILayoutUtility.GetRect(content, style, GUILayout.Height(54f), GUILayout.ExpandWidth(true));
            if (!reachable) EditorGUI.DrawRect(new Rect(row.x, row.y, 3f, row.height), new Color(1f, 0.55f, 0.2f));
            if (GUI.Button(row, content, style)) _selectedNodeID = node.NodeID;

            string destinations = string.Join(", ", Destinations(node).Where(id => !string.IsNullOrEmpty(id)).Select(ShortID));
            if (!string.IsNullOrEmpty(destinations))
                GUILayout.Label("      ↓  " + destinations, EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawSelectedNodeDetails(RuntimeNovelGraph graph)
        {
            RuntimeNode node = graph.AllNodes?.FirstOrDefault(candidate => candidate?.NodeID == _selectedNodeID);
            if (node == null) return;
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Selected node", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(NodeTitle(node), EditorStyles.boldLabel);
                DrawSelectable("Node ID", node.NodeID);
                DrawSelectable("Continue", node.NextNodeID);

                NovelCharacter character = NodeCharacter(node);
                if (character != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Sprite portrait = character.PortraitBody;
                        Texture texture = portrait != null ? AssetPreview.GetAssetPreview(portrait) ?? AssetPreview.GetMiniThumbnail(portrait) : null;
                        GUILayout.Box(texture, GUILayout.Width(96f), GUILayout.Height(96f));
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.ObjectField("Character", character, typeof(NovelCharacter), false);
                            EditorGUILayout.LabelField("Speaker", character.SpeakerName ?? string.Empty);
                            GUILayout.Label(NodePreview(node), new GUIStyle(EditorStyles.wordWrappedLabel));
                        }
                    }
                }
                else
                {
                    GUILayout.Label(NodePreview(node), new GUIStyle(EditorStyles.wordWrappedLabel));
                }

                if (node is RuntimeChoiceNode choice && choice.Choices != null)
                {
                    for (int index = 0; index < choice.Choices.Count; index++)
                    {
                        ChoiceData option = choice.Choices[index];
                        string behavior = option == null ? string.Empty :
                            $"  [{option.UnavailablePolicy}{(option.OnceOnly ? ", once" : string.Empty)}" +
                            $"{(option.StateChanges?.Count > 0 ? $", {option.StateChanges.Count} changes" : string.Empty)}]";
                        EditorGUILayout.LabelField($"Choice {index + 1}",
                            $"{option?.ChoiceText ?? ""}{behavior}  →  {ShortID(option?.DestinationNodeID)}");
                    }
                    EditorGUILayout.LabelField("Fallback", ShortID(choice.UnavailableDestinationNodeID));
                }
            }
        }

        private void Rebuild()
        {
            _catalog = _catalog != null
                ? NovelGraphCatalogBuilder.RebuildCatalog(_catalog)
                : NovelGraphCatalogBuilder.RebuildCatalog();
            EnsureSelection();
            Repaint();
        }

        private void EnsureSelection()
        {
            if (_catalog?.Graphs == null) return;
            if (_catalog.Graphs.Any(entry => entry?.Graph != null && entry.GraphID == _selectedGraphID)) return;
            _selectedGraphID = _catalog.Graphs.FirstOrDefault(entry => entry?.Graph != null)?.GraphID;
            _selectedNodeID = null;
        }

        private NovelGraphCatalog.Entry SelectedEntry() =>
            _catalog?.Graphs?.FirstOrDefault(entry => entry?.Graph != null && entry.GraphID == _selectedGraphID);

        private IEnumerable<NovelGraphCatalog.Entry> ValidEntries() =>
            _catalog?.Graphs?.Where(entry => entry?.Graph != null) ?? Enumerable.Empty<NovelGraphCatalog.Entry>();

        private IEnumerable<NovelGraphCatalog.Entry> FilteredEntries()
        {
            string query = (_search ?? string.Empty).Trim();
            return ValidEntries()
                .Where(entry => _filter switch
                {
                    EntryFilter.StoryGraphs => entry.Graph is not RuntimeNovelFunction,
                    EntryFilter.Functions => entry.Graph is RuntimeNovelFunction,
                    EntryFilter.Problems => Analyze(entry).Count > 0,
                    _ => true
                })
                .Where(entry => string.IsNullOrEmpty(query) ||
                    entry.Graph.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (entry.GraphID?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    AssetDatabase.GetAssetPath(entry.Graph).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(entry => entry.Graph is RuntimeNovelFunction)
                .ThenBy(entry => entry.Graph.name, StringComparer.OrdinalIgnoreCase);
        }

        private int NullEntryCount() => _catalog?.Graphs?.Count(entry => entry?.Graph == null) ?? 0;

        private List<string> Analyze(NovelGraphCatalog.Entry entry)
        {
            var issues = new List<string>();
            RuntimeNovelGraph graph = entry?.Graph;
            if (graph == null) { issues.Add("Catalogue entry has no graph asset."); return issues; }
            if (string.IsNullOrEmpty(entry.GraphID)) issues.Add("Catalogue entry has no graph ID.");
            else if ((_catalog?.Graphs?.Count(candidate => candidate != null && candidate.GraphID == entry.GraphID) ?? 0) > 1)
                issues.Add($"Catalogue contains duplicate graph ID {ShortID(entry.GraphID)}.");
            if (!string.Equals(entry.GraphID, graph.GraphID, StringComparison.Ordinal)) issues.Add("Catalogue ID does not match the compiled graph ID. Rebuild the catalogue.");
            if (string.IsNullOrEmpty(graph.ContentVersion)) issues.Add("Compiled graph has no content version. Reimport its source graph.");
            if (graph.SchemaVersion != RuntimeNovelGraph.CurrentSchemaVersion) issues.Add($"Schema {graph.SchemaVersion} does not match runtime schema {RuntimeNovelGraph.CurrentSchemaVersion}.");

            List<RuntimeNode> nodes = graph.AllNodes ?? new List<RuntimeNode>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (RuntimeNode node in nodes)
            {
                if (node == null) { issues.Add("Graph contains a null runtime node."); continue; }
                if (string.IsNullOrEmpty(node.NodeID)) issues.Add("Graph contains a node without a stable ID.");
                else if (!ids.Add(node.NodeID)) issues.Add($"Duplicate node ID {ShortID(node.NodeID)}.");
            }
            if (string.IsNullOrEmpty(graph.EntryNodeID)) issues.Add("Graph has no entry node.");
            else if (!ids.Contains(graph.EntryNodeID)) issues.Add("Entry node does not exist in the compiled node list.");
            foreach (RuntimeNode node in nodes.Where(node => node != null))
                foreach (string destination in Destinations(node))
                    if (!string.IsNullOrEmpty(destination) && !ids.Contains(destination))
                        issues.Add($"{NodeTitle(node)} points to missing node {ShortID(destination)}.");

            int reachable = FlowOrder(graph).Count;
            int concreteNodes = nodes.Count(node => node != null);
            if (reachable < concreteNodes) issues.Add($"{concreteNodes - reachable} runtime node(s) are unreachable from the entry point.");
            return issues.Distinct().ToList();
        }

        private static List<RuntimeNode> FlowOrder(RuntimeNovelGraph graph)
        {
            var result = new List<RuntimeNode>();
            if (graph?.AllNodes == null || string.IsNullOrEmpty(graph.EntryNodeID)) return result;
            Dictionary<string, RuntimeNode> lookup = graph.AllNodes
                .Where(node => node != null && !string.IsNullOrEmpty(node.NodeID))
                .GroupBy(node => node.NodeID).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(graph.EntryNodeID);
            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                if (!seen.Add(id) || !lookup.TryGetValue(id, out RuntimeNode node)) continue;
                result.Add(node);
                foreach (string destination in Destinations(node))
                    if (!string.IsNullOrEmpty(destination) && !seen.Contains(destination)) queue.Enqueue(destination);
            }
            return result;
        }

        private static IEnumerable<string> Destinations(RuntimeNode node)
        {
            if (!string.IsNullOrEmpty(node?.NextNodeID)) yield return node.NextNodeID;
            if (node is RuntimeBranchNode branch)
            {
                if (!string.IsNullOrEmpty(branch.TrueNodeID)) yield return branch.TrueNodeID;
                if (!string.IsNullOrEmpty(branch.FalseNodeID)) yield return branch.FalseNodeID;
            }
            if (node is RuntimeChoiceNode choice && choice.Choices != null)
            {
                foreach (ChoiceData option in choice.Choices)
                    if (!string.IsNullOrEmpty(option?.DestinationNodeID)) yield return option.DestinationNodeID;
                if (!string.IsNullOrEmpty(choice.UnavailableDestinationNodeID))
                    yield return choice.UnavailableDestinationNodeID;
            }
        }

        private static string NodeTitle(RuntimeNode node)
        {
            if (node == null) return "Missing Node";
            string name = node.GetType().Name.Replace("Runtime", string.Empty).Replace("Node", string.Empty);
            return string.Concat(name.Select((character, index) => index > 0 && char.IsUpper(character) ? " " + character : character.ToString()));
        }

        private static string NodePreview(RuntimeNode node)
        {
            switch (node)
            {
                case RuntimeChoiceNode choice: return $"{Trim(choice.DialogueText, 70)}  ·  {choice.Choices?.Count ?? 0} choices";
                case RuntimeDialogueNode dialogue: return string.IsNullOrEmpty(dialogue.DialogueText) ? "Empty dialogue line" : Trim(dialogue.DialogueText, 90);
                case RuntimeWaitNode wait: return $"Wait {wait.Duration:0.###} dialogue-clock seconds";
                case RuntimeDialogueEventNode signal: return $"Event: {signal.EventName}";
                case RuntimeSetVariableNode set: return $"Set {VariableName(set.Variable)}";
                case RuntimeModifyVariableNode modify: return $"{modify.Operation} {VariableName(modify.Variable)}";
                case RuntimeBranchNode branch: return $"True → {ShortID(branch.TrueNodeID)}  ·  False → {ShortID(branch.FalseNodeID)}";
                case RuntimeCheckpointNode checkpoint:
                    return checkpoint.SaveMode == NovelCheckpointSaveMode.Autosave
                        ? $"{checkpoint.CheckpointID}  ·  autosave → {checkpoint.AutosaveSlotID}"
                        : $"{checkpoint.CheckpointID}  ·  snapshot only";
                case RuntimeShowCharacterNode show: return $"{CharacterName(show.Character)}  ·  {show.PositionSpace} {show.Position}";
                case RuntimeTransformSpeakerPortraitNode move: return $"{CharacterName(move.Character)}  ·  {move.PositionSpace} ({move.OffsetX:0.##}, {move.OffsetY:0.##})";
                case RuntimeSetCharacterFacingNode facing: return $"{CharacterName(facing.Character)} faces {facing.Facing}";
                case RuntimeFlipCharacterNode flip: return $"Toggle X: {flip.FlipX}, Y: {flip.FlipY}";
                case RuntimeSetCharacterEmotionNode emotion: return $"{CharacterName(emotion.Character)}  ·  {emotion.Emotion}";
                case RuntimeHideCharacterNode hide: return $"Hide {CharacterName(hide.Character)}";
                case RuntimeHideAllCharactersNode: return "Hide every character instance";
                case RuntimePlaySoundNode sound: return sound.ClipSound != null ? sound.ClipSound.name : "Dynamic or missing audio clip";
                case RuntimeStopSoundNode: return "Stop the story sound channel";
                case RuntimeCallNovelFunctionNode function: return function.Function != null ? $"Call {function.Function.name}" : "Missing function";
                case RuntimeCallNovelPageNode page: return page.Graph != null ? $"Call {page.Graph.name}" : "Missing graph";
                default: return string.IsNullOrEmpty(node?.NextNodeID) ? "End of flow" : "Continue";
            }
        }

        private static NovelCharacter NodeCharacter(RuntimeNode node) => node switch
        {
            RuntimeDialogueNode dialogue => dialogue.NovelCharacter,
            RuntimeTransformSpeakerPortraitNode transform => transform.Character,
            RuntimeSetCharacterFacingNode facing => facing.Character,
            RuntimeFlipCharacterNode flip => flip.Character,
            RuntimeShowCharacterNode show => show.Character,
            RuntimeHideCharacterNode hide => hide.Character,
            RuntimeSetCharacterEmotionNode emotion => emotion.Character,
            _ => null
        };

        private static Texture GraphIcon(RuntimeNovelGraph graph) =>
            EditorGUIUtility.IconContent(graph is RuntimeNovelFunction ? "d_Profiler.NetworkMessages" : "d_UnityEditor.Graphs.AnimatorControllerTool").image;

        private static Texture NodeIcon(RuntimeNode node)
        {
            string icon = node switch
            {
                RuntimeChoiceNode => "d_TreeEditor.Duplicate",
                RuntimeDialogueNode => "d_console.infoicon",
                RuntimePlaySoundNode => "d_AudioSource Icon",
                RuntimeWaitNode => "d_WaitSpin00",
                RuntimeDialogueEventNode => "d_EventSystem Icon",
                RuntimeSetVariableNode => "d_Animation.Record",
                RuntimeModifyVariableNode => "d_Animation.AddKeyframe",
                RuntimeBranchNode => "d_TreeEditor.Duplicate",
                RuntimeTransformSpeakerPortraitNode => "d_MoveTool",
                RuntimeShowCharacterNode => "d_SceneViewVisibility",
                RuntimeHideCharacterNode => "d_scenevis_hidden_hover",
                _ => "d_UnityEditor.Graphs.AnimatorControllerTool"
            };
            return EditorGUIUtility.IconContent(icon).image;
        }

        private static string VariableName(NovelVariableDefinition variable) =>
            variable == null ? "missing variable" :
            string.IsNullOrWhiteSpace(variable.DisplayName) ? variable.name : variable.DisplayName;

        private void OpenSelectedGraph()
        {
            RuntimeNovelGraph graph = SelectedEntry()?.Graph;
            if (graph != null) OpenGraph(graph);
        }

        private void PingSelectedGraph()
        {
            RuntimeNovelGraph graph = SelectedEntry()?.Graph;
            if (graph != null) EditorGUIUtility.PingObject(graph);
        }

        private static void OpenGraph(RuntimeNovelGraph graph)
        {
            if (graph == null) return;
            AssetDatabase.OpenAsset(graph);
        }

        private void EnsureStyles()
        {
            if (_entryStyle != null) return;
            _entryStyle = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 6, 5, 5), fontSize = 11 };
            _selectedEntryStyle = new GUIStyle(_entryStyle);
            _selectedEntryStyle.normal.background = MakeTexture(new Color(0.18f, 0.38f, 0.58f, 0.72f));
            _selectedEntryStyle.normal.textColor = Color.white;
            _nodeStyle = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 8, 6, 6), wordWrap = true };
            _selectedNodeStyle = new GUIStyle(_nodeStyle);
            _selectedNodeStyle.normal.background = MakeTexture(new Color(0.2f, 0.42f, 0.34f, 0.72f));
            _selectedNodeStyle.normal.textColor = Color.white;
            _wrappedMiniLabel = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static string CharacterName(NovelCharacter character) => character != null ? character.SpeakerName : "Dynamic character";
        private static string ShortID(string id) => string.IsNullOrEmpty(id) ? "None" : id.Substring(0, Mathf.Min(8, id.Length));
        private static string Trim(string value, int length) => string.IsNullOrEmpty(value) || value.Length <= length ? value ?? string.Empty : value.Substring(0, length) + "…";
    }
}
