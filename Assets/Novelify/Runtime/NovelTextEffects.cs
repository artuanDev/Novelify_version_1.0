using UnityEngine;
using UnityEngine.UI;

namespace Novelify
{
    /// <summary>Animates dialogue ranges marked with Novelify wave/shake tags.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NovelText))]
    public sealed class NovelTextEffects : BaseMeshEffect
    {
        [SerializeField, Min(0f)] private float waveHeight = 2.5f;
        [SerializeField, Min(0f)] private float waveSpeed = 7f;
        [SerializeField, Min(0f)] private float shakeStrength = 1.5f;
        [SerializeField, Min(1f)] private float shakeFrequency = 24f;

        private NovelText _text;

        protected override void Awake()
        {
            base.Awake();
            _text = GetComponent<NovelText>();
        }

        private void LateUpdate()
        {
            if (_text != null && _text.hasAnimatedEffects)
                graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive()) return;
            _text ??= GetComponent<NovelText>();
            if (_text == null || !_text.hasAnimatedEffects) return;

            int characterCount = Mathf.Min(
                _text.visibleCharacterCount,
                vertices.currentVertCount / 4);
            int visibleLimit = _text.maxVisibleCharacters < 0
                ? characterCount
                : Mathf.Min(_text.maxVisibleCharacters, characterCount);
            var vertex = new UIVertex();
            for (int characterIndex = 0;
                 characterIndex < visibleLimit;
                 characterIndex++)
            {
                NovelTextEffect effect = _text.EffectAt(characterIndex);
                if (effect == NovelTextEffect.None ||
                    char.IsWhiteSpace(_text.CharacterAt(characterIndex)))
                    continue;

                Vector3 offset = effect == NovelTextEffect.Wave
                    ? GetWaveOffset(characterIndex)
                    : GetShakeOffset(characterIndex);
                int firstVertex = characterIndex * 4;
                for (int vertexIndex = firstVertex;
                     vertexIndex < firstVertex + 4;
                     vertexIndex++)
                {
                    vertices.PopulateUIVertex(ref vertex, vertexIndex);
                    vertex.position += offset;
                    vertices.SetUIVertex(vertex, vertexIndex);
                }
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
