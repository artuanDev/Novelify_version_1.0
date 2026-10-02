using UnityEngine;
using System;
using System.Collections;

namespace Novelify
{
    public class NovelifyUtilities : MonoBehaviour
    {
        public static IEnumerator ShowTextLetterByLetter(
            string text, NovelText textDisplay,
            AudioClip talkSound, AudioSource talkSoundSource,
            float minPitchVariation = 0, float maxPitchVariation = 0,
            float charactersPerSecond = 30f,
            Action<char> onCharacterShown = null,
            DialogueTimeMode timeMode = DialogueTimeMode.Unscaled,
            Action<int, char> onCharacterShownAtIndex = null)
        {
            text ??= string.Empty;
            textDisplay.richText = true;
            textDisplay.SetText(text);
            textDisplay.maxVisibleCharacters = 0;
            int visibleCharacterCount = textDisplay.visibleCharacterCount;
            float characterDelay = 1f / Mathf.Max(1f, charactersPerSecond);
            float talkSoundCooldown = 0f;

            for (int characterIndex = 0;
                 characterIndex < visibleCharacterCount;
                 characterIndex++)
            {
                // NovelText's parsed character data excludes formatting/control
                // tags, so tags remain hidden throughout the reveal.
                char letter = textDisplay.CharacterAt(characterIndex);

                if (talkSoundCooldown <= 0f &&
                    talkSoundSource != null &&
                    talkSound != null &&
                    char.IsLetterOrDigit(letter))
                {
                    float minimumPitch = Mathf.Min(minPitchVariation, maxPitchVariation);
                    float maximumPitch = Mathf.Max(minPitchVariation, maxPitchVariation);
                    talkSoundSource.clip = talkSound;
                    talkSoundSource.pitch = 1f + UnityEngine.Random.Range(minimumPitch, maximumPitch);
                    talkSoundSource.Play();
                    talkSoundCooldown = UnityEngine.Random.Range(0.055f, 0.085f);
                }

                textDisplay.maxVisibleCharacters = characterIndex + 1;
                onCharacterShown?.Invoke(letter);
                onCharacterShownAtIndex?.Invoke(characterIndex, letter);

                if (characterIndex == visibleCharacterCount - 1)
                {
                    continue;
                }

                float delayMultiplier = GetDelayMultiplier(letter);
                float delay = characterDelay * delayMultiplier;
                float remaining = delay;
                while (remaining > 0f)
                {
                    yield return null;
                    remaining -= timeMode == DialogueTimeMode.Unscaled
                        ? Time.unscaledDeltaTime
                        : Time.deltaTime;
                }
                talkSoundCooldown -= delay;
            }

            textDisplay.maxVisibleCharacters = int.MaxValue;
        }

        private static float GetDelayMultiplier(char character)
        {
            if (character == '.' || character == '!' || character == '?')
            {
                return 4f;
            }

            if (character == ',' || character == ';' || character == ':')
            {
                return 2.25f;
            }

            return char.IsWhiteSpace(character) ? 0.45f : 1f;
        }
    }
}
