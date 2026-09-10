using UnityEngine;
using UnityEngine.UI;

namespace Novelify
{
    public static class PortraitTweenEasingUtility
    {
        public static float Evaluate(PortraitTweenEasing easing, AnimationCurve customCurve, float progress)
        {
            float t = Mathf.Clamp01(progress);
            switch (easing)
            {
                case PortraitTweenEasing.EaseIn: return t * t * t;
                case PortraitTweenEasing.EaseOut: return 1f - Mathf.Pow(1f - t, 3f);
                case PortraitTweenEasing.EaseInOut: return t * t * (3f - 2f * t);
                case PortraitTweenEasing.Anticipation: return t * t * (2.70158f * t - 1.70158f);
                case PortraitTweenEasing.Overshoot:
                {
                    float shifted = t - 1f;
                    return 1f + 2.70158f * shifted * shifted * shifted + 1.70158f * shifted * shifted;
                }
                case PortraitTweenEasing.Bounce: return BounceOut(t);
                case PortraitTweenEasing.Custom:
                    return customCurve != null && customCurve.length > 0
                        ? Mathf.Clamp01(customCurve.Evaluate(t))
                        : t;
                default: return t;
            }
        }

        private static float BounceOut(float t)
        {
            const float scale = 7.5625f;
            const float divisor = 2.75f;
            if (t < 1f / divisor) return scale * t * t;
            if (t < 2f / divisor)
            {
                t -= 1.5f / divisor;
                return scale * t * t + 0.75f;
            }
            if (t < 2.5f / divisor)
            {
                t -= 2.25f / divisor;
                return scale * t * t + 0.9375f;
            }
            t -= 2.625f / divisor;
            return scale * t * t + 0.984375f;
        }
    }

    public class CharacterInfo : MonoBehaviour
    {
        [System.NonSerialized] public DialogueTimeMode TimeMode = DialogueTimeMode.Unscaled;
        public NovelCharacter character;
        [Tooltip("Leave empty for the default instance; use a unique ID for additional copies of this character.")]
        public string InstanceID;
        public Image Body;
        public Image Eyes;
        public Image Details;
        public Image Mouth;

        public CharacterEmotion Emotion { get; private set; }
        public bool IsMoving { get; private set; }
        public CharacterPortrait Portrait { get; private set; }

        private Vector2 _moveStart, _moveTarget;
        private Vector2 _scaleStart, _scaleTarget;
        private float _rotationStart, _rotationTarget;
        private float _opacityStart, _opacityTarget = 1f;
        private float _moveElapsed, _moveDuration;
        private PortraitTweenEasing _moveEasing;
        private AnimationCurve _moveCustomCurve;
        private bool _tweenTransform, _tweenOpacity, _deactivateAfterTween;
        private Vector2 _positionBeforeHide;
        private CanvasGroup _canvasGroup;
        private bool _speaking, _animateMouth, _animateBlinking = true, _speechPause;
        private bool _eyesClosed;
        private float _nextMouthFrame, _nextBlink;

        public Vector2 Position
        {
            get => transform is RectTransform rect ? rect.anchoredPosition : (Vector2)transform.localPosition;
            set
            {
                if (transform is RectTransform rect) rect.anchoredPosition = value;
                else transform.localPosition = new Vector3(value.x, value.y, transform.localPosition.z);
            }
        }

        public float Rotation
        {
            get => transform.localEulerAngles.z;
            set => transform.localRotation = Quaternion.Euler(0f, 0f, value);
        }

        public Vector2 Scale
        {
            get => new Vector2(transform.localScale.x, transform.localScale.y);
            set => transform.localScale = new Vector3(value.x, value.y, transform.localScale.z);
        }

        public float Opacity
        {
            get => ResolveCanvasGroup().alpha;
            set => ResolveCanvasGroup().alpha = Mathf.Clamp01(value);
        }

        private void Awake() => ResolveLayers();

        private void Start()
        {
            if (character != null) SetEmotion(Emotion);
        }

        public void Initialize(NovelCharacter definition, string instanceID = "")
        {
            character = definition;
            InstanceID = instanceID ?? string.Empty;
            ResolveLayers();
            SetEmotion(CharacterEmotion.Neutral);
        }

        private void ResolveLayers()
        {
            // Named fallback keeps existing portrait prefabs working. Custom prefabs can assign references.
            foreach (Image layer in GetComponentsInChildren<Image>(true))
            {
                switch (layer.name)
                {
                    case "PortraitBackground": if (Body == null) Body = layer; break;
                    case "PortraitEyes": if (Eyes == null) Eyes = layer; break;
                    case "PortraitEyesDetails": if (Details == null) Details = layer; break;
                    case "PortraitMouth": if (Mouth == null) Mouth = layer; break;
                }
                layer.raycastTarget = false;
            }
        }

        private CanvasGroup ResolveCanvasGroup()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
            return _canvasGroup;
        }

        public void SetEmotion(CharacterEmotion emotion)
        {
            Emotion = emotion;
            if (character == null) return;
            Portrait = character.GetPortrait(emotion);
            SetLayer(Body, Portrait.Body);
            SetLayer(Eyes, Portrait.Eyes);
            SetLayer(Details, Portrait.Details);
            SetLayer(Mouth, Portrait.Mouth);
            _eyesClosed = false;
            ScheduleBlink();
        }

        public void BeginDialogue(RuntimeDialogueNode node)
        {
            SetEmotion(node.Emotion);
            _animateMouth = node.AnimateMouth;
            _animateBlinking = node.AnimateBlinking;
            _speaking = !node.ShowTextImmediately;
            _speechPause = true;
            _nextMouthFrame = 0f;
        }

        public void RevealLetter(char letter)
        {
            _speechPause = !char.IsLetterOrDigit(letter);
        }

        public void StopSpeaking()
        {
            _speaking = false;
            SetLayer(Mouth, Portrait.Mouth);
        }

        public void MoveTo(Vector2 target, bool smooth, float duration, bool easeInOut = true)
        {
            TransformTo(target, Rotation, Scale, smooth, duration, easeInOut);
        }

        public Vector2 NormalizedToAnchoredPosition(Vector2 normalizedPosition, float margin)
        {
            normalizedPosition.x = Mathf.Clamp(normalizedPosition.x, -1f, 1f);
            normalizedPosition.y = Mathf.Clamp(normalizedPosition.y, -1f, 1f);
            return Vector2.Scale(normalizedPosition, GetStageExtent(margin));
        }

        public Vector2 AnchoredToNormalizedPosition(Vector2 anchoredPosition, float margin = 0f)
        {
            Vector2 extent = GetStageExtent(margin);
            return new Vector2(
                extent.x > 0f ? anchoredPosition.x / extent.x : 0f,
                extent.y > 0f ? anchoredPosition.y / extent.y : 0f);
        }

        public Vector2 ClampToStageBounds(Vector2 position, float margin)
        {
            Vector2 extent = GetStageExtent(margin);
            return new Vector2(
                Mathf.Clamp(position.x, -extent.x, extent.x),
                Mathf.Clamp(position.y, -extent.y, extent.y));
        }

        private Vector2 GetStageExtent(float margin)
        {
            margin = Mathf.Max(0f, margin);

            Vector2 stageSize = Vector2.zero;
            if (transform.parent is RectTransform parentRect)
                stageSize = parentRect.rect.size;

            if (stageSize.x <= 0f || stageSize.y <= 0f)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                float scaleFactor = canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
                stageSize = new Vector2(Screen.width / scaleFactor, Screen.height / scaleFactor);
            }

            return stageSize * 0.5f + Vector2.one * margin;
        }

        public void TransformTo(
            Vector2 targetPosition,
            float targetRotation,
            Vector2 targetScale,
            bool smooth,
            float duration,
            bool easeInOut = true)
        {
            TransformTo(
                targetPosition,
                targetRotation,
                targetScale,
                smooth,
                duration,
                easeInOut ? PortraitTweenEasing.EaseInOut : PortraitTweenEasing.None,
                null);
        }

        public void TransformTo(
            Vector2 targetPosition,
            float targetRotation,
            Vector2 targetScale,
            bool smooth,
            float duration,
            PortraitTweenEasing easing,
            AnimationCurve customCurve)
        {
            TransformTo(targetPosition, targetRotation, targetScale, smooth, duration,
                easing, customCurve, false, Opacity);
        }

        public void TransformTo(
            Vector2 targetPosition,
            float targetRotation,
            Vector2 targetScale,
            bool animateTransform,
            float duration,
            PortraitTweenEasing easing,
            AnimationCurve customCurve,
            bool animateOpacity,
            float targetOpacity)
        {
            StopMovement();
            _moveStart = Position;
            _moveTarget = targetPosition;
            _rotationStart = Rotation;
            _rotationTarget = targetRotation;
            _scaleStart = Scale;
            _scaleTarget = targetScale;
            _opacityStart = Opacity;
            _opacityTarget = Mathf.Clamp01(targetOpacity);
            _moveElapsed = 0f;
            _moveDuration = duration;
            _moveEasing = easing;
            _moveCustomCurve = customCurve;
            _tweenTransform = animateTransform;
            _tweenOpacity = animateOpacity;
            bool transformChanged = _moveStart != targetPosition ||
                                    !Mathf.Approximately(Mathf.DeltaAngle(_rotationStart, targetRotation), 0f) ||
                                    _scaleStart != targetScale;
            bool opacityChanged = !Mathf.Approximately(_opacityStart, _opacityTarget);
            bool validDuration = duration > 0f && !float.IsInfinity(duration);
            IsMoving = validDuration &&
                       (animateTransform && transformChanged || animateOpacity && opacityChanged);
            if (!animateTransform || !IsMoving)
            {
                Position = targetPosition;
                Rotation = targetRotation;
                Scale = targetScale;
            }
            if (animateOpacity && !IsMoving) Opacity = _opacityTarget;
        }

        public void TransitionIn(
            CharacterTransitionMode transition,
            CharacterTransitionDirection direction,
            float duration,
            float offset,
            PortraitTweenEasing easing)
        {
            bool slide = transition is CharacterTransitionMode.Slide or CharacterTransitionMode.FadeAndSlide;
            bool fade = transition is CharacterTransitionMode.Fade or CharacterTransitionMode.FadeAndSlide;
            Vector2 target = Position;
            if (slide) Position = target + TransitionOffset(direction, offset);
            if (fade) Opacity = 0f;
            TransformTo(target, Rotation, Scale, slide, duration, easing, null, fade, 1f);
        }

        public void TransitionOut(
            CharacterTransitionMode transition,
            CharacterTransitionDirection direction,
            float duration,
            float offset,
            PortraitTweenEasing easing)
        {
            bool slide = transition is CharacterTransitionMode.Slide or CharacterTransitionMode.FadeAndSlide;
            bool fade = transition is CharacterTransitionMode.Fade or CharacterTransitionMode.FadeAndSlide;
            _positionBeforeHide = Position;
            Vector2 target = slide ? Position + TransitionOffset(direction, offset) : Position;
            TransformTo(target, Rotation, Scale, slide, duration, easing, null, fade, 0f);
            _deactivateAfterTween = true;

            if (!IsMoving) CompleteHide();
        }

        public void HideImmediately()
        {
            bool restorePosition = _deactivateAfterTween;
            Vector2 position = _positionBeforeHide;
            StopMovement();
            if (restorePosition) Position = position;
            Opacity = 1f;
            gameObject.SetActive(false);
        }

        public void PrepareToShow()
        {
            bool restorePosition = _deactivateAfterTween;
            Vector2 position = _positionBeforeHide;
            bool wasHidden = !gameObject.activeSelf;
            if (restorePosition)
            {
                StopMovement();
                Position = position;
            }
            if (wasHidden || restorePosition) Opacity = 1f;
            gameObject.SetActive(true);
        }

        public void StopMovement()
        {
            IsMoving = false;
            _tweenTransform = false;
            _tweenOpacity = false;
            _deactivateAfterTween = false;
        }

        private Vector2 TransitionOffset(CharacterTransitionDirection direction, float transitionExtent)
        {
            Vector2 distance = GetStageExtent(0) * transitionExtent;
            //Vector2 distance = new Vector2(100, 100);
            return direction switch
            {
                CharacterTransitionDirection.Right => new Vector2(distance.x, 0f),
                CharacterTransitionDirection.Up => new Vector2(0f, distance.y),
                CharacterTransitionDirection.Down => new Vector2(0f, -distance.y),
                _ => new Vector2(-distance.x, 0f)
            };
        }

        private void CompleteHide()
        {
            Vector2 restorePosition = _positionBeforeHide;
            _deactivateAfterTween = false;
            IsMoving = false;
            gameObject.SetActive(false);
            Position = restorePosition;
            Opacity = 1f;
        }

        private void Update()
        {
            if (IsMoving)
            {
                _moveElapsed += TimeMode == DialogueTimeMode.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(_moveElapsed / _moveDuration);
                float easedT = PortraitTweenEasingUtility.Evaluate(_moveEasing, _moveCustomCurve, t);
                if (_tweenTransform)
                {
                    Position = Vector2.LerpUnclamped(_moveStart, _moveTarget, easedT);
                    Rotation = _rotationStart + Mathf.DeltaAngle(_rotationStart, _rotationTarget) * easedT;
                    Scale = Vector2.LerpUnclamped(_scaleStart, _scaleTarget, easedT);
                }
                if (_tweenOpacity)
                {
                    Opacity = Mathf.LerpUnclamped(_opacityStart, _opacityTarget, easedT);
                }

                //Function to call when the destination of a movement has been reached.
                if (t >= 1f)
                {
                    if (_tweenTransform)
                    {
                        Position = _moveTarget;
                        Rotation = _rotationTarget;
                        Scale = _scaleTarget;
                    }
                    if (_tweenOpacity) Opacity = _opacityTarget;
                    IsMoving = false;
                    if (_deactivateAfterTween)
                    {
                        CompleteHide();
                        return;
                    }
                }
            }

            if (character == null) return;
            float now = TimeMode == DialogueTimeMode.Unscaled ? Time.unscaledTime : Time.time;
            if (_animateBlinking && Portrait.EyesClosed != null && now >= _nextBlink)
            {
                _eyesClosed = !_eyesClosed;
                SetLayer(Eyes, _eyesClosed ? Portrait.EyesClosed : Portrait.Eyes);
                if (_eyesClosed) _nextBlink = now + Mathf.Max(0.02f, character.BlinkDuration);
                else ScheduleBlink();
            }

            if (_speaking && _animateMouth && Portrait.MouthOpen != null && now >= _nextMouthFrame)
            {
                bool open = !_speechPause && Random.value >= Mathf.Clamp01(character.MouthPauseChance);
                SetLayer(Mouth, open && Mouth != null && Mouth.sprite != Portrait.MouthOpen ? Portrait.MouthOpen : Portrait.Mouth);
                float variation = Mathf.Clamp(character.MouthTimingVariation, 0f, 0.75f);
                _nextMouthFrame = now + Mathf.Max(0.02f, character.MouthFrameInterval) *
                    Random.Range(1f - variation, 1f + variation) * (open ? 1f : Mathf.Max(1f, character.MouthPauseMultiplier));
            }
        }

        private void ScheduleBlink()
        {
            float min = Mathf.Max(0.1f, Mathf.Min(character.BlinkIntervalMin, character.BlinkIntervalMax));
            float max = Mathf.Max(min, Mathf.Max(character.BlinkIntervalMin, character.BlinkIntervalMax));
            float now = TimeMode == DialogueTimeMode.Unscaled ? Time.unscaledTime : Time.time;
            _nextBlink = now + Random.Range(min, max);
        }

        private void OnDisable()
        {
            StopMovement();
            StopSpeaking();
            _eyesClosed = false;
            SetLayer(Eyes, Portrait.Eyes);
        }

        private static void SetLayer(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.enabled = sprite != null;
        }
    }
}
