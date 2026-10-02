using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Novelify.Editor
{
    public sealed class NovelLineRecord
    {
        public string Key;
        public string Kind;
        public string Speaker;
        public string Source;
        public RuntimeNovelGraph Graph;
        public Font DefaultFont;
        public List<Font> FontAssets;
        public NovelCharacter Character;
        public CharacterEmotion Emotion;
    }

    public static class NovelLineIndex
    {
        public static List<NovelLineRecord> Collect(RuntimeNovelGraph selectedGraph = null)
        {
            var result = new List<NovelLineRecord>();
            IEnumerable<string> paths = selectedGraph != null
                ? new[] { AssetDatabase.GetAssetPath(selectedGraph) }
                : AssetDatabase.GetAllAssetPaths().Where(path =>
                    path.EndsWith(".novelgraph", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".novelfunction", StringComparison.OrdinalIgnoreCase));
            foreach (string path in paths.Where(path => !string.IsNullOrEmpty(path)))
            {
                RuntimeNovelGraph graph = AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>(path);
                if (graph?.AllNodes == null) continue;
                foreach (RuntimeDialogueNode node in graph.AllNodes.OfType<RuntimeDialogueNode>())
                {
                    string key = !string.IsNullOrEmpty(node.LineID)
                        ? node.LineID
                        : NovelLocalizationKey.Dialogue(graph.GraphID, node.NodeID);
                    string speaker = node.NovelCharacter != null
                        ? node.NovelCharacter.SpeakerName
                        : node.SpeakerName;
                    Add(result, graph, key, "Dialogue", speaker, node.DialogueText,
                        node.DialogueFont, node.DialogueFontAssets,
                        node.NovelCharacter, node.Emotion);
                    if (node is not RuntimeChoiceNode choiceNode || choiceNode.Choices == null) continue;
                    foreach (ChoiceData choice in choiceNode.Choices.Where(choice => choice != null))
                    {
                        Add(result, graph, NovelLocalizationKey.Choice(key, choice.ChoiceID),
                            "Choice", speaker, choice.ChoiceText,
                            character: node.NovelCharacter, emotion: node.Emotion);
                        if (!string.IsNullOrEmpty(choice.DisabledReason))
                            Add(result, graph, NovelLocalizationKey.DisabledReason(key, choice.ChoiceID),
                                "Disabled reason", speaker, choice.DisabledReason,
                                character: node.NovelCharacter, emotion: node.Emotion);
                    }
                }
            }
            return result;
        }

        private static void Add(List<NovelLineRecord> lines, RuntimeNovelGraph graph,
            string key, string kind, string speaker, string source,
            Font font = null, List<Font> fontAssets = null,
            NovelCharacter character = null, CharacterEmotion emotion = CharacterEmotion.Neutral)
        {
            lines.Add(new NovelLineRecord
            {
                Key = key, Kind = kind, Speaker = speaker ?? string.Empty,
                Source = source ?? string.Empty, Graph = graph,
                DefaultFont = font, FontAssets = fontAssets,
                Character = character, Emotion = emotion
            });
        }

        public static int Sync(NovelLocalizationTable table, IReadOnlyList<NovelLineRecord> records,
            string targetLocale = null)
        {
            if (table == null) return 0;
            Undo.RecordObject(table, "Sync Novelify localization keys");
            table.Lines ??= new List<NovelLocalizedLine>();
            var existing = table.Lines.Where(line => line != null && !string.IsNullOrEmpty(line.Key))
                .GroupBy(line => line.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            int added = 0;
            foreach (NovelLineRecord record in records)
            {
                if (record == null || (table.SourceGraph != null &&
                    record.Graph != table.SourceGraph)) continue;
                if (!existing.TryGetValue(record.Key, out NovelLocalizedLine line))
                {
                    line = new NovelLocalizedLine { Key = record.Key };
                    table.Lines.Add(line);
                    existing.Add(record.Key, line);
                    added++;
                }
                line.SourceText = record.Source;
                if (string.IsNullOrWhiteSpace(targetLocale) ||
                    string.Equals(targetLocale, table.SourceLocale, StringComparison.OrdinalIgnoreCase))
                    continue;
                line.Translations ??= new List<NovelLocalizedText>();
                NovelLocalizedText draft = line.Translations.FirstOrDefault(value =>
                    value != null && string.Equals(value.Locale, targetLocale,
                        StringComparison.OrdinalIgnoreCase));
                if (draft == null)
                    line.Translations.Add(new NovelLocalizedText
                    {
                        Locale = targetLocale, Text = record.Source, NeedsReview = true
                    });
                else if (draft.NeedsReview || string.IsNullOrEmpty(draft.Text))
                {
                    draft.Text = record.Source;
                    draft.NeedsReview = true;
                }
            }
            table.Invalidate();
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            return added;
        }

        public static int SaveTranslations(NovelLocalizationTable table,
            IReadOnlyList<NovelLineRecord> records, string locale,
            IReadOnlyDictionary<string, string> editedText)
        {
            if (table == null || records == null || editedText == null ||
                string.IsNullOrWhiteSpace(locale) ||
                string.Equals(locale, table.SourceLocale, StringComparison.OrdinalIgnoreCase))
                return 0;

            var recordByKey = records.Where(record => record != null &&
                    (table.SourceGraph == null || record.Graph == table.SourceGraph))
                .GroupBy(record => record.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var edits = editedText.Where(pair => recordByKey.ContainsKey(pair.Key)).ToList();
            if (edits.Count == 0) return 0;

            Undo.RecordObject(table, "Save Novelify translations");
            table.Lines ??= new List<NovelLocalizedLine>();
            var tableLines = table.Lines.Where(line => line != null &&
                    !string.IsNullOrEmpty(line.Key))
                .GroupBy(line => line.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> edit in edits)
            {
                NovelLineRecord record = recordByKey[edit.Key];
                if (!tableLines.TryGetValue(edit.Key, out NovelLocalizedLine line))
                {
                    line = new NovelLocalizedLine { Key = edit.Key };
                    table.Lines.Add(line);
                    tableLines.Add(edit.Key, line);
                }
                line.SourceText = record.Source;
                line.Translations ??= new List<NovelLocalizedText>();
                NovelLocalizedText translation = line.Translations.FirstOrDefault(value =>
                    value != null && string.Equals(value.Locale, locale,
                        StringComparison.OrdinalIgnoreCase));
                if (translation == null)
                    line.Translations.Add(new NovelLocalizedText
                    {
                        Locale = locale, Text = edit.Value ?? string.Empty,
                        NeedsReview = string.IsNullOrWhiteSpace(edit.Value)
                    });
                else
                {
                    translation.Text = edit.Value ?? string.Empty;
                    translation.NeedsReview = string.IsNullOrWhiteSpace(edit.Value);
                }
            }
            table.Invalidate();
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            return edits.Count;
        }
    }

    /// <summary>Graph-scoped text browser and translation CSV workflow.</summary>
    public sealed class NovelLocalizationWorkspace : EditorWindow
    {
        [SerializeField] private NovelLocalizationTable _table;
        [SerializeField] private RuntimeNovelGraph _graph;
        private List<NovelLineRecord> _records = new List<NovelLineRecord>();
        private string _query = string.Empty;
        [SerializeField] private string _locale = "es";
        private Vector2 _scroll;
        private int _page;
        private const int PageSize = 50;
        private readonly Dictionary<string, TranslationDraftState> _drafts =
            new Dictionary<string, TranslationDraftState>(StringComparer.Ordinal);

        private sealed class TranslationDraftState
        {
            public NovelTranslationDraft SourceDraft;
            public NovelTranslationDraft LocalizedDraft;
            public SerializedObject SourceSerialized;
            public SerializedObject LocalizedSerialized;
            public string InitialText;
            public bool IsDirty => !string.Equals(LocalizedDraft.RichText.Text,
                InitialText, StringComparison.Ordinal);
        }

        [MenuItem("Window/Novelify/Localization Workspace")]
        public static void Open() => GetWindow<NovelLocalizationWorkspace>("Novelify Text");

        public static void OpenFor(NovelLocalizationTable table)
        {
            NovelLocalizationWorkspace window = GetWindow<NovelLocalizationWorkspace>("Novelify Text");
            window._table = table;
            window._graph = FindGraphFor(table);
            window.Refresh();
        }

        public static RuntimeNovelGraph FindGraphFor(NovelLocalizationTable table)
        {
            if (table == null || table.SourceGraph != null)
                return table != null ? table.SourceGraph : null;

            string tablePath = AssetDatabase.GetAssetPath(table);
            string folder = Path.GetDirectoryName(tablePath)?.Replace('\\', '/');
            string[] graphPaths = AssetDatabase.GetAllAssetPaths().Where(path =>
                path.EndsWith(".novelgraph", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".novelfunction", StringComparison.OrdinalIgnoreCase)).ToArray();
            RuntimeNovelGraph[] nearby = graphPaths.Where(path =>
                    string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), folder,
                        StringComparison.OrdinalIgnoreCase))
                .Select(AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>)
                .Where(graph => graph != null).ToArray();
            if (nearby.Length == 1) return nearby[0];

            var keys = new HashSet<string>((table.Lines ?? new List<NovelLocalizedLine>())
                .Where(line => line != null && !string.IsNullOrEmpty(line.Key))
                .Select(line => line.Key), StringComparer.Ordinal);
            RuntimeNovelGraph best = null;
            int bestMatches = 0;
            foreach (string path in graphPaths)
            {
                RuntimeNovelGraph graph = AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>(path);
                if (graph == null || string.IsNullOrEmpty(graph.GraphID)) continue;
                int matches = keys.Count(key => key.StartsWith(graph.GraphID + ":",
                    StringComparison.Ordinal));
                if (matches <= bestMatches) continue;
                best = graph;
                bestMatches = matches;
            }
            return best;
        }

        private void OnEnable()
        {
            minSize = new Vector2(680f, 500f);
            if (Selection.activeObject is NovelLocalizationTable selectedTable)
            {
                _table = selectedTable;
                _graph = FindGraphFor(selectedTable);
            }
            else if (Selection.activeObject is RuntimeNovelGraph selectedGraph)
            {
                _graph = selectedGraph;
                if (_table != null && _table.SourceGraph != _graph)
                    _table = null;
            }
            if (Selection.activeGameObject != null &&
                Selection.activeGameObject.TryGetComponent(out NovelGraphRunner runner))
            {
                _table = runner.LocalizationTable;
                _graph = runner.RuntimeGraph;
            }
            Refresh();
        }

        private void Refresh()
        {
            _records = _graph != null
                ? NovelLineIndex.Collect(_graph) : new List<NovelLineRecord>();
            ResetDrafts();
            _page = 0;
            Repaint();
        }

        private void OnDisable()
        {
            if (PendingCount > 0) SavePending();
            ResetDrafts();
        }

        private void ResetDrafts()
        {
            foreach (TranslationDraftState draft in _drafts.Values)
            {
                if (draft.SourceDraft != null) DestroyImmediate(draft.SourceDraft);
                if (draft.LocalizedDraft != null) DestroyImmediate(draft.LocalizedDraft);
            }
            _drafts.Clear();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Story text and translations", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Edit every dialogue and choice directly below. The story text is read-only; " +
                "type in EDIT TRANSLATION on the right (or below in a narrow window). " +
                "Save all edits together when finished.",
                MessageType.Info);
            EditorGUI.BeginChangeCheck();
            NovelLocalizationTable selectedTable = (NovelLocalizationTable)EditorGUILayout.ObjectField(
                "Localization Table", _table, typeof(NovelLocalizationTable), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (ConfirmPendingChanges())
                {
                    _table = selectedTable;
                    _graph = FindGraphFor(_table);
                    Refresh();
                }
            }
            EditorGUI.BeginChangeCheck();
            RuntimeNovelGraph selectedGraph = (RuntimeNovelGraph)EditorGUILayout.ObjectField(
                "Story Graph", _graph, typeof(RuntimeNovelGraph), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (ConfirmPendingChanges())
                {
                    _graph = selectedGraph;
                    if (_table != null)
                    {
                        Undo.RecordObject(_table, "Set Novelify localization graph");
                        _table.SourceGraph = _graph;
                        EditorUtility.SetDirty(_table);
                    }
                    Refresh();
                }
            }
            if (_graph == null)
                EditorGUILayout.HelpBox(
                    "Choose a Story Graph to load its dialogue and choices. Existing tables usually " +
                    "find their graph automatically when opened from the table asset.",
                    MessageType.Warning);
            if (_table == null)
                EditorGUILayout.HelpBox(
                    "Select a Localization Table or click Create table before editing translations.",
                    MessageType.Warning);
            EditorGUI.BeginChangeCheck();
            string locale = EditorGUILayout.TextField("Translation Locale", _locale).Trim();
            if (EditorGUI.EndChangeCheck())
            {
                if (ConfirmPendingChanges())
                {
                    _locale = locale;
                    ResetDrafts();
                }
            }
            _query = EditorGUILayout.TextField("Search text, speaker or ID", _query);
            if (_table != null && string.Equals(_locale, _table.SourceLocale,
                    StringComparison.OrdinalIgnoreCase))
                EditorGUILayout.HelpBox("Choose a locale different from the source locale to edit translations.",
                    MessageType.Info);
            if (_table != null && _graph != null && _table.SourceGraph != null &&
                _table.SourceGraph != _graph)
                EditorGUILayout.HelpBox(
                    "This table is bound to a different Story Graph. Choose its graph or create a table for this one.",
                    MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh graph") && ConfirmPendingChanges()) Refresh();
                using (new EditorGUI.DisabledScope(_graph == null))
                    if (GUILayout.Button("Create table") && ConfirmPendingChanges()) CreateTable();
                using (new EditorGUI.DisabledScope(_table == null || _graph == null ||
                    _records.Count == 0 || (_table.SourceGraph != null &&
                    _table.SourceGraph != _graph)))
                {
                    if (GUILayout.Button("Sync keys"))
                    {
                        if (ConfirmPendingChanges())
                        {
                            Debug.Log($"Novelify added {NovelLineIndex.Sync(_table, _records, _locale)} keys and synced {_locale} source-formatted drafts.");
                            ResetDrafts();
                        }
                    }
                    if (GUILayout.Button("Export CSV") && ConfirmPendingChanges()) ExportCsv();
                    if (GUILayout.Button("Import CSV") && ConfirmPendingChanges())
                    {
                        ImportCsv();
                        ResetDrafts();
                    }
                }
            }

            List<NovelLineRecord> filtered = _records.Where(MatchesQuery).ToList();
            int wordCount = _records.Where(line => line.Kind == "Dialogue")
                .Sum(line => CountWords(line.Source));
            var tableLines = _table?.Lines?.Where(line => line != null && !string.IsNullOrEmpty(line.Key))
                .GroupBy(line => line.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            if (_graph != null && tableLines != null)
            {
                var visibleKeys = new HashSet<string>(_records.Select(record => record.Key),
                    StringComparer.Ordinal);
                int otherKeys = tableLines.Keys.Count(key => !visibleKeys.Contains(key));
                if (otherKeys > 0)
                    EditorGUILayout.HelpBox(
                        $"This table has {otherKeys} entries outside the selected graph's current lines. " +
                        "They are preserved but hidden here; Sync and CSV export use only this graph.",
                        MessageType.Info);
            }
            int missing = _table == null ||
                string.Equals(_locale, _table.SourceLocale, StringComparison.OrdinalIgnoreCase)
                ? 0
                : filtered.Count(line =>
                    tableLines == null || !tableLines.TryGetValue(line.Key, out NovelLocalizedLine entry) ||
                    entry.Translations == null || !entry.Translations.Any(translation => translation != null &&
                            string.Equals(translation.Locale, _locale,
                                StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrEmpty(translation.Text)));
            int drafts = tableLines == null ? 0 : filtered.Count(line =>
                tableLines.TryGetValue(line.Key, out NovelLocalizedLine entry) &&
                entry.Translations != null && entry.Translations.Any(translation =>
                    translation != null && translation.NeedsReview &&
                    string.Equals(translation.Locale, _locale, StringComparison.OrdinalIgnoreCase)));
            EditorGUILayout.LabelField(
                $"{_records.Count} entries | {wordCount} dialogue words | " +
                $"{filtered.Count} matches | {drafts} drafts to review | {missing} missing");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{PendingCount} unsaved translation edits",
                    EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(PendingCount == 0))
                {
                    if (GUILayout.Button("Save all translations", GUILayout.Width(190),
                            GUILayout.Height(30f)))
                        SavePending();
                    if (GUILayout.Button("Discard edits", GUILayout.Width(110),
                            GUILayout.Height(30f)))
                        ResetDrafts();
                }
            }

            int maxPage = Mathf.Max(0, (filtered.Count - 1) / PageSize);
            _page = Mathf.Clamp(_page, 0, maxPage);
            DrawPager(filtered.Count, maxPage);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (NovelLineRecord record in filtered.Skip(_page * PageSize).Take(PageSize))
                DrawRecord(record, tableLines);
            EditorGUILayout.EndScrollView();

            DrawPager(filtered.Count, maxPage);
            using (new EditorGUI.DisabledScope(PendingCount == 0))
                if (GUILayout.Button($"Save all translations ({PendingCount})", GUILayout.Height(32f)))
                    SavePending();
        }

        private void DrawPager(int filteredCount, int maxPage)
        {
            if (maxPage == 0) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_page == 0))
                    if (GUILayout.Button("Previous", GUILayout.Width(90)))
                    {
                        _page--;
                        _scroll = Vector2.zero;
                    }
                EditorGUILayout.LabelField(
                    $"Lines {_page * PageSize + 1}-{Mathf.Min(filteredCount, (_page + 1) * PageSize)} " +
                    $"of {filteredCount} - edits on other pages stay pending",
                    EditorStyles.centeredGreyMiniLabel);
                using (new EditorGUI.DisabledScope(_page == maxPage))
                    if (GUILayout.Button("Next", GUILayout.Width(90)))
                    {
                        _page++;
                        _scroll = Vector2.zero;
                    }
            }
        }

        private bool MatchesQuery(NovelLineRecord line) =>
            string.IsNullOrWhiteSpace(_query) ||
            Contains(line.Key, _query) || Contains(line.Source, _query) ||
            Contains(line.Speaker, _query) || Contains(line.Kind, _query);

        private static bool Contains(string value, string query) =>
            (value ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private static int CountWords(string value) =>
            (value ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;

        private void DrawRecord(NovelLineRecord record,
            IReadOnlyDictionary<string, NovelLocalizedLine> tableLines)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                NovelSpeakerPreview.Draw(record, 68f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(record.Kind, EditorStyles.boldLabel);
                    if (GUILayout.Button("Open graph", GUILayout.Width(85)))
                        AssetDatabase.OpenAsset(record.Graph);
                }
                EditorGUILayout.SelectableLabel(record.Key, EditorStyles.miniLabel,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
                DrawRichPair(record, tableLines);
            }
        }

        private void DrawRichPair(NovelLineRecord record,
            IReadOnlyDictionary<string, NovelLocalizedLine> tableLines)
        {
            NovelLocalizedLine line = null;
            tableLines?.TryGetValue(record.Key, out line);
            NovelLocalizedText translation = line?.Translations?.FirstOrDefault(value =>
                value != null && string.Equals(value.Locale, _locale,
                    StringComparison.OrdinalIgnoreCase));
            TranslationDraftState draft = GetDraft(record, translation);

            bool canTranslate = _table != null && !string.IsNullOrWhiteSpace(_locale) &&
                !string.Equals(_locale, _table.SourceLocale, StringComparison.OrdinalIgnoreCase) &&
                (_table.SourceGraph == null || _table.SourceGraph == _graph);
            bool columns = position.width >= 1050f;
            if (columns) EditorGUILayout.BeginHorizontal();
            using (new EditorGUILayout.VerticalScope(columns
                       ? GUILayout.Width((position.width - 65f) * 0.5f) : GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("ORIGINAL STORY TEXT - READ ONLY",
                    EditorStyles.boldLabel);
                draft.SourceSerialized.Update();
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(draft.SourceSerialized.FindProperty("RichText"),
                        GUIContent.none, true);
            }
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField($"EDIT TRANSLATION ({_locale})",
                    EditorStyles.boldLabel);
                if (!canTranslate)
                    EditorGUILayout.HelpBox(_table == null
                            ? "Choose or create a Localization Table above to type here."
                            : string.Equals(_locale, _table.SourceLocale,
                                StringComparison.OrdinalIgnoreCase)
                                ? "Choose a locale different from the source language above."
                                : "Choose the table's Story Graph above to type here.",
                        MessageType.Warning);
                draft.LocalizedSerialized.Update();
                using (new EditorGUI.DisabledScope(!canTranslate))
                    EditorGUILayout.PropertyField(draft.LocalizedSerialized.FindProperty("RichText"),
                        GUIContent.none, true);
                draft.LocalizedSerialized.ApplyModifiedProperties();
            }
            if (columns) EditorGUILayout.EndHorizontal();

            if (!canTranslate) return;
            if (!NovelTranslationEditorWindow.SameFormattingTags(
                    record.Source, draft.LocalizedDraft.RichText.Text))
                EditorGUILayout.HelpBox(
                    "Formatting or effect tags differ from the source. Review styled spans before saving.",
                    MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(draft.IsDirty ? "Unsaved edit" :
                    translation == null ? "Source draft - not yet translated" :
                    translation.NeedsReview ? "Source draft - needs translation" : "Reviewed",
                    EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(!canTranslate))
                    if (GUILayout.Button("Reset this draft to source", GUILayout.Width(175)))
                    {
                        draft.LocalizedDraft.RichText = RichDraft(record, record.Source);
                        draft.LocalizedSerialized.Update();
                    }
            }
        }

        private TranslationDraftState GetDraft(NovelLineRecord record,
            NovelLocalizedText translation)
        {
            if (_drafts.TryGetValue(record.Key, out TranslationDraftState draft))
                return draft;
            string initialText = translation?.Text ?? record.Source;
            draft = new TranslationDraftState
            {
                SourceDraft = CreateInstance<NovelTranslationDraft>(),
                LocalizedDraft = CreateInstance<NovelTranslationDraft>(),
                InitialText = initialText
            };
            draft.SourceDraft.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            draft.LocalizedDraft.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            draft.SourceDraft.RichText = RichDraft(record, record.Source);
            draft.LocalizedDraft.RichText = RichDraft(record, initialText);
            draft.SourceSerialized = new SerializedObject(draft.SourceDraft);
            draft.LocalizedSerialized = new SerializedObject(draft.LocalizedDraft);
            _drafts.Add(record.Key, draft);
            return draft;
        }

        private int PendingCount => _drafts.Values.Count(draft => draft.IsDirty);

        private bool ConfirmPendingChanges()
        {
            if (PendingCount == 0) return true;
            int choice = EditorUtility.DisplayDialogComplex("Unsaved translations",
                $"Save {PendingCount} edited translations before changing the workspace?",
                "Save all", "Cancel", "Discard edits");
            if (choice == 1) return false;
            if (choice == 0)
            {
                SavePending();
                if (PendingCount > 0)
                {
                    EditorUtility.DisplayDialog("Translations not saved",
                        "Could not save the pending edits. Check the selected table and Story Graph.",
                        "OK");
                    return false;
                }
            }
            else ResetDrafts();
            return true;
        }

        private void SavePending()
        {
            if (_table == null || _graph == null || string.IsNullOrWhiteSpace(_locale) ||
                string.Equals(_locale, _table.SourceLocale, StringComparison.OrdinalIgnoreCase))
                return;
            var edits = _drafts.Where(pair => pair.Value.IsDirty)
                .ToDictionary(pair => pair.Key,
                    pair => pair.Value.LocalizedDraft.RichText.Text,
                    StringComparer.Ordinal);
            if (edits.Count == 0) return;
            if (_table.SourceGraph == null)
            {
                Undo.RecordObject(_table, "Bind Novelify localization graph");
                _table.SourceGraph = _graph;
                EditorUtility.SetDirty(_table);
            }
            if (NovelLineIndex.SaveTranslations(_table, _records, _locale, edits) == 0)
                return;
            foreach (KeyValuePair<string, string> edit in edits)
                _drafts[edit.Key].InitialText = edit.Value;
            Repaint();
        }

        private static RichDialogueText RichDraft(NovelLineRecord record, string text) =>
            new RichDialogueText(text)
            {
                DefaultFont = record.DefaultFont,
                FontAssets = record.FontAssets != null
                    ? new List<Font>(record.FontAssets) : new List<Font>()
            };

        private void CreateTable()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create localization table",
                "NovelLocalization", "asset", "Choose where to store translations.");
            if (string.IsNullOrEmpty(path)) return;
            _table = CreateInstance<NovelLocalizationTable>();
            _table.SourceGraph = _graph;
            AssetDatabase.CreateAsset(_table, path);
            NovelLineIndex.Sync(_table, _records, _locale);
            ResetDrafts();
            Selection.activeObject = _table;
        }

        private void ExportCsv()
        {
            if (!CanEditLocale()) return;
            string path = EditorUtility.SaveFilePanel("Export Novelify translations",
                Application.dataPath, "novelify-" + _locale + ".csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            NovelLineIndex.Sync(_table, _records, _locale);
            var output = new StringBuilder("key,kind,speaker,source,translation\r\n");
            foreach (NovelLineRecord record in _records)
            {
                string translated = _table.Resolve(record.Key, _locale, string.Empty);
                output.Append(Csv(record.Key)).Append(',').Append(Csv(record.Kind)).Append(',')
                    .Append(Csv(record.Speaker)).Append(',').Append(Csv(record.Source)).Append(',')
                    .Append(Csv(translated)).Append("\r\n");
            }
            File.WriteAllText(path, output.ToString(), new UTF8Encoding(true));
            ResetDrafts();
        }

        private void ImportCsv()
        {
            if (!CanEditLocale()) return;
            string path = EditorUtility.OpenFilePanel("Import Novelify translations",
                Application.dataPath, "csv");
            if (string.IsNullOrEmpty(path)) return;
            List<List<string>> rows = ParseCsv(File.ReadAllText(path, Encoding.UTF8));
            if (rows.Count == 0 || rows[0].Count < 5 || rows[0][0] != "key" ||
                rows[0][4] != "translation")
            {
                EditorUtility.DisplayDialog("Invalid CSV",
                    "Expected columns: key,kind,speaker,source,translation", "OK");
                return;
            }
            var graphKeys = new HashSet<string>(_records.Select(record => record.Key),
                StringComparer.Ordinal);
            var known = (_table.Lines ?? new List<NovelLocalizedLine>())
                .Where(line => line != null && !string.IsNullOrEmpty(line.Key) &&
                    graphKeys.Contains(line.Key))
                .GroupBy(line => line.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var changes = new List<(NovelLocalizedLine Line, string Text)>();
            foreach (List<string> row in rows.Skip(1))
            {
                if (row.Count == 1 && row[0].Length == 0) continue;
                if (row.Count != 5 || !known.TryGetValue(row[0], out NovelLocalizedLine line) ||
                    !seen.Add(row[0]))
                {
                    EditorUtility.DisplayDialog("Invalid CSV",
                        "A row has an unknown or duplicate key, or an incorrect number of columns. No changes were made.", "OK");
                    return;
                }
                changes.Add((line, row[4]));
            }
            Undo.RecordObject(_table, "Import Novelify translations");
            foreach ((NovelLocalizedLine line, string translated) in changes)
            {
                line.Translations ??= new List<NovelLocalizedText>();
                NovelLocalizedText current = line.Translations.FirstOrDefault(value =>
                    value != null && string.Equals(value.Locale, _locale,
                        StringComparison.OrdinalIgnoreCase));
                if (current == null)
                    line.Translations.Add(new NovelLocalizedText
                        { Locale = _locale, Text = translated,
                          NeedsReview = string.IsNullOrWhiteSpace(translated) });
                else
                {
                    current.Text = translated;
                    current.NeedsReview = string.IsNullOrWhiteSpace(translated);
                }
            }
            _table.Invalidate();
            EditorUtility.SetDirty(_table);
            AssetDatabase.SaveAssets();
        }

        private bool CanEditLocale()
        {
            if (_table != null && !string.IsNullOrWhiteSpace(_locale) &&
                !string.Equals(_locale, _table.SourceLocale, StringComparison.OrdinalIgnoreCase))
                return true;
            EditorUtility.DisplayDialog("Choose a translation locale",
                "Select a localization table and enter a locale different from its source locale.", "OK");
            return false;
        }

        private static string Csv(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        private static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            string source = text ?? string.Empty;
            for (int index = 0; index < source.Length; index++)
            {
                char value = source[index];
                if (value == '"')
                {
                    if (quoted && index + 1 < source.Length && source[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else quoted = !quoted;
                    continue;
                }
                if (!quoted && (value == ',' || value == '\n'))
                {
                    row.Add(field.ToString().TrimEnd('\r'));
                    field.Clear();
                    if (value == '\n') { rows.Add(row); row = new List<string>(); }
                    continue;
                }
                field.Append(value);
            }
            if (quoted) return new List<List<string>>();
            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString().TrimEnd('\r'));
                rows.Add(row);
            }
            if (rows.Count > 0 && rows[0].Count > 0)
                rows[0][0] = rows[0][0].TrimStart('\ufeff');
            return rows;
        }
    }

    internal sealed class NovelTranslationDraft : ScriptableObject
    {
        public RichDialogueText RichText;
    }

    internal static class NovelSpeakerPreview
    {
        public static void Draw(NovelLineRecord record, float height)
        {
            Rect area = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(area, new Color(0.045f, 0.075f, 0.13f));
            var portraitArea = new Rect(area.x + 8f, area.y + 7f,
                Mathf.Min(110f, height * 0.72f), height - 14f);
            EditorGUI.DrawRect(portraitArea, new Color(0.02f, 0.04f, 0.08f));
            NovelCharacter character = record?.Character;
            bool hasPortrait = false;
            if (character != null)
            {
                CharacterPortrait portrait = character.GetPortrait(record.Emotion);
                GUI.BeginGroup(portraitArea);
                Rect clipped = new Rect(0f, 0f, portraitArea.width, portraitArea.height);
                hasPortrait |= DrawLayer(clipped, portrait.Body, character);
                hasPortrait |= DrawLayer(clipped, portrait.Eyes, character);
                hasPortrait |= DrawLayer(clipped, portrait.Details, character);
                hasPortrait |= DrawLayer(clipped, portrait.Mouth, character);
                GUI.EndGroup();
            }
            if (!hasPortrait)
                GUI.Label(portraitArea, "No portrait", EditorStyles.centeredGreyMiniLabel);
            Rect nameArea = new Rect(portraitArea.xMax + 14f, area.y + 12f,
                Mathf.Max(0f, area.xMax - portraitArea.xMax - 25f), 28f);
            string name = string.IsNullOrWhiteSpace(record?.Speaker)
                ? "Narrator / no speaker" : record.Speaker;
            EditorGUI.LabelField(nameArea, name, EditorStyles.boldLabel);
            Rect detailArea = new Rect(nameArea.x, nameArea.yMax + 3f,
                nameArea.width, 22f);
            EditorGUI.LabelField(detailArea, character != null
                ? $"{record.Emotion} portrait - {record.Kind}"
                : string.IsNullOrWhiteSpace(record?.Speaker)
                    ? $"Narration - {record?.Kind ?? "Text"}"
                    : $"Named speaker - {record?.Kind ?? "Text"}",
                EditorStyles.miniLabel);
        }

        private static bool DrawLayer(Rect clipped, Sprite sprite, NovelCharacter character)
        {
            if (sprite == null || sprite.texture == null) return false;
            float zoom = Mathf.Clamp(character.PreviewZoom, 1f, 4f);
            float scale = Mathf.Max(clipped.width / sprite.rect.width,
                clipped.height / sprite.rect.height) * zoom;
            float width = sprite.rect.width * scale;
            float height = sprite.rect.height * scale;
            var draw = new Rect((clipped.width - width) * 0.5f +
                    character.PreviewOffsetX * clipped.width * 0.5f,
                (clipped.height - height) * 0.5f -
                    character.PreviewOffsetY * clipped.height * 0.5f,
                width, height);
            Rect uv = new Rect(sprite.rect.x / sprite.texture.width,
                sprite.rect.y / sprite.texture.height,
                sprite.rect.width / sprite.texture.width,
                sprite.rect.height / sprite.texture.height);
            GUI.DrawTextureWithTexCoords(draw, sprite.texture, uv, true);
            return true;
        }
    }

    [CustomEditor(typeof(NovelLocalizationTable))]
    internal sealed class NovelLocalizationTableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SourceGraph"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SourceLocale"));
            serializedObject.ApplyModifiedProperties();
            NovelLocalizationTable table = (NovelLocalizationTable)target;
            EditorGUILayout.LabelField(
                $"{table.Lines?.Count ?? 0} localized entries",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The workspace is scoped to this table's Story Graph. It shows the speaker " +
                "and current text beside the translated text, both with rich-text previews.",
                MessageType.Info);
            if (table.SourceGraph == null)
                EditorGUILayout.HelpBox(
                    "This older table has no Story Graph reference. The translation workspace " +
                    "will try to find the graph automatically.", MessageType.Info);
            if (GUILayout.Button("EDIT TRANSLATIONS", GUILayout.Height(34f)))
                NovelLocalizationWorkspace.OpenFor((NovelLocalizationTable)target);
        }
    }

    /// <summary>Reuses the graph's rich dialogue editor for localized markup.</summary>
    internal sealed class NovelTranslationEditorWindow : EditorWindow
    {
        private NovelLocalizationTable _table;
        private NovelLocalizedLine _line;
        private NovelLineRecord _record;
        private string _locale;
        private NovelTranslationDraft _draft;
        private NovelTranslationDraft _sourceDraft;
        private SerializedObject _serializedDraft;
        private SerializedObject _serializedSource;
        private Vector2 _scroll;

        internal static void Open(NovelLocalizationTable table, NovelLocalizedLine line,
            NovelLineRecord record, string locale)
        {
            var window = CreateInstance<NovelTranslationEditorWindow>();
            window.titleContent = new GUIContent("Translate rich text");
            window.minSize = new Vector2(1080f, 610f);
            window._table = table;
            window._line = line;
            window._record = record;
            window._locale = locale;
            NovelLocalizedText translation = line.Translations?.FirstOrDefault(value =>
                value != null && string.Equals(value.Locale, locale,
                    StringComparison.OrdinalIgnoreCase));
            window._draft = CreateInstance<NovelTranslationDraft>();
            window._draft.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            window._draft.RichText = new RichDialogueText(
                translation?.Text ?? record.Source)
            {
                DefaultFont = record.DefaultFont,
                FontAssets = record.FontAssets != null
                    ? new List<Font>(record.FontAssets) : new List<Font>()
            };
            window._serializedDraft = new SerializedObject(window._draft);
            window._sourceDraft = CreateInstance<NovelTranslationDraft>();
            window._sourceDraft.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            window._sourceDraft.RichText = new RichDialogueText(record.Source)
            {
                DefaultFont = record.DefaultFont,
                FontAssets = record.FontAssets != null
                    ? new List<Font>(record.FontAssets) : new List<Font>()
            };
            window._serializedSource = new SerializedObject(window._sourceDraft);
            window.ShowUtility();
        }

        private void OnDestroy()
        {
            if (_draft != null) DestroyImmediate(_draft);
            if (_sourceDraft != null) DestroyImmediate(_sourceDraft);
        }

        private void OnGUI()
        {
            if (_table == null || _line == null || _record == null || _draft == null)
            {
                Close();
                return;
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField($"{_record.Kind} | {_locale}", EditorStyles.boldLabel);
            NovelSpeakerPreview.Draw(_record, 120f);
            EditorGUILayout.HelpBox(
                "The draft starts with the source's bold, italic, size, color and effect tags. " +
                "Translate the words and adjust styled spans with the same rich-text toolbar used in the graph.",
                MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width((position.width - 32f) * 0.5f)))
                {
                    EditorGUILayout.LabelField("CURRENT STORY TEXT", EditorStyles.boldLabel);
                    _serializedSource.Update();
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(_serializedSource.FindProperty("RichText"),
                            GUIContent.none, true);
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("LOCALIZED TEXT", EditorStyles.boldLabel);
                    _serializedDraft.Update();
                    EditorGUILayout.PropertyField(_serializedDraft.FindProperty("RichText"),
                        GUIContent.none, true);
                    _serializedDraft.ApplyModifiedProperties();
                }
            }
            if (!SameFormattingTags(_record.Source, _draft.RichText.Text))
                EditorGUILayout.HelpBox(
                    "Some formatting or effect tags differ from the source. " +
                    "Check bold, italic, size, color and animated spans before saving.",
                    MessageType.Warning);
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy source markup", GUILayout.Height(30f)))
                {
                    RichDialogueText richText = _draft.RichText;
                    richText.Text = _record.Source;
                    _draft.RichText = richText;
                    _serializedDraft.Update();
                }
                if (GUILayout.Button("Save translation", GUILayout.Height(30f)))
                {
                    Undo.RecordObject(_table, "Edit Novelify rich translation");
                    _line.Translations ??= new List<NovelLocalizedText>();
                    NovelLocalizedText translation = _line.Translations.FirstOrDefault(value =>
                        value != null && string.Equals(value.Locale, _locale,
                            StringComparison.OrdinalIgnoreCase));
                    if (translation == null)
                        _line.Translations.Add(new NovelLocalizedText
                            { Locale = _locale, Text = _draft.RichText.Text, NeedsReview = false });
                    else
                    {
                        translation.Text = _draft.RichText.Text;
                        translation.NeedsReview = false;
                    }
                    _table.Invalidate();
                    EditorUtility.SetDirty(_table);
                    AssetDatabase.SaveAssets();
                    Close();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        internal static bool SameFormattingTags(string source, string translated)
        {
            Dictionary<string, int> first = FormattingTags(source);
            Dictionary<string, int> second = FormattingTags(translated);
            return first.Count == second.Count && first.All(pair =>
                second.TryGetValue(pair.Key, out int count) && count == pair.Value);
        }

        private static Dictionary<string, int> FormattingTags(string markup)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string value = markup ?? string.Empty;
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] != '<') continue;
                int end = value.IndexOf('>', index + 1);
                if (end < 0) break;
                string tag = value.Substring(index + 1, end - index - 1).Trim();
                string lower = tag.ToLowerInvariant();
                if (lower == "b" || lower == "/b" ||
                    lower == "i" || lower == "/i" ||
                    lower == "/size" || lower == "/color" ||
                    lower == "/link" || lower == "/font" ||
                    lower.StartsWith("size=", StringComparison.Ordinal) ||
                    lower.StartsWith("color=", StringComparison.Ordinal) ||
                    lower.StartsWith("link=", StringComparison.Ordinal) ||
                    lower.StartsWith("font=", StringComparison.Ordinal))
                    result[lower] = result.TryGetValue(lower, out int count) ? count + 1 : 1;
                index = end;
            }
            return result;
        }
    }
}
