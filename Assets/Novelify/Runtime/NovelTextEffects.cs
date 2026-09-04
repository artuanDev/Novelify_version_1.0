using System;
using TMPro;
using UnityEngine;

namespace Novelify
{
    /// <summary>
    /// Animates character ranges marked by the Novelify dialogue editor. Effects
    /// use TMP link ranges so the control tags remain invisible at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class NovelTextEffects : MonoBehaviour
    {
        private const string WaveEffect = "novelify-wave";
        private const string ShakeEffect = "novelify-shake";

        [SerializeField, Min(0f)] private float waveHeight = 2.5f;
        [SerializeField, Min(0f)] private float waveSpeed = 7f;
        [SerializeField, Min(0f)] private float shakeStrength = 1.5f;
        [SerializeField, Min(1f)] private float shakeFrequency = 24f;

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void LateUpdate()
        {
            if (_text == null ||
                string.IsNullOrEmpty(_text.text) ||
                _text.text.IndexOf("novelify-", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            // Rebuild the unmodified geometry before applying this frame's offsets.
            _text.ForceMeshUpdate();
            TMP_TextInfo textInfo = _text.textInfo;
            bool verticesChanged = false;

            for (int linkIndex = 0; linkIndex < textInfo.linkCount; linkIndex++)
            {
                TMP_LinkInfo link = textInfo.linkInfo[linkIndex];
                string effect = link.GetLinkID();
                bool isWave = effect.Equals(
                    WaveEffect,
                    StringComparison.OrdinalIgnoreCase);
                bool isShake = effect.Equals(
                    ShakeEffect,
                    StringComparison.OrdinalIgnoreCase);
                if (!isWave && !isShake)
                {
                    continue;
                }

                int firstCharacter = link.linkTextfirstCharacterIndex;
                int lastCharacter = Mathf.Min(
                    firstCharacter + link.linkTextLength,
                    textInfo.characterCount);
                for (int characterIndex = firstCharacter;
                     characterIndex < lastCharacter;
                     characterIndex++)
                {
                    TMP_CharacterInfo character = textInfo.characterInfo[characterIndex];
                    if (!character.isVisible ||
                        characterIndex >= _text.maxVisibleCharacters)
                    {
                        continue;
                    }

                    Vector3 offset = isWave
                        ? GetWaveOffset(characterIndex)
                        : GetShakeOffset(characterIndex);
                    Vector3[] vertices = textInfo.meshInfo[character.materialReferenceIndex].vertices;
                    int vertexIndex = character.vertexIndex;
                    vertices[vertexIndex] += offset;
                    vertices[vertexIndex + 1] += offset;
                    vertices[vertexIndex + 2] += offset;
                    vertices[vertexIndex + 3] += offset;
                    verticesChanged = true;
                }
            }

            if (verticesChanged)
            {
                _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            }
        }

        private Vector3 GetWaveOffset(int characterIndex)
        {
            float y = Mathf.Sin(
                Time.unscaledTime * waveSpeed + characterIndex * 0.65f) *
                waveHeight;
            return new Vector3(0f, y, 0f);
        }

        private Vector3 GetShakeOffset(int characterIndex)
        {
            float frame = Mathf.Floor(Time.unscaledTime * shakeFrequency);
            float x = Hash(frame + characterIndex * 17.17f) * 2f - 1f;
            float y = Hash(frame * 1.37f + characterIndex * 41.73f) * 2f - 1f;
            return new Vector3(x, y, 0f) * shakeStrength;
        }

        private static float Hash(float value)
        {
            float sine = Mathf.Sin(value * 12.9898f) * 43758.5453f;
            return sine - Mathf.Floor(sine);
        }
    }
}
