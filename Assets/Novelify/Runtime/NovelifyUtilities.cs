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
            Action<char> onCharacterShown = null)
        {
            text ??= string.Empty;
            textDisplay.SetText(string.Empty);
            string currentTextShown = string.Empty;

            foreach (char letter in text)
            {
                yield return new WaitForSeconds(0.14f);

                if (talkSoundSource != null && talkSound != null && !char.IsWhiteSpace(letter))
                {
                    float minimumPitch = Mathf.Min(minPitchVariation, maxPitchVariation);
                    float maximumPitch = Mathf.Max(minPitchVariation, maxPitchVariation);
                    talkSoundSource.clip = talkSound;
                    talkSoundSource.pitch = 1f + UnityEngine.Random.Range(minimumPitch, maximumPitch);
                    talkSoundSource.Play();
                }

                currentTextShown += letter;
                textDisplay.SetText(currentTextShown);
                onCharacterShown?.Invoke(letter);
            }
        }
    }
}
