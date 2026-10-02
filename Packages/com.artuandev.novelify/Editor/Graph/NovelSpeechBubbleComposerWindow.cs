using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Novelify.Editor
{
    internal sealed class NovelSpeechBubbleComposerWindow : EditorWindow
    {
        private sealed class BubbleDraft
        {
            public NovelBoxStyle Style;
            public NovelSpeechBubblePlacement Placement;
            public NovelDialogueAnchor ScreenAnchor;
            public float HorizontalOffset;
            public float VerticalOffset;
            public bool KeepInsideViewport;
            public bool AutoSize;
            public float MinimumWidth;
            public float MaximumWidth;
            public float MinimumHeight;
            public float MaximumHeight;
            public float FixedWidth;
            public float FixedHeight;
            public NovelTextAlignment TextAlignment;
            public float HorizontalPadding;
            public float VerticalPadding;
            public float DialogueFontSize;
            public float SpeakerFontSize;
            public bool ShowSpeakerName;
            public bool ShowTail;
            public Vector2 TailTarget;
            public float TailWidth;
            public float TailLength;
            public float TargetMargin;
            public NovelCharacter PreviewCharacter;
            public CharacterEmotion PreviewEmotion = CharacterEmotion.Neutral;
            public bool ShowCharacter = true;
            public bool PreviewThinking;
            public string SampleSpeaker = "Test Speaker";
            public string SampleDialogue =
                "This speech bubble follows the speaking character.";
            public Vector2Int Resolution = new Vector2Int(1920, 1080);
            public Vector2 PreviewPosition;
            public float PreviewRotation;
            public Vector2 PreviewScale = Vector2.one;
            public bool HasAuthoredTransform;
        }

        private sealed class PreviewUndoState : ScriptableObject
        {
            public NovelCharacter Character;
            public CharacterEmotion Emotion;
            public bool ShowCharacter = true;
            public bool Thinking;
            public string Speaker;
            public string Dialogue;
        }

        private static readonly Color WindowColor =
            new Color32(6, 14, 24, 255);
        private static readonly Color ToolbarColor =
            new Color32(12, 29, 44, 255);
        private static readonly Color PanelColor =
            new Color32(14, 35, 53, 255);
        private static readonly Color CardColor =
            new Color32(20, 48, 69, 255);
        private static readonly Color StageColor =
            new Color32(7, 23, 36, 255);
        private static readonly Color AccentColor =
            new Color32(12, 150, 145, 255);
        private static readonly Color AccentBrightColor =
            new Color32(94, 231, 203, 255);
        private static readonly Color TextColor =
            new Color32(246, 244, 239, 255);
        private static readonly Color MutedTextColor =
            new Color32(151, 180, 196, 255);

        private const string InspectorWidthKey =
            "Novelify.SpeechBubbleComposer.InspectorWidth";
        private const float DefaultInspectorWidth = 380f;
        private const float MinimumInspectorWidth = 310f;

        private Graph _graph;
        private SpeechBubblePresentationNode _node;
        private SpeechBubbleNode _dialogueNode;
        private string _previewInstanceID = string.Empty;
        private BubbleDraft _draft;
        private PreviewUndoState _previewUndo;
        private bool _building;
        private bool _dirty;

        private VisualElement _previewHost;
        private VisualElement _stage;
        private VisualElement _portraitRoot;
        private Image _portraitBody;
        private Image _portraitEyes;
        private Image _portraitDetails;
        private Image _portraitMouth;
        private Label _portraitEmpty;
        private VisualElement _bubble;
        private VisualElement _tail;
        private readonly VisualElement[] _thoughtDots =
            new VisualElement[3];
        private VisualElement _tailTargetGuide;
        private Label _tailTargetHandle;
        private Label _tailTargetCaption;
        private Label _speakerLabel;
        private Label _dialogueLabel;
        private Label _resolutionLabel;
        private Label _status;
        private VisualElement _inspectorPanel;
        private bool _draggingBubble;
        private bool _bubbleDragMoved;
        private Vector2 _bubbleDragStartPointer;
        private Vector2 _bubbleDragStartOffset;
        private Vector2 _previewCanvasSize;
        private Vector2 _previewBubbleSize;
        private Vector2 _previewBubbleCenter;
        private Rect _previewPortraitRect;
        private Vector2 _previewTailBaseLeftScreen;
        private Vector2 _previewTailBaseRightScreen;
        private Vector2 _previewTailTipScreen;
        private Vector2 _previewTargetScreen;
        private float _previewScale = 1f;
        private bool _draggingTailTarget;
        private bool _tailTargetDragMoved;
        private Vector2 _tailTargetDragStartPointer;

        public static void Open(CreateSpeechBubbleNode source) =>
            OpenInternal(source);

        public static void Open(ChangeSpeechBubbleNode source) =>
            OpenInternal(source);

        private static void OpenInternal(SpeechBubblePresentationNode source)
        {
            if (source == null || source.Graph == null)
                return;
            NovelSpeechBubbleComposerWindow window =
                GetWindow<NovelSpeechBubbleComposerWindow>();
            window.titleContent = new GUIContent(
                "Speech Bubble Composer",
                EditorGUIUtility.IconContent("d_SceneViewOrtho").image);
            window.minSize = new Vector2(980f, 640f);
            window.Bind(source.Graph, source, null);
            window.Show();
            window.Focus();
        }

        private void Bind(
            Graph graph,
            SpeechBubblePresentationNode node,
            SpeechBubbleNode dialogueNode)
        {
            _graph = graph;
            _node = node;
            _dialogueNode = dialogueNode;
            LoadDraft();
            Rebuild();
        }

        private void OnEnable()
        {
            EnsurePreviewUndo();
            Undo.undoRedoPerformed += OnUndoRedo;
            rootVisualElement.RegisterCallback<KeyDownEvent>(
                OnKeyDown, TrickleDown.TrickleDown);
            if (_graph == null || _node == null && _dialogueNode == null)
                ShowReconnectMessage();
        }

        private void OnDisable()
        {
            SaveInspectorWidth();
            Undo.undoRedoPerformed -= OnUndoRedo;
            rootVisualElement.UnregisterCallback<KeyDownEvent>(
                OnKeyDown, TrickleDown.TrickleDown);
            if (_previewUndo != null)
                DestroyImmediate(_previewUndo);
            _previewUndo = null;
        }

        private void OnInspectorUpdate()
        {
            SyncGameViewResolution();
        }

        private void EnsurePreviewUndo()
        {
            if (_previewUndo != null)
                return;
            _previewUndo = CreateInstance<PreviewUndoState>();
            _previewUndo.hideFlags = HideFlags.HideAndDontSave;
        }

        private void SyncPreviewUndo()
        {
            if (_draft == null)
                return;
            EnsurePreviewUndo();
            _previewUndo.Character = _draft.PreviewCharacter;
            _previewUndo.Emotion = _draft.PreviewEmotion;
            _previewUndo.ShowCharacter = _draft.ShowCharacter;
            _previewUndo.Thinking = _draft.PreviewThinking;
            _previewUndo.Speaker = _draft.SampleSpeaker;
            _previewUndo.Dialogue = _draft.SampleDialogue;
        }

        private void RestorePreviewUndo()
        {
            if (_draft == null || _previewUndo == null)
                return;
            _draft.PreviewCharacter = _previewUndo.Character;
            _draft.PreviewEmotion = _previewUndo.Emotion;
            _draft.ShowCharacter = _previewUndo.ShowCharacter;
            _draft.PreviewThinking = _previewUndo.Thinking;
            _draft.SampleSpeaker = _previewUndo.Speaker ?? string.Empty;
            _draft.SampleDialogue = _previewUndo.Dialogue ?? string.Empty;
        }

        private void RecordPreviewChange(string name, Action change)
        {
            if (_draft == null || change == null)
                return;
            EnsurePreviewUndo();
            SyncPreviewUndo();
            Undo.RecordObject(_previewUndo, name);
            change();
            SyncPreviewUndo();
            EditorUtility.SetDirty(_previewUndo);
        }

        private void OnUndoRedo()
        {
            if (_graph == null || _node == null && _dialogueNode == null)
                return;
            NovelCharacter previewCharacter = _previewUndo?.Character;
            CharacterEmotion previewEmotion = _previewUndo != null
                ? _previewUndo.Emotion
                : CharacterEmotion.Neutral;
            bool showCharacter = _previewUndo == null ||
                _previewUndo.ShowCharacter;
            bool previewThinking = _previewUndo != null &&
                _previewUndo.Thinking;
            string speaker = _previewUndo?.Speaker;
            string dialogue = _previewUndo?.Dialogue;
            LoadDraft();
            if (_previewUndo != null)
            {
                _draft.PreviewCharacter = previewCharacter;
                _draft.PreviewEmotion = previewEmotion;
                _draft.ShowCharacter = showCharacter;
                _draft.PreviewThinking = previewThinking;
                _draft.SampleSpeaker = speaker ?? string.Empty;
                _draft.SampleDialogue = dialogue ?? string.Empty;
                SyncPreviewUndo();
            }
            Rebuild();
            SetStatus("Undo/redo applied to the selected bubble node.", false);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!evt.ctrlKey && !evt.commandKey)
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
            if (evt.keyCode == KeyCode.S)
            {
                SaveGraph();
                evt.StopImmediatePropagation();
            }
        }

        private void LoadDraft()
        {
            NovelCharacter previewCharacter = _draft?.PreviewCharacter;
            CharacterEmotion previewEmotion = _draft?.PreviewEmotion ??
                CharacterEmotion.Neutral;
            bool showCharacter = _draft?.ShowCharacter ?? true;
            bool previewThinking = _draft?.PreviewThinking ?? false;
            string sampleSpeaker = _draft?.SampleSpeaker;
            string sampleDialogue = _draft?.SampleDialogue;
            if (previewCharacter == null)
                TryFindGraphPreview(out previewCharacter, out previewEmotion);
            Vector2 previewPosition = Vector2.zero;
            float previewRotation = 0f;
            Vector2 previewScale = Vector2.one;
            bool hasAuthoredTransform = TryFindAuthoredTransform(
                previewCharacter, out previewPosition,
                out previewRotation, out previewScale);
            if (string.IsNullOrWhiteSpace(sampleSpeaker))
                sampleSpeaker = previewCharacter != null &&
                    !string.IsNullOrWhiteSpace(previewCharacter.SpeakerName)
                        ? previewCharacter.SpeakerName
                        : "Test Speaker";
            if (string.IsNullOrWhiteSpace(sampleDialogue))
            {
                if (_dialogueNode?.GetNodeOptionByName("Dialogue") is INodeOption dialogueOption &&
                    dialogueOption.TryGetValue(out RichDialogueText authoredDialogue) &&
                    !string.IsNullOrWhiteSpace(authoredDialogue.Text))
                    sampleDialogue = authoredDialogue.Text;
                else
                    sampleDialogue =
                        "This speech bubble follows the speaking character.";
            }
            if (_dialogueNode != null)
                previewThinking = Read(_dialogueNode, "Thinking", previewThinking);

            _draft = new BubbleDraft
            {
                Style = Read<NovelPresentationStyle>(
                    _node, "Style Asset", null) is NovelPresentationStyle styleAsset
                        ? styleAsset.SpeechBubbleStyle
                        : ReadStyle(_node, NovelBoxStyle.BubbleDefault),
                Placement = Read(
                    _node, "Placement",
                    NovelSpeechBubblePlacement.FollowSpeaker),
                ScreenAnchor = Read(
                    _node, "Screen Anchor", NovelDialogueAnchor.TopCenter),
                HorizontalOffset = Read(_node, "Horizontal Offset", 0f),
                VerticalOffset = Read(_node, "Vertical Offset", 0f),
                KeepInsideViewport = Read(
                    _node, "Keep Inside Viewport", true),
                AutoSize = Read(_node, "Auto Size", true),
                MinimumWidth = Read(_node, "Minimum Width", 180f),
                MaximumWidth = Read(_node, "Maximum Width", 520f),
                MinimumHeight = Read(_node, "Minimum Height", 88f),
                MaximumHeight = Read(_node, "Maximum Height", 320f),
                FixedWidth = Read(_node, "Fixed Width", 360f),
                FixedHeight = Read(_node, "Fixed Height", 160f),
                TextAlignment = Read(
                    _node, "Text Alignment", NovelTextAlignment.TopLeft),
                HorizontalPadding = Read(
                    _node, "Horizontal Padding", 24f),
                VerticalPadding = Read(_node, "Vertical Padding", 18f),
                DialogueFontSize = Read(
                    _node, "Dialogue Font Size", 24f),
                SpeakerFontSize = Read(
                    _node, "Speaker Font Size", 21f),
                ShowSpeakerName = Read(
                    _node, "Show Speaker Name", false),
                ShowTail = Read(_node, "Show Tail", true),
                TailTarget = Read(
                    _node, "Tail Target", new Vector2(0.5f, 0.5f)),
                TailWidth = Read(_node, "Tail Width", 34f),
                TailLength = Read(_node, "Tail Length", 30f),
                TargetMargin = Read(_node, "Target Margin", 18f),
                PreviewCharacter = previewCharacter,
                PreviewEmotion = previewEmotion,
                ShowCharacter = showCharacter,
                PreviewThinking = previewThinking,
                SampleSpeaker = sampleSpeaker,
                SampleDialogue = sampleDialogue,
                Resolution = GetGameViewResolution(),
                PreviewPosition = previewPosition,
                PreviewRotation = previewRotation,
                PreviewScale = previewScale,
                HasAuthoredTransform = hasAuthoredTransform
            };
            ValidateDraft();
            _dirty = false;
            SyncPreviewUndo();
        }

        private void Rebuild()
        {
            _building = true;
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = WindowColor;
            rootVisualElement.style.color = TextColor;
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            if (_draft == null || _graph == null ||
                _node == null && _dialogueNode == null)
            {
                ShowReconnectMessage();
                _building = false;
                return;
            }

            rootVisualElement.Add(BuildToolbar());
            VisualElement body = new VisualElement();
            body.style.flexGrow = 1f;
            body.style.minHeight = 0f;
            body.style.paddingLeft = 12f;
            body.style.paddingRight = 12f;
            body.style.paddingTop = 12f;
            body.style.paddingBottom = 10f;
            TwoPaneSplitView split = new TwoPaneSplitView(
                1, GetInspectorWidth(),
                TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1f;
            split.style.minHeight = 0f;
            split.Add(BuildPreviewPanel());
            _inspectorPanel = BuildInspector();
            _inspectorPanel.RegisterCallback<GeometryChangedEvent>(evt =>
                SaveInspectorWidth(evt.newRect.width));
            split.Add(_inspectorPanel);
            body.Add(split);
            rootVisualElement.Add(body);
            rootVisualElement.Add(BuildFooter());
            _building = false;
            RefreshPreview();
        }

        private VisualElement BuildToolbar()
        {
            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingLeft = 16f;
            toolbar.style.paddingRight = 16f;
            toolbar.style.paddingTop = 12f;
            toolbar.style.paddingBottom = 12f;
            toolbar.style.backgroundColor = ToolbarColor;
            toolbar.style.borderTopWidth = 3f;
            toolbar.style.borderTopColor = AccentColor;
            toolbar.style.borderBottomWidth = 1f;
            toolbar.style.borderBottomColor =
                (Color)new Color32(48, 63, 88, 255);

            VisualElement mark = new VisualElement();
            mark.style.width = 34f;
            mark.style.height = 34f;
            mark.style.marginRight = 10f;
            mark.style.backgroundColor = AccentColor;
            SetRadius(mark, 9f);
            Label markText = new Label("B");
            markText.style.flexGrow = 1f;
            markText.style.unityTextAlign = TextAnchor.MiddleCenter;
            markText.style.unityFontStyleAndWeight = FontStyle.Bold;
            markText.style.fontSize = 17f;
            markText.style.color = Color.white;
            mark.Add(markText);
            toolbar.Add(mark);

            VisualElement titles = new VisualElement();
            Label title = new Label("Speech Bubble Composer");
            title.style.fontSize = 16f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = TextColor;
            Label subtitle = new Label(
                "Design character-following bubbles against the live Game View.");
            subtitle.style.fontSize = 10f;
            subtitle.style.color = MutedTextColor;
            titles.Add(title);
            titles.Add(subtitle);
            toolbar.Add(titles);
            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            toolbar.Add(spacer);
            toolbar.Add(CreatePill(
                _node is ChangeSpeechBubbleNode ? "CHANGE NODE" : "CREATE NODE",
                new Color32(21, 78, 74, 255), AccentBrightColor));
            _resolutionLabel = CreatePill(
                ResolutionText(_draft.Resolution),
                new Color32(30, 64, 91, 255),
                new Color32(125, 211, 252, 255));
            _resolutionLabel.style.marginLeft = 8f;
            toolbar.Add(_resolutionLabel);
            return toolbar;
        }

        private VisualElement BuildPreviewPanel()
        {
            VisualElement panel = NewPanel();
            panel.style.flexGrow = 1f;
            panel.style.minWidth = 480f;
            panel.style.marginRight = 12f;
            panel.style.paddingLeft = 14f;
            panel.style.paddingRight = 14f;
            panel.style.paddingTop = 12f;
            panel.style.paddingBottom = 12f;

            Label heading = new Label("Live bubble composition");
            heading.style.fontSize = 14f;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.color = TextColor;
            panel.Add(heading);
            Label subtitle = new Label(
                "Drag the bubble or the TAIL TARGET crosshair to compose the pointer.");
            subtitle.style.fontSize = 10f;
            subtitle.style.color = MutedTextColor;
            subtitle.style.marginBottom = 10f;
            panel.Add(subtitle);

            _previewHost = new VisualElement();
            _previewHost.style.flexGrow = 1f;
            _previewHost.style.minHeight = 380f;
            _previewHost.style.position = Position.Relative;
            _previewHost.style.overflow = Overflow.Hidden;
            _previewHost.style.backgroundColor = WindowColor;
            SetRadius(_previewHost, 9f);
            SetBorder(_previewHost, 1f, new Color32(45, 59, 84, 255));
            _previewHost.RegisterCallback<GeometryChangedEvent>(_ =>
                RefreshPreview());

            _stage = new VisualElement();
            _stage.style.position = Position.Absolute;
            _stage.style.overflow = Overflow.Hidden;
            _stage.style.backgroundColor = StageColor;
            SetBorder(_stage, 1f, new Color32(70, 91, 121, 255));
            _previewHost.Add(_stage);
            AddPreviewGrid(_stage);

            _portraitRoot = new VisualElement();
            _portraitRoot.style.position = Position.Absolute;
            _portraitRoot.style.overflow = Overflow.Visible;
            _portraitRoot.pickingMode = PickingMode.Ignore;
            _portraitBody = CreatePortraitLayer("bubble-portrait-body");
            _portraitEyes = CreatePortraitLayer("bubble-portrait-eyes");
            _portraitDetails = CreatePortraitLayer("bubble-portrait-details");
            _portraitMouth = CreatePortraitLayer("bubble-portrait-mouth");
            _portraitRoot.Add(_portraitBody);
            _portraitRoot.Add(_portraitEyes);
            _portraitRoot.Add(_portraitDetails);
            _portraitRoot.Add(_portraitMouth);
            _stage.Add(_portraitRoot);

            _portraitEmpty = new Label(
                "Choose a preview character to see the tracking target.");
            _portraitEmpty.style.position = Position.Absolute;
            _portraitEmpty.style.left = Length.Percent(28f);
            _portraitEmpty.style.right = Length.Percent(28f);
            _portraitEmpty.style.top = Length.Percent(46f);
            _portraitEmpty.style.paddingTop = 10f;
            _portraitEmpty.style.paddingBottom = 10f;
            _portraitEmpty.style.unityTextAlign = TextAnchor.MiddleCenter;
            _portraitEmpty.style.whiteSpace = WhiteSpace.Normal;
            _portraitEmpty.style.color = MutedTextColor;
            _portraitEmpty.style.backgroundColor =
                new Color(0.04f, 0.07f, 0.13f, 0.88f);
            SetRadius(_portraitEmpty, 8f);
            _stage.Add(_portraitEmpty);

            _tailTargetGuide = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            _tailTargetGuide.style.position = Position.Absolute;
            _tailTargetGuide.style.left = 0f;
            _tailTargetGuide.style.right = 0f;
            _tailTargetGuide.style.top = 0f;
            _tailTargetGuide.style.bottom = 0f;
            _tailTargetGuide.generateVisualContent += DrawTailTargetGuide;
            _stage.Add(_tailTargetGuide);

            _tail = new VisualElement();
            _tail.style.position = Position.Absolute;
            _tail.pickingMode = PickingMode.Ignore;
            _tail.generateVisualContent += DrawPreviewTail;
            _stage.Add(_tail);

            for (int index = 0; index < _thoughtDots.Length; index++)
            {
                VisualElement dot = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                dot.style.position = Position.Absolute;
                _thoughtDots[index] = dot;
                _stage.Add(dot);
            }

            _bubble = new VisualElement();
            _bubble.style.position = Position.Absolute;
            _bubble.style.overflow = Overflow.Hidden;
            _bubble.tooltip =
                "Drag to place the bubble at a fixed screen position.";
            _bubble.RegisterCallback<PointerDownEvent>(
                OnBubblePointerDown);
            _bubble.RegisterCallback<PointerMoveEvent>(
                OnBubblePointerMove);
            _bubble.RegisterCallback<PointerUpEvent>(
                OnBubblePointerUp);
            _bubble.RegisterCallback<PointerCaptureOutEvent>(
                OnBubblePointerCaptureOut);
            _stage.Add(_bubble);
            _speakerLabel = new Label();
            _speakerLabel.style.position = Position.Absolute;
            _speakerLabel.style.color = AccentBrightColor;
            _speakerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _speakerLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _bubble.Add(_speakerLabel);
            _dialogueLabel = new Label();
            _dialogueLabel.style.position = Position.Absolute;
            _dialogueLabel.style.color = Color.white;
            _dialogueLabel.style.whiteSpace = WhiteSpace.Normal;
            _bubble.Add(_dialogueLabel);

            _tailTargetHandle = new Label("+")
            {
                pickingMode = PickingMode.Position,
                tooltip = "Drag this target. The speech-bubble tail always points here."
            };
            _tailTargetHandle.style.position = Position.Absolute;
            _tailTargetHandle.style.width = 22f;
            _tailTargetHandle.style.height = 22f;
            _tailTargetHandle.style.unityTextAlign =
                TextAnchor.MiddleCenter;
            _tailTargetHandle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _tailTargetHandle.style.fontSize = 17f;
            _tailTargetHandle.style.color = WindowColor;
            _tailTargetHandle.style.backgroundColor = AccentBrightColor;
            SetRadius(_tailTargetHandle, 11f);
            SetBorder(_tailTargetHandle, 2f, Color.white);
            _tailTargetHandle.RegisterCallback<PointerDownEvent>(
                OnTailTargetPointerDown);
            _tailTargetHandle.RegisterCallback<PointerMoveEvent>(
                OnTailTargetPointerMove);
            _tailTargetHandle.RegisterCallback<PointerUpEvent>(
                OnTailTargetPointerUp);
            _tailTargetHandle.RegisterCallback<PointerCaptureOutEvent>(
                OnTailTargetPointerCaptureOut);
            _stage.Add(_tailTargetHandle);

            _tailTargetCaption = CreatePill(
                "TAIL TARGET", new Color32(13, 56, 69, 235),
                AccentBrightColor);
            _tailTargetCaption.style.position = Position.Absolute;
            _tailTargetCaption.pickingMode = PickingMode.Ignore;
            _stage.Add(_tailTargetCaption);

            Label badge = CreatePill(
                "LIVE PREVIEW", new Color32(19, 78, 74, 235),
                AccentBrightColor);
            badge.style.position = Position.Absolute;
            badge.style.left = 10f;
            badge.style.top = 8f;
            _stage.Add(badge);
            panel.Add(_previewHost);

            Label hint = new Label(
                "The target point is saved relative to the character and follows the active speaker at runtime.");
            hint.style.marginTop = 8f;
            hint.style.fontSize = 9f;
            hint.style.color = MutedTextColor;
            hint.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(hint);
            return panel;
        }

        private VisualElement BuildInspector()
        {
            VisualElement panel = NewPanel();
            panel.style.minWidth = MinimumInspectorWidth;
            panel.style.flexShrink = 0f;
            Label title = new Label("Bubble properties");
            title.style.fontSize = 14f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = TextColor;
            title.style.marginLeft = 14f;
            title.style.marginTop = 12f;
            panel.Add(title);
            Label note = new Label(
                "Every presentation edit updates the selected graph node immediately.");
            note.style.fontSize = 9f;
            note.style.color = MutedTextColor;
            note.style.marginLeft = 14f;
            note.style.marginRight = 14f;
            note.style.marginBottom = 8f;
            note.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(note);

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            scroll.style.paddingLeft = 12f;
            scroll.style.paddingRight = 12f;
            scroll.style.paddingBottom = 12f;

            Foldout preview = NewFoldout("Preview Content");
            ObjectField character = new ObjectField("Character")
            {
                objectType = typeof(NovelCharacter),
                allowSceneObjects = false,
                value = _draft.PreviewCharacter
            };
            character.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                RecordPreviewChange("Change Bubble Preview Character", () =>
                {
                    _draft.PreviewCharacter = evt.newValue as NovelCharacter;
                    if (_draft.PreviewCharacter != null &&
                        !string.IsNullOrWhiteSpace(
                            _draft.PreviewCharacter.SpeakerName))
                        _draft.SampleSpeaker =
                            _draft.PreviewCharacter.SpeakerName;
                });
                Rebuild();
            });
            preview.Add(character);
            Toggle show = new Toggle("Show Character")
            {
                value = _draft.ShowCharacter
            };
            show.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                RecordPreviewChange("Toggle Bubble Preview Character", () =>
                    _draft.ShowCharacter = evt.newValue);
                RefreshPreview();
            });
            preview.Add(show);
            Toggle thinkingPreview = new Toggle("Preview Thinking")
            {
                value = _draft.PreviewThinking,
                tooltip = "Preview the appearance used automatically when a Speech Bubble dialogue node has Thinking enabled."
            };
            thinkingPreview.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                RecordPreviewChange("Toggle Thought Bubble Preview", () =>
                    _draft.PreviewThinking = evt.newValue);
                RefreshPreview();
            });
            preview.Add(thinkingPreview);
            EnumField emotion = new EnumField(
                "Emotion", _draft.PreviewEmotion);
            emotion.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                RecordPreviewChange("Change Bubble Preview Emotion", () =>
                    _draft.PreviewEmotion =
                        (CharacterEmotion)evt.newValue);
                RefreshPreview();
            });
            preview.Add(emotion);
            AddText(preview, "Speaker", _draft.SampleSpeaker, false,
                value =>
                {
                    RecordPreviewChange("Edit Bubble Preview Speaker", () =>
                        _draft.SampleSpeaker = value);
                    RefreshPreview();
                });
            AddText(preview, "Dialogue", _draft.SampleDialogue, true,
                value =>
                {
                    RecordPreviewChange("Edit Bubble Preview Dialogue", () =>
                        _draft.SampleDialogue = value);
                    RefreshPreview();
                });
            scroll.Add(preview);

            Foldout placement = NewFoldout("Placement");
            EnumField placementMode = new EnumField(
                "Mode", _draft.Placement);
            placementMode.tooltip =
                "Follow Speaker moves with the character. Screen Anchor keeps the bubble at the chosen viewport position.";
            placementMode.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                _draft.Placement =
                    (NovelSpeechBubblePlacement)evt.newValue;
                DraftChanged("Change Speech Bubble Placement");
                Rebuild();
            });
            placement.Add(placementMode);
            EnumField screenAnchor = new EnumField(
                "Screen Anchor", _draft.ScreenAnchor);
            screenAnchor.tooltip =
                "The viewport point used as the stable origin for manual placement.";
            screenAnchor.SetEnabled(
                _draft.Placement == NovelSpeechBubblePlacement.ScreenAnchor);
            screenAnchor.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                _draft.ScreenAnchor = (NovelDialogueAnchor)evt.newValue;
                DraftChanged("Change Speech Bubble Screen Anchor");
            });
            placement.Add(screenAnchor);
            AddFloat(placement, "Horizontal Offset", _draft.HorizontalOffset,
                value => _draft.HorizontalOffset = value);
            AddFloat(placement, "Vertical Offset", _draft.VerticalOffset,
                value => _draft.VerticalOffset = value);
            AddToggle(placement, "Keep Inside Viewport",
                _draft.KeepInsideViewport,
                value => _draft.KeepInsideViewport = value);
            Button center = new Button(() =>
            {
                _draft.Placement = NovelSpeechBubblePlacement.ScreenAnchor;
                _draft.ScreenAnchor = NovelDialogueAnchor.CenterCenter;
                _draft.HorizontalOffset = 0f;
                _draft.VerticalOffset = 0f;
                DraftChanged("Center Speech Bubble");
                Rebuild();
            }) { text = "Center Bubble" };
            placement.Add(center);
            scroll.Add(placement);

            Foldout sizing = NewFoldout("Size and Text");
            Toggle autoSize = new Toggle("Fit To Text")
            {
                value = _draft.AutoSize,
                tooltip = "Grow with the current dialogue while respecting the minimum and maximum size."
            };
            autoSize.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                _draft.AutoSize = evt.newValue;
                DraftChanged("Toggle Speech Bubble Auto Size");
                Rebuild();
            });
            sizing.Add(autoSize);
            if (_draft.AutoSize)
            {
                AddFloat(sizing, "Minimum Width", _draft.MinimumWidth,
                    value => _draft.MinimumWidth = value);
                AddFloat(sizing, "Maximum Width", _draft.MaximumWidth,
                    value => _draft.MaximumWidth = value);
                AddFloat(sizing, "Minimum Height", _draft.MinimumHeight,
                    value => _draft.MinimumHeight = value);
                AddFloat(sizing, "Maximum Height", _draft.MaximumHeight,
                    value => _draft.MaximumHeight = value);
            }
            else
            {
                AddFloat(sizing, "Fixed Width", _draft.FixedWidth,
                    value => _draft.FixedWidth = value);
                AddFloat(sizing, "Fixed Height", _draft.FixedHeight,
                    value => _draft.FixedHeight = value);
            }
            EnumField textAlignment = new EnumField(
                "Text Alignment",
                _draft.AutoSize
                    ? NovelTextAlignment.CenterCenter
                    : _draft.TextAlignment)
            {
                tooltip = _draft.AutoSize
                    ? "Auto-sized bubbles always center dialogue text. Disable Fit To Text to choose another alignment."
                    : "Anchor the dialogue text within the bubble's padded content area."
            };
            textAlignment.SetEnabled(!_draft.AutoSize);
            textAlignment.RegisterValueChangedCallback(evt =>
            {
                if (_building || _draft.AutoSize) return;
                _draft.TextAlignment =
                    (NovelTextAlignment)evt.newValue;
                DraftChanged("Change Speech Bubble Text Alignment");
            });
            sizing.Add(textAlignment);
            AddFloat(sizing, "Horizontal Padding",
                _draft.HorizontalPadding,
                value => _draft.HorizontalPadding = value);
            AddFloat(sizing, "Vertical Padding", _draft.VerticalPadding,
                value => _draft.VerticalPadding = value);
            AddFloat(sizing, "Dialogue Font Size",
                _draft.DialogueFontSize,
                value => _draft.DialogueFontSize = value);
            AddFloat(sizing, "Speaker Font Size", _draft.SpeakerFontSize,
                value => _draft.SpeakerFontSize = value);
            AddToggle(sizing, "Show Speaker Name", _draft.ShowSpeakerName,
                value => _draft.ShowSpeakerName = value);
            scroll.Add(sizing);

            Foldout tail = NewFoldout("Tail");
            AddToggle(tail, "Show Tail", _draft.ShowTail,
                value => _draft.ShowTail = value);
            HelpBox targetHelp = new HelpBox(
                "The visible crosshair is stored relative to the character: (0, 0) is bottom-left and (1, 1) is top-right. Drag it in the preview or enter values here; values outside the character are allowed.",
                HelpBoxMessageType.Info);
            tail.Add(targetHelp);
            Vector2Field targetPoint = new Vector2Field("Target Point")
            {
                value = _draft.TailTarget,
                tooltip = "Character-local point the tail aims toward."
            };
            targetPoint.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                _draft.TailTarget = evt.newValue;
                DraftChanged("Move Speech Bubble Tail Target");
            });
            tail.Add(targetPoint);
            Button resetTarget = new Button(() =>
            {
                _draft.TailTarget = new Vector2(0.5f, 0.5f);
                DraftChanged("Reset Speech Bubble Tail Target");
                Rebuild();
            }) { text = "Reset Target To Character Center" };
            tail.Add(resetTarget);
            AddFloat(tail, "Tail Width", _draft.TailWidth,
                value => _draft.TailWidth = value);
            AddFloat(tail, "Tail Length", _draft.TailLength,
                value => _draft.TailLength = value);
            AddFloat(tail, "Target Margin", _draft.TargetMargin,
                value => _draft.TargetMargin = value);
            scroll.Add(tail);

            Foldout style = NewFoldout("Style");
            ObjectField reusableStyle = new ObjectField("Reusable Style Asset")
            {
                objectType = typeof(NovelPresentationStyle),
                allowSceneObjects = false,
                value = Read<NovelPresentationStyle>(
                    _node, "Style Asset", null),
                tooltip = "Optional appearance-only style shared with other presentation nodes."
            };
            reusableStyle.SetEnabled(_node != null);
            reusableStyle.RegisterValueChangedCallback(evt =>
            {
                if (_building || _node == null)
                    return;
                NovelPresentationStyle value =
                    evt.newValue as NovelPresentationStyle;
                if (value != null)
                    _draft.Style = value.SpeechBubbleStyle;
                if (RecordGraphChange(
                    "Assign Speech Bubble Style Asset",
                    () => Write(_node, "Style Asset", value)))
                {
                    _dirty = true;
                    Rebuild();
                }
            });
            style.Add(reusableStyle);
            AddColor(style, "Fill Color", _draft.Style.FillColor, value =>
            {
                NovelBoxStyle next = _draft.Style;
                next.FillColor = value;
                _draft.Style = next;
            });
            AddFloat(style, "Opacity", _draft.Style.Opacity, value =>
            {
                NovelBoxStyle next = _draft.Style;
                next.Opacity = value;
                _draft.Style = next;
            });
            ObjectField fillTexture = new ObjectField("Fill Texture")
            {
                objectType = typeof(Texture2D),
                allowSceneObjects = false,
                value = _draft.Style.FillTexture,
                tooltip = "Optional texture multiplied by the fill tint."
            };
            fillTexture.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                NovelBoxStyle next = _draft.Style;
                next.FillTexture = evt.newValue as Texture2D;
                _draft.Style = next;
                DraftChanged("Change Speech Bubble Fill Texture");
            });
            style.Add(fillTexture);
            Vector2Field fillTiling = new Vector2Field("Fill Tiling")
            { value = _draft.Style.FillTiling };
            fillTiling.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                NovelBoxStyle next = _draft.Style;
                next.FillTiling = evt.newValue;
                _draft.Style = next;
                DraftChanged("Change Speech Bubble Fill Tiling");
            });
            style.Add(fillTiling);
            Vector2Field fillOffset = new Vector2Field("Fill Offset")
            { value = _draft.Style.FillOffset };
            fillOffset.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                NovelBoxStyle next = _draft.Style;
                next.FillOffset = evt.newValue;
                _draft.Style = next;
                DraftChanged("Change Speech Bubble Fill Offset");
            });
            style.Add(fillOffset);
            AddFloat(style, "Corner Radius", _draft.Style.CornerRadius,
                value =>
                {
                    NovelBoxStyle next = _draft.Style;
                    next.CornerRadius = value;
                    _draft.Style = next;
                });
            AddToggle(style, "Outline", _draft.Style.OutlineEnabled,
                value =>
                {
                    NovelBoxStyle next = _draft.Style;
                    next.OutlineEnabled = value;
                    _draft.Style = next;
                });
            AddColor(style, "Outline Color", _draft.Style.OutlineColor,
                value =>
                {
                    NovelBoxStyle next = _draft.Style;
                    next.OutlineColor = value;
                    _draft.Style = next;
                });
            AddFloat(style, "Outline Transparency",
                _draft.Style.OutlineTransparency, value =>
                {
                    NovelBoxStyle next = _draft.Style;
                    next.OutlineTransparency = value;
                    _draft.Style = next;
                });
            AddFloat(style, "Outline Thickness",
                _draft.Style.OutlineThickness, value =>
                {
                    NovelBoxStyle next = _draft.Style;
                    next.OutlineThickness = value;
                    _draft.Style = next;
                });
            ObjectField outlineTexture = new ObjectField("Outline Texture")
            {
                objectType = typeof(Texture2D),
                allowSceneObjects = false,
                value = _draft.Style.OutlineTexture,
                tooltip = "Optional texture multiplied by the outline tint."
            };
            outlineTexture.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                NovelBoxStyle next = _draft.Style;
                next.OutlineTexture = evt.newValue as Texture2D;
                _draft.Style = next;
                DraftChanged("Change Speech Bubble Outline Texture");
            });
            style.Add(outlineTexture);
            Vector2Field outlineTiling = new Vector2Field("Outline Tiling")
            { value = _draft.Style.OutlineTiling };
            outlineTiling.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                NovelBoxStyle next = _draft.Style;
                next.OutlineTiling = evt.newValue;
                _draft.Style = next;
                DraftChanged("Change Speech Bubble Outline Tiling");
            });
            style.Add(outlineTiling);
            Vector2Field outlineOffset = new Vector2Field("Outline Offset")
            { value = _draft.Style.OutlineOffset };
            outlineOffset.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                NovelBoxStyle next = _draft.Style;
                next.OutlineOffset = evt.newValue;
                _draft.Style = next;
                DraftChanged("Change Speech Bubble Outline Offset");
            });
            style.Add(outlineOffset);
            Button reset = new Button(() =>
            {
                _draft.Style = NovelBoxStyle.BubbleDefault;
                DraftChanged("Reset Speech Bubble Style");
                Rebuild();
            }) { text = "Reset Style" };
            style.Add(reset);
            scroll.Add(style);
            panel.Add(scroll);
            return panel;
        }

        private VisualElement BuildFooter()
        {
            VisualElement footer = new VisualElement();
            footer.style.height = 50f;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.paddingLeft = 16f;
            footer.style.paddingRight = 16f;
            footer.style.backgroundColor = ToolbarColor;
            footer.style.borderTopWidth = 1f;
            footer.style.borderTopColor =
                (Color)new Color32(48, 63, 88, 255);
            _status = new Label(
                _dirty
                    ? "Selected node updated; press Ctrl+S to save the graph."
                    : "Previewing saved graph values.");
            _status.style.flexGrow = 1f;
            _status.style.fontSize = 10f;
            _status.style.color = MutedTextColor;
            footer.Add(_status);
            Button undo = new Button(Undo.PerformUndo)
            {
                text = "Undo Last",
                tooltip = "Undo the last bubble edit. Shortcut: Ctrl+Z."
            };
            undo.style.marginRight = 8f;
            footer.Add(undo);
            Button save = new Button(SaveGraph)
            {
                text = "Save Graph",
                tooltip = "Save the selected bubble node. Shortcut: Ctrl+S."
            };
            save.style.backgroundColor = AccentColor;
            save.style.color = Color.white;
            footer.Add(save);
            return footer;
        }

        private void DraftChanged(string actionName)
        {
            ValidateDraft();
            if (!RecordGraphChange(actionName, WriteOptions))
            {
                SetStatus("The selected node could not be updated.", true);
                return;
            }
            _dirty = true;
            RefreshPreview();
            SetStatus(
                "Selected node updated. Ctrl+S saves the graph asset.", false);
        }

        private bool RecordGraphChange(string actionName, Action change)
        {
            if (_graph == null || _node == null || change == null)
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

        private void WriteOptions()
        {
            WriteStyle(_node, _draft.Style);
            NovelPresentationStyle styleAsset =
                Read<NovelPresentationStyle>(_node, "Style Asset", null);
            if (styleAsset != null)
            {
                Undo.RecordObject(styleAsset, "Edit Speech Bubble Style");
                styleAsset.SpeechBubble = _draft.Style.Validated();
                EditorUtility.SetDirty(styleAsset);
            }
            Write(_node, "Placement", _draft.Placement);
            Write(_node, "Screen Anchor", _draft.ScreenAnchor);
            Write(_node, "Horizontal Offset", _draft.HorizontalOffset);
            Write(_node, "Vertical Offset", _draft.VerticalOffset);
            Write(_node, "Keep Inside Viewport",
                _draft.KeepInsideViewport);
            Write(_node, "Auto Size", _draft.AutoSize);
            Write(_node, "Minimum Width", _draft.MinimumWidth);
            Write(_node, "Maximum Width", _draft.MaximumWidth);
            Write(_node, "Minimum Height", _draft.MinimumHeight);
            Write(_node, "Maximum Height", _draft.MaximumHeight);
            Write(_node, "Fixed Width", _draft.FixedWidth);
            Write(_node, "Fixed Height", _draft.FixedHeight);
            Write(_node, "Text Alignment", _draft.TextAlignment);
            Write(_node, "Horizontal Padding", _draft.HorizontalPadding);
            Write(_node, "Vertical Padding", _draft.VerticalPadding);
            Write(_node, "Dialogue Font Size", _draft.DialogueFontSize);
            Write(_node, "Speaker Font Size", _draft.SpeakerFontSize);
            Write(_node, "Show Speaker Name", _draft.ShowSpeakerName);
            Write(_node, "Show Tail", _draft.ShowTail);
            Write(_node, "Tail Target", _draft.TailTarget);
            Write(_node, "Tail Width", _draft.TailWidth);
            Write(_node, "Tail Length", _draft.TailLength);
            Write(_node, "Target Margin", _draft.TargetMargin);
        }

        private void SaveGraph()
        {
            if (_graph == null || _node == null)
                return;
            try
            {
                GraphDatabase.SaveGraph(_graph);
                AssetDatabase.SaveAssets();
                _dirty = false;
                SetStatus("Speech bubble presentation saved.", false);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus("Save failed. See the Console for details.", true);
            }
        }

        private void ValidateDraft()
        {
            if (_draft == null)
                return;
            _draft.Style = _draft.Style.Validated();
            _draft.MinimumWidth = Mathf.Max(120f, _draft.MinimumWidth);
            _draft.MaximumWidth = Mathf.Max(
                _draft.MinimumWidth, _draft.MaximumWidth);
            _draft.MinimumHeight = Mathf.Max(64f, _draft.MinimumHeight);
            _draft.MaximumHeight = Mathf.Max(
                _draft.MinimumHeight, _draft.MaximumHeight);
            _draft.FixedWidth = Mathf.Max(120f, _draft.FixedWidth);
            _draft.FixedHeight = Mathf.Max(64f, _draft.FixedHeight);
            _draft.HorizontalPadding = Mathf.Max(
                0f, _draft.HorizontalPadding);
            _draft.VerticalPadding = Mathf.Max(
                0f, _draft.VerticalPadding);
            _draft.DialogueFontSize = Mathf.Max(
                1f, _draft.DialogueFontSize);
            _draft.SpeakerFontSize = Mathf.Max(
                1f, _draft.SpeakerFontSize);
            _draft.TailWidth = Mathf.Max(2f, _draft.TailWidth);
            _draft.TailLength = Mathf.Max(2f, _draft.TailLength);
            _draft.TargetMargin = Mathf.Max(0f, _draft.TargetMargin);
        }

        private void RefreshPreview()
        {
            if (_building || _draft == null || _previewHost == null ||
                _stage == null || _bubble == null)
                return;
            float hostWidth = _previewHost.resolvedStyle.width;
            float hostHeight = _previewHost.resolvedStyle.height;
            if (hostWidth <= 1f || hostHeight <= 1f ||
                float.IsNaN(hostWidth) || float.IsNaN(hostHeight))
                return;
            Vector2 canvas = new Vector2(
                Mathf.Max(1, _draft.Resolution.x),
                Mathf.Max(1, _draft.Resolution.y));
            float scale = Mathf.Max(0.01f, Mathf.Min(
                (hostWidth - 36f) / canvas.x,
                (hostHeight - 36f) / canvas.y));
            float stageWidth = canvas.x * scale;
            float stageHeight = canvas.y * scale;
            _stage.style.left = (hostWidth - stageWidth) * 0.5f;
            _stage.style.top = (hostHeight - stageHeight) * 0.5f;
            _stage.style.width = stageWidth;
            _stage.style.height = stageHeight;
            if (_resolutionLabel != null)
                _resolutionLabel.text = ResolutionText(_draft.Resolution);

            Vector2 portraitSize = RefreshPortrait(canvas, scale);
            float dialogueFont = _draft.DialogueFontSize;
            float speakerFont = _draft.SpeakerFontSize;
            float speakerHeight = _draft.ShowSpeakerName
                ? Mathf.Max(24f, speakerFont + 7f)
                : 0f;
            float horizontal = _draft.HorizontalPadding;
            float vertical = _draft.VerticalPadding;
            float available = Mathf.Max(
                1f, _draft.MaximumWidth - horizontal * 2f);
            Vector2 preferred = MeasureText(
                _draft.SampleDialogue, dialogueFont, available);
            float width = _draft.AutoSize
                ? Mathf.Clamp(
                    preferred.x + horizontal * 2f,
                    _draft.MinimumWidth,
                    _draft.MaximumWidth)
                : _draft.FixedWidth;
            float textWidth = Mathf.Max(1f, width - horizontal * 2f);
            preferred = MeasureText(
                _draft.SampleDialogue, dialogueFont, textWidth);
            float height = _draft.AutoSize
                ? Mathf.Clamp(
                    preferred.y + vertical * 2f + speakerHeight,
                    _draft.MinimumHeight,
                    _draft.MaximumHeight)
                : _draft.FixedHeight;

            Vector2 targetReferenceSize = portraitSize.y > 0f
                ? portraitSize
                : ResolvePortraitSize(canvas);
            if (_previewPortraitRect.width <= 0f ||
                _previewPortraitRect.height <= 0f)
                _previewPortraitRect = new Rect(
                    (canvas.x - targetReferenceSize.x) * 0.5f,
                    0f,
                    targetReferenceSize.x,
                    targetReferenceSize.y);
            Vector2 target = _previewPortraitRect.position + Vector2.Scale(
                _draft.TailTarget, _previewPortraitRect.size);
            float margin = Mathf.Max(8f, _draft.TargetMargin);
            Vector2 bubbleSize = new Vector2(width, height);
            Rect viewport = new Rect(Vector2.zero, canvas);
            Vector2 center;
            if (_draft.Placement ==
                NovelSpeechBubblePlacement.ScreenAnchor)
            {
                center = GetAnchoredPreviewCenter(
                    viewport, bubbleSize, _draft.ScreenAnchor, margin);
            }
            else
            {
                center = new Vector2(
                    target.x,
                    target.y + margin + _draft.TailLength +
                    height * 0.5f);
                if (center.y + height * 0.5f > canvas.y - margin)
                    center.y = target.y - margin - _draft.TailLength -
                        height * 0.5f;
            }
            center += new Vector2(
                _draft.HorizontalOffset, _draft.VerticalOffset);
            if (_draft.KeepInsideViewport)
                center = ClampPreviewCenter(
                    center, viewport, bubbleSize, margin);

            _previewCanvasSize = canvas;
            _previewBubbleSize = bubbleSize;
            _previewBubbleCenter = center;
            _previewScale = scale;
            Rect bubbleRect = new Rect(
                center - bubbleSize * 0.5f, bubbleSize);
            Place(_bubble, bubbleRect, canvas.y, scale);
            NovelBoxStyle previewStyle = _draft.Style;
            if (_draft.PreviewThinking)
                previewStyle.CornerRadius = Mathf.Max(
                    previewStyle.CornerRadius, 40f);
            ApplyBoxStyle(_bubble, previewStyle, scale);

            _speakerLabel.text = _draft.SampleSpeaker ?? string.Empty;
            _speakerLabel.style.display = _draft.ShowSpeakerName
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _speakerLabel.style.left = horizontal * scale;
            _speakerLabel.style.right = horizontal * scale;
            _speakerLabel.style.top = vertical * scale;
            _speakerLabel.style.height = speakerHeight * scale;
            _speakerLabel.style.fontSize = Mathf.Max(7f, speakerFont * scale);
            _dialogueLabel.text = _draft.SampleDialogue ?? string.Empty;
            _dialogueLabel.style.left = horizontal * scale;
            _dialogueLabel.style.right = horizontal * scale;
            _dialogueLabel.style.top =
                (vertical + speakerHeight) * scale;
            _dialogueLabel.style.bottom = vertical * scale;
            _dialogueLabel.style.fontSize = Mathf.Max(
                7f, dialogueFont * scale);
            _dialogueLabel.style.unityTextAlign = ToTextAnchor(
                _draft.AutoSize
                    ? NovelTextAlignment.CenterCenter
                    : _draft.TextAlignment);

            Vector2 targetFromBubble = target - center;
            GetPreviewTailGeometry(
                targetFromBubble, bubbleSize,
                out Vector2 attachment,
                out Vector2 tipDirection,
                out Vector2 edgeAxis,
                out Vector2 inward);
            attachment += center;
            _tail.style.left = 0f;
            _tail.style.top = 0f;
            _tail.style.width = stageWidth;
            _tail.style.height = stageHeight;
            _tail.style.backgroundColor = Color.clear;

            float bodyOverlap = Mathf.Min(
                6f, _draft.TailLength * 0.25f);
            Vector2 tailBase = attachment + inward * bodyOverlap;
            Vector2 tailTip = attachment +
                tipDirection * _draft.TailLength;
            Vector2 baseLeft = tailBase +
                edgeAxis * (_draft.TailWidth * 0.5f);
            Vector2 baseRight = tailBase -
                edgeAxis * (_draft.TailWidth * 0.5f);
            _previewTailBaseLeftScreen = CanvasToPreviewScreen(
                baseLeft, canvas.y, scale);
            _previewTailBaseRightScreen = CanvasToPreviewScreen(
                baseRight, canvas.y, scale);
            _previewTailTipScreen = new Vector2(
                tailTip.x * scale,
                (canvas.y - tailTip.y) * scale);
            _tail.style.display = _draft.ShowTail &&
                !_draft.PreviewThinking
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            float[] thoughtProgress = { 0.12f, 0.5f, 0.88f };
            float[] thoughtScale = { 0.7f, 0.46f, 0.28f };
            for (int index = 0; index < _thoughtDots.Length; index++)
            {
                VisualElement dot = _thoughtDots[index];
                bool visible = _draft.ShowTail && _draft.PreviewThinking;
                dot.style.display = visible
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                float dotSize = Mathf.Max(
                    4f, _draft.TailWidth * thoughtScale[index]);
                Vector2 dotCenter = attachment + tipDirection *
                    (_draft.TailLength * thoughtProgress[index]);
                float scaledSize = dotSize * scale;
                dot.style.left = dotCenter.x * scale - scaledSize * 0.5f;
                dot.style.top = (canvas.y - dotCenter.y) * scale -
                    scaledSize * 0.5f;
                dot.style.width = scaledSize;
                dot.style.height = scaledSize;
                dot.style.backgroundColor =
                    previewStyle.EffectiveFillColor;
                SetRadius(dot, scaledSize * 0.5f);
                SetBorder(
                    dot,
                    previewStyle.OutlineEnabled
                        ? previewStyle.OutlineThickness * scale
                        : 0f,
                    previewStyle.EffectiveOutlineColor);
                if (index == _thoughtDots.Length - 1)
                {
                    Vector2 guideStart = dotCenter + tipDirection *
                        (dotSize * 0.5f);
                    _previewTailTipScreen = CanvasToPreviewScreen(
                        guideStart, canvas.y, scale);
                }
            }
            _previewTargetScreen = new Vector2(
                target.x * scale,
                (canvas.y - target.y) * scale);
            _tailTargetHandle.style.left = _previewTargetScreen.x - 11f;
            _tailTargetHandle.style.top = _previewTargetScreen.y - 11f;
            _tailTargetCaption.style.left = _previewTargetScreen.x + 15f;
            _tailTargetCaption.style.top = _previewTargetScreen.y - 12f;
            _tail.MarkDirtyRepaint();
            _tailTargetGuide.MarkDirtyRepaint();
        }

        private static Vector2 CanvasToPreviewScreen(
            Vector2 point, float canvasHeight, float scale) =>
            new Vector2(point.x * scale, (canvasHeight - point.y) * scale);

        private void DrawTailTargetGuide(MeshGenerationContext context)
        {
            if (_draft == null || !_draft.ShowTail)
                return;
            Painter2D painter = context.painter2D;
            painter.strokeColor = new Color(
                AccentBrightColor.r,
                AccentBrightColor.g,
                AccentBrightColor.b,
                0.55f);
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(_previewTailTipScreen);
            painter.LineTo(_previewTargetScreen);
            painter.Stroke();
        }

        private void DrawPreviewTail(MeshGenerationContext context)
        {
            if (_draft == null || !_draft.ShowTail ||
                _draft.PreviewThinking)
                return;
            Painter2D painter = context.painter2D;
            painter.fillColor = _draft.Style.EffectiveFillColor;
            painter.BeginPath();
            painter.MoveTo(_previewTailBaseLeftScreen);
            painter.LineTo(_previewTailBaseRightScreen);
            painter.LineTo(_previewTailTipScreen);
            painter.ClosePath();
            painter.Fill();
            if (!_draft.Style.OutlineEnabled ||
                _draft.Style.OutlineThickness <= 0f)
                return;
            painter.strokeColor = _draft.Style.EffectiveOutlineColor;
            painter.lineWidth = Mathf.Max(
                1f, _draft.Style.OutlineThickness * _previewScale);
            painter.Stroke();
        }

        private void OnTailTargetPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _stage == null || _draft == null)
                return;
            _draggingTailTarget = true;
            _tailTargetDragMoved = false;
            _tailTargetDragStartPointer =
                _stage.WorldToLocal(evt.position);
            _tailTargetHandle.CapturePointer(evt.pointerId);
            evt.StopImmediatePropagation();
        }

        private void OnTailTargetPointerMove(PointerMoveEvent evt)
        {
            if (!_draggingTailTarget ||
                !_tailTargetHandle.HasPointerCapture(evt.pointerId) ||
                _stage == null)
                return;
            Vector2 pointer = _stage.WorldToLocal(evt.position);
            if (!_tailTargetDragMoved &&
                (pointer - _tailTargetDragStartPointer).sqrMagnitude < 1f)
                return;
            _tailTargetDragMoved = true;
            Vector2 canvasPoint = new Vector2(
                pointer.x / Mathf.Max(0.01f, _previewScale),
                _previewCanvasSize.y -
                pointer.y / Mathf.Max(0.01f, _previewScale));
            _draft.TailTarget = new Vector2(
                (canvasPoint.x - _previewPortraitRect.xMin) /
                    Mathf.Max(1f, _previewPortraitRect.width),
                (canvasPoint.y - _previewPortraitRect.yMin) /
                    Mathf.Max(1f, _previewPortraitRect.height));
            RefreshPreview();
            evt.StopImmediatePropagation();
        }

        private void OnTailTargetPointerUp(PointerUpEvent evt)
        {
            if (!_draggingTailTarget || evt.button != 0)
                return;
            bool moved = _tailTargetDragMoved;
            _draggingTailTarget = false;
            _tailTargetDragMoved = false;
            if (_tailTargetHandle.HasPointerCapture(evt.pointerId))
                _tailTargetHandle.ReleasePointer(evt.pointerId);
            if (moved)
            {
                DraftChanged("Move Speech Bubble Tail Target");
                Rebuild();
            }
            evt.StopImmediatePropagation();
        }

        private void OnTailTargetPointerCaptureOut(
            PointerCaptureOutEvent evt)
        {
            if (!_draggingTailTarget)
                return;
            bool moved = _tailTargetDragMoved;
            _draggingTailTarget = false;
            _tailTargetDragMoved = false;
            if (moved)
            {
                DraftChanged("Move Speech Bubble Tail Target");
                Rebuild();
            }
        }

        private void OnBubblePointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _stage == null || _draft == null)
                return;
            _draggingBubble = true;
            _bubbleDragMoved = false;
            _bubbleDragStartPointer = _stage.WorldToLocal(evt.position);
            Vector2 anchorCenter = GetAnchoredPreviewCenter(
                new Rect(Vector2.zero, _previewCanvasSize),
                _previewBubbleSize, _draft.ScreenAnchor,
                Mathf.Max(8f, _draft.TargetMargin));
            _bubbleDragStartOffset = _previewBubbleCenter - anchorCenter;
            _bubble.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnBubblePointerMove(PointerMoveEvent evt)
        {
            if (!_draggingBubble ||
                !_bubble.HasPointerCapture(evt.pointerId) || _stage == null)
                return;
            Vector2 pointer = _stage.WorldToLocal(evt.position);
            Vector2 delta = pointer - _bubbleDragStartPointer;
            if (!_bubbleDragMoved && delta.sqrMagnitude < 4f)
                return;
            _bubbleDragMoved = true;
            _draft.Placement = NovelSpeechBubblePlacement.ScreenAnchor;
            Vector2 canvasDelta = new Vector2(
                delta.x / Mathf.Max(0.01f, _previewScale),
                -delta.y / Mathf.Max(0.01f, _previewScale));
            Vector2 offset = _bubbleDragStartOffset + canvasDelta;
            if (_draft.KeepInsideViewport)
            {
                Rect viewport = new Rect(
                    Vector2.zero, _previewCanvasSize);
                float margin = Mathf.Max(8f, _draft.TargetMargin);
                Vector2 anchorCenter = GetAnchoredPreviewCenter(
                    viewport, _previewBubbleSize,
                    _draft.ScreenAnchor, margin);
                Vector2 clamped = ClampPreviewCenter(
                    anchorCenter + offset, viewport,
                    _previewBubbleSize, margin);
                offset = clamped - anchorCenter;
            }
            _draft.HorizontalOffset = offset.x;
            _draft.VerticalOffset = offset.y;
            RefreshPreview();
            evt.StopPropagation();
        }

        private void OnBubblePointerUp(PointerUpEvent evt)
        {
            if (!_draggingBubble || evt.button != 0)
                return;
            bool moved = _bubbleDragMoved;
            _draggingBubble = false;
            _bubbleDragMoved = false;
            if (_bubble.HasPointerCapture(evt.pointerId))
                _bubble.ReleasePointer(evt.pointerId);
            if (moved)
            {
                DraftChanged("Place Speech Bubble");
                Rebuild();
            }
            evt.StopPropagation();
        }

        private void OnBubblePointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (!_draggingBubble)
                return;
            bool moved = _bubbleDragMoved;
            _draggingBubble = false;
            _bubbleDragMoved = false;
            if (moved)
            {
                DraftChanged("Place Speech Bubble");
                Rebuild();
            }
        }

        private static Vector2 GetAnchoredPreviewCenter(
            Rect viewport,
            Vector2 bubbleSize,
            NovelDialogueAnchor anchor,
            float margin)
        {
            float left = viewport.xMin + bubbleSize.x * 0.5f + margin;
            float centerX = viewport.center.x;
            float right = viewport.xMax - bubbleSize.x * 0.5f - margin;
            float bottom = viewport.yMin + bubbleSize.y * 0.5f + margin;
            float centerY = viewport.center.y;
            float top = viewport.yMax - bubbleSize.y * 0.5f - margin;
            return anchor switch
            {
                NovelDialogueAnchor.TopLeft => new Vector2(left, top),
                NovelDialogueAnchor.TopCenter => new Vector2(centerX, top),
                NovelDialogueAnchor.TopRight => new Vector2(right, top),
                NovelDialogueAnchor.CenterLeft =>
                    new Vector2(left, centerY),
                NovelDialogueAnchor.CenterRight =>
                    new Vector2(right, centerY),
                NovelDialogueAnchor.BottomLeft => new Vector2(left, bottom),
                NovelDialogueAnchor.BottomCenter =>
                    new Vector2(centerX, bottom),
                NovelDialogueAnchor.BottomRight =>
                    new Vector2(right, bottom),
                _ => new Vector2(centerX, centerY)
            };
        }

        private static Vector2 ClampPreviewCenter(
            Vector2 center,
            Rect viewport,
            Vector2 bubbleSize,
            float margin)
        {
            float minimumX = viewport.xMin + bubbleSize.x * 0.5f + margin;
            float maximumX = viewport.xMax - bubbleSize.x * 0.5f - margin;
            float minimumY = viewport.yMin + bubbleSize.y * 0.5f + margin;
            float maximumY = viewport.yMax - bubbleSize.y * 0.5f - margin;
            return new Vector2(
                minimumX <= maximumX
                    ? Mathf.Clamp(center.x, minimumX, maximumX)
                    : viewport.center.x,
                minimumY <= maximumY
                    ? Mathf.Clamp(center.y, minimumY, maximumY)
                    : viewport.center.y);
        }

        private void GetPreviewTailGeometry(
            Vector2 targetFromBubble,
            Vector2 bubbleSize,
            out Vector2 attachment,
            out Vector2 direction,
            out Vector2 edgeAxis,
            out Vector2 inward)
        {
            float halfWidth = bubbleSize.x * 0.5f;
            float halfHeight = bubbleSize.y * 0.5f;
            float safeCorner = _draft.Style.CornerRadius +
                _draft.TailWidth * 0.5f;
            float safeX = Mathf.Min(
                Mathf.Max(0f, halfWidth - 1f), safeCorner);
            float safeY = Mathf.Min(
                Mathf.Max(0f, halfHeight - 1f), safeCorner);
            Vector2 ray = targetFromBubble.sqrMagnitude > 0.0001f
                ? targetFromBubble
                : Vector2.down;
            if (Mathf.Abs(ray.x) > Mathf.Abs(ray.y))
            {
                float intersectionScale = halfWidth /
                    Mathf.Max(0.0001f, Mathf.Abs(ray.x));
                float y = Mathf.Clamp(
                    ray.y * intersectionScale,
                    -halfHeight + safeY,
                    halfHeight - safeY);
                attachment = new Vector2(
                    ray.x >= 0f ? halfWidth : -halfWidth, y);
                edgeAxis = Vector2.up;
                inward = ray.x >= 0f
                    ? Vector2.left
                    : Vector2.right;
            }
            else
            {
                float intersectionScale = halfHeight /
                    Mathf.Max(0.0001f, Mathf.Abs(ray.y));
                float x = Mathf.Clamp(
                    ray.x * intersectionScale,
                    -halfWidth + safeX,
                    halfWidth - safeX);
                attachment = new Vector2(
                    x, ray.y >= 0f ? halfHeight : -halfHeight);
                edgeAxis = Vector2.right;
                inward = ray.y >= 0f
                    ? Vector2.down
                    : Vector2.up;
            }
            direction = targetFromBubble - attachment;
            if (direction.sqrMagnitude < 0.0001f)
                direction = targetFromBubble.sqrMagnitude > 0.0001f
                    ? targetFromBubble
                    : Vector2.down;
            direction.Normalize();
        }

        private Vector2 RefreshPortrait(Vector2 canvas, float scale)
        {
            NovelCharacter character = _draft.PreviewCharacter;
            CharacterPortrait portrait = character != null
                ? character.GetPortrait(_draft.PreviewEmotion)
                : default;
            bool hasPortrait = portrait.Body != null ||
                portrait.Eyes != null || portrait.Details != null ||
                portrait.Mouth != null;
            bool showPortrait = _draft.ShowCharacter && hasPortrait;
            _portraitRoot.style.display = showPortrait
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _portraitEmpty.style.display =
                _draft.ShowCharacter && !hasPortrait
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            if (!showPortrait)
            {
                _previewPortraitRect = Rect.zero;
                return Vector2.zero;
            }
            _portraitBody.sprite = portrait.Body;
            _portraitEyes.sprite = portrait.Eyes;
            _portraitDetails.sprite = portrait.Details;
            _portraitMouth.sprite = portrait.Mouth;
            SetLayerVisible(_portraitBody, portrait.Body != null);
            SetLayerVisible(_portraitEyes, portrait.Eyes != null);
            SetLayerVisible(_portraitDetails, portrait.Details != null);
            SetLayerVisible(_portraitMouth, portrait.Mouth != null);
            Vector2 size = ResolvePortraitSize(canvas);
            Vector2 authoredScale = _draft.HasAuthoredTransform
                ? _draft.PreviewScale
                : Vector2.one;
            Rect visibleBounds = ResolveVisiblePortraitBounds(portrait, size);
            Vector2 pivot = new Vector2(
                visibleBounds.center.x, visibleBounds.yMin);
            Vector2 baseline = new Vector2(canvas.x * 0.5f, 0f);
            if (_draft.HasAuthoredTransform)
                baseline = canvas * 0.5f + Vector2.Scale(
                    _draft.PreviewPosition, canvas * 0.5f);
            float rotation = _draft.HasAuthoredTransform
                ? _draft.PreviewRotation
                : 0f;
            Vector2 minimum = new Vector2(
                float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(
                float.NegativeInfinity, float.NegativeInfinity);
            Vector2[] visibleCorners =
            {
                new Vector2(visibleBounds.xMin, visibleBounds.yMin),
                new Vector2(visibleBounds.xMax, visibleBounds.yMin),
                new Vector2(visibleBounds.xMin, visibleBounds.yMax),
                new Vector2(visibleBounds.xMax, visibleBounds.yMax)
            };
            foreach (Vector2 corner in visibleCorners)
            {
                Vector2 local = Vector2.Scale(
                    Vector2.Scale(corner - pivot, size),
                    authoredScale);
                float radians = rotation * Mathf.Deg2Rad;
                float sine = Mathf.Sin(radians);
                float cosine = Mathf.Cos(radians);
                Vector2 rotated = new Vector2(
                    local.x * cosine - local.y * sine,
                    local.x * sine + local.y * cosine);
                Vector2 point = baseline + rotated;
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            _previewPortraitRect = Rect.MinMaxRect(
                minimum.x, minimum.y, maximum.x, maximum.y);
            float rootLeft = baseline.x - pivot.x * size.x;
            float rootBottom = baseline.y - pivot.y * size.y;
            _portraitRoot.style.left = rootLeft * scale;
            _portraitRoot.style.top =
                (canvas.y - rootBottom - size.y) * scale;
            _portraitRoot.style.width = size.x * scale;
            _portraitRoot.style.height = size.y * scale;
            _portraitRoot.style.transformOrigin = new TransformOrigin(
                Length.Percent(pivot.x * 100f),
                Length.Percent((1f - pivot.y) * 100f),
                0f);
            _portraitRoot.style.rotate = new Rotate(new Angle(
                rotation,
                AngleUnit.Degree));
            _portraitRoot.style.scale = new Scale(new Vector3(
                authoredScale.x, authoredScale.y, 1f));
            return _previewPortraitRect.size;
        }

        private static Rect ResolveVisiblePortraitBounds(
            CharacterPortrait portrait, Vector2 rootSize)
        {
            Sprite[] sprites =
            {
                portrait.Body,
                portrait.Eyes,
                portrait.Details,
                portrait.Mouth
            };
            bool found = false;
            Rect combined = default;
            float rootRatio = rootSize.x / Mathf.Max(1f, rootSize.y);
            foreach (Sprite sprite in sprites)
            {
                if (sprite == null || sprite.rect.width <= 0f ||
                    sprite.rect.height <= 0f)
                    continue;
                float spriteRatio = sprite.rect.width / sprite.rect.height;
                Rect fitted;
                if (spriteRatio > rootRatio)
                {
                    float height = rootRatio / spriteRatio;
                    fitted = new Rect(0f, (1f - height) * 0.5f, 1f, height);
                }
                else
                {
                    float width = spriteRatio / rootRatio;
                    fitted = new Rect((1f - width) * 0.5f, 0f, width, 1f);
                }

                Rect spriteBounds = GetSpriteMeshBounds(sprite);
                Rect visible = new Rect(
                    fitted.xMin + spriteBounds.xMin * fitted.width,
                    fitted.yMin + spriteBounds.yMin * fitted.height,
                    spriteBounds.width * fitted.width,
                    spriteBounds.height * fitted.height);
                combined = found
                    ? Rect.MinMaxRect(
                        Mathf.Min(combined.xMin, visible.xMin),
                        Mathf.Min(combined.yMin, visible.yMin),
                        Mathf.Max(combined.xMax, visible.xMax),
                        Mathf.Max(combined.yMax, visible.yMax))
                    : visible;
                found = true;
            }
            return found ? combined : new Rect(0f, 0f, 1f, 1f);
        }

        private static Rect GetSpriteMeshBounds(Sprite sprite)
        {
            Vector2[] vertices = sprite.vertices;
            if (vertices == null || vertices.Length == 0 ||
                sprite.pixelsPerUnit <= Mathf.Epsilon)
                return new Rect(0f, 0f, 1f, 1f);
            Vector2 minimum = new Vector2(
                float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(
                float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector2 vertex in vertices)
            {
                Vector2 pixel = vertex * sprite.pixelsPerUnit + sprite.pivot;
                minimum = Vector2.Min(minimum, pixel);
                maximum = Vector2.Max(maximum, pixel);
            }
            return Rect.MinMaxRect(
                Mathf.Clamp01(minimum.x / sprite.rect.width),
                Mathf.Clamp01(minimum.y / sprite.rect.height),
                Mathf.Clamp01(maximum.x / sprite.rect.width),
                Mathf.Clamp01(maximum.y / sprite.rect.height));
        }

        private static Vector2 ResolvePortraitSize(Vector2 canvas)
        {
            Vector2 size = TryGetConfiguredPortraitSize(out Vector2 configured)
                ? configured
                : new Vector2(520f, 760f);
            float referenceScale = Mathf.Min(
                canvas.x / 1920f, canvas.y / 1080f);
            return new Vector2(
                Mathf.Max(1f, size.x * referenceScale),
                Mathf.Max(1f, size.y * referenceScale));
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
                        rootSize,
                        layerRect.anchorMax - layerRect.anchorMin) +
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

        private static Vector2 MeasureText(
            string text, float fontSize, float width)
        {
            GUIStyle style = new GUIStyle(EditorStyles.label)
            {
                fontSize = Mathf.RoundToInt(fontSize),
                wordWrap = true,
                richText = true
            };
            Vector2 value = style.CalcSize(new GUIContent(text ?? string.Empty));
            if (value.x <= width)
                return value;
            float height = style.CalcHeight(
                new GUIContent(text ?? string.Empty), width);
            return new Vector2(width, height);
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
                DraftChanged("Edit Speech Bubble " + label);
            });
            parent.Add(field);
        }

        private void AddColor(
            VisualElement parent,
            string label,
            Color value,
            Action<Color> setter)
        {
            ColorField field = new ColorField(label)
            {
                value = value,
                showAlpha = true
            };
            field.RegisterValueChangedCallback(evt =>
            {
                if (_building) return;
                setter(evt.newValue);
                DraftChanged("Edit Speech Bubble " + label);
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
                DraftChanged("Edit Speech Bubble " + label);
            });
            parent.Add(field);
        }

        private static void AddText(
            VisualElement parent,
            string label,
            string value,
            bool multiline,
            Action<string> setter)
        {
            TextField field = new TextField(label)
            {
                value = value ?? string.Empty,
                multiline = multiline
            };
            if (multiline)
                field.style.minHeight = 58f;
            field.RegisterValueChangedCallback(evt => setter(evt.newValue));
            parent.Add(field);
        }

        private static Foldout NewFoldout(string title)
        {
            Foldout foldout = new Foldout { text = title, value = true };
            foldout.style.marginTop = 8f;
            foldout.style.marginBottom = 2f;
            foldout.style.paddingLeft = 10f;
            foldout.style.paddingRight = 10f;
            foldout.style.paddingTop = 7f;
            foldout.style.paddingBottom = 10f;
            foldout.style.backgroundColor = CardColor;
            SetRadius(foldout, 10f);
            SetBorder(foldout, 1f, new Color32(43, 79, 98, 255));
            return foldout;
        }

        private static VisualElement NewPanel()
        {
            VisualElement panel = new VisualElement();
            panel.style.backgroundColor = PanelColor;
            SetRadius(panel, 14f);
            SetBorder(panel, 1f, new Color32(43, 79, 98, 255));
            return panel;
        }

        private static Label CreatePill(
            string text, Color background, Color foreground)
        {
            Label pill = new Label(text);
            pill.style.height = 24f;
            pill.style.paddingLeft = 9f;
            pill.style.paddingRight = 9f;
            pill.style.unityTextAlign = TextAnchor.MiddleCenter;
            pill.style.unityFontStyleAndWeight = FontStyle.Bold;
            pill.style.fontSize = 9f;
            pill.style.backgroundColor = background;
            pill.style.color = foreground;
            SetRadius(pill, 12f);
            return pill;
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
            layer.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private static void AddPreviewGrid(VisualElement stage)
        {
            for (int index = 1; index < 20; index++)
            {
                VisualElement line = new VisualElement();
                line.style.position = Position.Absolute;
                line.style.left = Length.Percent(index * 5f);
                line.style.top = 0f;
                line.style.bottom = 0f;
                line.style.width = 1f;
                line.style.backgroundColor = index % 5 == 0
                    ? new Color(0.36f, 0.72f, 0.72f, 0.18f)
                    : new Color(0.36f, 0.72f, 0.72f, 0.07f);
                line.pickingMode = PickingMode.Ignore;
                stage.Add(line);
            }
            for (int index = 1; index < 10; index++)
            {
                VisualElement line = new VisualElement();
                line.style.position = Position.Absolute;
                line.style.left = 0f;
                line.style.right = 0f;
                line.style.top = Length.Percent(index * 10f);
                line.style.height = 1f;
                line.style.backgroundColor = index % 5 == 0
                    ? new Color(0.36f, 0.72f, 0.72f, 0.18f)
                    : new Color(0.36f, 0.72f, 0.72f, 0.07f);
                line.pickingMode = PickingMode.Ignore;
                stage.Add(line);
            }
        }

        private static void Place(
            VisualElement element,
            Rect rect,
            float canvasHeight,
            float scale)
        {
            element.style.left = rect.x * scale;
            element.style.top = (canvasHeight - rect.yMax) * scale;
            element.style.width = rect.width * scale;
            element.style.height = rect.height * scale;
        }

        private static void ApplyBoxStyle(
            VisualElement element,
            NovelBoxStyle source,
            float scale)
        {
            NovelBoxStyle style = source.Validated();
            element.style.backgroundColor = style.EffectiveFillColor;
            SetRadius(element, style.CornerRadius * scale);
            SetBorder(element,
                style.OutlineEnabled
                    ? style.OutlineThickness * scale
                    : 0f,
                style.EffectiveOutlineColor);
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

        private static void SetRadius(VisualElement element, float value)
        {
            element.style.borderTopLeftRadius = value;
            element.style.borderTopRightRadius = value;
            element.style.borderBottomLeftRadius = value;
            element.style.borderBottomRightRadius = value;
        }

        private static void SetBorder(
            VisualElement element, float width, Color color)
        {
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
        }

        private static NovelBoxStyle ReadStyle(
            INode node, NovelBoxStyle fallback)
        {
            return new NovelBoxStyle
            {
                FillColor = Read(node, "Fill Color", fallback.FillColor),
                Opacity = Read(node, "Opacity", fallback.Opacity),
                FillTexture = Read(
                    node, "Fill Texture", fallback.FillTexture),
                FillTiling = Read(node, "Fill Tiling", fallback.FillTiling),
                FillOffset = Read(node, "Fill Offset", fallback.FillOffset),
                CornerRadius = Read(
                    node, "Corner Radius", fallback.CornerRadius),
                OutlineEnabled = Read(
                    node, "Outline", fallback.OutlineEnabled),
                OutlineColor = Read(
                    node, "Outline Color", fallback.OutlineColor),
                OutlineTransparency = Read(
                    node, "Outline Transparency",
                    fallback.OutlineTransparency),
                OutlineThickness = Read(
                    node, "Outline Thickness", fallback.OutlineThickness),
                OutlineTexture = Read(
                    node, "Outline Texture", fallback.OutlineTexture),
                OutlineTiling = Read(
                    node, "Outline Tiling", fallback.OutlineTiling),
                OutlineOffset = Read(
                    node, "Outline Offset", fallback.OutlineOffset)
            }.Validated();
        }

        private static void WriteStyle(INode node, NovelBoxStyle style)
        {
            style = style.Validated();
            Write(node, "Fill Color", style.FillColor);
            Write(node, "Opacity", style.Opacity);
            Write(node, "Fill Texture", style.FillTexture);
            Write(node, "Fill Tiling", style.FillTiling);
            Write(node, "Fill Offset", style.FillOffset);
            Write(node, "Corner Radius", style.CornerRadius);
            Write(node, "Outline", style.OutlineEnabled);
            Write(node, "Outline Color", style.OutlineColor);
            Write(node, "Outline Transparency", style.OutlineTransparency);
            Write(node, "Outline Thickness", style.OutlineThickness);
            Write(node, "Outline Texture", style.OutlineTexture);
            Write(node, "Outline Tiling", style.OutlineTiling);
            Write(node, "Outline Offset", style.OutlineOffset);
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

        private static SpeechBubblePresentationNode FindPreviousPresentation(
            INode origin)
        {
            var queue = new Queue<INode>();
            var visited = new HashSet<INode>();
            EnqueuePredecessors(origin, queue);
            int remaining = 256;
            while (queue.Count > 0 && remaining-- > 0)
            {
                INode candidate = queue.Dequeue();
                if (candidate == null || !visited.Add(candidate))
                    continue;
                if (candidate is SpeechBubblePresentationNode presentation)
                    return presentation;
                EnqueuePredecessors(candidate, queue);
            }
            return null;
        }

        private static void EnqueuePredecessors(
            INode node, Queue<INode> queue)
        {
            IPort input = node?.GetInputPortByName("in");
            if (input == null)
                return;
            var connected = new List<IPort>();
            input.GetConnectedPorts(connected);
            foreach (IPort port in connected)
            {
                INode predecessor = port?.GetNode();
                if (predecessor != null)
                    queue.Enqueue(predecessor);
            }
        }

        private bool TryFindAuthoredTransform(
            NovelCharacter character,
            out Vector2 position,
            out float rotation,
            out Vector2 scale)
        {
            return TryFindAuthoredTransform(
                _dialogueNode ?? (INode)_node,
                character,
                new HashSet<INode>(),
                out position,
                out rotation,
                out scale);
        }

        private bool TryFindAuthoredTransform(
            INode origin,
            NovelCharacter character,
            HashSet<INode> visited,
            out Vector2 position,
            out float rotation,
            out Vector2 scale)
        {
            position = Vector2.zero;
            rotation = 0f;
            scale = Vector2.one;
            if (_graph == null || origin == null || character == null)
                return false;

            var queue = new Queue<INode>();
            EnqueuePredecessors(origin, queue);
            int remaining = 256;
            while (queue.Count > 0 && remaining-- > 0)
            {
                INode candidate = queue.Dequeue();
                if (candidate == null || !visited.Add(candidate))
                    continue;

                if (candidate is TransformSpeakerPortraitNode transform &&
                    TryReadMatchingTransform(transform, character,
                        out position, out rotation, out scale,
                        out bool relative))
                {
                    if (relative)
                    {
                        Vector2 previousPosition;
                        if (!TryFindAuthoredTransform(
                                transform,
                                character,
                                new HashSet<INode>(visited),
                                out previousPosition,
                                out _,
                                out _))
                            previousPosition = new Vector2(0f, -1f);
                        position += previousPosition;
                    }
                    return true;
                }

                if (candidate is ShowCharacterNode show &&
                    ResolveCharacter(show, "Character", "Character Reference") == character &&
                    string.Equals(
                        ResolveInstanceID(show, "Character Reference", "Instance ID"),
                        _previewInstanceID, StringComparison.Ordinal))
                {
                    position = Read(show, "Position", Vector2.zero);
                    if (Read(show, "Coordinate Space", CharacterPositionSpace.Canvas) ==
                        CharacterPositionSpace.Canvas)
                        position = CanvasToNormalizedPreview(position, false);
                    return true;
                }

                EnqueuePredecessors(candidate, queue);
            }
            return false;
        }

        private bool TryReadMatchingTransform(
            TransformSpeakerPortraitNode node,
            NovelCharacter character,
            out Vector2 position,
            out float rotation,
            out Vector2 scale,
            out bool relative)
        {
            position = Vector2.zero;
            rotation = 0f;
            scale = Vector2.one;
            relative = false;
            int matchingTarget = 0;
            int targetCount = node.GetDesiredCharacterCount();
            for (int target = 1; target <= targetCount; target++)
            {
                string suffix = target == 1 ? string.Empty : $" {target}";
                if (ResolveCharacter(
                        node,
                        "Character" + suffix,
                        "Character Reference" + suffix) != character ||
                    !string.Equals(
                        ResolveInstanceID(
                            node,
                            "Character Reference" + suffix,
                            "Instance ID" + suffix),
                        _previewInstanceID,
                        StringComparison.Ordinal))
                    continue;
                matchingTarget = target;
                break;
            }
            if (matchingTarget == 0)
                return false;

            string targetSuffix = matchingTarget == 1
                ? string.Empty
                : $" {matchingTarget}";
            IPort positionPort = node.GetInputPortByName(
                "Position" + targetSuffix);
            IPort rotationPort = node.GetInputPortByName(
                "Rotation" + targetSuffix);
            IPort scalePort = node.GetInputPortByName(
                "Scale" + targetSuffix);
            position = NovelGraphValues.Resolve<Vector2>(_graph, positionPort);
            rotation = NovelGraphValues.Resolve<float>(_graph, rotationPort);
            scale = NovelGraphValues.Resolve<Vector2>(_graph, scalePort);
            if (matchingTarget == 1)
            {
                Vector2 legacyPosition = new Vector2(
                    Read(node, "OffsetX", 0f), Read(node, "OffsetY", 0f));
                float legacyRotation = Read(node, "Rotation", 0f);
                Vector2 legacyScale = Read(node, "Scale", Vector2.one);
                if (positionPort != null && !positionPort.IsConnected &&
                    position == Vector2.zero && legacyPosition != Vector2.zero)
                    position = legacyPosition;
                if (rotationPort != null && !rotationPort.IsConnected &&
                    Mathf.Approximately(rotation, 0f) &&
                    !Mathf.Approximately(legacyRotation, 0f))
                    rotation = legacyRotation;
                if (scalePort != null && !scalePort.IsConnected &&
                    scale == Vector2.one && legacyScale != Vector2.one)
                    scale = legacyScale;
            }
            relative = Read(node, "Relative", false);
            if (Read(node, "Coordinate Space", CharacterPositionSpace.Normalized) ==
                CharacterPositionSpace.Canvas)
                position = CanvasToNormalizedPreview(position, relative);
            return true;
        }

        private NovelCharacter ResolveCharacter(
            INode node,
            string characterPort,
            string referencePort)
        {
            NovelCharacterReference reference =
                NovelGraphValues.Resolve<NovelCharacterReference>(
                    _graph, node?.GetInputPortByName(referencePort));
            return reference.Character != null
                ? reference.Character
                : NovelGraphValues.Resolve<NovelCharacter>(
                    _graph, node?.GetInputPortByName(characterPort));
        }

        private string ResolveInstanceID(
            INode node,
            string referencePort,
            string instanceOption)
        {
            NovelCharacterReference reference =
                NovelGraphValues.Resolve<NovelCharacterReference>(
                    _graph, node?.GetInputPortByName(referencePort));
            return reference.Character != null
                ? reference.InstanceID ?? string.Empty
                : Read(node, instanceOption, string.Empty) ?? string.Empty;
        }

        private static Vector2 CanvasToNormalizedPreview(
            Vector2 position, bool relative)
        {
            Vector2Int resolution = GetGameViewResolution();
            return new Vector2(
                position.x / Mathf.Max(1f, resolution.x * 0.5f),
                position.y / Mathf.Max(1f, resolution.y * 0.5f) -
                (relative ? 0f : 1f));
        }

        private void TryFindGraphPreview(
            out NovelCharacter character,
            out CharacterEmotion emotion)
        {
            character = null;
            emotion = CharacterEmotion.Neutral;
            _previewInstanceID = string.Empty;
            if (_dialogueNode != null)
            {
                NovelCharacterReference reference =
                    NovelGraphValues.Resolve<NovelCharacterReference>(
                        _graph, _dialogueNode.GetInputPortByName(
                            "Character Reference"));
                character = reference.Character != null
                    ? reference.Character
                    : NovelGraphValues.Resolve<NovelCharacter>(
                        _graph, _dialogueNode.GetInputPortByName("Character"));
                if (character == null)
                {
                    reference = NovelGraphValues.Resolve<NovelCharacterReference>(
                        _graph, _dialogueNode.GetInputPortByName(
                            "Speaker Reference"));
                    character = reference.Character != null
                        ? reference.Character
                        : NovelGraphValues.Resolve<NovelCharacter>(
                            _graph, _dialogueNode.GetInputPortByName("Speaker"));
                }
                _previewInstanceID = reference.Character != null
                    ? reference.InstanceID ?? string.Empty
                    : Read(_dialogueNode, "Instance ID", string.Empty) ??
                      string.Empty;
                emotion = Read(
                    _dialogueNode, "Emotion", CharacterEmotion.Neutral);
                // A story bubble must preview its own target. Showing an
                // unrelated graph character hides missing wiring mistakes.
                return;
            }
            if (_node != null)
            {
                NovelCharacterReference reference =
                    NovelGraphValues.Resolve<NovelCharacterReference>(
                        _graph, _node.GetInputPortByName(
                            "Preview Character Reference"));
                character = reference.Character != null
                    ? reference.Character
                    : NovelGraphValues.Resolve<NovelCharacter>(
                        _graph, _node.GetInputPortByName(
                            "Preview Character"));
                _previewInstanceID = reference.Character != null
                    ? reference.InstanceID ?? string.Empty
                    : Read(
                        _node, "Preview Instance ID", string.Empty) ??
                      string.Empty;

                // The box node's target is authoritative. An empty target
                // should be obvious instead of silently previewing a random
                // character asset from the project.
                return;
            }
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
            string[] guids = AssetDatabase.FindAssets(
                "t:NovelCharacter", new[] { "Assets" });
            if (guids.Length == 0)
                return;
            Array.Sort(guids, StringComparer.Ordinal);
            character = AssetDatabase.LoadAssetAtPath<NovelCharacter>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
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
                    return new Vector2Int(
                        Mathf.RoundToInt(size.x),
                        Mathf.RoundToInt(size.y));
            }
            catch (InvalidOperationException)
            {
            }
            return new Vector2Int(
                Mathf.Max(1, PlayerSettings.defaultScreenWidth),
                Mathf.Max(1, PlayerSettings.defaultScreenHeight));
        }

        private static string ResolutionText(Vector2Int resolution) =>
            $"{resolution.x} x {resolution.y}  •  GAME VIEW";

        private float GetInspectorWidth()
        {
            float maximum = Mathf.Max(
                MinimumInspectorWidth,
                (position.width > 1f ? position.width : minSize.x) - 500f);
            return Mathf.Clamp(
                EditorPrefs.GetFloat(
                    InspectorWidthKey, DefaultInspectorWidth),
                MinimumInspectorWidth,
                maximum);
        }

        private void SaveInspectorWidth()
        {
            if (_inspectorPanel != null)
                SaveInspectorWidth(_inspectorPanel.resolvedStyle.width);
        }

        private static void SaveInspectorWidth(float width)
        {
            if (width >= MinimumInspectorWidth && !float.IsNaN(width))
                EditorPrefs.SetFloat(InspectorWidthKey, width);
        }

        private void SetStatus(string message, bool error)
        {
            if (_status == null)
                return;
            _status.text = message;
            _status.style.color = error
                ? (Color)new Color32(248, 113, 113, 255)
                : MutedTextColor;
        }

        private void ShowReconnectMessage()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = WindowColor;
            HelpBox box = new HelpBox(
                "The graph was reloaded. Double-click a Create Speech Bubble " +
                "or Change Speech Bubble node to reconnect this composer.",
                HelpBoxMessageType.Info);
            box.style.marginLeft = 16f;
            box.style.marginRight = 16f;
            box.style.marginTop = 16f;
            rootVisualElement.Add(box);
        }
    }
}
