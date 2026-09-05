using UnityEngine;
using UnityEngine.UI;

namespace Novelify
{
    public class CharacterInfo : MonoBehaviour
    {
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
        private float _moveElapsed, _moveDuration;
        private bool _easeMovement;
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
            _moveStart = Position;
            _moveTarget = target;
            _moveElapsed = 0f;
            _moveDuration = duration;
            _easeMovement = easeInOut;
            IsMoving = smooth && duration > 0f && !float.IsInfinity(duration) && _moveStart != target;
            if (!IsMoving) Position = target;
        }

        public void StopMovement() => IsMoving = false;

        private void Update()
        {
            if (IsMoving)
            {
                _moveElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_moveElapsed / _moveDuration);
                Position = Vector2.LerpUnclamped(_moveStart, _moveTarget, _easeMovement ? t * t * (3f - 2f * t) : t);
                if (t >= 1f)
                {
                    Position = _moveTarget;
                    IsMoving = false;
                }
            }

            if (character == null) return;
            float now = Time.unscaledTime;
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
            _nextBlink = Time.unscaledTime + Random.Range(min, max);
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
