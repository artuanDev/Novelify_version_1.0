using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Novelify.Editor
{
    /// <summary>
    /// This window does only contain the logic on how the window looks and behaves, it does not contain HOW 
    /// it opens, closes, etc.
    /// </summary>
    internal sealed class NovelPresentationComposerWindow : EditorWindow
    {
        private sealed class PresentationDraft
        {
            public DialogueDraft Dialogue = new DialogueDraft();
            public SpeakerDraft Speaker = new SpeakerDraft();
            public string SampleSpeaker = "Test Speaker";
            public string SampleDialogue = "This is a sample dialogue for layout preview";
            public NovelCharacter PreviewCharacter;
            public CharacterEmotion PreviewEmotion = CharacterEmotion.Neutral;
            public bool ShowCharacter = true;
            public Vector2Int Resolution = new Vector2Int(1920, 1080);
        }

        private sealed class DialogueDraft
        {
            //we need every information from the runtime dialogue box to be the same
            public NovelBoxStyle Style;
            public NovelTextAlignment TextAlignment;
            public NovelDialogueAnchor Anchor;
            public float BaseFontSize;
            public bool AutoSize;
            public float MinimumFontSize;
            public float MaximumFontSize;
            public float Height;
            public float Width;
            public float BottomMargin;
            public float HorizontalMargin;
            public float HorizontalPadding;
            public float VerticalPadding;
        }

        private sealed class SpeakerDraft
        {
            ///same here
            public NovelBoxStyle Style;
            public NovelSpeakerAnchor Anchor;
            public float FontSize;
            public float HorizontalPadding;
            public float VerticalPadding;
            public float HorizontalOffset;
            public float VerticalOverlap;
        }

        private sealed class ComposerUndoState : ScriptableObject
        {
            public NovelCharacter PreviewCharacter;
            public CharacterEmotion PreviewEmotion;
            public bool ShowCharacter = true;
            public string SampleSpeaker;
            public string SampleDialogue;
        }

        private static readonly Color WindowColor = new Color32(10, 15, 28, 255);
        private static readonly Color ToolbarColor = new Color32(19, 27, 45, 255);
        private static readonly Color PanelColor = new Color32(23, 33, 53, 255);
        private static readonly Color CardColor = new Color32(30, 42, 66, 255);
        private static readonly Color StageColor = new Color32(16, 25, 43, 255);
        private static readonly Color AccentColor = new Color32(99, 102, 241, 255);
        private static readonly Color AccentBrightColor = new Color32(129, 140, 248, 255);
        private static readonly Color TextColor = new Color32(241, 245, 249, 255);
        private static readonly Color MutedTextColor = new Color32(148, 163, 184, 255);
        private const string InspectorWidthPrefsKey =
            "Novelify.PresentationComposer.InspectorWidth";
        private const float DefaultInspectorWidth = 370f;
        private const float MinimumInspectorWidth = 300f;

        private Graph _graph; //the graph from which we opened the windows
        private readonly List<CreateDialogueBoxNode> _dialogueNodes =
            new List<CreateDialogueBoxNode>(); //this to detect the closest dialogue box node
        private readonly List<CreateDialogueSpeakerBoxNode> _speakerNodes =
            new List<CreateDialogueSpeakerBoxNode>();//same but with the dialogue speaker box node
        private int _dialogueIndex;
        private int _speakerIndex;
        private PresentationDraft _draft;
        private ComposerUndoState _undoState;
        private bool _draftDirty;
        private bool _building;
        private VisualElement _previewHost;
        private VisualElement _stage;
        private VisualElement _portraitRoot;
        private Image _portraitBody;
        private Image _portraitEyes;
        private Image _portraitDetails;
        private Image _portraitMouth;
        private Label _portraitEmptyLabel;
        private VisualElement _dialoguePreview;
        private Label _dialogueLabel;
        private VisualElement _speakerPreview;
        private Label _speakerLabel;
        private Label _resolutionLabel;
        private TextField _sampleSpeakerField;
        private VisualElement _inspectorPanel;
        private Label _status;
        private Button _applyButton;
        private GameObject _measurementObject;
        private TextMeshProUGUI _measurementText;
        private CreateDialogueBoxNode DialogueNode =>
            _dialogueIndex >= 0 && _dialogueIndex < _dialogueNodes.Count
                ? _dialogueNodes[_dialogueIndex]
                : null; //the opened create dialogue box node
        private CreateDialogueSpeakerBoxNode SpeakerNode =>
            _speakerIndex >= 0 && _speakerIndex < _speakerNodes.Count
                ? _speakerNodes[_speakerIndex]
                : null;//the opened create speaker box node

        //the window opens based on the selected create dialogue box node
        public static void Open(CreateDialogueBoxNode source)
        {
            if (source == null || source.Graph == null) return;
            Open(source.Graph, source, null);
        }
        //the window opens based on the selected create speake box node
        public static void Open(CreateDialogueSpeakerBoxNode source)
        {
            if (source == null || source.Graph == null) return;
            Open(source.Graph, null, source);
        }
        //the logic that runs when the window editor opens
        private static void Open(
            Graph graph,
            CreateDialogueBoxNode dialogue,
            CreateDialogueSpeakerBoxNode speaker)
        {
            NovelPresentationComposerWindow window =
                GetWindow<NovelPresentationComposerWindow>();
            window.titleContent = new GUIContent(
                "Presentation Composer",
                EditorGUIUtility.IconContent("Canvas Icon").image);
            window.minSize = new Vector2(1040f, 640f);
            window.Bind(graph, dialogue, speaker);
            window.Show();
            window.Focus();
        }
        private void Bind(
            Graph graph,
            CreateDialogueBoxNode dialogue,
            CreateDialogueSpeakerBoxNode speaker)
        {
            _graph = graph;
            _dialogueNodes.Clear();
            _speakerNodes.Clear();
            CreateDialogueBoxNode pairedDialogue = dialogue ??
                FindClosestNode<CreateDialogueBoxNode>(graph, speaker);
            CreateDialogueSpeakerBoxNode pairedSpeaker = speaker ??
                FindClosestNode<CreateDialogueSpeakerBoxNode>(graph, dialogue);
            if (pairedDialogue != null)
                _dialogueNodes.Add(pairedDialogue);
            if (pairedSpeaker != null)
                _speakerNodes.Add(pairedSpeaker);
            _dialogueIndex = 0;
            _speakerIndex = 0;
            if (DialogueNode != null && SpeakerNode != null)
                LoadDraft();
            else
                _draft = null;
            Rebuild();
        }
        private void OnEnable()
        {
            EnsureUndoState();
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            rootVisualElement.RegisterCallback<KeyDownEvent>(
                OnComposerKeyDown, TrickleDown.TrickleDown);
            if (_graph == null)
                ShowReconnectMessage();
        }

        private void OnInspectorUpdate()
        {
            SyncGameViewResolution();
        }
        private void OnDisable()
        {
            SaveInspectorWidth();
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            rootVisualElement.UnregisterCallback<KeyDownEvent>(
                OnComposerKeyDown, TrickleDown.TrickleDown);
            if (_measurementObject != null)
                DestroyImmediate(_measurementObject);
            _measurementObject = null;
            _measurementText = null;
            if (_undoState != null)
                DestroyImmediate(_undoState);
            _undoState = null;
        }

        private void EnsureUndoState()
        {
            if (_undoState != null)
                return;
            _undoState = CreateInstance<ComposerUndoState>();
            _undoState.hideFlags = HideFlags.HideAndDontSave;
        }

        private void SyncUndoStateFromDraft()
        {
            if (_draft == null)
                return;
            EnsureUndoState();
            _undoState.PreviewCharacter = _draft.PreviewCharacter;
            _undoState.PreviewEmotion = _draft.PreviewEmotion;
            _undoState.ShowCharacter = _draft.ShowCharacter;
            _undoState.SampleSpeaker = _draft.SampleSpeaker;
            _undoState.SampleDialogue = _draft.SampleDialogue;
        }

        private void ApplyUndoStateToDraft()
        {
            if (_draft == null || _undoState == null)
                return;
            _draft.PreviewCharacter = _undoState.PreviewCharacter;
            _draft.PreviewEmotion = _undoState.PreviewEmotion;
            _draft.ShowCharacter = _undoState.ShowCharacter;
            _draft.SampleSpeaker = _undoState.SampleSpeaker ?? string.Empty;
            _draft.SampleDialogue = _undoState.SampleDialogue ?? string.Empty;
        }

        private void RecordPreviewChange(string actionName, Action change)
        {
            if (_draft == null || change == null)
                return;
            EnsureUndoState();
            SyncUndoStateFromDraft();
            Undo.RecordObject(_undoState, actionName);
            change();
            SyncUndoStateFromDraft();
            EditorUtility.SetDirty(_undoState);
        }

        private void OnUndoRedoPerformed()
        {
            if (_draft == null || _graph == null)
                return;
            ApplyUndoStateToDraft();
            LoadDraft();
            Rebuild();
            SetStatus("Undo/redo applied to the composer and selected nodes.", false);
        }

        private void OnComposerKeyDown(KeyDownEvent evt)
        {
            bool actionModifier = evt.ctrlKey || evt.commandKey;
            if (!actionModifier)
                return;

            if (evt.keyCode == KeyCode.Z)
            {
                if (evt.shiftKey)
                    Undo.PerformRedo();
                else
                    Undo.PerformUndo();
                evt.StopImmediatePropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Y)
            {
                Undo.PerformRedo();
                evt.StopImmediatePropagation();
                return;
            }

            if (evt.keyCode != KeyCode.S ||
                _draft == null || _graph == null ||
                DialogueNode == null || SpeakerNode == null)
                return;
            ApplyToGraph();
            evt.StopImmediatePropagation();
        }

        private float GetSavedInspectorWidth()
        {
            float saved = EditorPrefs.GetFloat(
                InspectorWidthPrefsKey, DefaultInspectorWidth);
            float windowWidth = position.width > 1f
                ? position.width
                : minSize.x;
            float maximum = Mathf.Max(
                MinimumInspectorWidth, windowWidth - 520f);
            return Mathf.Clamp(saved, MinimumInspectorWidth, maximum);
        }

        private void OnInspectorGeometryChanged(GeometryChangedEvent evt)
        {
            SaveInspectorWidth(evt.newRect.width);
        }

        private void SaveInspectorWidth()
        {
            if (_inspectorPanel == null)
                return;
            SaveInspectorWidth(_inspectorPanel.resolvedStyle.width);
        }

        private static void SaveInspectorWidth(float width)
        {
            if (float.IsNaN(width) || float.IsInfinity(width) ||
                width < MinimumInspectorWidth - 1f)
                return;
            EditorPrefs.SetFloat(InspectorWidthPrefsKey, width);
        }

        private void ShowReconnectMessage()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor =
                (Color)new Color32(18, 27, 43, 255);
            HelpBox box = new HelpBox(
                "The graph was reloaded. Double-click a Create Dialogue Box " +
                "or Create Dialogue Speaker Box node to reconnect the composer.",
                HelpBoxMessageType.Info);
            box.style.marginLeft = 16f;
            box.style.marginRight = 16f;
            box.style.marginTop = 16f;
            rootVisualElement.Add(box);
        }

        private void LoadDraft()
        {
            NovelCharacter previewCharacter = _draft?.PreviewCharacter;
            CharacterEmotion previewEmotion = _draft?.PreviewEmotion ??
                CharacterEmotion.Neutral;
            bool showCharacter = _draft?.ShowCharacter ?? true;
            if (previewCharacter == null)
                TryFindGraphPreview(out previewCharacter, out previewEmotion);
            string defaultSpeaker = previewCharacter != null &&
                !string.IsNullOrWhiteSpace(previewCharacter.SpeakerName)
                    ? previewCharacter.SpeakerName
                    : "Test Speaker";
            string sampleSpeaker = _draft?.SampleSpeaker ?? defaultSpeaker;
            string sampleDialogue = _draft?.SampleDialogue ??
                "This is sample dialogue for layout preview.";
            Vector2Int resolution = GetGameViewResolution();
            _draft = new PresentationDraft
            {
                SampleSpeaker = sampleSpeaker,
                SampleDialogue = sampleDialogue,
                PreviewCharacter = previewCharacter,
                PreviewEmotion = previewEmotion,
                ShowCharacter = showCharacter,
                Resolution = resolution
            };
            _draft.Dialogue.Style = ReadStyle(
                DialogueNode, NovelBoxStyle.DialogueDefault);
            _draft.Dialogue.TextAlignment = Read(
                DialogueNode, "Text Alignment", NovelTextAlignment.TopLeft);
            _draft.Dialogue.Anchor = Read(
                DialogueNode, "Anchor", NovelDialogueAnchor.BottomCenter);
            _draft.Dialogue.BaseFontSize = Read(
                DialogueNode, "Base Font Size", 30f);
            _draft.Dialogue.AutoSize = Read(
                DialogueNode, "Auto Size", false);
            _draft.Dialogue.MinimumFontSize = Read(
                DialogueNode, "Minimum Font Size", 18f);
            _draft.Dialogue.MaximumFontSize = Read(
                DialogueNode, "Maximum Font Size", 30f);
            _draft.Dialogue.Height = Read(
                DialogueNode, "Height", 180f);
            _draft.Dialogue.Width = Read(
                DialogueNode, "Width", 0f);
            _draft.Dialogue.BottomMargin = Read(
                DialogueNode, "Bottom Margin", 32f);
            _draft.Dialogue.HorizontalMargin = Read(
                DialogueNode, "Horizontal Margin", 48f);
            _draft.Dialogue.HorizontalPadding = Read(
                DialogueNode, "Horizontal Padding", 32f);
            _draft.Dialogue.VerticalPadding = Read(
                DialogueNode, "Vertical Padding", 22f);
            _draft.Speaker.Style = ReadStyle(
                SpeakerNode, NovelBoxStyle.SpeakerDefault);
            _draft.Speaker.Anchor = Read(
                SpeakerNode, "Anchor", NovelSpeakerAnchor.TopLeft);
            _draft.Speaker.FontSize = Read(
                SpeakerNode, "Font Size", 25f);
            _draft.Speaker.HorizontalPadding = Read(
                SpeakerNode, "Horizontal Padding", 16f);
            _draft.Speaker.VerticalPadding = Read(
                SpeakerNode, "Vertical Padding", 6f);
            _draft.Speaker.HorizontalOffset = Read(
                SpeakerNode, "Horizontal Offset", 24f);
            _draft.Speaker.VerticalOverlap = Read(
                SpeakerNode, "Vertical Overlap", 27f);
            ValidateDraft();
            _draftDirty = false;
            SyncUndoStateFromDraft();
        }
        private void Rebuild()
        {
            _building = true;
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = WindowColor;
            rootVisualElement.style.color = TextColor;
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(BuildToolbar());
            if (_draft == null)
            {
                HelpBox missing = new HelpBox(
                    MissingNodeMessage(), HelpBoxMessageType.Warning);
                missing.style.marginLeft = 16f;
                missing.style.marginRight = 16f;
                missing.style.marginTop = 16f;
                rootVisualElement.Add(missing);
                _building = false;
                return;
            }
            VisualElement body = new VisualElement();
            body.style.flexGrow = 1f;
            body.style.minHeight = 0f;
            body.style.paddingLeft = 12f;
            body.style.paddingRight = 12f;
            body.style.paddingTop = 12f;
            body.style.paddingBottom = 10f;
            float inspectorWidth = GetSavedInspectorWidth();
            TwoPaneSplitView splitView = new TwoPaneSplitView(
                1, inspectorWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.name = "presentation-composer-split";
            splitView.style.flexGrow = 1f;
            splitView.style.minHeight = 0f;
            splitView.Add(BuildPreviewHost());
            _inspectorPanel = BuildInspector();
            _inspectorPanel.RegisterCallback<GeometryChangedEvent>(
                OnInspectorGeometryChanged);
            splitView.Add(_inspectorPanel);
            body.Add(splitView);
            rootVisualElement.Add(body);
            rootVisualElement.Add(BuildFooter());
            _building = false;
            RefreshPreview();
        }
        private VisualElement BuildToolbar()
        {
            VisualElement toolbar = new VisualElement();
            toolbar.style.paddingLeft = 16f;
            toolbar.style.paddingRight = 16f;
            toolbar.style.paddingTop = 12f;
            toolbar.style.paddingBottom = 10f;
            toolbar.style.backgroundColor = ToolbarColor;
            toolbar.style.borderBottomWidth = 1f;
            toolbar.style.borderBottomColor = (Color)new Color32(49, 63, 88, 255);

            VisualElement brandRow = new VisualElement();
            brandRow.style.flexDirection = FlexDirection.Row;
            brandRow.style.alignItems = Align.Center;
            VisualElement mark = new VisualElement();
            mark.style.width = 34f;
            mark.style.height = 34f;
            mark.style.marginRight = 10f;
            mark.style.backgroundColor = AccentColor;
            SetRadius(mark, 9f);
            Label markText = new Label("N");
            markText.style.flexGrow = 1f;
            markText.style.unityTextAlign = TextAnchor.MiddleCenter;
            markText.style.unityFontStyleAndWeight = FontStyle.Bold;
            markText.style.fontSize = 17f;
            markText.style.color = Color.white;
            mark.Add(markText);
            brandRow.Add(mark);
            VisualElement titles = new VisualElement();
            Label title = new Label("Presentation Composer");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 16f;
            title.style.color = TextColor;
            Label subtitle = new Label(
                "Design dialogue and speaker surfaces against the live Game View.");
            subtitle.style.fontSize = 10f;
            subtitle.style.color = MutedTextColor;
            titles.Add(title);
            titles.Add(subtitle);
            brandRow.Add(titles);
            VisualElement brandSpacer = new VisualElement();
            brandSpacer.style.flexGrow = 1f;
            brandRow.Add(brandSpacer);
            _resolutionLabel = CreatePill(
                ResolutionText(_draft?.Resolution ?? GetGameViewResolution()),
                new Color32(30, 64, 91, 255),
                new Color32(125, 211, 252, 255));
            _resolutionLabel.tooltip =
                "Automatically follows the current Game View rendering size.";
            brandRow.Add(_resolutionLabel);
            toolbar.Add(brandRow);

            VisualElement selectors = new VisualElement();
            selectors.style.flexDirection = FlexDirection.Row;
            selectors.style.alignItems = Align.Center;
            selectors.style.marginTop = 10f;
            Label sourceLabel = new Label("GRAPH SOURCES");
            sourceLabel.style.fontSize = 9f;
            sourceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            sourceLabel.style.color = AccentBrightColor;
            sourceLabel.style.marginRight = 10f;
            selectors.Add(sourceLabel);
            Label dialogueSource = CreatePill(
                DialogueNode != null ? "DIALOGUE BOX · ACTIVE" : "DIALOGUE BOX · MISSING",
                new Color32(30, 58, 86, 255),
                DialogueNode != null
                    ? new Color32(125, 211, 252, 255)
                    : new Color32(248, 113, 113, 255));
            dialogueSource.style.marginRight = 7f;
            dialogueSource.tooltip =
                "The opened dialogue node, or the nearest dialogue node to the opened speaker node.";
            selectors.Add(dialogueSource);
            Label speakerSource = CreatePill(
                SpeakerNode != null ? "SPEAKER BOX · ACTIVE" : "SPEAKER BOX · MISSING",
                new Color32(56, 43, 83, 255),
                SpeakerNode != null
                    ? new Color32(196, 181, 253, 255)
                    : new Color32(248, 113, 113, 255));
            speakerSource.tooltip =
                "The opened speaker node, or the nearest speaker node to the opened dialogue node.";
            selectors.Add(speakerSource);
            VisualElement selectorSpacer = new VisualElement();
            selectorSpacer.style.flexGrow = 1f;
            selectors.Add(selectorSpacer);
            Label sync = new Label("AUTO-SYNCED");
            sync.style.fontSize = 9f;
            sync.style.color = (Color)new Color32(52, 211, 153, 255);
            selectors.Add(sync);
            toolbar.Add(selectors);
            return toolbar;
        }
        private VisualElement BuildPreviewHost()
        {
            VisualElement previewPanel = new VisualElement();
            previewPanel.style.flexGrow = 1f;
            previewPanel.style.minWidth = 500f;
            previewPanel.style.marginRight = 12f;
            previewPanel.style.paddingLeft = 14f;
            previewPanel.style.paddingRight = 14f;
            previewPanel.style.paddingTop = 12f;
            previewPanel.style.paddingBottom = 12f;
            previewPanel.style.backgroundColor = PanelColor;
            SetRadius(previewPanel, 12f);
            SetBorder(previewPanel, 1f, new Color32(45, 58, 83, 255));

            VisualElement previewHeader = new VisualElement();
            previewHeader.style.flexDirection = FlexDirection.Row;
            previewHeader.style.alignItems = Align.Center;
            previewHeader.style.marginBottom = 10f;
            VisualElement previewTitles = new VisualElement();
            Label heading = new Label("Live composition");
            heading.style.fontSize = 14f;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.color = TextColor;
            Label explanation = new Label(
                "Character layers, dialogue geometry, and speaker placement");
            explanation.style.fontSize = 10f;
            explanation.style.color = MutedTextColor;
            previewTitles.Add(heading);
            previewTitles.Add(explanation);
            previewHeader.Add(previewTitles);
            VisualElement headerSpacer = new VisualElement();
            headerSpacer.style.flexGrow = 1f;
            previewHeader.Add(headerSpacer);
            previewHeader.Add(CreatePill(
                "GAME VIEW", new Color32(45, 38, 78, 255),
                new Color32(196, 181, 253, 255)));
            previewPanel.Add(previewHeader);

            _previewHost = new VisualElement();
            _previewHost.style.flexGrow = 1f;
            _previewHost.style.minHeight = 360f;
            _previewHost.style.position = Position.Relative;
            _previewHost.style.overflow = Overflow.Hidden;
            _previewHost.style.backgroundColor = WindowColor;
            SetRadius(_previewHost, 9f);
            SetBorder(_previewHost, 1f, new Color32(41, 53, 77, 255));
            _previewHost.RegisterCallback<GeometryChangedEvent>(_ =>
                RefreshPreview());
            _stage = new VisualElement();
            _stage.style.position = Position.Absolute;
            _stage.style.overflow = Overflow.Hidden;
            _stage.style.backgroundColor = StageColor;
            SetBorder(_stage, 1f, new Color32(76, 91, 121, 255));
            _previewHost.Add(_stage);

            VisualElement horizonGlow = new VisualElement();
            horizonGlow.style.position = Position.Absolute;
            horizonGlow.style.left = Length.Percent(18f);
            horizonGlow.style.right = Length.Percent(18f);
            horizonGlow.style.bottom = Length.Percent(-22f);
            horizonGlow.style.height = Length.Percent(55f);
            horizonGlow.style.backgroundColor = new Color(0.25f, 0.28f, 0.72f, 0.13f);
            SetRadius(horizonGlow, 999f);
            horizonGlow.pickingMode = PickingMode.Ignore;
            _stage.Add(horizonGlow);

            AddPreviewGrid(_stage);

            _portraitRoot = new VisualElement();
            _portraitRoot.style.position = Position.Absolute;
            _portraitRoot.style.overflow = Overflow.Visible;
            _portraitRoot.pickingMode = PickingMode.Ignore;
            _portraitBody = CreatePortraitLayer("portrait-body");
            _portraitEyes = CreatePortraitLayer("portrait-eyes");
            _portraitDetails = CreatePortraitLayer("portrait-details");
            _portraitMouth = CreatePortraitLayer("portrait-mouth");
            _portraitRoot.Add(_portraitBody);
            _portraitRoot.Add(_portraitEyes);
            _portraitRoot.Add(_portraitDetails);
            _portraitRoot.Add(_portraitMouth);
            _stage.Add(_portraitRoot);

            _portraitEmptyLabel = new Label(
                "Choose a Character in Preview Content\nto render every portrait layer.");
            _portraitEmptyLabel.style.position = Position.Absolute;
            _portraitEmptyLabel.style.left = Length.Percent(25f);
            _portraitEmptyLabel.style.right = Length.Percent(25f);
            _portraitEmptyLabel.style.top = Length.Percent(40f);
            _portraitEmptyLabel.style.paddingTop = 12f;
            _portraitEmptyLabel.style.paddingBottom = 12f;
            _portraitEmptyLabel.style.paddingLeft = 14f;
            _portraitEmptyLabel.style.paddingRight = 14f;
            _portraitEmptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _portraitEmptyLabel.style.whiteSpace = WhiteSpace.Normal;
            _portraitEmptyLabel.style.fontSize = 11f;
            _portraitEmptyLabel.style.color = MutedTextColor;
            _portraitEmptyLabel.style.backgroundColor = new Color(0.04f, 0.07f, 0.13f, 0.86f);
            SetRadius(_portraitEmptyLabel, 9f);
            SetBorder(_portraitEmptyLabel, 1f, new Color32(62, 76, 102, 255));
            _stage.Add(_portraitEmptyLabel);

            _dialoguePreview = new VisualElement();
            _dialoguePreview.style.position = Position.Absolute;
            _dialoguePreview.style.overflow = Overflow.Hidden;
            _stage.Add(_dialoguePreview);
            _dialogueLabel = new Label();
            _dialogueLabel.style.position = Position.Absolute;
            _dialogueLabel.style.whiteSpace = WhiteSpace.Normal;
            _dialogueLabel.style.color = Color.white;
            _dialoguePreview.Add(_dialogueLabel);
            _speakerPreview = new VisualElement();
            _speakerPreview.style.position = Position.Absolute;
            _speakerPreview.style.overflow = Overflow.Hidden;
            _stage.Add(_speakerPreview);
            _speakerLabel = new Label();
            _speakerLabel.style.position = Position.Absolute;
            _speakerLabel.style.left = 0f;
            _speakerLabel.style.right = 0f;
            _speakerLabel.style.top = 0f;
            _speakerLabel.style.bottom = 0f;
            _speakerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _speakerLabel.style.color = Color.white;
            _speakerPreview.Add(_speakerLabel);
            Label badge = CreatePill(
                "LIVE PREVIEW", new Color32(29, 78, 67, 235),
                new Color32(110, 231, 183, 255));
            badge.style.position = Position.Absolute;
            badge.style.left = 10f;
            badge.style.top = 8f;
            _stage.Add(badge);

            previewPanel.Add(_previewHost);
            Label hint = new Label(
                "Preview size follows the Game View automatically. Character and sample text are preview-only.");
            hint.style.marginTop = 8f;
            hint.style.fontSize = 9f;
            hint.style.color = MutedTextColor;
            hint.style.whiteSpace = WhiteSpace.Normal;
            previewPanel.Add(hint);
            return previewPanel;
        }
        private VisualElement BuildInspector()
        {
            VisualElement panel = new VisualElement();
            panel.style.minWidth = MinimumInspectorWidth;
            panel.style.flexShrink = 0f;
            panel.style.backgroundColor = PanelColor;
            SetRadius(panel, 12f);
            SetBorder(panel, 1f, new Color32(45, 58, 83, 255));

            VisualElement inspectorHeader = new VisualElement();
            inspectorHeader.style.paddingLeft = 14f;
            inspectorHeader.style.paddingRight = 14f;
            inspectorHeader.style.paddingTop = 12f;
            inspectorHeader.style.paddingBottom = 10f;
            inspectorHeader.style.borderBottomWidth = 1f;
            inspectorHeader.style.borderBottomColor = (Color)new Color32(45, 58, 83, 255);
            Label inspectorTitle = new Label("Presentation properties");
            inspectorTitle.style.fontSize = 14f;
            inspectorTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            inspectorTitle.style.color = TextColor;
            Label inspectorSubtitle = new Label(
                "Tune the selected graph nodes with immediate visual feedback.");
            inspectorSubtitle.style.marginTop = 2f;
            inspectorSubtitle.style.fontSize = 9f;
            inspectorSubtitle.style.color = MutedTextColor;
            inspectorSubtitle.style.whiteSpace = WhiteSpace.Normal;
            inspectorHeader.Add(inspectorTitle);
            inspectorHeader.Add(inspectorSubtitle);
            panel.Add(inspectorHeader);

            ScrollView inspector = new ScrollView(ScrollViewMode.Vertical);
            inspector.style.flexGrow = 1f;
            inspector.style.paddingLeft = 12f;
            inspector.style.paddingRight = 12f;
            inspector.style.paddingBottom = 12f;
            Foldout samples = NewFoldout("Preview Content");
            Label previewNote = new Label(
                "Preview-only settings. They are never written to the graph.");
            previewNote.style.fontSize = 9f;
            previewNote.style.color = (Color)new Color32(125, 211, 252, 255);
            previewNote.style.whiteSpace = WhiteSpace.Normal;
            previewNote.style.marginBottom = 6f;
            samples.Add(previewNote);
            ObjectField character = new ObjectField("Character")
            {
                objectType = typeof(NovelCharacter),
                allowSceneObjects = false,
                value = _draft.PreviewCharacter
            };
            character.tooltip =
                "Renders Body, Eyes, Details, and Mouth as one layered portrait.";
            character.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                RecordPreviewChange("Change Preview Character", () =>
                {
                    _draft.PreviewCharacter = evt.newValue as NovelCharacter;
                    if (_draft.PreviewCharacter != null &&
                        !string.IsNullOrWhiteSpace(
                            _draft.PreviewCharacter.SpeakerName))
                    {
                        _draft.SampleSpeaker =
                            _draft.PreviewCharacter.SpeakerName;
                        _sampleSpeakerField?.SetValueWithoutNotify(
                            _draft.SampleSpeaker);
                    }
                });
                PreviewChanged("Character preview updated.");
            });
            samples.Add(character);
            Toggle showCharacter = new Toggle("Show Character")
            {
                value = _draft.ShowCharacter
            };
            showCharacter.tooltip =
                "Hide the portrait when you want to inspect only the dialogue and speaker boxes.";
            showCharacter.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                RecordPreviewChange("Toggle Preview Character", () =>
                    _draft.ShowCharacter = evt.newValue);
                PreviewChanged(evt.newValue
                    ? "Character preview shown."
                    : "Character preview hidden.");
            });
            samples.Add(showCharacter);
            EnumField emotion = new EnumField("Emotion", _draft.PreviewEmotion);
            emotion.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                RecordPreviewChange("Change Preview Emotion", () =>
                    _draft.PreviewEmotion = (CharacterEmotion)evt.newValue);
                PreviewChanged("Preview emotion updated.");
            });
            samples.Add(emotion);
            _sampleSpeakerField = AddText(
                samples, "Speaker Name", _draft.SampleSpeaker, false,
                value => _draft.SampleSpeaker = value, false);
            AddText(samples, "Dialogue", _draft.SampleDialogue, true,
                value => _draft.SampleDialogue = value, false);
            inspector.Add(samples);
            Foldout dialogue = NewFoldout("Dialogue Box");
            AddEnum(dialogue, "Anchor", _draft.Dialogue.Anchor,
                value => _draft.Dialogue.Anchor = value);
            AddEnum(dialogue, "Text Alignment", _draft.Dialogue.TextAlignment,
                value => _draft.Dialogue.TextAlignment = value);
            AddFloat(dialogue, "Height", _draft.Dialogue.Height,
                value => _draft.Dialogue.Height = Mathf.Max(80f, value));
            AddFloat(dialogue, "Width (0 = Stretch)", _draft.Dialogue.Width,
                value => _draft.Dialogue.Width = Mathf.Max(0f, value));
            AddFloat(dialogue, "Bottom Margin", _draft.Dialogue.BottomMargin,
                value => _draft.Dialogue.BottomMargin = Mathf.Max(0f, value));
            AddFloat(dialogue, "Horizontal Margin",
                _draft.Dialogue.HorizontalMargin,
                value => _draft.Dialogue.HorizontalMargin = Mathf.Max(0f, value));
            AddFloat(dialogue, "Horizontal Padding",
                _draft.Dialogue.HorizontalPadding,
                value => _draft.Dialogue.HorizontalPadding = Mathf.Max(0f, value));
            AddFloat(dialogue, "Vertical Padding",
                _draft.Dialogue.VerticalPadding,
                value => _draft.Dialogue.VerticalPadding = Mathf.Max(0f, value));
            AddFloat(dialogue, "Base Font Size", _draft.Dialogue.BaseFontSize,
                value => _draft.Dialogue.BaseFontSize = Mathf.Max(1f, value));
            AddToggle(dialogue, "Auto Size", _draft.Dialogue.AutoSize,
                value => _draft.Dialogue.AutoSize = value);
            AddFloat(dialogue, "Minimum Font Size",
                _draft.Dialogue.MinimumFontSize,
                value => _draft.Dialogue.MinimumFontSize = Mathf.Max(1f, value));
            AddFloat(dialogue, "Maximum Font Size",
                _draft.Dialogue.MaximumFontSize,
                value => _draft.Dialogue.MaximumFontSize = Mathf.Max(1f, value));
            AddStyleFields(
                dialogue,
                () => _draft.Dialogue.Style,
                value => _draft.Dialogue.Style = value,
                NovelBoxStyle.DialogueDefault);
            inspector.Add(dialogue);
            Foldout speaker = NewFoldout("Speaker Box");
            AddEnum(speaker, "Anchor", _draft.Speaker.Anchor,
                value => _draft.Speaker.Anchor = value);
            AddFloat(speaker, "Font Size", _draft.Speaker.FontSize,
                value => _draft.Speaker.FontSize = Mathf.Max(1f, value));
            AddFloat(speaker, "Horizontal Padding",
                _draft.Speaker.HorizontalPadding,
                value => _draft.Speaker.HorizontalPadding = Mathf.Max(0f, value));
            AddFloat(speaker, "Vertical Padding",
                _draft.Speaker.VerticalPadding,
                value => _draft.Speaker.VerticalPadding = Mathf.Max(0f, value));
            AddFloat(speaker, "Horizontal Offset",
                _draft.Speaker.HorizontalOffset,
                value => _draft.Speaker.HorizontalOffset = value);
            AddFloat(speaker, "Vertical Overlap",
                _draft.Speaker.VerticalOverlap,
                value => _draft.Speaker.VerticalOverlap = value);
            AddStyleFields(
                speaker,
                () => _draft.Speaker.Style,
                value => _draft.Speaker.Style = value,
                NovelBoxStyle.SpeakerDefault);
            inspector.Add(speaker);
            panel.Add(inspector);
            return panel;
        }
        private VisualElement BuildFooter()
        {
            VisualElement footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.paddingLeft = 16f;
            footer.style.paddingRight = 16f;
            footer.style.paddingTop = 9f;
            footer.style.paddingBottom = 9f;
            footer.style.backgroundColor = ToolbarColor;
            footer.style.borderTopWidth = 1f;
            footer.style.borderTopColor = (Color)new Color32(49, 63, 88, 255);
            _status = new Label(
                "Edits update the selected nodes live.  •  Ctrl+S saves both boxes.");
            _status.style.flexGrow = 1f;
            _status.style.color = MutedTextColor;
            _status.style.fontSize = 10f;
            footer.Add(_status);
            Button revert = new Button(UndoLastChange)
            {
                text = "Undo Last",
                tooltip = "Undo the last composer or graph edit. Shortcut: Ctrl+Z."
            };
            revert.style.width = 90f;
            revert.style.height = 30f;
            revert.style.marginRight = 6f;
            footer.Add(revert);
            _applyButton = new Button(ApplyToGraph)
            {
                text = "Save Graph",
                tooltip = "Save the live dialogue and speaker changes. Shortcut: Ctrl+S (Cmd+S on macOS)."
            };
            _applyButton.style.width = 125f;
            _applyButton.style.height = 30f;
            _applyButton.style.backgroundColor = AccentColor;
            _applyButton.style.color = Color.white;
            _applyButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            SetRadius(_applyButton, 6f);
            footer.Add(_applyButton);
            return footer;
        }
        private static Foldout NewFoldout(string text)
        {
            Foldout foldout = new Foldout { text = text, value = true };
            foldout.style.marginTop = 10f;
            foldout.style.marginBottom = 2f;
            foldout.style.paddingLeft = 10f;
            foldout.style.paddingRight = 10f;
            foldout.style.paddingTop = 7f;
            foldout.style.paddingBottom = 9f;
            foldout.style.backgroundColor = CardColor;
            foldout.style.color = TextColor;
            SetRadius(foldout, 8f);
            SetBorder(foldout, 1f, new Color32(49, 63, 88, 255));
            return foldout;
        }
        private TextField AddText(
            VisualElement parent,
            string label,
            string value,
            bool multiline,
            Action<string> setter,
            bool marksGraphDirty = true)
        {
            TextField field = new TextField(label)
            {
                value = value ?? string.Empty,
                multiline = multiline
            };
            if (multiline) field.style.minHeight = 58f;
            field.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                if (marksGraphDirty)
                {
                    setter(evt.newValue ?? string.Empty);
                    DraftChanged();
                }
                else
                {
                    RecordPreviewChange("Edit Preview Content", () =>
                        setter(evt.newValue ?? string.Empty));
                    PreviewChanged("Preview content updated.");
                }
            });
            parent.Add(field);
            return field;
        }
        private void AddFloat(
            VisualElement parent,
            string label,
            float value,
            Action<float> setter)
        {
            FloatField field = new FloatField(label) { value = value };
            field.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                setter(evt.newValue);
                DraftChanged();
            });
            parent.Add(field);
        }
        private void AddToggle(
            VisualElement parent,
            string label,
            bool value,
            Action<bool> setter)
        {
            Toggle field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                setter(evt.newValue);
                DraftChanged();
            });
            parent.Add(field);
        }
        private void AddEnum<T>(
            VisualElement parent,
            string label,
            T value,
            Action<T> setter)
            where T : Enum
        {
            EnumField field = new EnumField(label, value);
            field.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                setter((T)evt.newValue);
                DraftChanged();
            });
            parent.Add(field);
        }

        private void AddStyleFields(
            VisualElement parent,
            Func<NovelBoxStyle> getter,
            Action<NovelBoxStyle> setter,
            NovelBoxStyle defaults)
        {
            Foldout styleFoldout = new Foldout
            {
                text = "Style",
                value = false
            };
            ColorField fill = new ColorField("Fill Color")
            { value = getter().FillColor };
            fill.RegisterValueChangedCallback(evt =>
            {
                NovelBoxStyle style = getter();
                style.FillColor = evt.newValue;
                setter(style);
                DraftChanged();
            });
            styleFoldout.Add(fill);
            AddFloat(styleFoldout, "Opacity", getter().Opacity, value =>
            {
                NovelBoxStyle style = getter();
                style.Opacity = Mathf.Clamp01(value);
                setter(style);
            });
            AddFloat(styleFoldout, "Corner Radius", getter().CornerRadius, value =>
            {
                NovelBoxStyle style = getter();
                style.CornerRadius = Mathf.Max(0f, value);
                setter(style);
            });
            AddToggle(styleFoldout, "Outline", getter().OutlineEnabled, value =>
            {
                NovelBoxStyle style = getter();
                style.OutlineEnabled = value;
                setter(style);
            });
            ColorField outline = new ColorField("Outline Color")
            { value = getter().OutlineColor };
            outline.RegisterValueChangedCallback(evt =>
            {
                NovelBoxStyle style = getter();
                style.OutlineColor = evt.newValue;
                setter(style);
                DraftChanged();
            });
            styleFoldout.Add(outline);
            AddFloat(styleFoldout, "Outline Thickness",
                getter().OutlineThickness, value =>
                {
                    NovelBoxStyle style = getter();
                    style.OutlineThickness = Mathf.Max(0f, value);
                    setter(style);
                });
            Button reset = new Button(() =>
            {
                setter(defaults);
                DraftChanged("Reset Presentation Style");
                Rebuild();
                SetStatus(
                    "Style reset and updated on the selected node. Ctrl+S saves it.",
                    false);
            })
            { text = "Reset Style" };
            styleFoldout.Add(reset);
            parent.Add(styleFoldout);
        }
        private void DraftChanged(
            string actionName = "Edit Dialogue Presentation")
        {
            ValidateDraft();
            bool updated = RecordGraphChange(actionName, () =>
            {
                WriteDialogueOptions(DialogueNode, _draft.Dialogue);
                WriteSpeakerOptions(SpeakerNode, _draft.Speaker);
            });
            if (updated)
                _draftDirty = true;
            RefreshPreview();
            SetStatus(updated
                ? "Selected nodes updated. Ctrl+S saves the graph asset."
                : "The selected nodes could not be updated.", !updated);
        }

        private bool RecordGraphChange(string actionName, Action change)
        {
            if (_graph == null || DialogueNode == null ||
                SpeakerNode == null || change == null)
                return false;
            bool recording = false;
            try
            {
                _graph.UndoBeginRecordGraph(actionName);
                recording = true;
                change();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
            finally
            {
                if (recording)
                    _graph.UndoEndRecordGraph();
            }
        }

        private void PreviewChanged(string message)
        {
            RefreshPreview();
            SetStatus(message + " Preview-only; graph values are unchanged.", false);
        }
        private void RefreshPreview()
        {
            if (_building || _draft == null || _previewHost == null ||
                _stage == null || _dialoguePreview == null ||
                _speakerPreview == null)
                return;
            float hostWidth = _previewHost.resolvedStyle.width;
            float hostHeight = _previewHost.resolvedStyle.height;
            if (hostWidth <= 1f || hostHeight <= 1f ||
                float.IsNaN(hostWidth) || float.IsNaN(hostHeight))
                return;
            Vector2 canvasSize = new Vector2(
                Mathf.Max(1, _draft.Resolution.x),
                Mathf.Max(1, _draft.Resolution.y));
            float scale = Mathf.Min(
                (hostWidth - 36f) / canvasSize.x,
                (hostHeight - 36f) / canvasSize.y);
            scale = Mathf.Max(0.01f, scale);
            float stageWidth = canvasSize.x * scale;
            float stageHeight = canvasSize.y * scale;
            _stage.style.left = (hostWidth - stageWidth) * 0.5f;
            _stage.style.top = (hostHeight - stageHeight) * 0.5f;
            _stage.style.width = stageWidth;
            _stage.style.height = stageHeight;
            if (_resolutionLabel != null)
                _resolutionLabel.text = ResolutionText(_draft.Resolution);
            RefreshPortraitPreview(canvasSize, scale);
            NovelRectLayout dialogueLayout = NovelPresentationLayout.Dialogue(
                _draft.Dialogue.Anchor,
                _draft.Dialogue.Width,
                _draft.Dialogue.Height,
                _draft.Dialogue.BottomMargin,
                _draft.Dialogue.HorizontalMargin);
            Rect dialogueRect = ResolveRect(canvasSize, dialogueLayout);
            Place(_dialoguePreview, dialogueRect, canvasSize.y, scale);
            ApplyBoxStyle(
                _dialoguePreview, _draft.Dialogue.Style, scale);
            float horizontalPadding =
                Mathf.Max(0f, _draft.Dialogue.HorizontalPadding) * scale;
            float verticalPadding =
                Mathf.Max(0f, _draft.Dialogue.VerticalPadding) * scale;
            _dialogueLabel.text = _draft.SampleDialogue ?? string.Empty;
            _dialogueLabel.style.left = horizontalPadding;
            _dialogueLabel.style.right = horizontalPadding;
            _dialogueLabel.style.top = verticalPadding;
            _dialogueLabel.style.bottom = verticalPadding;
            _dialogueLabel.style.unityTextAlign =
                ToTextAnchor(_draft.Dialogue.TextAlignment);
            _dialogueLabel.style.fontSize = Mathf.Max(
                6f, PreviewDialogueFontSize() * scale);
            Vector2 preferred = MeasureSpeaker(
                _draft.SampleSpeaker,
                _draft.Speaker.FontSize);
            float speakerWidth = preferred.x +
                Mathf.Max(0f, _draft.Speaker.HorizontalPadding) * 2f;
            float speakerHeight = preferred.y +
                Mathf.Max(0f, _draft.Speaker.VerticalPadding) * 2f;
            NovelRectLayout speakerLayout = NovelPresentationLayout.Speaker(
                _draft.Speaker.Anchor,
                speakerWidth,
                speakerHeight,
                _draft.Speaker.HorizontalOffset,
                _draft.Speaker.VerticalOverlap);
            Rect speakerLocal = ResolveRect(dialogueRect.size, speakerLayout);
            Rect speakerRect = new Rect(
                dialogueRect.position + speakerLocal.position,
                speakerLocal.size);
            Place(_speakerPreview, speakerRect, canvasSize.y, scale);
            ApplyBoxStyle(
                _speakerPreview, _draft.Speaker.Style, scale);
            _speakerLabel.text = _draft.SampleSpeaker ?? string.Empty;
            _speakerLabel.style.fontSize = Mathf.Max(
                6f, _draft.Speaker.FontSize * scale);
        }

        private void RefreshPortraitPreview(Vector2 canvasSize, float scale)
        {
            if (_portraitRoot == null || _portraitEmptyLabel == null)
                return;
            NovelCharacter character = _draft.PreviewCharacter;
            CharacterPortrait portrait = character != null
                ? character.GetPortrait(_draft.PreviewEmotion)
                : default;
            bool hasPortrait = portrait.Body != null || portrait.Eyes != null ||
                portrait.Details != null || portrait.Mouth != null;
            bool showPortrait = _draft.ShowCharacter && hasPortrait;
            _portraitRoot.style.display = showPortrait
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _portraitEmptyLabel.style.display =
                _draft.ShowCharacter && !hasPortrait
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            if (!showPortrait)
                return;

            _portraitBody.sprite = portrait.Body;
            _portraitEyes.sprite = portrait.Eyes;
            _portraitDetails.sprite = portrait.Details;
            _portraitMouth.sprite = portrait.Mouth;
            SetLayerVisible(_portraitBody, portrait.Body != null);
            SetLayerVisible(_portraitEyes, portrait.Eyes != null);
            SetLayerVisible(_portraitDetails, portrait.Details != null);
            SetLayerVisible(_portraitMouth, portrait.Mouth != null);

            Vector2 portraitSize = ResolvePortraitSize(canvasSize);
            _portraitRoot.style.left =
                (canvasSize.x - portraitSize.x) * 0.5f * scale;
            _portraitRoot.style.top =
                (canvasSize.y - portraitSize.y) * scale;
            _portraitRoot.style.width = portraitSize.x * scale;
            _portraitRoot.style.height = portraitSize.y * scale;
        }

        private static Vector2 ResolvePortraitSize(Vector2 canvasSize)
        {
            Vector2 referenceSize = TryGetConfiguredPortraitSize(out Vector2 size)
                ? size
                : new Vector2(520f, 760f);
            float referenceScale = Mathf.Min(
                canvasSize.x / 1920f,
                canvasSize.y / 1080f);
            return new Vector2(
                Mathf.Max(1f, referenceSize.x * referenceScale),
                Mathf.Max(1f, referenceSize.y * referenceScale));
        }

        private static bool TryGetConfiguredPortraitSize(out Vector2 size)
        {
            size = Vector2.zero;
            foreach (NovelGraphRunner runner in
                     Resources.FindObjectsOfTypeAll<NovelGraphRunner>())
            {
                if (runner == null || !runner.gameObject.scene.IsValid() ||
                    runner.PortraitPrefab == null)
                    continue;
                CharacterInfo info =
                    runner.PortraitPrefab.GetComponent<CharacterInfo>();
                if (info == null || info.transform is not RectTransform root)
                    continue;
                UnityEngine.UI.Image[] layers =
                    info.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                if (layers.Length == 0)
                    continue;

                Vector2 minimum = new Vector2(
                    float.PositiveInfinity, float.PositiveInfinity);
                Vector2 maximum = new Vector2(
                    float.NegativeInfinity, float.NegativeInfinity);
                Vector2 rootSize = root.rect.size;
                if (rootSize.x <= 1f || rootSize.y <= 1f)
                    rootSize = root.sizeDelta;
                bool found = false;
                foreach (UnityEngine.UI.Image layer in layers)
                {
                    if (layer == null ||
                        layer.transform is not RectTransform layerRect)
                        continue;
                    Vector2 layerSize = Vector2.Scale(
                        rootSize, layerRect.anchorMax - layerRect.anchorMin) +
                        layerRect.sizeDelta;
                    Vector2 anchorCenter = Vector2.Scale(
                        rootSize,
                        (layerRect.anchorMin + layerRect.anchorMax) * 0.5f) -
                        Vector2.Scale(rootSize, root.pivot);
                    Vector2 pivotPosition =
                        anchorCenter + layerRect.anchoredPosition;
                    Vector2 layerMinimum = pivotPosition -
                        Vector2.Scale(layerSize, layerRect.pivot);
                    Vector2 layerMaximum = layerMinimum + layerSize;
                    minimum = Vector2.Min(minimum, layerMinimum);
                    maximum = Vector2.Max(maximum, layerMaximum);
                    found = true;
                }

                size = maximum - minimum;
                if (found && size.x > 1f && size.y > 1f &&
                    !float.IsNaN(size.x) && !float.IsNaN(size.y))
                    return true;
            }
            size = Vector2.zero;
            return false;
        }

        private static void AddPreviewGrid(VisualElement stage)
        {
            for (int index = 1; index < 20; index++)
            {
                bool major = index % 5 == 0;
                VisualElement vertical = new VisualElement();
                vertical.style.position = Position.Absolute;
                vertical.style.left = Length.Percent(index * 5f);
                vertical.style.top = 0f;
                vertical.style.bottom = 0f;
                vertical.style.width = 1f;
                vertical.style.backgroundColor = major
                    ? new Color(0.45f, 0.52f, 0.75f, 0.18f)
                    : new Color(0.45f, 0.52f, 0.75f, 0.075f);
                vertical.pickingMode = PickingMode.Ignore;
                stage.Add(vertical);
            }

            for (int index = 1; index < 10; index++)
            {
                bool major = index % 5 == 0;
                VisualElement horizontal = new VisualElement();
                horizontal.style.position = Position.Absolute;
                horizontal.style.left = 0f;
                horizontal.style.right = 0f;
                horizontal.style.top = Length.Percent(index * 10f);
                horizontal.style.height = 1f;
                horizontal.style.backgroundColor = major
                    ? new Color(0.45f, 0.52f, 0.75f, 0.18f)
                    : new Color(0.45f, 0.52f, 0.75f, 0.075f);
                horizontal.pickingMode = PickingMode.Ignore;
                stage.Add(horizontal);
            }
        }

        private static Image CreatePortraitLayer(string name)
        {
            Image layer = new Image
            {
                name = name,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            layer.style.position = Position.Absolute;
            layer.style.left = 0f;
            layer.style.right = 0f;
            layer.style.top = 0f;
            layer.style.bottom = 0f;
            return layer;
        }

        private static void SetLayerVisible(Image layer, bool visible)
        {
            if (layer != null)
                layer.style.display = visible
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        private float PreviewDialogueFontSize()
        {
            float minimum = Mathf.Max(1f, _draft.Dialogue.MinimumFontSize);
            float maximum = Mathf.Max(
                minimum, _draft.Dialogue.MaximumFontSize);
            float baseSize = Mathf.Max(1f, _draft.Dialogue.BaseFontSize);
            return _draft.Dialogue.AutoSize
                ? Mathf.Clamp(baseSize, minimum, maximum)
                : baseSize;
        }

        private Vector2 MeasureSpeaker(string value, float fontSize)
        {
            if (_measurementText == null)
            {
                _measurementObject = new GameObject(
                    "Novelify Preview Text",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                _measurementObject.hideFlags = HideFlags.HideAndDontSave;
                _measurementText =
                    _measurementObject.GetComponent<TextMeshProUGUI>();
                _measurementText.font = TMP_Settings.defaultFontAsset;
                _measurementText.enableAutoSizing = false;
                _measurementText.textWrappingMode = TextWrappingModes.NoWrap;
                _measurementText.richText = true;
            }
            _measurementText.fontSize = Mathf.Max(1f, fontSize);
            return _measurementText.GetPreferredValues(
                value ?? string.Empty, 10000f, 10000f);
        }
        private static Rect ResolveRect(
            Vector2 parentSize,
            NovelRectLayout layout)
        {
            Vector2 anchorSpan = layout.AnchorMax - layout.AnchorMin;
            Vector2 size = Vector2.Scale(parentSize, anchorSpan) +
                layout.SizeDelta;
            Vector2 anchor = Vector2.Scale(
                parentSize,
                (layout.AnchorMin + layout.AnchorMax) * 0.5f);
            Vector2 minimum = anchor + layout.AnchoredPosition -
                Vector2.Scale(size, layout.Pivot);
            return new Rect(minimum, size);
        }
        private static void Place(
            VisualElement target,
            Rect pixels,
            float canvasHeight,
            float scale)
        {
            target.style.left = pixels.x * scale;
            target.style.top = (canvasHeight - pixels.yMax) * scale;
            target.style.width = pixels.width * scale;
            target.style.height = pixels.height * scale;
        }
        private static void ApplyBoxStyle(
            VisualElement target,
            NovelBoxStyle source,
            float scale)
        {
            NovelBoxStyle style = source.Validated();
            Color fill = style.FillColor;
            fill.a *= style.Opacity;
            target.style.backgroundColor = fill;
            float radius = style.CornerRadius * scale;
            target.style.borderTopLeftRadius = radius;
            target.style.borderTopRightRadius = radius;
            target.style.borderBottomLeftRadius = radius;
            target.style.borderBottomRightRadius = radius;
            float border = style.OutlineEnabled
                ? style.OutlineThickness * scale
                : 0f;
            SetBorder(target, border, style.OutlineColor);
        }
        private static void SetBorder(
            VisualElement target,
            float width,
            Color color)
        {
            target.style.borderLeftWidth = width;
            target.style.borderRightWidth = width;
            target.style.borderTopWidth = width;
            target.style.borderBottomWidth = width;
            target.style.borderLeftColor = color;
            target.style.borderRightColor = color;
            target.style.borderTopColor = color;
            target.style.borderBottomColor = color;
        }

        private static void SetRadius(VisualElement target, float radius)
        {
            target.style.borderTopLeftRadius = radius;
            target.style.borderTopRightRadius = radius;
            target.style.borderBottomLeftRadius = radius;
            target.style.borderBottomRightRadius = radius;
        }

        private static Label CreatePill(string text, Color background, Color color)
        {
            Label pill = new Label(text);
            pill.style.height = 24f;
            pill.style.paddingLeft = 9f;
            pill.style.paddingRight = 9f;
            pill.style.unityTextAlign = TextAnchor.MiddleCenter;
            pill.style.unityFontStyleAndWeight = FontStyle.Bold;
            pill.style.fontSize = 9f;
            pill.style.backgroundColor = background;
            pill.style.color = color;
            SetRadius(pill, 12f);
            return pill;
        }
        private static TextAnchor ToTextAnchor(
            NovelTextAlignment alignment)
        {
            return alignment switch
            {
                NovelTextAlignment.TopCenter => TextAnchor.UpperCenter,
                NovelTextAlignment.TopRight => TextAnchor.UpperRight,
                NovelTextAlignment.CenterLeft => TextAnchor.MiddleLeft,
                NovelTextAlignment.CenterCenter => TextAnchor.MiddleCenter,
                NovelTextAlignment.CenterRight => TextAnchor.MiddleRight,
                NovelTextAlignment.BottomLeft => TextAnchor.LowerLeft,
                NovelTextAlignment.BottomCenter => TextAnchor.LowerCenter,
                NovelTextAlignment.BottomRight => TextAnchor.LowerRight,
                _ => TextAnchor.UpperLeft
            };
        }
        private void ApplyToGraph()
        {
            if (_graph == null || DialogueNode == null || SpeakerNode == null)
                return;
            try
            {
                GraphDatabase.SaveGraph(_graph);
                AssetDatabase.SaveAssets();
                _draftDirty = false;
                SetStatus(
                    "Dialogue and speaker presentation saved to the graph asset.",
                    false);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus(
                    "Save failed. See the Console for the exact error.", true);
            }
        }

        private void UndoLastChange()
        {
            Undo.PerformUndo();
        }
        private void SelectDialogue(int index)
        {
            if (index == _dialogueIndex) return;
            if (!ConfirmDiscard())
            {
                Rebuild();
                return;
            }
            _dialogueIndex = Mathf.Clamp(index, 0, _dialogueNodes.Count - 1);
            LoadDraft();
            Rebuild();
        }
        private void SelectSpeaker(int index)
        {
            if (index == _speakerIndex) return;
            if (!ConfirmDiscard())
            {
                Rebuild();
                return;
            }
            _speakerIndex = Mathf.Clamp(index, 0, _speakerNodes.Count - 1);
            LoadDraft();
            Rebuild();
        }
        private bool ConfirmDiscard()
        {
            return !_draftDirty || EditorUtility.DisplayDialog(
                "Discard presentation draft?",
                "Changing the selected nodes discards unapplied changes.",
                "Discard",
                "Keep Editing");
        }
        private void ValidateDraft()
        {
            if (_draft == null) return;
            _draft.Dialogue.Style = _draft.Dialogue.Style.Validated();
            _draft.Dialogue.Height = Mathf.Max(80f, _draft.Dialogue.Height);
            _draft.Dialogue.Width = Mathf.Max(0f, _draft.Dialogue.Width);
            _draft.Dialogue.BottomMargin =
                Mathf.Max(0f, _draft.Dialogue.BottomMargin);
            _draft.Dialogue.HorizontalMargin =
                Mathf.Max(0f, _draft.Dialogue.HorizontalMargin);
            _draft.Dialogue.HorizontalPadding =
                Mathf.Max(0f, _draft.Dialogue.HorizontalPadding);
            _draft.Dialogue.VerticalPadding =
                Mathf.Max(0f, _draft.Dialogue.VerticalPadding);
            _draft.Dialogue.BaseFontSize =
                Mathf.Max(1f, _draft.Dialogue.BaseFontSize);
            _draft.Dialogue.MinimumFontSize =
                Mathf.Max(1f, _draft.Dialogue.MinimumFontSize);
            _draft.Dialogue.MaximumFontSize = Mathf.Max(
                _draft.Dialogue.MinimumFontSize,
                _draft.Dialogue.MaximumFontSize);
            _draft.Speaker.Style = _draft.Speaker.Style.Validated();
            _draft.Speaker.FontSize =
                Mathf.Max(1f, _draft.Speaker.FontSize);
            _draft.Speaker.HorizontalPadding =
                Mathf.Max(0f, _draft.Speaker.HorizontalPadding);
            _draft.Speaker.VerticalPadding =
                Mathf.Max(0f, _draft.Speaker.VerticalPadding);
        }
        private static void WriteDialogueOptions(
            INode node,
            DialogueDraft draft)
        {
            WriteStyle(node, draft.Style);
            Write(node, "Text Alignment", draft.TextAlignment);
            Write(node, "Anchor", draft.Anchor);
            Write(node, "Base Font Size", draft.BaseFontSize);
            Write(node, "Auto Size", draft.AutoSize);
            Write(node, "Minimum Font Size", draft.MinimumFontSize);
            Write(node, "Maximum Font Size", draft.MaximumFontSize);
            Write(node, "Height", draft.Height);
            Write(node, "Width", draft.Width);
            Write(node, "Bottom Margin", draft.BottomMargin);
            Write(node, "Horizontal Margin", draft.HorizontalMargin);
            Write(node, "Horizontal Padding", draft.HorizontalPadding);
            Write(node, "Vertical Padding", draft.VerticalPadding);
        }
        private static void WriteSpeakerOptions(
            INode node,
            SpeakerDraft draft)
        {
            WriteStyle(node, draft.Style);
            Write(node, "Anchor", draft.Anchor);
            Write(node, "Font Size", draft.FontSize);
            Write(node, "Horizontal Padding", draft.HorizontalPadding);
            Write(node, "Vertical Padding", draft.VerticalPadding);
            Write(node, "Horizontal Offset", draft.HorizontalOffset);
            Write(node, "Vertical Overlap", draft.VerticalOverlap);
        }
        private static NovelBoxStyle ReadStyle(
            INode node,
            NovelBoxStyle fallback)
        {
            return new NovelBoxStyle
            {
                FillColor = Read(node, "Fill Color", fallback.FillColor),
                Opacity = Read(node, "Opacity", fallback.Opacity),
                CornerRadius = Read(
                    node, "Corner Radius", fallback.CornerRadius),
                OutlineEnabled = Read(
                    node, "Outline", fallback.OutlineEnabled),
                OutlineColor = Read(
                    node, "Outline Color", fallback.OutlineColor),
                OutlineThickness = Read(
                    node, "Outline Thickness", fallback.OutlineThickness)
            }.Validated();
        }
        private static void WriteStyle(INode node, NovelBoxStyle style)
        {
            style = style.Validated();
            Write(node, "Fill Color", style.FillColor);
            Write(node, "Opacity", style.Opacity);
            Write(node, "Corner Radius", style.CornerRadius);
            Write(node, "Outline", style.OutlineEnabled);
            Write(node, "Outline Color", style.OutlineColor);
            Write(node, "Outline Thickness", style.OutlineThickness);
        }
        private static T Read<T>(INode node, string name, T fallback)
        {
            INodeOption option = node?.GetNodeOptionByName(name);
            return option != null && option.TryGetValue(out T value)
                ? value
                : fallback;
        }
        private static void Write<T>(INode node, string name, T value)
        {
            node?.GetNodeOptionByName(name)?.TrySetValue(value);
        }
        private void SetStatus(string message, bool error)
        {
            if (_status == null) return;
            _status.text = message;
            _status.style.color = error
                ? (Color)new Color32(248, 113, 113, 255)
                : (Color)new Color32(148, 163, 184, 255);
        }
        private string MissingNodeMessage()
        {
            if (_dialogueNodes.Count == 0 && _speakerNodes.Count == 0)
                return "Add a Create Dialogue Box node and a Create Dialogue " +
                       "Speaker Box node, then open the composer again.";
            if (_dialogueNodes.Count == 0)
                return "Add a Create Dialogue Box node, then open the " +
                       "composer again.";
            return "Add a Create Dialogue Speaker Box node, then open the " +
                   "composer again.";
        }
        private static List<string> NodeNames(string prefix, int count)
        {
            List<string> names = new List<string>(count);
            for (int index = 0; index < count; index++)
                names.Add($"{prefix} {index + 1}");
            return names;
        }

        private static T FindClosestNode<T>(Graph graph, INode source)
            where T : class, INode
        {
            if (graph == null)
                return null;
            List<T> candidates = graph.GetNodes().OfType<T>().ToList();
            if (candidates.Count == 0)
                return null;
            if (source == null || !TryGetNodePosition(source, out Vector2 origin))
                return candidates[0];

            T closest = candidates[0];
            float closestDistance = float.PositiveInfinity;
            foreach (T candidate in candidates)
            {
                if (!TryGetNodePosition(candidate, out Vector2 position))
                    continue;
                float distance = (position - origin).sqrMagnitude;
                if (distance >= closestDistance)
                    continue;
                closest = candidate;
                closestDistance = distance;
            }
            return closest;
        }

        private static bool TryGetNodePosition(INode node, out Vector2 position)
        {
            position = Vector2.zero;
            object model = ReadMember(node, "NodeModel") ??
                ReadMember(node, "m_Implementation") ?? node;
            object nested = ReadMember(model, "NodeModel");
            if (nested != null)
                model = nested;
            object value = ReadMember(model, "Position") ??
                ReadMember(model, "m_Position");
            if (value is Vector2 vector)
            {
                position = vector;
                return true;
            }
            if (value is Rect rect)
            {
                position = rect.position;
                return true;
            }
            return false;
        }

        private static object ReadMember(object source, string name)
        {
            if (source == null)
                return null;
            const BindingFlags flags = BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic;
            Type type = source.GetType();
            while (type != null)
            {
                try
                {
                    PropertyInfo property = type.GetProperty(
                        name, flags | BindingFlags.DeclaredOnly);
                    if (property != null &&
                        property.GetIndexParameters().Length == 0)
                        return property.GetValue(source);
                    FieldInfo field = type.GetField(
                        name, flags | BindingFlags.DeclaredOnly);
                    if (field != null)
                        return field.GetValue(source);
                }
                catch
                {
                    return null;
                }
                type = type.BaseType;
            }
            return null;
        }

        private void TryFindGraphPreview(
            out NovelCharacter character,
            out CharacterEmotion emotion)
        {
            character = null;
            emotion = CharacterEmotion.Neutral;
            if (_graph != null)
            {
                foreach (INode node in _graph.GetNodes())
                {
                    INodeOption option =
                        node.GetNodeOptionByName("Speaker Preview");
                    if (option == null ||
                        !option.TryGetValue(out SpeakerPortraitOption preview) ||
                        preview.Character == null)
                        continue;
                    character = preview.Character;
                    emotion = preview.Emotion;
                    return;
                }
            }

            string[] characterGuids = AssetDatabase.FindAssets(
                "t:NovelCharacter", new[] { "Assets" });
            if (characterGuids.Length == 0)
                return;
            Array.Sort(characterGuids, StringComparer.Ordinal);
            string path = AssetDatabase.GUIDToAssetPath(characterGuids[0]);
            character = AssetDatabase.LoadAssetAtPath<NovelCharacter>(path);
        }

        private void SyncGameViewResolution()
        {
            if (_draft == null)
                return;
            Vector2Int resolution = GetGameViewResolution();
            if (_draft.Resolution == resolution)
                return;
            _draft.Resolution = resolution;
            if (_resolutionLabel != null)
                _resolutionLabel.text = ResolutionText(resolution);
            RefreshPreview();
            Repaint();
        }

        private static Vector2Int GetGameViewResolution()
        {
            try
            {
                Vector2 size = Handles.GetMainGameViewSize();
                if (size.x >= 1f && size.y >= 1f &&
                    !float.IsNaN(size.x) && !float.IsNaN(size.y))
                {
                    return new Vector2Int(
                        Mathf.Max(1, Mathf.RoundToInt(size.x)),
                        Mathf.Max(1, Mathf.RoundToInt(size.y)));
                }
            }
            catch (InvalidOperationException)
            {
                // A Game View may not exist yet during an editor domain reload.
            }

            return new Vector2Int(
                Mathf.Max(1, PlayerSettings.defaultScreenWidth),
                Mathf.Max(1, PlayerSettings.defaultScreenHeight));
        }

        private static string ResolutionText(Vector2Int resolution)
        {
            return $"{resolution.x} × {resolution.y}  •  GAME VIEW";
        }
    }
}
