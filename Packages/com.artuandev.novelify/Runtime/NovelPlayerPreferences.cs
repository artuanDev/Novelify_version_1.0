using UnityEngine;

namespace Novelify
{
    /// <summary>Small local preference set used by the default player controller.</summary>
    public sealed class NovelPlayerPreferences
    {
        private const string Prefix = "Novelify.Player.";

        public float TextSpeedMultiplier = 1f;
        public float AutoDelay = 2f;
        public float TalkVolume = 1f;
        public bool SkipReadOnly;

        public void Clamp()
        {
            if (float.IsNaN(TextSpeedMultiplier) || float.IsInfinity(TextSpeedMultiplier)) TextSpeedMultiplier = 1f;
            if (float.IsNaN(AutoDelay) || float.IsInfinity(AutoDelay)) AutoDelay = 2f;
            if (float.IsNaN(TalkVolume) || float.IsInfinity(TalkVolume)) TalkVolume = 1f;
            TextSpeedMultiplier = Mathf.Clamp(TextSpeedMultiplier, 0.25f, 4f);
            AutoDelay = Mathf.Clamp(AutoDelay, 0.25f, 10f);
            TalkVolume = Mathf.Clamp01(TalkVolume);
        }

        public static NovelPlayerPreferences Load()
        {
            var result = new NovelPlayerPreferences
            {
                TextSpeedMultiplier = PlayerPrefs.GetFloat(Prefix + "TextSpeed", 1f),
                AutoDelay = PlayerPrefs.GetFloat(Prefix + "AutoDelay", 2f),
                TalkVolume = PlayerPrefs.GetFloat(Prefix + "TalkVolume", 1f),
                // V2 intentionally defaults to all text. The original preference
                // silently disabled Skip on the first unread starter line.
                SkipReadOnly = PlayerPrefs.GetInt(Prefix + "SkipReadOnlyV2", 0) != 0
            };
            result.Clamp();
            return result;
        }

        public void Save()
        {
            Clamp();
            PlayerPrefs.SetFloat(Prefix + "TextSpeed", TextSpeedMultiplier);
            PlayerPrefs.SetFloat(Prefix + "AutoDelay", AutoDelay);
            PlayerPrefs.SetFloat(Prefix + "TalkVolume", TalkVolume);
            PlayerPrefs.SetInt(Prefix + "SkipReadOnlyV2", SkipReadOnly ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
