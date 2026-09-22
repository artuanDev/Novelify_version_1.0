using Novelify;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Novelify
{
    [DisallowMultipleComponent]
    public sealed class NovelGeneratedPresentation : MonoBehaviour
    {
        private NovelGraphRunner _runner;
        private Canvas _canvas;
        private RectTransform _backgroundLayer;
        private RectTransform _generatedRoot;
        private RectTransform _dialogueLayer;
        private RectTransform _bubbleLayer;
        private RectTransform _fadeLayer;
        private RectTransform _templates;
        private bool _ownsCanvas;
        private GameObject _ownedEventSystem;

        private RectTransform _standardPanel;
        private NovelText _standardDialogueText;
        private GameObject _standardSpeakerBox;
        private NovelText _standardSpeakerText;
        private GameObject _standardChoicesPanel;
        private Transform _standardChoiceContainer;
        private Button _standardChoicePrefab;

        private NovelBoxStyle _dialogueStyle = NovelBoxStyle.DialogueDefault;
        private NovelBoxStyle _speakerStyle = NovelBoxStyle.SpeakerDefault;

        private NovelTextAlignment _dialogueTextAlignment =
            NovelTextAlignment.TopLeft;
        private NovelDialogueAnchor _dialogueAnchor =
            NovelDialogueAnchor.BottomCenter;
        private float _dialogueHeight = 180f;
        private float _dialogueWidth;
        private float _dialogueBottomMargin = 32f;
        private float _dialogueHorizontalMargin = 48f;
        private float _dialogueHorizontalPadding = 32f;
        private float _dialogueVerticalPadding = 22f;

        private NovelSpeakerAnchor _speakerAnchor = NovelSpeakerAnchor.TopLeft;
        private float _speakerAlongEdgeOffset = 24f;
        private float _speakerOverlap = 27f;
        private float _speakerHorizontalPadding = 16f;
        private float _speakerVerticalPadding = 6f;

        private RectTransform _bubbleWrapper;
        private RectTransform _bubbleBody;
        private NovelRoundedGraphic _bubbleGraphic;
        private NovelText _bubbleDialogueText;
        private NovelText _bubbleSpeakerText;
        private RectTransform _tailOutlineRect;
        private RectTransform _tailFillRect;
        private readonly RectTransform[] _thoughtDotRects =
            new RectTransform[3];
        private readonly NovelRoundedGraphic[] _thoughtDots =
            new NovelRoundedGraphic[3];
        private readonly List<GameObject> _heldBubbles =
            new List<GameObject>();

        private NovelTriangleGraphic _tailOutline;
        private NovelTriangleGraphic _tailFill;
        private RuntimeSpeechBubbleNode _trackedBubble;
        private CharacterInfo _trackedCharacter;
        private NovelBoxStyle _bubbleStyle = NovelBoxStyle.BubbleDefault;
        private NovelSpeechBubblePlacement _bubblePlacement =
            NovelSpeechBubblePlacement.FollowSpeaker;
        private NovelDialogueAnchor _bubbleScreenAnchor =
            NovelDialogueAnchor.TopCenter;
        private float _bubbleHorizontalOffset;
        private float _bubbleVerticalOffset;
        private bool _bubbleKeepInsideViewport = true;
        private bool _bubbleAutoSize = true;
        private float _bubbleMinimumWidth = 180f;
        private float _bubbleMaximumWidth = 520f;
        private float _bubbleMinimumHeight = 88f;
        private float _bubbleMaximumHeight = 320f;
        private float _bubbleFixedWidth = 360f;
        private float _bubbleFixedHeight = 160f;
        private NovelTextAlignment _bubbleTextAlignment =
            NovelTextAlignment.TopLeft;
        private float _bubbleHorizontalPadding = 24f;
        private float _bubbleVerticalPadding = 18f;
        private float _bubbleDialogueFontSize = 24f;
        private float _bubbleSpeakerFontSize = 21f;
        private bool _bubbleShowSpeakerName;
        private bool _bubbleShowTail = true;
        private bool _bubbleIsThinking;
        private Vector2 _bubbleTailTarget = new Vector2(0.5f, 0.5f);
        private float _bubbleTailWidth = 34f;
        private float _bubbleTailLength = 30f;
        private float _bubbleTargetMargin = 18f;

        private RectTransform _fadeRect;
        private Image _fadeImage;
        private CanvasGroup _fadeGroup;
        private Coroutine _fadeCoroutine;
        private int _fadeGeneration;
        private readonly Image[] _backgroundImages = new Image[2];
        private readonly AspectRatioFitter[] _backgroundFitters =
            new AspectRatioFitter[2];
        private Coroutine _backgroundCoroutine;
        private int _backgroundGeneration;
        private int _activeBackground = -1;

        private readonly Dictionary<NovelAudioChannel, AudioSource> _audioSources =
             new Dictionary<NovelAudioChannel, AudioSource>();

        public void Initialize(NovelGraphRunner runner)
        {
            //If there is no novel graph runner we stop directly
            if (_runner == runner)
                return;

            //We set here all defaults
            _runner = runner;
            _standardPanel = runner.DialoguePanel != null
                ? runner.DialoguePanel.GetComponent<RectTransform>()
                : null;
            _standardDialogueText = runner.DialogueText;
            _standardSpeakerBox = runner.NameBackground;
            _standardSpeakerText = runner.SpeakerNameText;
            _standardChoicesPanel = runner.BackgroundChoicesPanel;
            _standardChoiceContainer = runner.ChoiceButtonContainer;
            _standardChoicePrefab = runner.ChoiceButtonPrefab;
        }

        //Helper function to ensure everything is set up correctly before continuing
        public void EnsureReady()
        {
            if (_runner == null)
                throw new InvalidOperationException(
                    "NovelGeneratedPresentation must be initialized first.");

            EnsureCanvasAndLayers();
            EnsureDialogueBox();
            EnsureSpeakerBox();
            EnsureChoiceUI();
            EnsurePortraitTemplate();
            EnsureEventSystem();
            EnsureTalkAudio();

            if (_trackedBubble == null)
                BindStandardSurface();
        }

        public void CreateDialogueBox(RuntimeCreateDialogueBoxNode node)
        {
            EnsureReady();
            _dialogueStyle = node.ResolvedStyle;
            _dialogueTextAlignment = node.TextAlignment;
            _dialogueAnchor = node.Anchor;
            _dialogueHeight = Mathf.Max(80f, node.Height);
            _dialogueWidth = Mathf.Max(0f, node.Width);
            _dialogueBottomMargin = Mathf.Max(0f, node.BottomMargin);
            _dialogueHorizontalMargin = Mathf.Max(0f, node.HorizontalMargin);
            _dialogueHorizontalPadding = Mathf.Max(0f, node.HorizontalPadding);
            _dialogueVerticalPadding = Mathf.Max(0f, node.VerticalPadding);
            ApplyDialogueLayout();
            ApplyStyle(_standardPanel.gameObject, _dialogueStyle);
            ApplyTextAlignment(_standardDialogueText, _dialogueTextAlignment);
            ApplyTextSizing(
                _standardDialogueText,
                node.BaseFontSize,
                node.AutoSize,
                node.MinimumFontSize,
                node.MaximumFontSize);
            BindStandardSurface();
        }

        public void CreateSpeakerBox(RuntimeCreateDialogueSpeakerBoxNode node)
        {
            EnsureReady();
            _speakerStyle = node.ResolvedStyle;
            _speakerAnchor = node.Anchor;
            _speakerAlongEdgeOffset = node.HorizontalOffset;
            _speakerOverlap = node.VerticalOverlap;
            _speakerHorizontalPadding =
                Mathf.Max(0f, node.HorizontalPadding);
            _speakerVerticalPadding =
                Mathf.Max(0f, node.VerticalPadding);
            _standardSpeakerText.fontSize =
                Mathf.Max(1f, node.FontSize);
            _standardSpeakerText.enableAutoSizing = false;
            _standardSpeakerText.alignment = TextAnchor.MiddleCenter;
            ApplyStyle(_standardSpeakerBox, _speakerStyle);
            if (!string.IsNullOrEmpty(_standardSpeakerText.text))
                RefreshSpeakerNameLayout();
            BindStandardSurface();
        }

        public void CreateSpeechBubble(RuntimeCreateSpeechBubbleNode node)
        {
            ApplySpeechBubblePresentation(node);
        }

        public void ChangeSpeechBubble(RuntimeChangeSpeechBubbleNode node)
        {
            ApplySpeechBubblePresentation(node);
        }

        private void ApplySpeechBubblePresentation(
            RuntimeSpeechBubblePresentationNode node)
        {
            if (node == null)
                return;
            EnsureReady();
            _bubbleStyle = node.ResolvedStyle;
            _bubblePlacement = node.Placement;
            _bubbleScreenAnchor = node.ScreenAnchor;
            _bubbleHorizontalOffset = node.HorizontalOffset;
            _bubbleVerticalOffset = node.VerticalOffset;
            _bubbleKeepInsideViewport = node.KeepInsideViewport;
            _bubbleAutoSize = node.AutoSize;
            _bubbleMinimumWidth = Mathf.Max(120f, node.MinimumWidth);
            _bubbleMaximumWidth = Mathf.Max(
                _bubbleMinimumWidth, node.MaximumWidth);
            _bubbleMinimumHeight = Mathf.Max(64f, node.MinimumHeight);
            _bubbleMaximumHeight = Mathf.Max(
                _bubbleMinimumHeight, node.MaximumHeight);
            _bubbleFixedWidth = Mathf.Max(120f, node.FixedWidth);
            _bubbleFixedHeight = Mathf.Max(64f, node.FixedHeight);
            _bubbleTextAlignment = node.TextAlignment;
            _bubbleHorizontalPadding = Mathf.Max(
                0f, node.HorizontalPadding);
            _bubbleVerticalPadding = Mathf.Max(
                0f, node.VerticalPadding);
            _bubbleDialogueFontSize = Mathf.Max(
                1f, node.DialogueFontSize);
            _bubbleSpeakerFontSize = Mathf.Max(
                1f, node.SpeakerFontSize);
            _bubbleShowSpeakerName = node.ShowSpeakerName;
            _bubbleShowTail = node.ShowTail;
            _bubbleTailTarget = node.TailTarget;
            _bubbleTailWidth = Mathf.Max(2f, node.TailWidth);
            _bubbleTailLength = Mathf.Max(2f, node.TailLength);
            _bubbleTargetMargin = Mathf.Max(0f, node.TargetMargin);
            EnsureBubble();
            if (_trackedBubble != null)
            {
                ConfigureBubble(_trackedBubble);
                UpdateBubblePosition();
            }
        }

        public void RefreshSpeakerNameLayout()
        {
            if (_standardSpeakerBox == null ||
                _standardSpeakerText == null)
                return;
            float horizontal = Mathf.Max(
                0f, _speakerHorizontalPadding);
            float vertical = Mathf.Max(
                0f, _speakerVerticalPadding);
            _standardSpeakerText.alignment = TextAnchor.MiddleCenter;
            _standardSpeakerText.enableAutoSizing = false;
            RectTransform textRect =
                _standardSpeakerText.rectTransform;
            textRect.offsetMin = new Vector2(horizontal, vertical);
            textRect.offsetMax = new Vector2(-horizontal, -vertical);
            string value = _standardSpeakerText.text ?? string.Empty;
            Vector2 preferred = _standardSpeakerText.GetPreferredValues(
                value, 10000f, 10000f);
            RectTransform boxRect =
                _standardSpeakerBox.GetComponent<RectTransform>();
            boxRect.sizeDelta = new Vector2(
                Mathf.Max(1f, preferred.x + horizontal * 2f),
                Mathf.Max(1f, preferred.y + vertical * 2f));
            NovelPresentationLayout.Speaker(
                _speakerAnchor,
                boxRect.sizeDelta.x,
                boxRect.sizeDelta.y,
                _speakerAlongEdgeOffset,
                _speakerOverlap)
                .Apply(boxRect);
        }

        public void ChangeStyle(RuntimeChangeDialogueStyleNode node)
        {
            EnsureReady();
            if (node.Target == NovelBoxTarget.Dialogue ||
                node.Target == NovelBoxTarget.Both)
            {
                _dialogueStyle = node.Resolve(NovelBoxTarget.Dialogue);
                ApplyStyle(_standardPanel.gameObject, _dialogueStyle);
            }
            if (node.Target == NovelBoxTarget.Speaker ||
                node.Target == NovelBoxTarget.Both)
            {
                _speakerStyle = node.Resolve(NovelBoxTarget.Speaker);
                ApplyStyle(_standardSpeakerBox, _speakerStyle);
            }
        }

        public void ResetStyle(RuntimeResetDialogueStyleNode node)
        {
            EnsureReady();
            if (node.Target == NovelBoxTarget.Dialogue ||
                node.Target == NovelBoxTarget.Both)
            {
                _dialogueStyle = NovelBoxStyle.DialogueDefault;
                ApplyStyle(_standardPanel.gameObject, _dialogueStyle);
            }
            if (node.Target == NovelBoxTarget.Speaker ||
                node.Target == NovelBoxTarget.Both)
            {
                _speakerStyle = NovelBoxStyle.SpeakerDefault;
                ApplyStyle(_standardSpeakerBox, _speakerStyle);
            }
        }

        public void PrepareForDialogue(RuntimeDialogueNode node)
        {
            EnsureReady();
            if (node is RuntimeSpeechBubbleNode bubble)
            {
                if (bubble.OverlapMode == NovelSpeechBubbleOverlapMode.KeepPrevious &&
                    _trackedBubble != null)
                    HoldCurrentBubble();
                else
                    ClearHeldBubbles();
                EnsureBubble();
                ConfigureBubble(bubble);
                BindBubbleSurface();
                return;
            }

            ClearHeldBubbles();
            StopTrackingSpeechBubble();
            if (_bubbleWrapper != null)
                _bubbleWrapper.gameObject.SetActive(false);
            BindStandardSurface();
        }

        public void TrackSpeechBubble(
            RuntimeSpeechBubbleNode node,
            CharacterInfo character)
        {
            _trackedBubble = node;
            _trackedCharacter = character;
            UpdateBubblePosition();
        }

        public void StopTrackingSpeechBubble()
        {
            _trackedBubble = null;
            _trackedCharacter = null;
            SetTailVisible(false);
        }

        private void HoldCurrentBubble()
        {
            if (_bubbleWrapper == null)
                return;
            foreach (NovelText text in
                     _bubbleWrapper.GetComponentsInChildren<NovelText>(true))
                text.ClearTransientFontOverlays();
            GameObject held = Instantiate(
                _bubbleWrapper.gameObject, _bubbleLayer, false);
            held.name = "Held Speech Bubble";
            held.SetActive(true);
            CanvasGroup group = held.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
            foreach (NovelText text in held.GetComponentsInChildren<NovelText>(true))
            {
                text.maxVisibleCharacters = int.MaxValue;
                if (text.name == "Dialogue Text" && _trackedBubble != null)
                {
                    text.SetFontAssets(
                        _trackedBubble.DialogueFont != null
                            ? _trackedBubble.DialogueFont
                            : text.font,
                        _trackedBubble.DialogueFontAssets);
                }
            }
            _heldBubbles.Add(held);
            while (_heldBubbles.Count > 4)
            {
                GameObject oldest = _heldBubbles[0];
                _heldBubbles.RemoveAt(0);
                if (oldest != null)
                    Destroy(oldest);
            }
        }

        public void ClearHeldBubbles()
        {
            foreach (GameObject held in _heldBubbles)
                if (held != null)
                    Destroy(held);
            _heldBubbles.Clear();
        }

        public void PlayAudio(
            NovelAudioChannel channel,
            AudioClip clip,
            float volume,
            float pitch,
            bool loop,
            bool replaceCurrent,
            int priority)
        {
            if (clip == null)
            {
                Debug.LogWarning("Play Music has no AudioClip.", _runner);
                return;
            }

            AudioSource source = GetAudioSource(channel);
            source.volume = Mathf.Clamp01(volume);
            source.pitch = Mathf.Clamp(pitch, -3f, 3f);
            source.priority = Mathf.Clamp(priority, 0, 256);

            if (!replaceCurrent && !loop)
            {
                source.PlayOneShot(clip);
                return;
            }

            source.Stop();
            source.clip = clip;
            source.loop = loop;
            source.Play();
        }
        public void StopAudio(NovelAudioChannel channel)
        {
            if (!_audioSources.TryGetValue(channel, out AudioSource source) ||
                source == null)
                return;

            source.Stop();
            source.clip = null;
            source.loop = false;
        }

        public void SetBackground(
            Sprite sprite,
            Color tint,
            NovelBackgroundScaleMode scaleMode,
            float duration,
            Action completed = null)
        {
            EnsureReady();
            EnsureBackgroundLayer();
            int generation = ++_backgroundGeneration;
            if (_backgroundCoroutine != null)
                StopCoroutine(_backgroundCoroutine);
            _backgroundCoroutine = null;

            duration = Mathf.Max(0f, duration);
            int outgoingIndex = _activeBackground;
            int incomingIndex = outgoingIndex == 0 ? 1 : 0;
            Image outgoing = outgoingIndex >= 0
                ? _backgroundImages[outgoingIndex]
                : null;
            Image incoming = _backgroundImages[incomingIndex];

            if (sprite != null)
            {
                ConfigureBackgroundImage(
                    incomingIndex, sprite, tint, scaleMode);
                incoming.transform.SetAsLastSibling();
                incoming.gameObject.SetActive(true);
                Color transparent = tint;
                transparent.a = 0f;
                incoming.color = duration > 0f ? transparent : tint;
                _activeBackground = incomingIndex;
            }
            else
            {
                incoming.gameObject.SetActive(false);
                _activeBackground = -1;
            }

            if (duration <= 0f)
            {
                if (outgoing != null && outgoing != incoming)
                    outgoing.gameObject.SetActive(false);
                completed?.Invoke();
                return;
            }

            _backgroundCoroutine = StartCoroutine(BackgroundTransition(
                generation,
                outgoing,
                sprite != null ? incoming : null,
                tint,
                duration,
                completed));
        }

        private IEnumerator BackgroundTransition(
            int generation,
            Image outgoing,
            Image incoming,
            Color incomingTint,
            float duration,
            Action completed)
        {
            Color outgoingStart = outgoing != null
                ? outgoing.color
                : Color.clear;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                if (generation != _backgroundGeneration)
                    yield break;
                elapsed += _runner.TimeMode == DialogueTimeMode.Unscaled
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                if (outgoing != null)
                {
                    Color color = outgoingStart;
                    color.a = Mathf.Lerp(outgoingStart.a, 0f, t);
                    outgoing.color = color;
                }
                if (incoming != null)
                {
                    Color color = incomingTint;
                    color.a = Mathf.Lerp(0f, incomingTint.a, t);
                    incoming.color = color;
                }
            }

            if (outgoing != null && outgoing != incoming)
                outgoing.gameObject.SetActive(false);
            if (incoming != null)
                incoming.color = incomingTint;
            _backgroundCoroutine = null;
            completed?.Invoke();
        }

        public void BeginFade(
            bool fadeOut,
            float seconds,
            Color color,
            NovelFadeEasing easing,
            bool blockInput,
            Action completed)
        {
            EnsureFadeOverlay();
            int generation = ++_fadeGeneration;
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            bool wasVisible = _fadeRect.gameObject.activeSelf;
            _fadeRect.gameObject.SetActive(true);
            _fadeLayer.SetAsLastSibling();
            _fadeImage.color = color;

            float start = wasVisible
                ? _fadeGroup.alpha
                     : fadeOut ? 0f : 1f;
            float target = fadeOut ? 1f : 0f;
            seconds = Mathf.Max(0f, seconds);

            if (seconds <= 0f)
            {
                ApplyFadeEndpoint(target, blockInput);
                completed?.Invoke();
                return;
            }

            _fadeCoroutine = StartCoroutine(FadeRoutine(
                generation, start, target, seconds, easing,
                blockInput, completed));
        }

        public void StopGraphEffects()
        {
            foreach (AudioSource source in _audioSources.Values)
            {
                if (source != null)
                    source.Stop();
            }

            ++_fadeGeneration;
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
            ++_backgroundGeneration;
            if (_backgroundCoroutine != null)
                StopCoroutine(_backgroundCoroutine);
            _backgroundCoroutine = null;
            StopTrackingSpeechBubble();
            ClearHeldBubbles();
            if (_bubbleWrapper != null)
                _bubbleWrapper.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_backgroundLayer != null)
                _backgroundLayer.SetAsFirstSibling();
            if (_trackedBubble != null &&
                _bubbleWrapper != null &&
                _bubbleWrapper.gameObject.activeInHierarchy)
                UpdateBubblePosition();
        }

        private void EnsureCanvasAndLayers()
        {
            if (_canvas == null)
            {
                if (_runner.CanvasDialogue != null)
                    _canvas = _runner.CanvasDialogue.GetComponentInParent<Canvas>();
                if (_canvas == null && _standardPanel != null)
                    _canvas = _standardPanel.GetComponentInParent<Canvas>();
                if (_canvas == null)
                    _canvas = _runner.GetComponentInParent<Canvas>();
                if (_canvas == null)
                    CreateCanvas();
            }

            ConfigureCanvasScaler();
            EnsureBackgroundLayer();

            if (_generatedRoot == null)
            {
                _generatedRoot = CreateRect("Novelify Generated", _canvas.transform);
                Stretch(_generatedRoot);
                _generatedRoot.SetAsLastSibling();
            }

            if (_dialogueLayer == null)
            {
                _dialogueLayer = CreateRect("Dialogue Layer", _generatedRoot);
                Stretch(_dialogueLayer);
            }
            if (_bubbleLayer == null)
            {
                _bubbleLayer = CreateRect("Bubble Layer", _generatedRoot);
                Stretch(_bubbleLayer);
            }
            if (_fadeLayer == null)
            {
                _fadeLayer = CreateRect("Fade Layer", _generatedRoot);
                Stretch(_fadeLayer);
            }
            if (_templates == null)
            {
                _templates = CreateRect("Templates", _generatedRoot);
                Stretch(_templates);
                _templates.gameObject.SetActive(false);
            }
        }
        private void CreateCanvas()
        {
            var canvasObject = new GameObject(
                "Novelify Runtime UI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            ConfigureCanvasScaler();
            _ownsCanvas = true;
        }

        private void ConfigureCanvasScaler()
        {
            if (_canvas == null || !_runner.ScalePresentationWithScreenSize ||
                _canvas.renderMode == RenderMode.WorldSpace)
                return;
            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = _canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            Vector2 reference = _runner.PresentationReferenceResolution;
            scaler.referenceResolution = new Vector2(
                Mathf.Max(1f, reference.x), Mathf.Max(1f, reference.y));
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = Mathf.Clamp01(
                _runner.PresentationMatchWidthOrHeight);
        }
        private void EnsureDialogueBox()
        {
            if (_standardPanel == null)
            {
                _standardPanel = CreateRect("Dialogue Panel", _dialogueLayer);
                _standardPanel.gameObject.AddComponent<NovelRoundedGraphic>();
                CanvasGroup group =
                    _standardPanel.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            if (_standardDialogueText == null)
            {
                RectTransform textRect = CreateRect(
                    "Dialogue Text", _standardPanel);
                Stretch(textRect);
                _standardDialogueText = CreateText(
                    textRect.gameObject, 30f, TextAnchor.UpperLeft);
            }

            ApplyDialogueLayout();
            ApplyStyle(_standardPanel.gameObject, _dialogueStyle);
            if (_runner.CanvasDialogue == null)
                _runner.CanvasDialogue = _standardPanel.gameObject;
        }

        private void ApplyDialogueLayout()
        {
            NovelPresentationLayout.Dialogue(
                _dialogueAnchor,
                _dialogueWidth,
                _dialogueHeight,
                _dialogueBottomMargin,
                _dialogueHorizontalMargin)
                .Apply(_standardPanel);

            if (_standardDialogueText != null)
            {
                RectTransform textRect =
                    _standardDialogueText.rectTransform;
                textRect.offsetMin = new Vector2(
                    _dialogueHorizontalPadding,
                    _dialogueVerticalPadding);
                textRect.offsetMax = new Vector2(
                    -_dialogueHorizontalPadding,
                    -_dialogueVerticalPadding);
            }
            if (_standardSpeakerBox != null)
            {
                RectTransform speakerRect =
                    _standardSpeakerBox.GetComponent<RectTransform>();
                NovelPresentationLayout.Speaker(
                    _speakerAnchor,
                    speakerRect.sizeDelta.x,
                    speakerRect.sizeDelta.y,
                    _speakerAlongEdgeOffset,
                    _speakerOverlap)
                    .Apply(speakerRect);
            }
        }

        private void EnsureSpeakerBox()
        {
            if (_standardSpeakerBox == null)
            {
                RectTransform rect = CreateRect(
                    "Speaker Box", _standardPanel);
                _standardSpeakerBox = rect.gameObject;
                _standardSpeakerBox.AddComponent<NovelRoundedGraphic>();
                rect.sizeDelta = new Vector2(260f, 54f);
            }
            if (_standardSpeakerText == null)
            {
                RectTransform textRect = CreateRect(
                    "Speaker Name", _standardSpeakerBox.transform);
                Stretch(textRect);
                textRect.offsetMin = new Vector2(16f, 6f);
                textRect.offsetMax = new Vector2(-16f, -6f);
                _standardSpeakerText = CreateText(
                    textRect.gameObject, 25f,
                    TextAnchor.MiddleCenter);
            }
            RectTransform speakerRect = _standardSpeakerBox.GetComponent<RectTransform>();

            NovelPresentationLayout.Speaker(
                _speakerAnchor,
                speakerRect.sizeDelta.x,
                speakerRect.sizeDelta.y,
                _speakerAlongEdgeOffset,
                _speakerOverlap)
                .Apply(speakerRect);

            ApplyStyle(_standardSpeakerBox, _speakerStyle);
        }

        private void EnsureChoiceUI()
        {
            if (_standardChoicesPanel == null)
            {
                RectTransform panel = CreateRect(
                    "Choice Panel", _dialogueLayer);
                panel.anchorMin = new Vector2(0.5f, 0.5f);
                panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.pivot = new Vector2(0.5f, 0.5f);
                panel.sizeDelta = new Vector2(720f, 500f);
                _standardChoicesPanel = panel.gameObject;
            }

            if (_standardChoiceContainer == null)
            {
                RectTransform container = CreateRect(
                    "Choice Container", _standardChoicesPanel.transform);
                Stretch(container);
                VerticalLayoutGroup layout =
                    container.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 14f;
                layout.padding = new RectOffset(24, 24, 24, 24);
                layout.childControlHeight = true;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;
                _standardChoiceContainer = container;
            }

            if (_standardChoicePrefab == null)
                _standardChoicePrefab = CreateChoiceTemplate();
            _standardChoicesPanel.SetActive(false);
        }
        private Button CreateChoiceTemplate()
        {
            RectTransform rect = CreateRect("Choice Button Template", _templates);
            rect.sizeDelta = new Vector2(640f, 64f);
            NovelRoundedGraphic background =
                rect.gameObject.AddComponent<NovelRoundedGraphic>();
            NovelBoxStyle style = NovelBoxStyle.DialogueDefault;
            style.FillColor = new Color(0.10f, 0.16f, 0.28f, 1f);
            style.CornerRadius = 16f;
            background.Apply(style);
            background.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            RectTransform labelRect = CreateRect("Label", rect);
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(18f, 10f);
            labelRect.offsetMax = new Vector2(-18f, -10f);
            CreateText(labelRect.gameObject, 24f, TextAnchor.MiddleCenter);
            return button;
        }

        private void EnsurePortraitTemplate()
        {
            if (_runner.PortraitPrefab != null)
                return;

            RectTransform root = CreateRect("Generated Portrait Template", _templates);
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(520f, 760f);
            CharacterInfo info = root.gameObject.AddComponent<CharacterInfo>();
            info.Body = CreatePortraitLayer("PortraitBackground", root);
            info.Eyes = CreatePortraitLayer("PortraitEyes", root);
            info.Details = CreatePortraitLayer("PortraitEyesDetails", root);
            info.Mouth = CreatePortraitLayer("PortraitMouth", root);
            _runner.PortraitPrefab = root.gameObject;
        }

        private static Image CreatePortraitLayer(string name, Transform parent)
        {
            RectTransform rect = CreateRect(name, parent);
            Stretch(rect);
            Image image = rect.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            _ownedEventSystem = new GameObject(
                "Novelify EventSystem",
                typeof(EventSystem));
            InputSystemUIInputModule module =
                _ownedEventSystem.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private void EnsureTalkAudio()
        {
            if (_runner.TalkSource != null)
                return;
            var audioObject = new GameObject("Novelify Talk Audio");
            audioObject.transform.SetParent(transform, false);
            _runner.TalkSource = audioObject.AddComponent<AudioSource>();
            _runner.TalkSource.playOnAwake = false;

        }
        private void EnsureBubble()
        {
            if (_bubbleWrapper != null)
                return;

            _bubbleWrapper = CreateRect("Speech Bubble", _bubbleLayer);
            _bubbleWrapper.anchorMin = new Vector2(0.5f, 0.5f);
            _bubbleWrapper.anchorMax = new Vector2(0.5f, 0.5f);
            _bubbleWrapper.pivot = new Vector2(0.5f, 0.5f);

            _tailOutlineRect = CreateRect("Tail Outline", _bubbleWrapper);
            _tailOutline =
                _tailOutlineRect.gameObject.AddComponent<NovelTriangleGraphic>();
            _tailOutline.raycastTarget = false;
            Stretch(_tailOutlineRect);
            _tailFillRect = CreateRect("Tail Fill", _bubbleWrapper);
            _tailFill =
                _tailFillRect.gameObject.AddComponent<NovelTriangleGraphic>();
            _tailFill.raycastTarget = false;
            Stretch(_tailFillRect);

            for (int index = 0; index < _thoughtDotRects.Length; index++)
            {
                RectTransform dot = CreateRect(
                    $"Thought Dot {index + 1}", _bubbleWrapper);
                dot.anchorMin = new Vector2(0.5f, 0.5f);
                dot.anchorMax = new Vector2(0.5f, 0.5f);
                dot.pivot = new Vector2(0.5f, 0.5f);
                NovelRoundedGraphic graphic =
                    dot.gameObject.AddComponent<NovelRoundedGraphic>();
                graphic.raycastTarget = false;
                _thoughtDotRects[index] = dot;
                _thoughtDots[index] = graphic;
            }

            _bubbleBody = CreateRect("Body", _bubbleWrapper);
            Stretch(_bubbleBody);
            _bubbleGraphic =
                _bubbleBody.gameObject.AddComponent<NovelRoundedGraphic>();

            RectTransform speakerRect = CreateRect(
                "Speaker Name", _bubbleBody);
            _bubbleSpeakerText = CreateText(
                speakerRect.gameObject, 21f, TextAnchor.MiddleLeft);

            RectTransform dialogueRect = CreateRect(
                "Dialogue Text", _bubbleBody);
            _bubbleDialogueText = CreateText(
                dialogueRect.gameObject, 24f, TextAnchor.UpperLeft);
            _bubbleDialogueText.gameObject.AddComponent<NovelTextEffects>();

            _bubbleWrapper.gameObject.SetActive(false);
        }

        private void ConfigureBubble(RuntimeSpeechBubbleNode node)
        {
            NovelBoxStyle style = _bubbleStyle.Validated();
            _bubbleIsThinking = node.Thinking;
            if (_bubbleIsThinking)
                style.CornerRadius = Mathf.Max(style.CornerRadius, 40f);
            _bubbleGraphic.Apply(style);
            _bubbleDialogueText.SetText(node.DialogueText ?? string.Empty);
            _bubbleDialogueText.fontSize = _bubbleDialogueFontSize;
            ApplyTextAlignment(
                _bubbleDialogueText,
                _bubbleAutoSize
                    ? NovelTextAlignment.CenterCenter
                    : _bubbleTextAlignment);
            _bubbleSpeakerText.fontSize = _bubbleSpeakerFontSize;
            _bubbleDialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bubbleDialogueText.verticalOverflow = VerticalWrapMode.Truncate;
            _bubbleDialogueText.ForceMeshUpdate();

            float horizontal = _bubbleHorizontalPadding;
            float vertical = _bubbleVerticalPadding;
            float minimum = _bubbleMinimumWidth;
            float maximum = _bubbleMaximumWidth;
            float width = _bubbleAutoSize
                ? Mathf.Clamp(
                    _bubbleDialogueText.preferredWidth + horizontal * 2f,
                    minimum, maximum)
                : _bubbleFixedWidth;
            float textWidth = Mathf.Max(1f, width - horizontal * 2f);
            Vector2 preferred = _bubbleDialogueText.GetPreferredValues(
                node.DialogueText ?? string.Empty, textWidth, 10000f);
            float speakerHeight = _bubbleShowSpeakerName
                ? Mathf.Max(24f, _bubbleSpeakerFontSize + 7f)
                : 0f;
            float height = _bubbleAutoSize
                ? Mathf.Clamp(
                    preferred.y + vertical * 2f + speakerHeight,
                    _bubbleMinimumHeight, _bubbleMaximumHeight)
                : _bubbleFixedHeight;
            _bubbleWrapper.sizeDelta = new Vector2(width, height);

            RectTransform speakerRect = _bubbleSpeakerText.rectTransform;
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(1f, 1f);
            speakerRect.pivot = new Vector2(0.5f, 1f);
            speakerRect.anchoredPosition = new Vector2(0f, -vertical);
            speakerRect.sizeDelta = new Vector2(
                -horizontal * 2f, speakerHeight);
            _bubbleSpeakerText.gameObject.SetActive(
                _bubbleShowSpeakerName);

            RectTransform dialogueRect = _bubbleDialogueText.rectTransform;
            dialogueRect.anchorMin = Vector2.zero;
            dialogueRect.anchorMax = Vector2.one;
            dialogueRect.offsetMin = new Vector2(horizontal, vertical);
            dialogueRect.offsetMax = new Vector2(
                -horizontal, -(vertical + speakerHeight));

            _tailOutline.ApplyAppearance(
                style.OutlineEnabled
                    ? style.OutlineTexture
                    : style.FillTexture,
                style.OutlineEnabled
                    ? style.EffectiveOutlineColor
                    : style.EffectiveFillColor,
                style.OutlineEnabled
                    ? style.OutlineTiling
                    : style.FillTiling,
                style.OutlineEnabled
                    ? style.OutlineOffset
                    : style.FillOffset);
            _tailFill.ApplyAppearance(
                style.FillTexture,
                style.EffectiveFillColor,
                style.FillTiling,
                style.FillOffset);
            NovelBoxStyle thoughtDotStyle = style;
            thoughtDotStyle.CornerRadius = 999f;
            for (int index = 0; index < _thoughtDots.Length; index++)
                _thoughtDots[index].Apply(thoughtDotStyle);
            SetTailVisible(_bubbleShowTail);
        }

        private void BindStandardSurface()
        {
            _runner.DialoguePanel = _standardPanel.gameObject;
            _runner.DialogueText = _standardDialogueText;
            _runner.NameBackground = _standardSpeakerBox;
            _runner.SpeakerNameText = _standardSpeakerText;
            _runner.BackgroundChoicesPanel = _standardChoicesPanel;
            _runner.ChoiceButtonContainer = _standardChoiceContainer;
            _runner.ChoiceButtonPrefab = _standardChoicePrefab;
        }

        private void BindBubbleSurface()
        {
            _runner.DialoguePanel = _bubbleWrapper.gameObject;
            _runner.DialogueText = _bubbleDialogueText;
            _runner.NameBackground = _bubbleShowSpeakerName
                ? _bubbleSpeakerText.gameObject
                : null;
            _runner.SpeakerNameText = _bubbleSpeakerText;
        }

        private void UpdateBubblePosition()
        {
            if (_trackedBubble == null || _bubbleWrapper == null)
                return;

            if (_trackedCharacter == null ||
                _trackedCharacter.transform is not RectTransform characterRect)
            {
                Rect fallbackSafe = _bubbleLayer.rect;
                Vector2 fallbackSize = _bubbleWrapper.rect.size;
                Vector2 fallback = _bubblePlacement ==
                    NovelSpeechBubblePlacement.ScreenAnchor
                        ? GetAnchoredBubblePosition(
                            fallbackSafe, fallbackSize,
                            _bubbleScreenAnchor,
                            Mathf.Max(8f, _bubbleTargetMargin))
                        : new Vector2(0f, 120f);
                fallback += new Vector2(
                    _bubbleHorizontalOffset, _bubbleVerticalOffset);
                _bubbleWrapper.anchoredPosition = _bubbleKeepInsideViewport
                    ? ClampBubbleToViewport(
                        fallback, fallbackSafe, fallbackSize,
                        Mathf.Max(8f, _bubbleTargetMargin))
                    : fallback;
                SetTailVisible(false);
                return;
            }

            Vector3[] corners = new Vector3[4];
            characterRect.GetWorldCorners(corners);
            Vector3 targetOnLeft = Vector3.LerpUnclamped(
                corners[0], corners[1], _bubbleTailTarget.y);
            Vector3 targetOnRight = Vector3.LerpUnclamped(
                corners[3], corners[2], _bubbleTailTarget.y);
            Vector3 worldTarget = Vector3.LerpUnclamped(
                targetOnLeft, targetOnRight, _bubbleTailTarget.x);
            Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                camera, worldTarget);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _bubbleLayer, screen, camera, out Vector2 target);

            Rect safe = _bubbleLayer.rect;
            Vector2 size = _bubbleWrapper.rect.size;
            float margin = Mathf.Max(8f, _bubbleTargetMargin);
            float tailLength = _bubbleTailLength;
            Vector2 desired;
            if (_bubblePlacement == NovelSpeechBubblePlacement.ScreenAnchor)
            {
                desired = GetAnchoredBubblePosition(
                    safe, size, _bubbleScreenAnchor, margin);
            }
            else
            {
                desired = new Vector2(
                    target.x,
                    target.y + margin + tailLength + size.y * 0.5f);
                if (desired.y + size.y * 0.5f > safe.yMax - margin)
                    desired.y = target.y - margin - tailLength -
                        size.y * 0.5f;
            }
            desired += new Vector2(
                _bubbleHorizontalOffset, _bubbleVerticalOffset);
            if (_bubbleKeepInsideViewport)
                desired = ClampBubbleToViewport(
                    desired, safe, size, margin);
            _bubbleWrapper.anchoredPosition = desired;
            PositionTail(target - desired, size);
        }

        private void PositionTail(
            Vector2 targetFromBubble,
            Vector2 bubbleSize)
        {
            if (!_bubbleShowTail)
            {
                SetTailVisible(false);
                return;
            }
            SetTailVisible(true);
            float halfWidth = bubbleSize.x * 0.5f;
            float halfHeight = bubbleSize.y * 0.5f;
            float tailLength = _bubbleTailLength;
            float safeCorner = _bubbleStyle.CornerRadius +
                _bubbleTailWidth * 0.5f;
            float safeX = Mathf.Min(
                Mathf.Max(0f, halfWidth - 1f), safeCorner);
            float safeY = Mathf.Min(
                Mathf.Max(0f, halfHeight - 1f), safeCorner);
            Vector2 attachment;
            Vector2 inward;
            bool verticalEdge;
            Vector2 ray = targetFromBubble.sqrMagnitude > 0.0001f
                ? targetFromBubble
                : Vector2.down;
            if (Mathf.Abs(ray.x) > Mathf.Abs(ray.y))
            {
                verticalEdge = true;
                float intersectionScale = halfWidth /
                    Mathf.Max(0.0001f, Mathf.Abs(ray.x));
                float y = Mathf.Clamp(
                    ray.y * intersectionScale,
                    -halfHeight + safeY,
                    halfHeight - safeY);
                if (ray.x >= 0f)
                {
                    attachment = new Vector2(halfWidth, y);
                    inward = Vector2.left;
                }
                else
                {
                    attachment = new Vector2(-halfWidth, y);
                    inward = Vector2.right;
                }
            }
            else
            {
                verticalEdge = false;
                float intersectionScale = halfHeight /
                    Mathf.Max(0.0001f, Mathf.Abs(ray.y));
                float x = Mathf.Clamp(
                    ray.x * intersectionScale,
                    -halfWidth + safeX,
                    halfWidth - safeX);
                if (ray.y >= 0f)
                {
                    attachment = new Vector2(x, halfHeight);
                    inward = Vector2.down;
                }
                else
                {
                    attachment = new Vector2(x, -halfHeight);
                    inward = Vector2.up;
                }
            }

            Vector2 direction = targetFromBubble - attachment;
            if (direction.sqrMagnitude < 0.0001f)
                direction = targetFromBubble.sqrMagnitude > 0.0001f
                    ? targetFromBubble
                    : Vector2.down;
            direction.Normalize();
            if (_bubbleIsThinking)
            {
                PositionThoughtDots(attachment, direction, tailLength);
                return;
            }

            float bodyOverlap = GetTailBodyOverlap(tailLength);
            Vector2 baseCenter = attachment + inward * bodyOverlap;
            Vector2 edgeAxis = verticalEdge ? Vector2.up : Vector2.right;
            Vector2 tip = attachment + direction * tailLength;
            float outline = _bubbleStyle.OutlineEnabled
                ? _bubbleStyle.OutlineThickness
                : 0f;
            float fillHalfWidth = _bubbleTailWidth * 0.5f;
            float outlineHalfWidth = fillHalfWidth + outline;

            _tailOutline.SetPoints(
                baseCenter + edgeAxis * outlineHalfWidth,
                baseCenter - edgeAxis * outlineHalfWidth,
                tip + direction * outline);
            _tailFill.SetPoints(
                baseCenter + edgeAxis * fillHalfWidth,
                baseCenter - edgeAxis * fillHalfWidth,
                tip);
        }

        private void PositionThoughtDots(
            Vector2 attachment,
            Vector2 direction,
            float tailLength)
        {
            float[] progress = { 0.12f, 0.5f, 0.88f };
            float[] scale = { 0.7f, 0.46f, 0.28f };
            for (int index = 0; index < _thoughtDotRects.Length; index++)
            {
                float size = Mathf.Max(
                    4f, _bubbleTailWidth * scale[index]);
                RectTransform dot = _thoughtDotRects[index];
                dot.sizeDelta = new Vector2(size, size);
                dot.anchoredPosition = attachment + direction *
                    (tailLength * progress[index]);
            }
        }

        private void EnsureBackgroundLayer()
        {
            if (_canvas == null)
                return;
            if (_backgroundLayer == null)
            {
                _backgroundLayer = CreateRect(
                    "Novelify Background Layer", _canvas.transform);
                Stretch(_backgroundLayer);
                _backgroundLayer.SetAsFirstSibling();
            }
            for (int index = 0; index < _backgroundImages.Length; index++)
            {
                if (_backgroundImages[index] != null)
                    continue;
                RectTransform rect = CreateRect(
                    $"Background {index + 1}", _backgroundLayer);
                Stretch(rect);
                Image image = rect.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                image.gameObject.SetActive(false);
                _backgroundImages[index] = image;
                _backgroundFitters[index] =
                    rect.gameObject.AddComponent<AspectRatioFitter>();
            }
        }

        private void ConfigureBackgroundImage(
            int index,
            Sprite sprite,
            Color tint,
            NovelBackgroundScaleMode scaleMode)
        {
            Image image = _backgroundImages[index];
            AspectRatioFitter fitter = _backgroundFitters[index];
            RectTransform rect = image.rectTransform;
            fitter.aspectMode = AspectRatioFitter.AspectMode.None;
            image.sprite = sprite;
            image.color = tint;
            image.preserveAspect = false;
            Stretch(rect);

            if (scaleMode == NovelBackgroundScaleMode.Stretch ||
                sprite == null || sprite.rect.height <= 0f)
            {
                return;
            }

            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            fitter.aspectMode = scaleMode == NovelBackgroundScaleMode.Contain
                ? AspectRatioFitter.AspectMode.FitInParent
                : AspectRatioFitter.AspectMode.EnvelopeParent;
        }

        private static float GetTailBodyOverlap(float tailLength) =>
            Mathf.Min(6f, Mathf.Max(0f, tailLength) * 0.25f);

        private static Vector2 GetAnchoredBubblePosition(
            Rect viewport,
            Vector2 bubbleSize,
            NovelDialogueAnchor anchor,
            float margin)
        {
            float left = viewport.xMin + bubbleSize.x * 0.5f + margin;
            float centerX = viewport.center.x;
            float right = viewport.xMax - bubbleSize.x * 0.5f - margin;
            float bottom = viewport.yMin + bubbleSize.y * 0.5f + margin;
            float centerY = viewport.center.y;
            float top = viewport.yMax - bubbleSize.y * 0.5f - margin;
            return anchor switch
            {
                NovelDialogueAnchor.TopLeft => new Vector2(left, top),
                NovelDialogueAnchor.TopCenter => new Vector2(centerX, top),
                NovelDialogueAnchor.TopRight => new Vector2(right, top),
                NovelDialogueAnchor.CenterLeft =>
                    new Vector2(left, centerY),
                NovelDialogueAnchor.CenterRight =>
                    new Vector2(right, centerY),
                NovelDialogueAnchor.BottomLeft => new Vector2(left, bottom),
                NovelDialogueAnchor.BottomCenter =>
                    new Vector2(centerX, bottom),
                NovelDialogueAnchor.BottomRight =>
                    new Vector2(right, bottom),
                _ => new Vector2(centerX, centerY)
            };
        }

        private static Vector2 ClampBubbleToViewport(
            Vector2 position,
            Rect viewport,
            Vector2 bubbleSize,
            float margin)
        {
            float minimumX = viewport.xMin + bubbleSize.x * 0.5f + margin;
            float maximumX = viewport.xMax - bubbleSize.x * 0.5f - margin;
            float minimumY = viewport.yMin + bubbleSize.y * 0.5f + margin;
            float maximumY = viewport.yMax - bubbleSize.y * 0.5f - margin;
            return new Vector2(
                minimumX <= maximumX
                    ? Mathf.Clamp(position.x, minimumX, maximumX)
                    : viewport.center.x,
                minimumY <= maximumY
                    ? Mathf.Clamp(position.y, minimumY, maximumY)
                    : viewport.center.y);
        }

        private void SetTailVisible(bool visible)
        {
            if (_tailOutlineRect != null)
                _tailOutlineRect.gameObject.SetActive(
                    visible && !_bubbleIsThinking);
            if (_tailFillRect != null)
                _tailFillRect.gameObject.SetActive(
                    visible && !_bubbleIsThinking);
            for (int index = 0; index < _thoughtDotRects.Length; index++)
            {
                if (_thoughtDotRects[index] != null)
                    _thoughtDotRects[index].gameObject.SetActive(
                        visible && _bubbleIsThinking);
            }
        }

        private AudioSource GetAudioSource(NovelAudioChannel channel)
        {
            if (_audioSources.TryGetValue(channel, out AudioSource source) &&
                source != null)
                return source;

            var audioObject = new GameObject("Novelify Audio - " + channel);
            audioObject.transform.SetParent(transform, false);
            source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            _audioSources[channel] = source;
            return source;
        }

        private void EnsureFadeOverlay()
        {
            EnsureCanvasAndLayers();
            if (_fadeRect != null)
                return;

            _fadeRect = CreateRect("Fade Overlay", _fadeLayer);
            Stretch(_fadeRect);
            _fadeImage = _fadeRect.gameObject.AddComponent<Image>();
            _fadeImage.raycastTarget = false;
            _fadeGroup = _fadeRect.gameObject.AddComponent<CanvasGroup>();
            _fadeGroup.alpha = 0f;
            _fadeGroup.interactable = false;
            _fadeGroup.blocksRaycasts = false;
            _fadeRect.gameObject.SetActive(false);
        }

        private IEnumerator FadeRoutine(
           int generation,
           float start,
           float target,
           float seconds,
             NovelFadeEasing easing,
            bool blockInput,
            Action completed)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (generation != _fadeGeneration)
                    yield break;
                elapsed += _runner.TimeMode == DialogueTimeMode.Unscaled
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / seconds);
                _fadeGroup.alpha = Mathf.Lerp(
                    start, target, Ease(easing, progress));
                _fadeGroup.blocksRaycasts =
                    blockInput && _fadeGroup.alpha > 0.001f;
                yield return null;
            }

            if (generation != _fadeGeneration)
                yield break;
            _fadeCoroutine = null;
            ApplyFadeEndpoint(target, blockInput);
            completed?.Invoke();
        }

        private void ApplyFadeEndpoint(float alpha, bool blockInput)
        {
            _fadeGroup.alpha = alpha;
            _fadeGroup.blocksRaycasts = blockInput && alpha > 0.001f;
            if (alpha <= 0.001f)
                _fadeRect.gameObject.SetActive(false);
        }

        private static float Ease(NovelFadeEasing easing, float value)
        {
            value = Mathf.Clamp01(value);
            return easing switch
            {
                NovelFadeEasing.EaseIn => value * value * value,
                NovelFadeEasing.EaseOut => 1f - Mathf.Pow(1f - value, 3f),
                NovelFadeEasing.EaseInOut =>
                    value * value * (3f - 2f * value),
                _ => value
            };
        }

        private static void ApplyTextAlignment(
            NovelText text,
            NovelTextAlignment alignment)
        {
            if (text == null) return;

            /*this allows setting or getting a value from a different enum using another as input
             Basically we ask which value is alignment set to?  based on that set the text alignment to the corresponding one
             */
            text.alignment = alignment switch
            {
                NovelTextAlignment.TopCenter => TextAnchor.UpperCenter,
                NovelTextAlignment.TopRight => TextAnchor.UpperRight,
                NovelTextAlignment.CenterLeft => TextAnchor.MiddleLeft,
                NovelTextAlignment.CenterCenter => TextAnchor.MiddleCenter,
                NovelTextAlignment.CenterRight => TextAnchor.MiddleRight,
                NovelTextAlignment.BottomLeft => TextAnchor.LowerLeft,
                NovelTextAlignment.BottomCenter => TextAnchor.LowerCenter,
                NovelTextAlignment.BottomRight => TextAnchor.LowerRight,
                _ => TextAnchor.UpperLeft
            };
        }

        private static void ApplyTextSizing(
            NovelText text,
            float baseFontSize,
            bool autoSize,
            float minimumFontSize,
            float maximumFontSize)
        {
            if (text == null) return;
            float minimum = Mathf.Max(1f, minimumFontSize);
            float maximum = Mathf.Max(minimum, maximumFontSize);
            text.fontSize = Mathf.Max(1f, baseFontSize);
            text.enableAutoSizing = autoSize;
            text.fontSizeMin = minimum;
            text.fontSizeMax = maximum;
        }

        private static void ApplyStyle(GameObject target, NovelBoxStyle style)
        {
            if (target == null)
                return;
            NovelRoundedGraphic rounded =
                target.GetComponent<NovelRoundedGraphic>();
            if (rounded != null)
            {
                rounded.Apply(style);
                return;
            }

            Image image = target.GetComponent<Image>();
            if (image != null)
                image.color = style.EffectiveFillColor;
        }

        private static RectTransform CreateRect(string name, UnityEngine.Transform parent)
        {
            var value = new GameObject(name, typeof(RectTransform));
            RectTransform rect = value.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
        private static NovelText CreateText(
            GameObject target,
            float size,
            TextAnchor alignment)
        {
            NovelText text = target.AddComponent<NovelText>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.richText = true;
            text.raycastTarget = false;
            return text;
        }
        private void OnDestroy()
        {
            if (_ownedEventSystem != null)
                DestroyOwned(_ownedEventSystem);
            if (_ownsCanvas && _canvas != null)
                DestroyOwned(_canvas.gameObject);
            else if (_generatedRoot != null)
                DestroyOwned(_generatedRoot.gameObject);
        }

        private static void DestroyOwned(UnityEngine.Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
