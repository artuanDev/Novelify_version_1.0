using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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
        private bool _wasHiddenBeforePrepare;
        private bool _hasEnteredStage;
        private Vector2 _positionBeforeHide;
        private CanvasGroup _canvasGroup;
        private bool _speaking, _animateMouth, _animateBlinking = true, _speechPause;
        private bool _eyesClosed;
        private float _nextMouthFrame, _nextBlink;
        private readonly Dictionary<Image, Color> _baseLayerColors =
            new Dictionary<Image, Color>();
        private Color _focusTint = Color.white;
        private Color _focusTintStart = Color.white;
        private Color _focusTintTarget = Color.white;
        private float _focusTintElapsed;
        private float _focusTintDuration;
        private static readonly Dictionary<Sprite, Rect>
            VisibleSpriteBounds = new Dictionary<Sprite, Rect>();

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

        public void AnchorAtStageBottomCenter()
        {
            if (transform is not RectTransform rect)
                return;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            AlignVisiblePortraitPivotToBaseline();
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
                if (!_baseLayerColors.ContainsKey(layer))
                    _baseLayerColors.Add(layer, layer.color);
            }
            ApplyFocusTint();
        }

        public void SetSpeakerFocus(
            bool focused,
            Color inactiveTint,
            float transitionDuration)
        {
            Color target = focused ? Color.white : inactiveTint;
            target.a = 1f;
            _focusTintStart = _focusTint;
            _focusTintTarget = target;
            _focusTintElapsed = 0f;
            _focusTintDuration = Mathf.Max(0f, transitionDuration);
            if (_focusTintDuration <= 0f)
            {
                _focusTint = _focusTintTarget;
                ApplyFocusTint();
            }
        }

        private void ApplyFocusTint()
        {
            foreach (KeyValuePair<Image, Color> entry in _baseLayerColors)
            {
                if (entry.Key == null)
                    continue;
                Color color = entry.Value;
                color.r *= _focusTint.r;
                color.g *= _focusTint.g;
                color.b *= _focusTint.b;
                entry.Key.color = color;
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
            AlignVisiblePortraitPivotToBaseline();
            _eyesClosed = false;
            ScheduleBlink();
        }

        private void AlignVisiblePortraitPivotToBaseline()
        {
            if (transform is not RectTransform rect)
                return;
            Image[] layers = { Body, Eyes, Details, Mouth };
            bool found = false;
            Vector2 minimum = new Vector2(
                float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(
                float.NegativeInfinity, float.NegativeInfinity);
            foreach (Image layer in layers)
            {
                Sprite sprite = layer != null ? layer.sprite : null;
                if (sprite == null ||
                    layer.transform is not RectTransform layerRect ||
                    !TryGetVisibleSpriteBounds(sprite, out Rect visible))
                    continue;

                Rect drawing = GetSpriteDrawingRect(layer, sprite);
                Vector2[] corners =
                {
                    new Vector2(
                        Mathf.Lerp(drawing.xMin, drawing.xMax, visible.xMin),
                        Mathf.Lerp(drawing.yMin, drawing.yMax, visible.yMin)),
                    new Vector2(
                        Mathf.Lerp(drawing.xMin, drawing.xMax, visible.xMax),
                        Mathf.Lerp(drawing.yMin, drawing.yMax, visible.yMin)),
                    new Vector2(
                        Mathf.Lerp(drawing.xMin, drawing.xMax, visible.xMin),
                        Mathf.Lerp(drawing.yMin, drawing.yMax, visible.yMax)),
                    new Vector2(
                        Mathf.Lerp(drawing.xMin, drawing.xMax, visible.xMax),
                        Mathf.Lerp(drawing.yMin, drawing.yMax, visible.yMax))
                };
                for (int index = 0; index < corners.Length; index++)
                {
                    Vector3 world = layerRect.TransformPoint(corners[index]);
                    Vector2 local = rect.InverseTransformPoint(world);
                    minimum = Vector2.Min(minimum, local);
                    maximum = Vector2.Max(maximum, local);
                    found = true;
                }
            }

            if (!found || rect.rect.width <= Mathf.Epsilon ||
                rect.rect.height <= Mathf.Epsilon)
            {
                rect.pivot = new Vector2(0.5f, 0f);
                return;
            }

            // The root can be much smaller than its composed Image children,
            // so this pivot is intentionally allowed outside 0..1. Moving the
            // parent pivot shifts every anchored child by the inverse amount;
            // adding the current visible baseline in root units therefore puts
            // that baseline exactly on the root's logical position.
            Vector2 baseline = new Vector2(
                (minimum.x + maximum.x) * 0.5f,
                minimum.y);
            rect.pivot += new Vector2(
                baseline.x / rect.rect.width,
                baseline.y / rect.rect.height);
        }

        private static Rect GetSpriteDrawingRect(Image image, Sprite sprite)
        {
            Rect result = image.rectTransform.rect;
            if (!image.preserveAspect || sprite.rect.width <= Mathf.Epsilon ||
                sprite.rect.height <= Mathf.Epsilon ||
                result.width <= Mathf.Epsilon ||
                result.height <= Mathf.Epsilon)
                return result;

            float spriteRatio = sprite.rect.width / sprite.rect.height;
            float rectRatio = result.width / result.height;
            Vector2 pivot = image.rectTransform.pivot;
            if (spriteRatio > rectRatio)
            {
                float previousHeight = result.height;
                result.height = result.width / spriteRatio;
                result.y += (previousHeight - result.height) * pivot.y;
            }
            else
            {
                float previousWidth = result.width;
                result.width = result.height * spriteRatio;
                result.x += (previousWidth - result.width) * pivot.x;
            }
            return result;
        }

        private static bool TryGetVisibleSpriteBounds(
            Sprite sprite, out Rect bounds)
        {
            bounds = new Rect(0f, 0f, 1f, 1f);
            if (sprite == null || sprite.rect.width <= Mathf.Epsilon ||
                sprite.rect.height <= Mathf.Epsilon)
                return false;
            if (VisibleSpriteBounds.TryGetValue(sprite, out bounds))
                return true;

            Vector2[] vertices = sprite.vertices;
            if (vertices != null && vertices.Length > 0 &&
                sprite.pixelsPerUnit > Mathf.Epsilon)
            {
                Vector2 minimum = new Vector2(
                    float.PositiveInfinity, float.PositiveInfinity);
                Vector2 maximum = new Vector2(
                    float.NegativeInfinity, float.NegativeInfinity);
                foreach (Vector2 vertex in vertices)
                {
                    Vector2 pixel = vertex * sprite.pixelsPerUnit +
                                    sprite.pivot;
                    minimum = Vector2.Min(minimum, pixel);
                    maximum = Vector2.Max(maximum, pixel);
                }
                bounds = Rect.MinMaxRect(
                    Mathf.Clamp01(minimum.x / sprite.rect.width),
                    Mathf.Clamp01(minimum.y / sprite.rect.height),
                    Mathf.Clamp01(maximum.x / sprite.rect.width),
                    Mathf.Clamp01(maximum.y / sprite.rect.height));
            }

            VisibleSpriteBounds[sprite] = bounds;
            return true;
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
            Vector2 localTarget = Vector2.Scale(
                normalizedPosition, GetStageExtent(margin));
            return localTarget - GetAnchorReferencePosition();
        }

        public Vector2 NormalizedToAnchoredOffset(
            Vector2 normalizedOffset,
            float margin)
        {
            return Vector2.Scale(normalizedOffset, GetStageExtent(margin));
        }

        public Vector2 AnchoredToNormalizedPosition(Vector2 anchoredPosition, float margin = 0f)
        {
            Vector2 extent = GetStageExtent(margin);
            Vector2 localPosition = anchoredPosition +
                                    GetAnchorReferencePosition();
            return new Vector2(
                extent.x > 0f ? localPosition.x / extent.x : 0f,
                extent.y > 0f ? localPosition.y / extent.y : 0f);
        }

        public Vector2 ClampToStageBounds(Vector2 position, float margin)
        {
            Vector2 extent = GetStageExtent(margin);
            Vector2 anchorReference = GetAnchorReferencePosition();
            Vector2 localPosition = position + anchorReference;
            localPosition = new Vector2(
                Mathf.Clamp(localPosition.x, -extent.x, extent.x),
                Mathf.Clamp(localPosition.y, -extent.y, extent.y));
            return localPosition - anchorReference;
        }

        private Vector2 GetAnchorReferencePosition()
        {
            if (transform is not RectTransform rect ||
                rect.parent is not RectTransform parentRect)
                return Vector2.zero;
            Vector2 anchor = (rect.anchorMin + rect.anchorMax) * 0.5f;
            Rect parentBounds = parentRect.rect;
            return new Vector2(
                Mathf.Lerp(parentBounds.xMin, parentBounds.xMax, anchor.x),
                Mathf.Lerp(parentBounds.yMin, parentBounds.yMax, anchor.y));
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
            bool shouldTransition = !_hasEnteredStage || _wasHiddenBeforePrepare || !gameObject.activeSelf;
            _wasHiddenBeforePrepare = false;

            if (!shouldTransition)
                return;

            _hasEnteredStage = true;

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
            _wasHiddenBeforePrepare = !_hasEnteredStage || wasHidden;
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
            if (_focusTint != _focusTintTarget)
            {
                _focusTintElapsed += TimeMode == DialogueTimeMode.Unscaled
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime;
                float focusProgress = _focusTintDuration > 0f
                    ? Mathf.Clamp01(_focusTintElapsed / _focusTintDuration)
                    : 1f;
                _focusTint = Color.Lerp(
                    _focusTintStart, _focusTintTarget,
                    focusProgress * focusProgress * (3f - 2f * focusProgress));
                ApplyFocusTint();
            }

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
