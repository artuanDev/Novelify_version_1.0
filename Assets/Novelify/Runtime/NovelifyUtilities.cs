using UnityEngine;
using System;
using System.Collections;
using TMPro;

namespace Novelify
{
    public class NovelifyUtilities : MonoBehaviour
    {
        public static IEnumerator ShowTextLetterByLetter(
            string _text, TextMeshProUGUI _texDisplay,
            AudioClip _talkSound, AudioSource _talkSoundSource,
            float minPitchVariation = 0, float maxPitchVariation = 0)
        {
            string current_text_shown = "";

            foreach (char letter in _text)
            {
                yield return new WaitForSeconds(0.14f);

                _talkSoundSource.clip = _talkSound;
                float pitchVariation = UnityEngine.Random.Range(minPitchVariation, maxPitchVariation);
                _talkSoundSource.pitch += pitchVariation;
                _talkSoundSource.Play();

                current_text_shown += letter;
                _texDisplay.text = current_text_shown;
            }

            yield return null;
        }
    }
}
