using UnityEngine;

namespace Novelify
{
    /// <summary>
    /// Reusable visual styling for generated player-choice buttons.
    /// Choice placement and grouping intentionally live on the graph's
    /// Create Choice Layout node instead.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Novelify/Choice Style",
        fileName = "New Novel Choice Style")]
    public sealed class NovelChoiceStyle : ScriptableObject
    {
        public NovelBoxStyle Background = DefaultBackground;
        public Font Font;
        public FontStyle FontStyle = FontStyle.Normal;
        public NovelTextAlignment TextAlignment =
            NovelTextAlignment.CenterCenter;
        public Color TextColor = Color.white;
        public Color DisabledTextColor =
            new Color(0.62f, 0.65f, 0.72f, 1f);
        [Min(1f)] public float FontSize = 24f;
        public bool AutoSize;
        [Min(1f)] public float MinimumFontSize = 16f;
        [Min(1f)] public float MaximumFontSize = 24f;
        [Tooltip("Width of the entire generated choice button, including its background, in canvas units.")]
        [Min(1f)] public float ButtonWidth = 640f;
        [Tooltip("Height of the entire generated choice button, including its background, in canvas units.")]
        [Min(1f)] public float ButtonHeight = 64f;
        [Tooltip("Horizontal inset between the button edge and its text.")]
        [Min(0f)] public float HorizontalPadding = 18f;
        [Tooltip("Vertical inset between the button edge and its text. It is safely limited when necessary so the text area cannot collapse.")]
        [Min(0f)] public float VerticalPadding = 10f;

        [Header("Choice Panel")]
        public bool ShowPanelBackground;
        public NovelBoxStyle PanelBackground = DefaultPanelBackground;

        [Header("Button States")]
        public Color HighlightedTint =
            new Color(0.88f, 0.94f, 1f, 1f);
        public Color PressedTint =
            new Color(0.72f, 0.82f, 0.94f, 1f);
        public Color SelectedTint =
            new Color(0.82f, 0.9f, 1f, 1f);
        public Color DisabledTint =
            new Color(0.45f, 0.48f, 0.55f, 0.7f);
        [Min(0f)] public float TransitionDuration = 0.1f;

        public static NovelBoxStyle DefaultBackground => new NovelBoxStyle
        {
            FillColor = new Color(0.10f, 0.16f, 0.28f, 1f),
            Opacity = 1f,
            FillTiling = Vector2.one,
            CornerRadius = 16f,
            OutlineEnabled = false,
            OutlineColor = Color.white,
            OutlineTransparency = 0f,
            OutlineThickness = 2f,
            OutlineTiling = Vector2.one
        };

        public static NovelBoxStyle DefaultPanelBackground =>
            new NovelBoxStyle
            {
                FillColor = new Color(0.035f, 0.065f, 0.11f, 0.92f),
                Opacity = 0.92f,
                FillTiling = Vector2.one,
                CornerRadius = 24f,
                OutlineEnabled = false,
                OutlineColor = Color.white,
                OutlineTransparency = 0f,
                OutlineThickness = 2f,
                OutlineTiling = Vector2.one
            };

        /// <summary>
        /// Keeps the requested inset from reducing a one-line label below its
        /// readable height. ButtonHeight remains the exact button body height.
        /// </summary>
        public float EffectiveVerticalPadding
        {
            get
            {
                float smallestFont = AutoSize
                    ? Mathf.Min(FontSize, MinimumFontSize)
                    : FontSize;
                float minimumTextHeight = Mathf.Min(
                    Mathf.Max(1f, ButtonHeight),
                    Mathf.Max(1f, smallestFont) * 1.25f);
                float maximumInset = Mathf.Max(
                    0f, (Mathf.Max(1f, ButtonHeight) -
                         minimumTextHeight) * 0.5f);
                return Mathf.Min(Mathf.Max(0f, VerticalPadding),
                    maximumInset);
            }
        }

        private void OnValidate()
        {
            Background = Background.Validated();
            PanelBackground = PanelBackground.Validated();
            FontSize = Mathf.Max(1f, FontSize);
            MinimumFontSize = Mathf.Max(1f, MinimumFontSize);
            MaximumFontSize = Mathf.Max(MinimumFontSize, MaximumFontSize);
            ButtonWidth = Mathf.Max(1f, ButtonWidth);
            ButtonHeight = Mathf.Max(1f, ButtonHeight);
            HorizontalPadding = Mathf.Max(0f, HorizontalPadding);
            VerticalPadding = Mathf.Max(0f, VerticalPadding);
            TransitionDuration = Mathf.Max(0f, TransitionDuration);
        }
    }
}
