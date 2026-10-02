using UnityEngine;

namespace Novelify
{
    public partial class CharacterInfo
    {
        public bool IsAnimating { get; private set; }
        private NovelCharacterAnimation _simpleAnimation;
        private float _animationAmplitude, _animationFrequency, _animationDuration, _animationElapsed;
        private Vector2 _animationPositionOffset;
        private float _animationRotationOffset;
        private Vector2 _animationBasePosition;
        private float _animationBaseRotation;

        /// <summary>Starts a visual effect over the authored transform. Zero duration runs until stopped.</summary>
        public void StartSimpleAnimation(NovelCharacterAnimation animation, float amplitude,
            float frequency, float duration = 0f)
        {
            StopSimpleAnimation();
            if (!isActiveAndEnabled || !Finite(amplitude) || !Finite(frequency) ||
                !Finite(duration) || amplitude <= 0f || frequency <= 0f)
                return;
            _simpleAnimation = animation;
            _animationAmplitude = amplitude;
            _animationFrequency = frequency;
            _animationDuration = Mathf.Max(0f, duration);
            _animationElapsed = 0f;
            _animationBasePosition = Position;
            _animationBaseRotation = Rotation;
            IsAnimating = true;
        }

        public void StopSimpleAnimation()
        {
            if (!IsAnimating && _animationPositionOffset == Vector2.zero && _animationRotationOffset == 0f) return;
            Vector2 authoredPosition = Position;
            float authoredRotation = Rotation;
            IsAnimating = false;
            _animationPositionOffset = Vector2.zero;
            _animationRotationOffset = 0f;
            Position = authoredPosition;
            Rotation = authoredRotation;
        }

        private void LateUpdate()
        {
            if (!IsAnimating) return;
            _animationElapsed += TimeMode == DialogueTimeMode.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_animationDuration > 0f && _animationElapsed >= _animationDuration)
            {
                StopSimpleAnimation();
                return;
            }
            Vector2 authoredPosition = Position;
            float authoredRotation = Rotation;
            float phase = _animationElapsed * _animationFrequency;
            _animationPositionOffset = Vector2.zero;
            _animationRotationOffset = 0f;
            switch (_simpleAnimation)
            {
                case NovelCharacterAnimation.Bounce:
                    _animationPositionOffset.y = Mathf.Abs(Mathf.Sin(phase * Mathf.PI)) * _animationAmplitude;
                    break;
                case NovelCharacterAnimation.Shake:
                    _animationPositionOffset = new Vector2(
                        Mathf.PerlinNoise(phase, 13.7f) * 2f - 1f,
                        Mathf.PerlinNoise(phase, 47.3f) * 2f - 1f) * _animationAmplitude;
                    break;
                case NovelCharacterAnimation.Sway:
                    _animationRotationOffset = Mathf.Sin(phase * Mathf.PI * 2f) * _animationAmplitude;
                    break;
            }
            Position = authoredPosition;
            Rotation = authoredRotation;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
