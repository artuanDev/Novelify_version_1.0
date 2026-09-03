using UnityEngine;
using System;
using System.Collections;
using TMPro;

namespace Novelify
{
    public class NovelifyUtilities : MonoBehaviour
    {
        public static IEnumerator ShowTextLetterByLetter(string _text, TextMeshProUGUI _texDisplay)
        {
            print("Intento funcionar");
            string current_text_shown = "";

            foreach (char letter in _text)
            {
                yield return new WaitForSeconds(0.03f);

                current_text_shown += letter;
                _texDisplay.text = current_text_shown;
            }

            yield return null;
        }
    }
}
