using UnityEngine;
using System;
using System.Collections;
using TMPro;

namespace Novelify
{
    public class NovelifyUtilities : MonoBehaviour
    {
        public static IEnumerator ShowTextLetterByLetter(
            string text, TextMeshProUGUI textDisplay,
            AudioClip talkSound, AudioSource talkSoundSource,
            float minPitchVariation = 0, float maxPitchVariation = 0,
            float charactersPerSecond = 30f,
            Action<char> onCharacterShown = null)
        {
            text ??= string.Empty;
            textDisplay.SetText(string.Empty);
            string currentTextShown = string.Empty;
            float characterDelay = 1f / Mathf.Max(1f, charactersPerSecond);
            float talkSoundCooldown = 0f;

            for (int characterIndex = 0; characterIndex < text.Length; characterIndex++)
            {
                char letter = text[characterIndex];

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

                currentTextShown += letter;
                textDisplay.SetText(currentTextShown);
                onCharacterShown?.Invoke(letter);

                if (characterIndex == text.Length - 1)
                {
                    continue;
                }

                float delayMultiplier = GetDelayMultiplier(letter);
                float delay = characterDelay * delayMultiplier;
                yield return new WaitForSeconds(delay);
                talkSoundCooldown -= delay;
            }
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
