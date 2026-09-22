using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Novelify
{
    /// <summary>
    /// Novelify's dependency-free Canvas text control. It accepts the dialogue
    /// markup produced by the graph editor and translates it to Unity UI rich
    /// text while retaining visible-character and effect information.
    /// </summary>
    [AddComponentMenu("UI/Novelify Text")]
    [DisallowMultipleComponent]
    public sealed class NovelText : Text
    {
        [SerializeField, TextArea(3, 10)] private string sourceText = string.Empty;
        [SerializeField] private int visibleCharacterLimit = int.MaxValue;

        private NovelTextDocument _document = NovelTextDocument.Empty;
        private readonly Dictionary<string, Font> _fontAssets =
            new Dictionary<string, Font>(StringComparer.Ordinal);
        private readonly List<FontOverlay> _fontOverlays =
            new List<FontOverlay>();
        private bool _fontOverlaysDirty;
        private bool _buildingMesh;

        private sealed class FontOverlay
        {
            public GameObject GameObject;
            public RectTransform RectTransform;
            public int CharacterIndex;
            public Vector2 BasePosition;
        }

        public override string text
        {
            get => _buildingMesh ? base.text : sourceText ?? string.Empty;
            set
            {
                value ??= string.Empty;
                if (sourceText == value && _document.Source == value)
                    return;

                sourceText = value;
                _document = NovelTextMarkup.Parse(value);
                RefreshRenderedText();
            }
        }

        public int maxVisibleCharacters
        {
            get => visibleCharacterLimit;
            set
            {
                if (visibleCharacterLimit == value)
                    return;
                visibleCharacterLimit = value;
                RefreshRenderedText();
            }
        }

        public int visibleCharacterCount => _document.Characters.Count;
        public bool hasAnimatedEffects => _document.HasAnimatedEffects;

        // Compatibility-shaped properties keep the presentation code concise
        // while using UnityEngine.UI.Text internally.
        public bool richText
        {
            get => supportRichText;
            set => supportRichText = value;
        }

        public bool enableAutoSizing
        {
            get => resizeTextForBestFit;
            set => resizeTextForBestFit = value;
        }

        public float fontSizeMin
        {
            get => resizeTextMinSize;
            set => resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(value));
        }

        public float fontSizeMax
        {
            get => resizeTextMaxSize;
            set => resizeTextMaxSize = Mathf.Max(1, Mathf.RoundToInt(value));
        }

        public new float fontSize
        {
            get => base.fontSize;
            set
            {
                int rounded = Mathf.Max(1, Mathf.RoundToInt(value));
                if (base.fontSize == rounded)
                    return;
                base.fontSize = rounded;
                RefreshRenderedText();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (string.IsNullOrEmpty(sourceText) && !string.IsNullOrEmpty(base.text))
                sourceText = base.text;
            _document = NovelTextMarkup.Parse(sourceText);
            supportRichText = true;
            EnsureFont();
            RefreshRenderedText();
        }

        protected override void OnEnable()
        {
            EnsureFont();
            base.OnEnable();
            _document = NovelTextMarkup.Parse(sourceText);
            RefreshRenderedText();
        }

        protected override void OnDisable()
        {
            ClearFontOverlays();
            base.OnDisable();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            _fontOverlaysDirty = true;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            visibleCharacterLimit = visibleCharacterLimit < 0
                ? int.MaxValue
                : visibleCharacterLimit;
            _document = NovelTextMarkup.Parse(sourceText);
            EnsureFont();
            RefreshRenderedText();
            base.OnValidate();
        }
#endif

        public void SetText(string value) => text = value;

        public void SetFontAssets(Font defaultFont, IEnumerable<Font> fonts)
        {
            font = defaultFont != null ? defaultFont : font;
            _fontAssets.Clear();
            if (fonts != null)
            {
                foreach (Font candidate in fonts)
                {
                    if (candidate != null && !_fontAssets.ContainsKey(candidate.name))
                        _fontAssets.Add(candidate.name, candidate);
                }
            }
            if (font != null && !_fontAssets.ContainsKey(font.name))
                _fontAssets.Add(font.name, font);
            RefreshRenderedText();
        }

        internal void ClearTransientFontOverlays()
        {
            ClearFontOverlays();
            _fontOverlaysDirty = true;
        }

        public void ForceMeshUpdate()
        {
            SetVerticesDirty();
            SetLayoutDirty();
            Canvas.ForceUpdateCanvases();
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            _buildingMesh = true;
            try
            {
                base.OnPopulateMesh(toFill);
            }
            finally
            {
                _buildingMesh = false;
            }
        }

        public Vector2 GetPreferredValues(string value, float width, float height)
        {
            EnsureFont();
            string rendered = NovelTextMarkup.Parse(value).BuildRichText(
                int.MaxValue,
                Mathf.Max(1, base.fontSize));
            TextGenerationSettings settings = GetGenerationSettings(
                new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height)));
            var generator = new TextGenerator(rendered.Length);
            float preferredWidth = generator.GetPreferredWidth(rendered, settings) /
                                   pixelsPerUnit;
            float preferredHeight = generator.GetPreferredHeight(rendered, settings) /
                                    pixelsPerUnit;
            return new Vector2(preferredWidth, preferredHeight);
        }

        internal NovelTextEffect EffectAt(int characterIndex)
        {
            return characterIndex >= 0 && characterIndex < _document.Characters.Count
                ? _document.Characters[characterIndex].Effect
                : NovelTextEffect.None;
        }

        internal char CharacterAt(int characterIndex)
        {
            return characterIndex >= 0 && characterIndex < _document.Characters.Count
                ? _document.Characters[characterIndex].Character
                : '\0';
        }

        private void RefreshRenderedText()
        {
            if (_document.Source != (sourceText ?? string.Empty))
                _document = NovelTextMarkup.Parse(sourceText);

            supportRichText = true;
            base.text = _document.BuildRichText(
                visibleCharacterLimit,
                Mathf.Max(1, base.fontSize),
                HasFontOverride);
            _fontOverlaysDirty = true;
            SetVerticesDirty();
            SetLayoutDirty();
        }

        private void LateUpdate()
        {
            if (_fontOverlaysDirty)
                RebuildFontOverlays();

            float time = Time.unscaledTime;
            float shakeFrame = Mathf.Floor(time * 24f);
            foreach (FontOverlay overlay in _fontOverlays)
            {
                if (overlay?.RectTransform == null) continue;
                Vector2 offset = Vector2.zero;
                switch (EffectAt(overlay.CharacterIndex))
                {
                    case NovelTextEffect.Wave:
                        offset.y = Mathf.Sin(
                            time * 7f + overlay.CharacterIndex * 0.65f) * 2.5f;
                        break;
                    case NovelTextEffect.Shake:
                        offset.x = (EffectHash(
                            shakeFrame + overlay.CharacterIndex * 17.17f) * 2f - 1f) * 1.5f;
                        offset.y = (EffectHash(
                            shakeFrame * 1.37f + overlay.CharacterIndex * 41.73f) * 2f - 1f) * 1.5f;
                        break;
                }
                overlay.RectTransform.anchoredPosition = overlay.BasePosition + offset;
            }
        }

        private bool HasFontOverride(string fontName)
        {
            return !string.IsNullOrEmpty(fontName) &&
                   _fontAssets.TryGetValue(fontName, out Font selected) &&
                   selected != null && selected != font;
        }

        private void RebuildFontOverlays()
        {
            _fontOverlaysDirty = false;
            ClearFontOverlays();
            if (!isActiveAndEnabled || _fontAssets.Count == 0 || font == null)
                return;

            int count = visibleCharacterLimit < 0 || visibleCharacterLimit == int.MaxValue
                ? _document.Characters.Count
                : Mathf.Min(visibleCharacterLimit, _document.Characters.Count);
            bool hasOverrides = false;
            for (int index = 0; index < count; index++)
            {
                if (HasFontOverride(_document.Characters[index].FontName))
                {
                    hasOverrides = true;
                    break;
                }
            }
            if (!hasOverrides) return;

            TextGenerationSettings settings = GetGenerationSettings(rectTransform.rect.size);
            cachedTextGenerator.PopulateWithErrors(base.text, settings, gameObject);
            IList<UIVertex> vertices = cachedTextGenerator.verts;
            int generatedCharacters = Mathf.Min(count, vertices.Count / 4);
            float units = 1f / pixelsPerUnit;
            for (int index = 0; index < generatedCharacters; index++)
            {
                NovelTextCharacter character = _document.Characters[index];
                if (!HasFontOverride(character.FontName) ||
                    char.IsWhiteSpace(character.Character))
                    continue;

                int vertexIndex = index * 4;
                Vector3 minimum = vertices[vertexIndex].position;
                Vector3 maximum = minimum;
                for (int corner = 1; corner < 4; corner++)
                {
                    Vector3 position = vertices[vertexIndex + corner].position;
                    minimum = Vector3.Min(minimum, position);
                    maximum = Vector3.Max(maximum, position);
                }
                minimum *= units;
                maximum *= units;
                Vector2 size = new Vector2(
                    Mathf.Max(2f, maximum.x - minimum.x + 8f),
                    Mathf.Max(2f, maximum.y - minimum.y + 8f));
                Vector2 center = (minimum + maximum) * 0.5f;

                var glyphObject = new GameObject(
                    "Novelify Font Glyph",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text));
                glyphObject.layer = gameObject.layer;
                RectTransform glyphRect = glyphObject.GetComponent<RectTransform>();
                glyphRect.SetParent(rectTransform, false);
                glyphRect.anchorMin = glyphRect.anchorMax = rectTransform.pivot;
                glyphRect.pivot = new Vector2(0.5f, 0.5f);
                glyphRect.sizeDelta = size;
                glyphRect.anchoredPosition = center;

                Text glyph = glyphObject.GetComponent<Text>();
                glyph.font = _fontAssets[character.FontName];
                glyph.text = character.Character.ToString();
                glyph.fontSize = NovelTextMarkup.ResolveFontSize(
                    character.Size,
                    Mathf.Max(1, base.fontSize));
                glyph.fontStyle = character.Bold
                    ? character.Italic ? FontStyle.BoldAndItalic : FontStyle.Bold
                    : character.Italic ? FontStyle.Italic : FontStyle.Normal;
                glyph.color = ResolveColor(character.Color, color);
                glyph.alignment = TextAnchor.MiddleCenter;
                glyph.horizontalOverflow = HorizontalWrapMode.Overflow;
                glyph.verticalOverflow = VerticalWrapMode.Overflow;
                glyph.raycastTarget = false;

                _fontOverlays.Add(new FontOverlay
                {
                    GameObject = glyphObject,
                    RectTransform = glyphRect,
                    CharacterIndex = index,
                    BasePosition = center
                });
            }
        }

        private void ClearFontOverlays()
        {
            foreach (FontOverlay overlay in _fontOverlays)
            {
                if (overlay?.GameObject == null) continue;
                overlay.GameObject.SetActive(false);
                if (overlay.RectTransform != null)
                    overlay.RectTransform.SetParent(null, false);
                if (Application.isPlaying) Destroy(overlay.GameObject);
                else DestroyImmediate(overlay.GameObject);
            }
            _fontOverlays.Clear();
        }

        private static Color ResolveColor(string html, Color fallback)
        {
            return !string.IsNullOrWhiteSpace(html) &&
                   ColorUtility.TryParseHtmlString(html, out Color parsed)
                ? parsed
                : fallback;
        }

        private static float EffectHash(float value)
        {
            float sine = Mathf.Sin(value * 12.9898f) * 43758.5453f;
            return sine - Mathf.Floor(sine);
        }

        private void EnsureFont()
        {
            if (font != null)
                return;

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    internal enum NovelTextEffect
    {
        None,
        Wave,
        Shake
    }

    internal readonly struct NovelTextCharacter
    {
        public readonly char Character;
        public readonly bool Bold;
        public readonly bool Italic;
        public readonly string Size;
        public readonly string Color;
        public readonly string FontName;
        public readonly NovelTextEffect Effect;
        public readonly bool SoundCue;

        public NovelTextCharacter(
            char character,
            bool bold,
            bool italic,
            string size,
            string color,
            string fontName,
            NovelTextEffect effect,
            bool soundCue)
        {
            Character = character;
            Bold = bold;
            Italic = italic;
            Size = size;
            Color = color;
            FontName = fontName;
            Effect = effect;
            SoundCue = soundCue;
        }
    }

    internal sealed class NovelTextDocument
    {
        public static readonly NovelTextDocument Empty = new NovelTextDocument(
            string.Empty,
            new List<NovelTextCharacter>());

        public string Source { get; }
        public IReadOnlyList<NovelTextCharacter> Characters { get; }
        public bool HasAnimatedEffects { get; }

        public NovelTextDocument(
            string source,
            IReadOnlyList<NovelTextCharacter> characters)
        {
            Source = source ?? string.Empty;
            Characters = characters ?? Array.Empty<NovelTextCharacter>();
            for (int index = 0; index < Characters.Count; index++)
            {
                if (Characters[index].Effect != NovelTextEffect.None)
                {
                    HasAnimatedEffects = true;
                    break;
                }
            }
        }

        public string BuildRichText(
            int maximumCharacters,
            int baseFontSize,
            Func<string, bool> hideFont = null)
        {
            int count = maximumCharacters < 0 || maximumCharacters == int.MaxValue
                ? Characters.Count
                : Mathf.Clamp(maximumCharacters, 0, Characters.Count);
            if (count == 0)
                return string.Empty;

            var result = new StringBuilder(Source.Length);
            NovelTextCharacter? active = null;
            bool activeHiddenFont = false;
            for (int index = 0; index < count; index++)
            {
                NovelTextCharacter character = Characters[index];
                if (!active.HasValue || !NovelTextMarkup.SameVisualStyle(
                        active.Value,
                        character))
                {
                    if (active.HasValue)
                        NovelTextMarkup.AppendClosingTags(
                            result,
                            active.Value,
                            activeHiddenFont);
                    activeHiddenFont = hideFont?.Invoke(character.FontName) == true;
                    NovelTextMarkup.AppendOpeningTags(
                        result,
                        character,
                        baseFontSize,
                        activeHiddenFont);
                    active = character;
                }
                result.Append(character.Character);
            }

            if (active.HasValue)
                NovelTextMarkup.AppendClosingTags(
                    result,
                    active.Value,
                    activeHiddenFont);
            return result.ToString();
        }
    }

    internal static class NovelTextMarkup
    {
        private const string WaveLink = "novelify-wave";
        private const string ShakeLink = "novelify-shake";
        private const string SoundLink = "novelify-sound";

        public static NovelTextDocument Parse(string markup)
        {
            markup ??= string.Empty;
            var characters = new List<NovelTextCharacter>(markup.Length);
            int boldDepth = 0;
            int italicDepth = 0;
            int soundDepth = 0;
            var sizes = new Stack<string>();
            var colors = new Stack<string>();
            var fonts = new Stack<string>();
            var effects = new Stack<NovelTextEffect>();

            for (int index = 0; index < markup.Length; index++)
            {
                if (markup[index] == '<')
                {
                    int end = markup.IndexOf('>', index + 1);
                    if (end >= 0)
                    {
                        string tag = markup.Substring(index + 1, end - index - 1).Trim();
                        if (ConsumeTag(
                                tag,
                                ref boldDepth,
                                ref italicDepth,
                                sizes,
                                colors,
                                fonts,
                                effects,
                                ref soundDepth))
                        {
                            index = end;
                            continue;
                        }
                    }
                }

                characters.Add(new NovelTextCharacter(
                    markup[index],
                    boldDepth > 0,
                    italicDepth > 0,
                    sizes.Count > 0 ? sizes.Peek() : null,
                    colors.Count > 0 ? colors.Peek() : null,
                    fonts.Count > 0 ? fonts.Peek() : null,
                    effects.Count > 0 ? effects.Peek() : NovelTextEffect.None,
                    soundDepth > 0));
            }

            return new NovelTextDocument(markup, characters);
        }

        internal static bool SameVisualStyle(
            NovelTextCharacter left,
            NovelTextCharacter right)
        {
            return left.Bold == right.Bold &&
                   left.Italic == right.Italic &&
                   string.Equals(left.Size, right.Size, StringComparison.Ordinal) &&
                   string.Equals(left.Color, right.Color, StringComparison.Ordinal) &&
                   string.Equals(left.FontName, right.FontName, StringComparison.Ordinal);
        }

        internal static void AppendOpeningTags(
            StringBuilder result,
            NovelTextCharacter style,
            int baseFontSize,
            bool hiddenFont = false)
        {
            if (style.Bold) result.Append("<b>");
            if (style.Italic) result.Append("<i>");
            if (!string.IsNullOrWhiteSpace(style.Size))
            {
                result.Append("<size=")
                    .Append(ResolveFontSize(style.Size, baseFontSize))
                    .Append('>');
            }
            if (hiddenFont)
                result.Append("<color=#00000000>");
            else if (!string.IsNullOrWhiteSpace(style.Color))
                result.Append("<color=").Append(style.Color).Append('>');
        }

        internal static void AppendClosingTags(
            StringBuilder result,
            NovelTextCharacter style,
            bool hiddenFont = false)
        {
            if (hiddenFont || !string.IsNullOrWhiteSpace(style.Color))
                result.Append("</color>");
            if (!string.IsNullOrWhiteSpace(style.Size)) result.Append("</size>");
            if (style.Italic) result.Append("</i>");
            if (style.Bold) result.Append("</b>");
        }

        private static bool ConsumeTag(
            string tag,
            ref int boldDepth,
            ref int italicDepth,
            Stack<string> sizes,
            Stack<string> colors,
            Stack<string> fonts,
            Stack<NovelTextEffect> effects,
            ref int soundDepth)
        {
            if (tag.Equals("b", StringComparison.OrdinalIgnoreCase))
            {
                boldDepth++;
                return true;
            }
            if (tag.Equals("/b", StringComparison.OrdinalIgnoreCase))
            {
                boldDepth = Mathf.Max(0, boldDepth - 1);
                return true;
            }
            if (tag.Equals("i", StringComparison.OrdinalIgnoreCase))
            {
                italicDepth++;
                return true;
            }
            if (tag.Equals("/i", StringComparison.OrdinalIgnoreCase))
            {
                italicDepth = Mathf.Max(0, italicDepth - 1);
                return true;
            }
            if (PushValue(tag, "size=", sizes) ||
                PushValue(tag, "color=", colors) ||
                PushValue(tag, "font=", fonts))
                return true;
            if (PopValue(tag, "/size", sizes) ||
                PopValue(tag, "/color", colors) ||
                PopValue(tag, "/font", fonts))
                return true;
            if (tag.StartsWith("link=", StringComparison.OrdinalIgnoreCase))
            {
                string value = CleanValue(tag.Substring("link=".Length));
                if (value.Equals(SoundLink, StringComparison.OrdinalIgnoreCase))
                {
                    soundDepth++;
                    return true;
                }
                if (value.Equals(WaveLink, StringComparison.OrdinalIgnoreCase) ||
                    value.Equals(ShakeLink, StringComparison.OrdinalIgnoreCase))
                {
                    effects.Push(value.Equals(WaveLink, StringComparison.OrdinalIgnoreCase)
                        ? NovelTextEffect.Wave
                        : NovelTextEffect.Shake);
                    return true;
                }
            }
            if (tag.Equals("/link", StringComparison.OrdinalIgnoreCase) &&
                (soundDepth > 0 || effects.Count > 0))
            {
                if (soundDepth > 0) soundDepth--;
                else effects.Pop();
                return true;
            }
            return false;
        }

        private static bool PushValue(string tag, string prefix, Stack<string> values)
        {
            if (!tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
            values.Push(CleanValue(tag.Substring(prefix.Length)));
            return true;
        }

        private static bool PopValue(string tag, string closingTag, Stack<string> values)
        {
            if (!tag.Equals(closingTag, StringComparison.OrdinalIgnoreCase))
                return false;
            if (values.Count > 0) values.Pop();
            return true;
        }

        private static string CleanValue(string value) =>
            (value ?? string.Empty).Trim().Trim('"', '\'');

        internal static int ResolveFontSize(string value, int baseFontSize)
        {
            value = CleanValue(value);
            if (value.EndsWith("%", StringComparison.Ordinal) &&
                float.TryParse(
                    value.Substring(0, value.Length - 1),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float percent))
            {
                return Mathf.Max(1, Mathf.RoundToInt(baseFontSize * percent / 100f));
            }
            return int.TryParse(value, out int absolute)
                ? Mathf.Max(1, absolute)
                : Mathf.Max(1, baseFontSize);
        }
    }
}
