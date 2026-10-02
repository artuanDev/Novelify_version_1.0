using System;
using System.Collections.Generic;
using UnityEngine;

namespace Novelify
{
    [Serializable]
    public sealed class NovelLocalizedText
    {
        public string Locale;
        [TextArea(2, 8)] public string Text;
        [Tooltip("This entry was copied from the source markup and still needs translation review.")]
        public bool NeedsReview;
    }

    [Serializable]
    public sealed class NovelLocalizedLine
    {
        public string Key;
        [TextArea(2, 8)] public string SourceText;
        public List<NovelLocalizedText> Translations = new List<NovelLocalizedText>();
    }

    /// <summary>Translations indexed by stable graph and node identities.</summary>
    [CreateAssetMenu(menuName = "Novelify/Localization Table", fileName = "NovelLocalization")]
    public sealed class NovelLocalizationTable : ScriptableObject
    {
        [Tooltip("Story graph this table is authored for. The workspace opens this graph by default.")]
        public RuntimeNovelGraph SourceGraph;
        public string SourceLocale = "en";
        public List<NovelLocalizedLine> Lines = new List<NovelLocalizedLine>();

        private Dictionary<string, NovelLocalizedLine> _lookup;

        public string Resolve(string key, string locale, string fallback)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrWhiteSpace(locale) ||
                string.Equals(locale, SourceLocale, StringComparison.OrdinalIgnoreCase))
                return fallback ?? string.Empty;

            EnsureLookup();
            if (!_lookup.TryGetValue(key, out NovelLocalizedLine line) || line.Translations == null)
                return fallback ?? string.Empty;

            foreach (NovelLocalizedText translation in line.Translations)
                if (translation != null &&
                    string.Equals(translation.Locale, locale, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(translation.Text))
                    return translation.Text;

            return fallback ?? string.Empty;
        }

        public void Invalidate() => _lookup = null;

        private void OnEnable() => Invalidate();

#if UNITY_EDITOR
        private void OnValidate() => Invalidate();
#endif

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, NovelLocalizedLine>(StringComparer.Ordinal);
            if (Lines == null) return;
            foreach (NovelLocalizedLine line in Lines)
                if (line != null && !string.IsNullOrEmpty(line.Key))
                    _lookup.TryAdd(line.Key, line);
        }
    }

    public static class NovelLocalizationKey
    {
        public static string Dialogue(string graphID, string nodeID) =>
            (graphID ?? string.Empty) + ":" + (nodeID ?? string.Empty);

        public static string Choice(string lineID, string choiceID) =>
            (lineID ?? string.Empty) + ":choice:" + (choiceID ?? string.Empty);

        public static string DisabledReason(string lineID, string choiceID) =>
            (lineID ?? string.Empty) + ":reason:" + (choiceID ?? string.Empty);
    }
}
