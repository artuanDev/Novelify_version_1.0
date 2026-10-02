#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Novelify
{
    public partial class CharacterInfo
    {
        /// <summary>Samples the actual runtime effect on a disposable editor preview portrait.</summary>
        public void SampleEditorAnimation(NovelCharacterAnimation animation, float amplitude,
            float frequency, float duration, float seconds)
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(gameObject))
                throw new InvalidOperationException("Animation sampling requires an isolated preview scene.");
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) seconds = 0f;
            StartSimpleAnimation(animation, amplitude, frequency, duration);
            // LateUpdate adds the engine delta. Compensate so scrubbing evaluates precisely
            // the requested timestamp, using the existing runtime implementation unchanged.
            _animationElapsed = Mathf.Max(0f, seconds) -
                (TimeMode == DialogueTimeMode.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime);
            LateUpdate();
        }
    }
}
#endif
