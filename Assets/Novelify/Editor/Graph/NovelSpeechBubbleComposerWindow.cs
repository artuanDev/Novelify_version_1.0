using System;
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
            public float MinimumWidth;
            public float MaximumWidth;
            public float HorizontalPadding;
            public float VerticalPadding;
            public float TailWidth;
            public float TailLength;
            public float TargetMargin;
            public NovelCharacter PreviewCharacter;
            public CharacterEmotion PreviewEmotion = CharacterEmotion.Neutral;
            public bool ShowCharacter = true;
            public string SampleSpeaker = "Test Speaker";
            public string SampleDialogue =
                "This speech bubble follows the speaking character.";
            public Vector2Int Resolution = new Vector2Int(1920, 1080);
        }

        private sealed class PreviewUndoState : ScriptableObject
        {
            public NovelCharacter Character;
            public CharacterEmotion Emotion;
            public bool ShowCharacter = true;
            public string Speaker;
            public string Dialogue;
        }

        private static readonly Color WindowColor =
            new Color32(9, 15, 27, 255);
        private static readonly Color ToolbarColor =
            new Color32(17, 26, 44, 255);
        private static readonly Color PanelColor =
            new Color32(23, 34, 55, 255);
        private static readonly Color CardColor =
            new Color32(31, 45, 70, 255);
        private static readonly Color StageColor =
            new Color32(14, 24, 41, 255);
        private static readonly Color AccentColor =
            new Color32(14, 165, 164, 255);
        private static readonly Color AccentBrightColor =
            new Color32(94, 234, 212, 255);
        private static readonly Color TextColor =
            new Color32(241, 245, 249, 255);
        private static readonly Color MutedTextColor =
            new Color32(148, 163, 184, 255);

        private const string InspectorWidthKey =
            "Novelify.SpeechBubbleComposer.InspectorWidth";
        private const float DefaultInspectorWidth = 380f;
        private const float MinimumInspectorWidth = 310f;

        private Graph _graph;
        private SpeechBubblePresentationNode _node;
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
        private Label _speakerLabel;
        private Label _dialogueLabel;
        private Label _resolutionLabel;
        private Label _status;
        private VisualElement _inspectorPanel;

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
            window.Bind(source.Graph, source);
            window.Show();
            window.Focus();
        }

        private void Bind(Graph graph, SpeechBubblePresentationNode node)
        {
            _graph = graph;
            _node = node;
            LoadDraft();
            Rebuild();
        }

        private void OnEnable()
        {
            EnsurePreviewUndo();
            Undo.undoRedoPerformed += OnUndoRedo;
            rootVisualElement.RegisterCallback<KeyDownEvent>(
                OnKeyDown, TrickleDown.TrickleDown);
            if (_graph == null || _node == null)
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
            if (_graph == null || _node == null)
                return;
            NovelCharacter previewCharacter = _previewUndo?.Character;
            CharacterEmotion previewEmotion = _previewUndo != null
                ? _previewUndo.Emotion
                : CharacterEmotion.Neutral;
            bool showCharacter = _previewUndo == null ||
                _previewUndo.ShowCharacter;
            string speaker = _previewUndo?.Speaker;
            string dialogue = _previewUndo?.Dialogue;
            LoadDraft();
            if (_previewUndo != null)
            {
                _draft.PreviewCharacter = previewCharacter;
                _draft.PreviewEmotion = previewEmotion;
                _draft.ShowCharacter = showCharacter;
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
            string sampleSpeaker = _draft?.SampleSpeaker;
            string sampleDialogue = _draft?.SampleDialogue;
            if (previewCharacter == null)
                TryFindGraphPreview(out previewCharacter, out previewEmotion);
            if (string.IsNullOrWhiteSpace(sampleSpeaker))
                sampleSpeaker = previewCharacter != null &&
                    !string.IsNullOrWhiteSpace(previewCharacter.SpeakerName)
                        ? previewCharacter.SpeakerName
                        : "Test Speaker";
            if (string.IsNullOrWhiteSpace(sampleDialogue))
                sampleDialogue =
                    "This speech bubble follows the speaking character.";

            _draft = new BubbleDraft
            {
                Style = ReadStyle(_node, NovelBoxStyle.BubbleDefault),
                MinimumWidth = Read(_node, "Minimum Width", 180f),
                MaximumWidth = Read(_node, "Maximum Width", 520f),
                HorizontalPadding = Read(
                    _node, "Horizontal Padding", 24f),
                VerticalPadding = Read(_node, "Vertical Padding", 18f),
                TailWidth = Read(_node, "Tail Width", 34f),
                TailLength = Read(_node, "Tail Length", 30f),
                TargetMargin = Read(_node, "Target Margin", 18f),
                PreviewCharacter = previewCharacter,
                PreviewEmotion = previewEmotion,
                ShowCharacter = showCharacter,
                SampleSpeaker = sampleSpeaker,
                SampleDialogue = sampleDialogue,
                Resolution = GetGameViewResolution()
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
            if (_draft == null || _graph == null || _node == null)
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
                "The tail follows the character while the body stays inside the viewport.");
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

            _tail = new VisualElement();
            _tail.style.position = Position.Absolute;
            _tail.pickingMode = PickingMode.Ignore;
            _stage.Add(_tail);

            _bubble = new VisualElement();
            _bubble.style.position = Position.Absolute;
            _bubble.style.overflow = Overflow.Hidden;
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

            Label badge = CreatePill(
                "LIVE PREVIEW", new Color32(19, 78, 74, 235),
                AccentBrightColor);
            badge.style.position = Position.Absolute;
            badge.style.left = 10f;
            badge.style.top = 8f;
            _stage.Add(badge);
            panel.Add(_previewHost);

            Label hint = new Label(
                "Preview resolution follows Game View automatically. Preview content is not saved to the node.");
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

            Foldout geometry = NewFoldout("Geometry and Tail");
            AddFloat(geometry, "Minimum Width", _draft.MinimumWidth,
                value => _draft.MinimumWidth = value);
            AddFloat(geometry, "Maximum Width", _draft.MaximumWidth,
                value => _draft.MaximumWidth = value);
            AddFloat(geometry, "Horizontal Padding",
                _draft.HorizontalPadding,
                value => _draft.HorizontalPadding = value);
            AddFloat(geometry, "Vertical Padding", _draft.VerticalPadding,
                value => _draft.VerticalPadding = value);
            AddFloat(geometry, "Tail Width", _draft.TailWidth,
                value => _draft.TailWidth = value);
            AddFloat(geometry, "Tail Length", _draft.TailLength,
                value => _draft.TailLength = value);
            AddFloat(geometry, "Target Margin", _draft.TargetMargin,
                value => _draft.TargetMargin = value);
            scroll.Add(geometry);

            Foldout style = NewFoldout("Style");
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
            AddFloat(style, "Outline Thickness",
                _draft.Style.OutlineThickness, value =>
                {
                    NovelBoxStyle next = _draft.Style;
                    next.OutlineThickness = value;
                    _draft.Style = next;
                });
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
            Write(_node, "Minimum Width", _draft.MinimumWidth);
            Write(_node, "Maximum Width", _draft.MaximumWidth);
            Write(_node, "Horizontal Padding", _draft.HorizontalPadding);
            Write(_node, "Vertical Padding", _draft.VerticalPadding);
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
            _draft.HorizontalPadding = Mathf.Max(
                0f, _draft.HorizontalPadding);
            _draft.VerticalPadding = Mathf.Max(
                0f, _draft.VerticalPadding);
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
            const float dialogueFont = 24f;
            const float speakerFont = 21f;
            const float speakerHeight = 28f;
            float horizontal = _draft.HorizontalPadding;
            float vertical = _draft.VerticalPadding;
            float available = Mathf.Max(
                1f, _draft.MaximumWidth - horizontal * 2f);
            Vector2 preferred = MeasureText(
                _draft.SampleDialogue, dialogueFont, available);
            float width = Mathf.Clamp(
                preferred.x + horizontal * 2f,
                _draft.MinimumWidth,
                _draft.MaximumWidth);
            float textWidth = Mathf.Max(1f, width - horizontal * 2f);
            preferred = MeasureText(
                _draft.SampleDialogue, dialogueFont, textWidth);
            float height = Mathf.Max(
                88f, preferred.y + vertical * 2f + speakerHeight);

            Vector2 target = _draft.ShowCharacter && portraitSize.y > 0f
                ? new Vector2(canvas.x * 0.5f, portraitSize.y)
                : new Vector2(canvas.x * 0.5f, canvas.y * 0.45f);
            float margin = Mathf.Max(8f, _draft.TargetMargin);
            bool bubbleAbove = true;
            Vector2 center = new Vector2(
                target.x,
                target.y + margin + _draft.TailLength + height * 0.5f);
            if (center.y + height * 0.5f > canvas.y - margin)
            {
                bubbleAbove = false;
                center.y = target.y - margin - _draft.TailLength -
                    height * 0.5f;
            }
            center.x = Mathf.Clamp(center.x,
                width * 0.5f + margin,
                canvas.x - width * 0.5f - margin);
            center.y = Mathf.Clamp(center.y,
                height * 0.5f + margin,
                canvas.y - height * 0.5f - margin);
            Rect bubbleRect = new Rect(
                center - new Vector2(width, height) * 0.5f,
                new Vector2(width, height));
            Place(_bubble, bubbleRect, canvas.y, scale);
            ApplyBoxStyle(_bubble, _draft.Style, scale);

            _speakerLabel.text = _draft.SampleSpeaker ?? string.Empty;
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

            float tailWidth = _draft.TailWidth * scale;
            float tailLength = _draft.TailLength * scale;
            _tail.style.width = tailWidth;
            _tail.style.height = tailLength;
            _tail.style.left = center.x * scale - tailWidth * 0.5f;
            _tail.style.top = bubbleAbove
                ? (canvas.y - bubbleRect.yMin) * scale - 1f
                : (canvas.y - bubbleRect.yMax) * scale - tailLength + 1f;
            _tail.style.backgroundColor =
                _draft.Style.EffectiveFillColor;
            _tail.style.rotate = new Rotate(
                new Angle(45f, AngleUnit.Degree));
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
            bool visible = _draft.ShowCharacter && hasPortrait;
            _portraitRoot.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _portraitEmpty.style.display =
                _draft.ShowCharacter && !hasPortrait
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            if (!visible)
                return Vector2.zero;
            _portraitBody.sprite = portrait.Body;
            _portraitEyes.sprite = portrait.Eyes;
            _portraitDetails.sprite = portrait.Details;
            _portraitMouth.sprite = portrait.Mouth;
            SetLayerVisible(_portraitBody, portrait.Body != null);
            SetLayerVisible(_portraitEyes, portrait.Eyes != null);
            SetLayerVisible(_portraitDetails, portrait.Details != null);
            SetLayerVisible(_portraitMouth, portrait.Mouth != null);
            Vector2 size = ResolvePortraitSize(canvas);
            _portraitRoot.style.left = (canvas.x - size.x) * 0.5f * scale;
            _portraitRoot.style.top = (canvas.y - size.y) * scale;
            _portraitRoot.style.width = size.x * scale;
            _portraitRoot.style.height = size.y * scale;
            return size;
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
            foldout.style.paddingLeft = 10f;
            foldout.style.paddingRight = 10f;
            foldout.style.paddingBottom = 10f;
            foldout.style.backgroundColor = CardColor;
            SetRadius(foldout, 8f);
            return foldout;
        }

        private static VisualElement NewPanel()
        {
            VisualElement panel = new VisualElement();
            panel.style.backgroundColor = PanelColor;
            SetRadius(panel, 12f);
            SetBorder(panel, 1f, new Color32(45, 59, 84, 255));
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
                style.OutlineColor);
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
