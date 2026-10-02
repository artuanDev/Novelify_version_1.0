using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Novelify.Editor
{
    internal static class NovelChoicePreviewGUI
    {
        public static void DrawButton(
            Rect rect,
            NovelChoiceStyle style,
            string label,
            Color stateTint,
            bool disabled = false,
            float visualScale = 1f)
        {
            NovelBoxStyle box = style != null
                ? style.Background.Validated()
                : NovelChoiceStyle.DefaultBackground.Validated();
            Color fill = Multiply(box.EffectiveFillColor, stateTint);
            Color outline = Multiply(box.EffectiveOutlineColor, stateTint);
            visualScale = Mathf.Max(0.01f, visualScale);
            DrawBox(rect, box, fill, outline, visualScale);

            GUIStyle text = new GUIStyle(EditorStyles.label)
            {
                alignment = ToTextAnchor(style != null
                    ? style.TextAlignment
                    : NovelTextAlignment.CenterCenter),
                font = style != null && style.Font != null
                    ? style.Font
                    : EditorStyles.label.font,
                fontStyle = style != null
                    ? style.FontStyle
                    : FontStyle.Normal,
                fontSize = Mathf.Max(7, Mathf.RoundToInt((style != null
                    ? Mathf.Max(8f, style.FontSize)
                    : 24f) * visualScale)),
                wordWrap = true
            };
            text.normal.textColor = disabled && style != null
                ? style.DisabledTextColor
                : style != null
                    ? style.TextColor
                    : Color.white;
            float horizontal = style != null
                ? style.HorizontalPadding * visualScale
                : 18f * visualScale;
            float vertical = style != null
                ? style.EffectiveVerticalPadding * visualScale
                : 10f * visualScale;
            float textWidth = Mathf.Max(
                1f, rect.width - horizontal * 2f);
            if (style == null || !style.AutoSize)
            {
                float requiredHeight = text.CalcHeight(
                    new GUIContent(label), textWidth);
                vertical = Mathf.Min(vertical, Mathf.Max(
                    0f, (rect.height - requiredHeight) * 0.5f));
            }
            Rect textRect = new Rect(
                rect.x + horizontal,
                rect.y + vertical,
                Mathf.Max(0f, textWidth),
                Mathf.Max(0f, rect.height - vertical * 2f));
            if (style != null && style.AutoSize)
            {
                int minimum = Mathf.Max(7, Mathf.RoundToInt(
                    style.MinimumFontSize * visualScale));
                int maximum = Mathf.Max(minimum, Mathf.RoundToInt(
                    style.MaximumFontSize * visualScale));
                text.fontSize = maximum;
                while (text.fontSize > minimum &&
                       text.CalcHeight(new GUIContent(label), textRect.width) >
                       textRect.height)
                    text.fontSize--;
            }
            GUI.Label(textRect, label, text);
        }

        public static void DrawPanel(
            Rect rect,
            NovelChoiceStyle style,
            float visualScale = 1f)
        {
            if (style == null || !style.ShowPanelBackground)
                return;
            NovelBoxStyle box = style.PanelBackground.Validated();
            DrawBox(
                rect,
                box,
                box.EffectiveFillColor,
                box.EffectiveOutlineColor,
                Mathf.Max(0.01f, visualScale));
        }

        private static void DrawBox(
            Rect rect,
            NovelBoxStyle box,
            Color fill,
            Color outline,
            float visualScale)
        {
            DrawRounded(rect, fill, box.CornerRadius * visualScale);
            if (box.OutlineEnabled && box.OutlineThickness > 0f)
            {
                Handles.BeginGUI();
                Handles.color = outline;
                Handles.DrawAAPolyLine(
                    Mathf.Max(1f, box.OutlineThickness * visualScale),
                    ClosedContour(rect, box.CornerRadius * visualScale));
                Handles.EndGUI();
            }
            if (box.FillTexture != null)
            {
                DrawRoundedTexture(
                    rect,
                    box.FillTexture,
                    fill,
                    box.FillTiling,
                    box.FillOffset,
                    box.CornerRadius * visualScale);
            }
        }

        public static Color NormalTint => Color.white;
        public static Color HighlightedTint(NovelChoiceStyle style) =>
            style != null ? style.HighlightedTint :
                new Color(0.88f, 0.94f, 1f, 1f);
        public static Color PressedTint(NovelChoiceStyle style) =>
            style != null ? style.PressedTint :
                new Color(0.72f, 0.82f, 0.94f, 1f);
        public static Color SelectedTint(NovelChoiceStyle style) =>
            style != null ? style.SelectedTint :
                new Color(0.82f, 0.9f, 1f, 1f);
        public static Color DisabledTint(NovelChoiceStyle style) =>
            style != null ? style.DisabledTint :
                new Color(0.45f, 0.48f, 0.55f, 0.7f);

        private static void DrawRounded(Rect rect, Color color, float radius)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAConvexPolygon(OpenContour(rect, radius));
            Handles.EndGUI();
        }

        private static void DrawRoundedTexture(
            Rect rect,
            Texture texture,
            Color tint,
            Vector2 tiling,
            Vector2 offset,
            float radius)
        {
            radius = Mathf.Clamp(
                radius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
            DrawTextureRegion(
                new Rect(rect.x, rect.y + radius, rect.width,
                    Mathf.Max(0f, rect.height - radius * 2f)),
                rect, texture, tint, tiling, offset);
            DrawTextureRegion(
                new Rect(rect.x + radius, rect.y,
                    Mathf.Max(0f, rect.width - radius * 2f), radius),
                rect, texture, tint, tiling, offset);
            DrawTextureRegion(
                new Rect(rect.x + radius, rect.yMax - radius,
                    Mathf.Max(0f, rect.width - radius * 2f), radius),
                rect, texture, tint, tiling, offset);
        }

        private static void DrawTextureRegion(
            Rect drawRect,
            Rect mappingRect,
            Texture texture,
            Color tint,
            Vector2 tiling,
            Vector2 offset)
        {
            if (texture == null || drawRect.width <= 0f ||
                drawRect.height <= 0f)
                return;
            Rect uv = new Rect(
                (drawRect.xMin - mappingRect.xMin) / mappingRect.width *
                    tiling.x + offset.x,
                (drawRect.yMin - mappingRect.yMin) / mappingRect.height *
                    tiling.y + offset.y,
                drawRect.width / mappingRect.width * tiling.x,
                drawRect.height / mappingRect.height * tiling.y);
            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(drawRect, texture, uv, true);
            GUI.color = previous;
        }

        private static Vector3[] ClosedContour(Rect rect, float radius)
        {
            List<Vector3> values = BuildContour(rect, radius);
            if (values.Count > 0)
                values.Add(values[0]);
            return values.ToArray();
        }

        private static Vector3[] OpenContour(Rect rect, float radius) =>
            BuildContour(rect, radius).ToArray();

        private static List<Vector3> BuildContour(Rect rect, float radius)
        {
            radius = Mathf.Clamp(
                radius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
            const int segments = 8;
            var points = new List<Vector3>((segments + 1) * 4);
            AddArc(points, new Vector2(rect.xMin + radius,
                rect.yMin + radius), radius, 180f, 270f, segments);
            AddArc(points, new Vector2(rect.xMax - radius,
                rect.yMin + radius), radius, 270f, 360f, segments);
            AddArc(points, new Vector2(rect.xMax - radius,
                rect.yMax - radius), radius, 0f, 90f, segments);
            AddArc(points, new Vector2(rect.xMin + radius,
                rect.yMax - radius), radius, 90f, 180f, segments);
            return points;
        }

        private static void AddArc(
            List<Vector3> points,
            Vector2 center,
            float radius,
            float start,
            float end,
            int segments)
        {
            for (int index = 0; index <= segments; index++)
            {
                float radians = Mathf.Lerp(start, end,
                    index / (float)segments) * Mathf.Deg2Rad;
                points.Add(center + new Vector2(
                    Mathf.Cos(radians), Mathf.Sin(radians)) * radius);
            }
        }

        private static Color Multiply(Color left, Color right) => new Color(
            left.r * right.r,
            left.g * right.g,
            left.b * right.b,
            left.a * right.a);

        private static TextAnchor ToTextAnchor(
            NovelTextAlignment alignment) => alignment switch
        {
            NovelTextAlignment.TopCenter => TextAnchor.UpperCenter,
            NovelTextAlignment.TopRight => TextAnchor.UpperRight,
            NovelTextAlignment.CenterLeft => TextAnchor.MiddleLeft,
            NovelTextAlignment.CenterRight => TextAnchor.MiddleRight,
            NovelTextAlignment.BottomLeft => TextAnchor.LowerLeft,
            NovelTextAlignment.BottomCenter => TextAnchor.LowerCenter,
            NovelTextAlignment.BottomRight => TextAnchor.LowerRight,
            NovelTextAlignment.TopLeft => TextAnchor.UpperLeft,
            _ => TextAnchor.MiddleCenter
        };
    }

    [CustomEditor(typeof(NovelChoiceStyle))]
    internal sealed class NovelChoiceStyleInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            NovelBoxStyleEditorGUI.Draw(
                serializedObject.FindProperty(nameof(NovelChoiceStyle.Background)),
                new GUIContent("Button Background"));
            Draw(nameof(NovelChoiceStyle.Font));
            Draw(nameof(NovelChoiceStyle.FontStyle));
            Draw(nameof(NovelChoiceStyle.TextAlignment));
            Draw(nameof(NovelChoiceStyle.TextColor));
            Draw(nameof(NovelChoiceStyle.DisabledTextColor));
            Draw(nameof(NovelChoiceStyle.FontSize));
            Draw(nameof(NovelChoiceStyle.AutoSize));
            Draw(nameof(NovelChoiceStyle.MinimumFontSize));
            Draw(nameof(NovelChoiceStyle.MaximumFontSize));
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Button Body Size",
                EditorStyles.boldLabel);
            Draw(nameof(NovelChoiceStyle.ButtonWidth),
                "Button Width (Entire Button)");
            Draw(nameof(NovelChoiceStyle.ButtonHeight),
                "Button Height (Entire Button)");
            Draw(nameof(NovelChoiceStyle.HorizontalPadding));
            Draw(nameof(NovelChoiceStyle.VerticalPadding),
                "Vertical Text Inset (Safe)");
            DrawPaddingNotice((NovelChoiceStyle)target);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Choice Panel",
                EditorStyles.boldLabel);
            Draw(nameof(NovelChoiceStyle.ShowPanelBackground));
            if (serializedObject.FindProperty(
                    nameof(NovelChoiceStyle.ShowPanelBackground)).boolValue)
            {
                NovelBoxStyleEditorGUI.Draw(
                    serializedObject.FindProperty(
                        nameof(NovelChoiceStyle.PanelBackground)),
                    new GUIContent("Panel Background"));
            }
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Button States",
                EditorStyles.boldLabel);
            Draw(nameof(NovelChoiceStyle.HighlightedTint));
            Draw(nameof(NovelChoiceStyle.PressedTint));
            Draw(nameof(NovelChoiceStyle.SelectedTint));
            Draw(nameof(NovelChoiceStyle.DisabledTint));
            Draw(nameof(NovelChoiceStyle.TransitionDuration));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Open Choice Style Preview",
                    GUILayout.Height(28f)))
                NovelChoiceStyleWindow.Open((NovelChoiceStyle)target);
        }

        private void Draw(string name) => EditorGUILayout.PropertyField(
            serializedObject.FindProperty(name), true);

        private void Draw(string name, string label) =>
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(name),
                new GUIContent(label), true);

        internal static void DrawPaddingNotice(NovelChoiceStyle style)
        {
            if (style != null && style.VerticalPadding >
                style.EffectiveVerticalPadding + 0.01f)
            {
                EditorGUILayout.HelpBox(
                    $"The vertical inset is limited to " +
                    $"{style.EffectiveVerticalPadding:0.#} for the current " +
                    "button height and font size, so it cannot cut the text.",
                    MessageType.Info);
            }
        }

        [UnityEditor.Callbacks.OnOpenAsset(1)]
        private static bool OpenAsset(EntityId entityID, int line)
        {
            NovelChoiceStyle style =
                EditorUtility.EntityIdToObject(entityID) as NovelChoiceStyle;
            if (style == null)
                return false;
            NovelChoiceStyleWindow.Open(style);
            return true;
        }
    }

    internal sealed class NovelChoiceStyleWindow : EditorWindow
    {
        private NovelChoiceStyle _style;
        private SerializedObject _serialized;
        private Vector2 _scroll;
        private Vector2 _previewScroll;
        private string _sampleText = "Ask about the hidden room";

        public static void Open(NovelChoiceStyle style)
        {
            if (style == null)
                return;
            NovelChoiceStyleWindow window =
                GetWindow<NovelChoiceStyleWindow>();
            window.titleContent = new GUIContent("Choice Style Preview");
            window.minSize = new Vector2(760f, 680f);
            window._style = style;
            window._serialized = new SerializedObject(style);
            window.Show();
            window.Focus();
        }

        private void OnGUI()
        {
            if (_style == null)
            {
                EditorGUILayout.HelpBox(
                    "Double-click a Novel Choice Style asset to preview it.",
                    MessageType.Info);
                return;
            }
            _serialized ??= new SerializedObject(_style);
            _serialized.Update();
            EditorGUILayout.LabelField(_style.name, EditorStyles.largeLabel);
            EditorGUILayout.HelpBox(
                "This asset controls generated choice appearance. Placement and grouping remain on Create Choice Layout nodes.",
                MessageType.None);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _sampleText = EditorGUILayout.TextField(
                "Preview Text", _sampleText ?? string.Empty);
            Rect preview = GUILayoutUtility.GetRect(
                720f, 310f, GUILayout.ExpandWidth(true));
            DrawPreview(preview);
            NovelBoxStyleEditorGUI.Draw(
                _serialized.FindProperty(nameof(NovelChoiceStyle.Background)),
                new GUIContent("Button Background"));
            DrawRemainingProperties(_serialized);
            SerializedProperty showPanel = _serialized.FindProperty(
                nameof(NovelChoiceStyle.ShowPanelBackground));
            EditorGUILayout.PropertyField(showPanel);
            if (showPanel.boolValue)
            {
                NovelBoxStyleEditorGUI.Draw(
                    _serialized.FindProperty(
                        nameof(NovelChoiceStyle.PanelBackground)),
                    new GUIContent("Panel Background"));
            }
            NovelChoiceStyleInspector.DrawPaddingNotice(_style);
            EditorGUILayout.EndScrollView();
            if (_serialized.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_style);
                Repaint();
            }
            if (GUILayout.Button("Save Asset", GUILayout.Height(26f)))
                AssetDatabase.SaveAssetIfDirty(_style);
        }

        private void DrawPreview(Rect area)
        {
            EditorGUI.DrawRect(area, new Color(0.025f, 0.055f, 0.1f));
            float contentWidth = Mathf.Max(1f, area.width - 16f);
            float contentScale = Mathf.Min(
                1f,
                Mathf.Max(1f, contentWidth - 48f) / 1200f);
            float contentButtonHeight = Mathf.Max(
                2f, _style.ButtonHeight * contentScale);
            float contentHeight = Mathf.Max(
                area.height - 1f,
                34f + (contentButtonHeight + 8f) * 5f);
            Rect content = new Rect(
                0f, 0f, contentWidth, contentHeight);
            _previewScroll = GUI.BeginScrollView(
                area, _previewScroll, content, false, false);
            area = content;
            EditorGUI.DrawRect(area, new Color(0.025f, 0.055f, 0.1f));
            string[] labels = { "Normal", "Highlighted", "Pressed",
                "Selected", "Unavailable" };
            Color[] tints = { NovelChoicePreviewGUI.NormalTint,
                NovelChoicePreviewGUI.HighlightedTint(_style),
                NovelChoicePreviewGUI.PressedTint(_style),
                NovelChoicePreviewGUI.SelectedTint(_style),
                NovelChoicePreviewGUI.DisabledTint(_style) };
            Rect panel = new Rect(
                area.x + 10f,
                area.y + 25f,
                Mathf.Max(1f, area.width - 20f),
                Mathf.Max(1f, area.height - 31f));
            const float referenceCanvasWidth = 1200f;
            float availableWidth = Mathf.Max(1f, area.width - 48f);
            float visualScale = Mathf.Min(
                1f,
                availableWidth / referenceCanvasWidth);
            NovelChoicePreviewGUI.DrawPanel(
                panel, _style, visualScale);
            float width = Mathf.Clamp(
                _style.ButtonWidth * visualScale, 2f, availableWidth);
            float height = Mathf.Max(
                2f, _style.ButtonHeight * visualScale);
            float rowHeight = height + 8f;
            GUIStyle dimensions = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            dimensions.normal.textColor = new Color(0.72f, 0.8f, 0.9f);
            GUI.Label(new Rect(area.x + 12f, area.y + 5f,
                    area.width - 24f, 18f),
                $"Entire button: {_style.ButtonWidth:0.#} x " +
                $"{_style.ButtonHeight:0.#} canvas units  |  " +
                $"Preview scale: {visualScale * 100f:0}%",
                dimensions);
            for (int index = 0; index < labels.Length; index++)
            {
                Rect button = new Rect(
                    area.center.x - width * 0.5f,
                    area.y + 28f + index * rowHeight +
                    (rowHeight - height) * 0.5f,
                    width, height);
                string text = $"{labels[index]}  •  {_sampleText}";
                NovelChoicePreviewGUI.DrawButton(
                    button, _style, text, tints[index], index == 4,
                    visualScale);
            }
            GUI.EndScrollView();
        }

        private static void DrawRemainingProperties(SerializedObject value)
        {
            string[] names =
            {
                nameof(NovelChoiceStyle.Font),
                nameof(NovelChoiceStyle.FontStyle),
                nameof(NovelChoiceStyle.TextAlignment),
                nameof(NovelChoiceStyle.TextColor),
                nameof(NovelChoiceStyle.DisabledTextColor),
                nameof(NovelChoiceStyle.FontSize),
                nameof(NovelChoiceStyle.AutoSize),
                nameof(NovelChoiceStyle.MinimumFontSize),
                nameof(NovelChoiceStyle.MaximumFontSize),
                nameof(NovelChoiceStyle.ButtonWidth),
                nameof(NovelChoiceStyle.ButtonHeight),
                nameof(NovelChoiceStyle.HorizontalPadding),
                nameof(NovelChoiceStyle.VerticalPadding),
                nameof(NovelChoiceStyle.HighlightedTint),
                nameof(NovelChoiceStyle.PressedTint),
                nameof(NovelChoiceStyle.SelectedTint),
                nameof(NovelChoiceStyle.DisabledTint),
                nameof(NovelChoiceStyle.TransitionDuration)
            };
            foreach (string name in names)
            {
                GUIContent label = name == nameof(NovelChoiceStyle.ButtonWidth)
                    ? new GUIContent("Button Width (Entire Button)")
                    : name == nameof(NovelChoiceStyle.ButtonHeight)
                        ? new GUIContent("Button Height (Entire Button)")
                        : name == nameof(NovelChoiceStyle.VerticalPadding)
                            ? new GUIContent(
                                "Vertical Text Inset (Safe)")
                        : null;
                if (label != null)
                    EditorGUILayout.PropertyField(
                        value.FindProperty(name), label, true);
                else
                    EditorGUILayout.PropertyField(
                        value.FindProperty(name), true);
            }
        }
    }

    internal sealed class NovelChoiceComposerWindow : EditorWindow
    {
        private enum PreviewResolutionPreset
        {
            GameView,
            FullHD,
            HD,
            Portrait,
            Square,
            Custom
        }

        private enum PreviewChoiceState
        {
            Normal,
            Highlighted,
            Pressed,
            Selected,
            Disabled
        }

        private sealed class Draft
        {
            public NovelChoiceStyle Style;
            public NovelDialogueAnchor Anchor;
            public Vector2 Offset;
            public Vector2 PanelSize;
            public NovelChoiceArrangement Arrangement;
            public int ChoicesPerGroup;
            public float ChoiceSpacing;
            public float GroupSpacing;
            public float CircleRadius;
            public float CircleStartAngle;
            public float CircleArc;
            public int PreviewChoices = 6;
        }

        private Graph _graph;
        private CreateChoiceLayoutNode _node;
        private Draft _draft;
        private Vector2 _scroll;
        private bool _dirty;
        private string _status = "Live preview";
        private PreviewResolutionPreset _resolutionPreset =
            PreviewResolutionPreset.GameView;
        private Vector2Int _customResolution = new Vector2Int(1920, 1080);
        private float _previewZoom = 1f;
        private bool _showSafeArea = true;
        private int _focusedChoice = 1;
        private PreviewChoiceState _focusedState =
            PreviewChoiceState.Highlighted;
        private string _sampleChoiceText = "Continue the story";
        private float _lastLayoutFit = 1f;
        private Vector2 _lastAuthoredChoiceSize = new Vector2(640f, 64f);
        private Vector2 _lastEffectiveChoiceSize = new Vector2(640f, 64f);

        public static void Open(CreateChoiceLayoutNode source)
        {
            if (source == null || source.Graph == null)
                return;
            NovelChoiceComposerWindow window =
                GetWindow<NovelChoiceComposerWindow>();
            window.titleContent = new GUIContent("Choice Layout Composer");
            window.minSize = new Vector2(1040f, 650f);
            window._graph = source.Graph;
            window._node = source;
            window.LoadDraft();
            window.Show();
            window.Focus();
        }

        private void OnEnable() => Undo.undoRedoPerformed += ReloadAfterUndo;
        private void OnDisable() => Undo.undoRedoPerformed -= ReloadAfterUndo;

        private void OnInspectorUpdate()
        {
            if (_resolutionPreset == PreviewResolutionPreset.GameView)
                Repaint();
        }

        private void ReloadAfterUndo()
        {
            if (_node != null)
                LoadDraft();
            Repaint();
        }

        private void LoadDraft()
        {
            _draft = new Draft
            {
                Style = Read<NovelChoiceStyle>("Style Asset", null),
                Anchor = Read("Screen Anchor",
                    NovelDialogueAnchor.CenterCenter),
                Offset = Read("Offset", Vector2.zero),
                PanelSize = Read("Panel Size", new Vector2(1440f, 720f)),
                Arrangement = Read("Arrangement",
                    NovelChoiceArrangement.Vertical),
                ChoicesPerGroup = Read("Choices Per Group", 0),
                ChoiceSpacing = Read("Choice Spacing", 14f),
                GroupSpacing = Read("Group Spacing", 24f),
                CircleRadius = Read("Circle Radius", 210f),
                CircleStartAngle = Read("Circle Start Angle", 90f),
                CircleArc = Read("Circle Arc", 360f),
                PreviewChoices = _draft?.PreviewChoices ?? 6
            };
            ValidateDraft();
            ValidatePreview();
            _dirty = false;
            _status = "Live preview";
        }

        private void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, 52f),
                new Color32(13, 26, 45, 255));
            GUIStyle title = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                normal = { textColor = new Color32(246, 244, 239, 255) }
            };
            GUI.Label(new Rect(18f, 9f, 360f, 22f),
                "Choice Layout Composer", title);
            GUI.Label(new Rect(18f, 30f, 560f, 18f),
                "Preview generated choices against the current Game View aspect ratio.",
                EditorStyles.miniLabel);

            if (_node == null || _graph == null || _draft == null)
            {
                EditorGUI.HelpBox(new Rect(16f, 68f,
                    position.width - 32f, 52f),
                    "The graph was reloaded. Double-click a Create Choice Layout node to reconnect this composer.",
                    MessageType.Info);
                return;
            }

            const float inspectorWidth = 350f;
            Rect stage = new Rect(12f, 64f,
                Mathf.Max(400f, position.width - inspectorWidth - 36f),
                Mathf.Max(480f, position.height - 112f));
            Rect inspector = new Rect(stage.xMax + 12f, 64f,
                inspectorWidth, stage.height);
            DrawStage(stage);
            DrawInspector(inspector);

            EditorGUI.DrawRect(new Rect(0f, position.height - 38f,
                position.width, 38f), new Color32(13, 26, 45, 255));
            GUI.Label(new Rect(16f, position.height - 28f, 500f, 20f),
                _status, EditorStyles.miniLabel);
            if (GUI.Button(new Rect(position.width - 250f,
                    position.height - 32f, 110f, 24f), "Revert"))
                LoadDraft();
            GUI.enabled = _dirty;
            if (GUI.Button(new Rect(position.width - 132f,
                    position.height - 32f, 116f, 24f), "Apply & Save"))
                ApplyAndSave();
            GUI.enabled = true;
        }

        private void DrawInspector(Rect area)
        {
            GUI.Box(area, GUIContent.none, EditorStyles.helpBox);
            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 8f,
                area.width - 20f, area.height - 16f));
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUI.BeginChangeCheck();
            _draft.Style = (NovelChoiceStyle)EditorGUILayout.ObjectField(
                "Style Asset", _draft.Style, typeof(NovelChoiceStyle), false);
            if (_draft.Style != null &&
                GUILayout.Button("Open Style Preview"))
                NovelChoiceStyleWindow.Open(_draft.Style);
            EditorGUILayout.LabelField(
                "Authored Entire Button",
                $"{_lastAuthoredChoiceSize.x:0.#} x " +
                $"{_lastAuthoredChoiceSize.y:0.#}");
            EditorGUILayout.LabelField(
                "Current Preview Button",
                $"{_lastEffectiveChoiceSize.x:0.#} x " +
                $"{_lastEffectiveChoiceSize.y:0.#}");
            if (_lastLayoutFit < 0.999f)
            {
                EditorGUILayout.HelpBox(
                    $"The complete layout is fitted to " +
                    $"{_lastLayoutFit * 100f:0}% so no button leaves the " +
                    "panel. Increase Panel Size, reduce the preview choice " +
                    "count, or use more groups to preserve the exact " +
                    "authored button size.",
                    MessageType.Info);
            }
            if (_draft.Style != null &&
                _draft.Style.VerticalPadding >
                _draft.Style.EffectiveVerticalPadding + 0.01f)
            {
                EditorGUILayout.HelpBox(
                    $"Vertical text inset is safely limited to " +
                    $"{_draft.Style.EffectiveVerticalPadding:0.#}; the " +
                    "requested value would leave too little room for text.",
                    MessageType.Info);
            }
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
            _draft.Anchor = (NovelDialogueAnchor)EditorGUILayout.EnumPopup(
                "Screen Anchor", _draft.Anchor);
            _draft.Offset = EditorGUILayout.Vector2Field("Offset", _draft.Offset);
            _draft.PanelSize = EditorGUILayout.Vector2Field(
                "Panel Size", _draft.PanelSize);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Arrangement", EditorStyles.boldLabel);
            _draft.Arrangement = (NovelChoiceArrangement)
                EditorGUILayout.EnumPopup("Arrangement", _draft.Arrangement);
            _draft.ChoicesPerGroup = EditorGUILayout.IntField(
                "Choices Per Group", _draft.ChoicesPerGroup);
            EditorGUILayout.HelpBox(
                "0 keeps all choices together. Groups become columns, rows, or rings according to the arrangement.",
                MessageType.None);
            _draft.ChoiceSpacing = EditorGUILayout.FloatField(
                "Choice Spacing", _draft.ChoiceSpacing);
            _draft.GroupSpacing = EditorGUILayout.FloatField(
                "Group Spacing", _draft.GroupSpacing);
            if (_draft.Arrangement == NovelChoiceArrangement.Circular)
            {
                _draft.CircleRadius = EditorGUILayout.FloatField(
                    "Circle Radius", _draft.CircleRadius);
                _draft.CircleStartAngle = EditorGUILayout.FloatField(
                    "Circle Start Angle", _draft.CircleStartAngle);
                _draft.CircleArc = EditorGUILayout.Slider(
                    "Circle Arc", _draft.CircleArc, -360f, 360f);
            }
            bool nodeChanged = EditorGUI.EndChangeCheck();
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _resolutionPreset = (PreviewResolutionPreset)
                EditorGUILayout.EnumPopup(
                    "Resolution", _resolutionPreset);
            if (_resolutionPreset == PreviewResolutionPreset.Custom)
                _customResolution = EditorGUILayout.Vector2IntField(
                    "Custom Size", _customResolution);
            _previewZoom = EditorGUILayout.Slider(
                "Zoom", _previewZoom, 0.5f, 2f);
            _showSafeArea = EditorGUILayout.Toggle(
                "Safe-area Guide (5%)", _showSafeArea);
            _draft.PreviewChoices = EditorGUILayout.IntSlider(
                "Choice Count", _draft.PreviewChoices, 1, 12);
            bool resizePanel = GUILayout.Button(
                "Size Panel to Preview Choices (100%)");
            _focusedChoice = EditorGUILayout.IntSlider(
                "Focused Choice", _focusedChoice, 1,
                Mathf.Max(1, _draft.PreviewChoices));
            _focusedState = (PreviewChoiceState)EditorGUILayout.EnumPopup(
                "Focused State", _focusedState);
            _sampleChoiceText = EditorGUILayout.TextField(
                "Sample Text", _sampleChoiceText ?? string.Empty);
            bool previewChanged = EditorGUI.EndChangeCheck();
            if (resizePanel)
            {
                ResizePanelToPreviewChoices();
                nodeChanged = true;
            }
            ValidatePreview();
            if (nodeChanged)
            {
                ValidateDraft();
                _dirty = true;
                _status = "Preview changed. Apply & Save writes every option to the node.";
                Repaint();
            }
            else if (previewChanged)
            {
                _status = "Preview controls do not change the graph.";
                Repaint();
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawStage(Rect area)
        {
            GUI.BeginGroup(area);
            area = new Rect(0f, 0f, area.width, area.height);
            EditorGUI.DrawRect(area, new Color32(8, 22, 39, 255));
            Vector2 resolution = GetPreviewResolution();
            float scale = Mathf.Min(
                (area.width - 30f) / resolution.x,
                (area.height - 54f) / resolution.y) * _previewZoom;
            Vector2 viewportSize = resolution * scale;
            Rect viewport = new Rect(
                area.center.x - viewportSize.x * 0.5f,
                area.center.y - viewportSize.y * 0.5f + 9f,
                viewportSize.x, viewportSize.y);
            EditorGUI.DrawRect(viewport, new Color32(17, 35, 58, 255));
            GUI.Box(viewport, GUIContent.none);
            GUI.Label(new Rect(viewport.x + 8f, viewport.y + 6f,
                320f, 18f),
                $"{_resolutionPreset.ToString().ToUpperInvariant()}  " +
                $"{resolution.x:0} x {resolution.y:0}  •  {_previewZoom:0.00}x",
                EditorStyles.miniLabel);

            DrawViewportGuides(viewport);

            Vector2 anchor = AnchorPoint(_draft.Anchor);
            Vector2 panelSize = _draft.PanelSize * scale;
            Rect panel = new Rect(
                viewport.x + anchor.x * viewport.width +
                _draft.Offset.x * scale - anchor.x * panelSize.x,
                viewport.y + (1f - anchor.y) * viewport.height -
                _draft.Offset.y * scale - (1f - anchor.y) * panelSize.y,
                panelSize.x, panelSize.y);
            bool panelAdjusted = KeepRectInside(ref panel, viewport);
            if (_draft.Style != null &&
                _draft.Style.ShowPanelBackground)
                NovelChoicePreviewGUI.DrawPanel(
                    panel, _draft.Style, scale);
            else
                EditorGUI.DrawRect(
                    panel, new Color(0.16f, 0.24f, 0.38f, 0.18f));
            Handles.BeginGUI();
            Handles.color = new Color(0.45f, 0.62f, 1f, 0.65f);
            Handles.DrawAAPolyLine(1.5f,
                new Vector3(panel.xMin, panel.yMin),
                new Vector3(panel.xMax, panel.yMin),
                new Vector3(panel.xMax, panel.yMax),
                new Vector3(panel.xMin, panel.yMax),
                new Vector3(panel.xMin, panel.yMin));
            Handles.EndGUI();

            Vector2 anchorPosition = new Vector2(
                viewport.x + anchor.x * viewport.width,
                viewport.y + (1f - anchor.y) * viewport.height);
            DrawCross(anchorPosition, 7f,
                new Color(1f, 0.78f, 0.3f, 0.9f));

            int count = Mathf.Max(1, _draft.PreviewChoices);
            int perGroup = _draft.ChoicesPerGroup <= 0
                ? count
                : Mathf.Max(1, _draft.ChoicesPerGroup);
            int groups = Mathf.CeilToInt(count / (float)perGroup);
            Vector2 authoredChoiceSize = new Vector2(
                _draft.Style != null ? _draft.Style.ButtonWidth : 640f,
                _draft.Style != null ? _draft.Style.ButtonHeight : 64f);
            Vector2 effectivePanelSize = new Vector2(
                panel.width / Mathf.Max(0.0001f, scale),
                panel.height / Mathf.Max(0.0001f, scale));
            float layoutFit = NovelChoiceLayoutGroup.CalculateFitScale(
                _draft.Arrangement,
                count,
                perGroup,
                authoredChoiceSize,
                _draft.ChoiceSpacing,
                _draft.GroupSpacing,
                _draft.CircleRadius,
                _draft.CircleStartAngle,
                _draft.CircleArc,
                effectivePanelSize);
            _lastLayoutFit = layoutFit;
            _lastAuthoredChoiceSize = authoredChoiceSize;
            _lastEffectiveChoiceSize = authoredChoiceSize * layoutFit;
            float contentScale = scale * layoutFit;
            float width = authoredChoiceSize.x * contentScale;
            float height = authoredChoiceSize.y * contentScale;
            bool outsidePanel = false;
            bool outsideViewport = false;
            for (int index = 0; index < count; index++)
            {
                Vector2 position = NovelChoiceLayoutGroup
                    .CalculateChoicePosition(
                        _draft.Arrangement,
                        index,
                        count,
                        perGroup,
                        groups,
                        authoredChoiceSize,
                        _draft.ChoiceSpacing,
                        _draft.GroupSpacing,
                        _draft.CircleRadius,
                        _draft.CircleStartAngle,
                        _draft.CircleArc) * contentScale;
                Rect button = new Rect(
                    panel.center.x + position.x - width * 0.5f,
                    panel.center.y - position.y - height * 0.5f,
                    width, height);
                bool focused = index == _focusedChoice - 1;
                PreviewChoiceState state = focused
                    ? _focusedState
                    : PreviewChoiceState.Normal;
                bool hovered = button.Contains(Event.current.mousePosition);
                if (!focused && hovered)
                    state = PreviewChoiceState.Highlighted;
                bool disabled = state == PreviewChoiceState.Disabled;
                Color tint = StateTint(state);
                outsidePanel |= !Contains(panel, button);
                outsideViewport |= !Contains(viewport, button);
                int group = index / perGroup;
                NovelChoicePreviewGUI.DrawButton(button, _draft.Style,
                    disabled
                        ? $"{_sampleChoiceText}  •  Unavailable"
                        : $"{_sampleChoiceText}  {index + 1}",
                    tint, disabled, contentScale);
                if (groups > 1)
                {
                    GUI.Label(new Rect(button.x + 4f, button.y + 2f,
                        38f, 14f), $"G{group + 1}",
                        EditorStyles.centeredGreyMiniLabel);
                }
                if (hovered && Event.current.type == EventType.MouseDown &&
                    Event.current.button == 0)
                {
                    _focusedChoice = index + 1;
                    _focusedState = PreviewChoiceState.Selected;
                    _status = $"Previewing Choice {index + 1} as Selected.";
                    Event.current.Use();
                    Repaint();
                }
            }
            if (outsidePanel || outsideViewport)
                DrawWarningBadge(area, outsideViewport
                    ? "Choices extend outside the screen"
                    : "Choices extend outside the authored panel");
            else if (layoutFit < 0.999f)
                DrawInfoBadge(area,
                    $"Choices fitted to {layoutFit * 100f:0}% to stay in frame");
            else if (panelAdjusted)
                DrawInfoBadge(area, "Panel constrained to the screen bounds");
            GUI.EndGroup();
        }

        private void ResizePanelToPreviewChoices()
        {
            int count = Mathf.Max(1, _draft.PreviewChoices);
            int perGroup = _draft.ChoicesPerGroup <= 0
                ? count
                : Mathf.Max(1, _draft.ChoicesPerGroup);
            Vector2 choiceSize = new Vector2(
                _draft.Style != null ? _draft.Style.ButtonWidth : 640f,
                _draft.Style != null ? _draft.Style.ButtonHeight : 64f);
            Vector2 contentSize = NovelChoiceLayoutGroup.CalculateContentSize(
                _draft.Arrangement,
                count,
                perGroup,
                choiceSize,
                _draft.ChoiceSpacing,
                _draft.GroupSpacing,
                _draft.CircleRadius,
                _draft.CircleStartAngle,
                _draft.CircleArc);
            _draft.PanelSize = contentSize + Vector2.one * 24f;
            ValidateDraft();
        }

        private void ApplyAndSave()
        {
            bool recording = false;
            try
            {
                _graph.UndoBeginRecordGraph("Edit Choice Layout");
                recording = true;
                Write("Style Asset", _draft.Style);
                Write("Screen Anchor", _draft.Anchor);
                Write("Offset", _draft.Offset);
                Write("Panel Size", _draft.PanelSize);
                Write("Arrangement", _draft.Arrangement);
                Write("Choices Per Group", _draft.ChoicesPerGroup);
                Write("Choice Spacing", _draft.ChoiceSpacing);
                Write("Group Spacing", _draft.GroupSpacing);
                Write("Circle Radius", _draft.CircleRadius);
                Write("Circle Start Angle", _draft.CircleStartAngle);
                Write("Circle Arc", _draft.CircleArc);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _status = "Could not update the selected node. See Console.";
                return;
            }
            finally
            {
                if (recording)
                    _graph.UndoEndRecordGraph();
            }
            GraphDatabase.SaveGraph(_graph);
            AssetDatabase.SaveAssets();
            _dirty = false;
            _status = "Choice layout saved to the graph asset.";
        }

        private void ValidateDraft()
        {
            _draft.PanelSize = new Vector2(
                Mathf.Max(1f, _draft.PanelSize.x),
                Mathf.Max(1f, _draft.PanelSize.y));
            _draft.ChoicesPerGroup = Mathf.Max(0, _draft.ChoicesPerGroup);
            _draft.ChoiceSpacing = Mathf.Max(0f, _draft.ChoiceSpacing);
            _draft.GroupSpacing = Mathf.Max(0f, _draft.GroupSpacing);
            _draft.CircleRadius = Mathf.Max(0f, _draft.CircleRadius);
            _draft.CircleArc = Mathf.Clamp(_draft.CircleArc, -360f, 360f);
            _draft.PreviewChoices = Mathf.Clamp(_draft.PreviewChoices, 1, 12);
        }

        private void ValidatePreview()
        {
            _customResolution = new Vector2Int(
                Mathf.Clamp(_customResolution.x, 240, 7680),
                Mathf.Clamp(_customResolution.y, 240, 7680));
            _previewZoom = Mathf.Clamp(_previewZoom, 0.5f, 2f);
            int count = _draft != null
                ? Mathf.Clamp(_draft.PreviewChoices, 1, 12)
                : 1;
            _focusedChoice = Mathf.Clamp(_focusedChoice, 1, count);
        }

        private T Read<T>(string name, T fallback)
        {
            INodeOption option = _node?.GetNodeOptionByName(name);
            return option != null && option.TryGetValue(out T value)
                ? value
                : fallback;
        }

        private void Write<T>(string name, T value) =>
            _node?.GetNodeOptionByName(name)?.TrySetValue(value);

        private static Vector2 AnchorPoint(NovelDialogueAnchor value) =>
            value switch
            {
                NovelDialogueAnchor.TopLeft => new Vector2(0f, 1f),
                NovelDialogueAnchor.TopCenter => new Vector2(0.5f, 1f),
                NovelDialogueAnchor.TopRight => new Vector2(1f, 1f),
                NovelDialogueAnchor.CenterLeft => new Vector2(0f, 0.5f),
                NovelDialogueAnchor.CenterRight => new Vector2(1f, 0.5f),
                NovelDialogueAnchor.BottomLeft => Vector2.zero,
                NovelDialogueAnchor.BottomCenter => new Vector2(0.5f, 0f),
                NovelDialogueAnchor.BottomRight => new Vector2(1f, 0f),
                _ => new Vector2(0.5f, 0.5f)
            };

        private Color StateTint(PreviewChoiceState state) => state switch
        {
            PreviewChoiceState.Highlighted =>
                NovelChoicePreviewGUI.HighlightedTint(_draft.Style),
            PreviewChoiceState.Pressed =>
                NovelChoicePreviewGUI.PressedTint(_draft.Style),
            PreviewChoiceState.Selected =>
                NovelChoicePreviewGUI.SelectedTint(_draft.Style),
            PreviewChoiceState.Disabled =>
                NovelChoicePreviewGUI.DisabledTint(_draft.Style),
            _ => NovelChoicePreviewGUI.NormalTint
        };

        private Vector2 GetPreviewResolution() => _resolutionPreset switch
        {
            PreviewResolutionPreset.FullHD => new Vector2(1920f, 1080f),
            PreviewResolutionPreset.HD => new Vector2(1280f, 720f),
            PreviewResolutionPreset.Portrait => new Vector2(1080f, 1920f),
            PreviewResolutionPreset.Square => new Vector2(1080f, 1080f),
            PreviewResolutionPreset.Custom => new Vector2(
                _customResolution.x, _customResolution.y),
            _ => GetGameViewResolution()
        };

        private void DrawViewportGuides(Rect viewport)
        {
            Handles.BeginGUI();
            Handles.color = new Color(0.45f, 0.62f, 0.82f, 0.22f);
            Handles.DrawDottedLine(
                new Vector3(viewport.center.x, viewport.yMin),
                new Vector3(viewport.center.x, viewport.yMax), 4f);
            Handles.DrawDottedLine(
                new Vector3(viewport.xMin, viewport.center.y),
                new Vector3(viewport.xMax, viewport.center.y), 4f);
            if (_showSafeArea)
            {
                Rect safe = new Rect(
                    viewport.x + viewport.width * 0.05f,
                    viewport.y + viewport.height * 0.05f,
                    viewport.width * 0.9f,
                    viewport.height * 0.9f);
                Handles.color = new Color(0.3f, 0.9f, 0.65f, 0.45f);
                Handles.DrawAAPolyLine(1f,
                    new Vector3(safe.xMin, safe.yMin),
                    new Vector3(safe.xMax, safe.yMin),
                    new Vector3(safe.xMax, safe.yMax),
                    new Vector3(safe.xMin, safe.yMax),
                    new Vector3(safe.xMin, safe.yMin));
            }
            Handles.EndGUI();
        }

        private static void DrawCross(
            Vector2 center, float radius, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAPolyLine(2f,
                center + Vector2.left * radius,
                center + Vector2.right * radius);
            Handles.DrawAAPolyLine(2f,
                center + Vector2.up * radius,
                center + Vector2.down * radius);
            Handles.EndGUI();
        }

        private static void DrawWarningBadge(Rect area, string message)
        {
            GUIStyle badge = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            badge.normal.textColor = new Color(1f, 0.78f, 0.35f);
            GUI.Label(new Rect(area.x + 14f, area.yMax - 34f,
                Mathf.Min(330f, area.width - 28f), 24f), message, badge);
        }

        private static void DrawInfoBadge(Rect area, string message)
        {
            GUIStyle badge = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            badge.normal.textColor = new Color(0.48f, 0.9f, 0.72f);
            GUI.Label(new Rect(area.x + 14f, area.yMax - 34f,
                Mathf.Min(360f, area.width - 28f), 24f), message, badge);
        }

        private static bool KeepRectInside(ref Rect value, Rect bounds)
        {
            Rect original = value;
            value.width = Mathf.Min(value.width, bounds.width);
            value.height = Mathf.Min(value.height, bounds.height);
            value.x = Mathf.Clamp(
                value.x, bounds.xMin, bounds.xMax - value.width);
            value.y = Mathf.Clamp(
                value.y, bounds.yMin, bounds.yMax - value.height);
            return RectChanged(original, value);
        }

        private static bool RectChanged(Rect left, Rect right) =>
            Mathf.Abs(left.x - right.x) > 0.01f ||
            Mathf.Abs(left.y - right.y) > 0.01f ||
            Mathf.Abs(left.width - right.width) > 0.01f ||
            Mathf.Abs(left.height - right.height) > 0.01f;

        private static bool Contains(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - 0.5f &&
            inner.xMax <= outer.xMax + 0.5f &&
            inner.yMin >= outer.yMin - 0.5f &&
            inner.yMax <= outer.yMax + 0.5f;

        private static Vector2 GetGameViewResolution()
        {
            try
            {
                Vector2 size = Handles.GetMainGameViewSize();
                if (size.x >= 1f && size.y >= 1f)
                    return size;
            }
            catch (InvalidOperationException)
            {
            }
            return new Vector2(
                Mathf.Max(1, PlayerSettings.defaultScreenWidth),
                Mathf.Max(1, PlayerSettings.defaultScreenHeight));
        }
    }
}
