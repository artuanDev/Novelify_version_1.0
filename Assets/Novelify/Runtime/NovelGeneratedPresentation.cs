using Novelify;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using TMPro;
using Unity.Burst.CompilerServices;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEditor.Graphs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

namespace Novelify
{
    [DisallowMultipleComponent]
    public sealed class NovelGeneratedPresentation : MonoBehaviour
    {
        private NovelGraphRunner _runner;
        private Canvas _canvas;
        private RectTransform _generatedRoot;
        private RectTransform _dialogueLayer;
        private RectTransform _bubbleLayer;
        private RectTransform _fadeLayer;
        private RectTransform _templates;
        private bool _ownsCanvas;
        private GameObject _ownedEventSystem;

        private RectTransform _standardPanel;
        private TextMeshProUGUI _standardDialogueText;
        private GameObject _standardSpeakerBox;
        private TextMeshProUGUI _standardSpeakerText;
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

        private RectTransform _bubbleWrapper;
        private RectTransform _bubbleBody;
        private NovelRoundedGraphic _bubbleGraphic;
        private TextMeshProUGUI _bubbleDialogueText;
        private TextMeshProUGUI _bubbleSpeakerText;
        private RectTransform _tailOutlineRect;
        private RectTransform _tailFillRect;

        private NovelTriangleGraphic _tailOutline;
        private NovelTriangleGraphic _tailFill;
        private RuntimeSpeechBubbleNode _trackedBubble;
        private CharacterInfo _trackedCharacter;

        private RectTransform _fadeRect;
        private Image _fadeImage;
        private CanvasGroup _fadeGroup;
        private Coroutine _fadeCoroutine;
        private int _fadeGeneration;

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
            _dialogueStyle = node.Style.Validated();
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
            _speakerStyle = node.Style.Validated();
            ApplyStyle(_standardSpeakerBox, _speakerStyle);
            RectTransform rect = _standardSpeakerBox.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(
                Mathf.Max(80f, node.Width),
                Mathf.Max(30f, node.Height));
            rect.anchoredPosition = new Vector2(
                node.HorizontalOffset,
                rect.sizeDelta.y * 0.5f - node.VerticalOverlap);
            BindStandardSurface();
        }

        public void ChangeStyle(RuntimeChangeDialogueStyleNode node)
        {
            EnsureReady();
            NovelBoxStyle value = node.Style.Validated();
            if (node.Target == NovelBoxTarget.Dialogue ||
                node.Target == NovelBoxTarget.Both)
            {
                _dialogueStyle = value;
                ApplyStyle(_standardPanel.gameObject, _dialogueStyle);
            }
            if (node.Target == NovelBoxTarget.Speaker ||
                node.Target == NovelBoxTarget.Both)
            {
                _speakerStyle = value;
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
                EnsureBubble();
                ConfigureBubble(bubble);
                BindBubbleSurface();
                return;
            }

            StopTrackingSpeechBubble();
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
            StopTrackingSpeechBubble();
        }

        private void LateUpdate()
        {
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
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _ownsCanvas = true;
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
                    textRect.gameObject, 30f, TextAlignmentOptions.TopLeft);
            }

            ApplyDialogueLayout();
            ApplyStyle(_standardPanel.gameObject, _dialogueStyle);
            if (_runner.CanvasDialogue == null)
                _runner.CanvasDialogue = _standardPanel.gameObject;
        }
        //since anchor is an enum, therefore an int, we can quickly change how these offsets behave depending on ranges
        private static Vector2 DialogueAnchorPoint(NovelDialogueAnchor anchor)
        {
            int value = (int)anchor;
            int column = value % 3;
            int row = value / 3;
            return new Vector2(column * 0.5f, 1f - row * 0.5f);
        }
        private static float EdgeOffset(float anchor, float margin)
        {
            if (anchor < 0.25f) return margin;
            if (anchor > 0.75f) return -margin;
            return 0f;
        }

        private void ApplyDialogueLayout()
        {
            Vector2 anchor = DialogueAnchorPoint(_dialogueAnchor);
            bool stretch = _dialogueWidth <= 0f;
            _standardPanel.anchorMin = stretch
                ? new Vector2(0f, anchor.y)
                : anchor;
            _standardPanel.anchorMax = stretch
                ? new Vector2(1f, anchor.y)
                : anchor;
            _standardPanel.pivot = stretch
                ? new Vector2(0.5f, anchor.y)
                : anchor;

            _standardPanel.anchoredPosition = new Vector2(
                stretch
                    ? 0f
                    : EdgeOffset(anchor.x, _dialogueHorizontalMargin),
                EdgeOffset(anchor.y, _dialogueBottomMargin));
                    _standardPanel.sizeDelta = new Vector2(
                        stretch
                    ? -_dialogueHorizontalMargin * 2f
                    : _dialogueWidth,
                _dialogueHeight);

            if (_standardDialogueText != null)
            {
                RectTransform textRect = _standardDialogueText.rectTransform;
                textRect.offsetMin = new Vector2(
                    _dialogueHorizontalPadding, _dialogueVerticalPadding);
                textRect.offsetMax = new Vector2(
                    -_dialogueHorizontalPadding, -_dialogueVerticalPadding);
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
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(260f, 54f);
                rect.anchoredPosition = new Vector2(24f, 0f);
            }

            if (_standardSpeakerText == null)
            {
                RectTransform textRect = CreateRect(
                    "Speaker Name", _standardSpeakerBox.transform);
                Stretch(textRect);
                textRect.offsetMin = new Vector2(16f, 6f);
                textRect.offsetMax = new Vector2(-16f, -6f);
                _standardSpeakerText = CreateText(
                    textRect.gameObject, 25f, TextAlignmentOptions.Center);
            }

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
            CreateText(labelRect.gameObject, 24f, TextAlignmentOptions.Center);
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
            _tailFillRect = CreateRect("Tail Fill", _bubbleWrapper);
            _tailFill =
                _tailFillRect.gameObject.AddComponent<NovelTriangleGraphic>();
            _tailFill.raycastTarget = false;

            _bubbleBody = CreateRect("Body", _bubbleWrapper);
            Stretch(_bubbleBody);
            _bubbleGraphic =
                _bubbleBody.gameObject.AddComponent<NovelRoundedGraphic>();

            RectTransform speakerRect = CreateRect(
                "Speaker Name", _bubbleBody);
            _bubbleSpeakerText = CreateText(
                speakerRect.gameObject, 21f, TextAlignmentOptions.Left);

            RectTransform dialogueRect = CreateRect(
                "Dialogue Text", _bubbleBody);
            _bubbleDialogueText = CreateText(
                dialogueRect.gameObject, 24f, TextAlignmentOptions.TopLeft);
            _bubbleDialogueText.gameObject.AddComponent<NovelTextEffects>();

            _bubbleWrapper.gameObject.SetActive(false);
        }

        private void ConfigureBubble(RuntimeSpeechBubbleNode node)
        {
            NovelBoxStyle style = node.BubbleStyle.Validated();
            _bubbleGraphic.Apply(style);
            _bubbleDialogueText.SetText(node.DialogueText ?? string.Empty);
            _bubbleDialogueText.ForceMeshUpdate();

            float horizontal = Mathf.Max(0f, node.HorizontalPadding);
            float vertical = Mathf.Max(0f, node.VerticalPadding);
            float minimum = Mathf.Max(120f, node.MinimumWidth);
            float maximum = Mathf.Max(minimum, node.MaximumWidth);
            float width = Mathf.Clamp(
                _bubbleDialogueText.preferredWidth + horizontal * 2f,
                minimum, maximum);
            float textWidth = Mathf.Max(1f, width - horizontal * 2f);
            Vector2 preferred = _bubbleDialogueText.GetPreferredValues(
                node.DialogueText ?? string.Empty, textWidth, 10000f);
            float speakerHeight = 28f;
            float height = Mathf.Max(
                88f, preferred.y + vertical * 2f + speakerHeight);
            _bubbleWrapper.sizeDelta = new Vector2(width, height);

            RectTransform speakerRect = _bubbleSpeakerText.rectTransform;
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(1f, 1f);
            speakerRect.pivot = new Vector2(0.5f, 1f);
            speakerRect.anchoredPosition = new Vector2(0f, -vertical);
            speakerRect.sizeDelta = new Vector2(
                -horizontal * 2f, speakerHeight);

            RectTransform dialogueRect = _bubbleDialogueText.rectTransform;
            dialogueRect.anchorMin = Vector2.zero;
            dialogueRect.anchorMax = Vector2.one;
            dialogueRect.offsetMin = new Vector2(horizontal, vertical);
            dialogueRect.offsetMax = new Vector2(
                -horizontal, -(vertical + speakerHeight));

            float outline = style.OutlineEnabled
                ? style.OutlineThickness
                : 0f;
            _tailOutline.color = style.OutlineEnabled
                ? style.OutlineColor
                : style.EffectiveFillColor;
            _tailFill.color = style.EffectiveFillColor;
            _tailOutlineRect.sizeDelta = new Vector2(
                Mathf.Max(4f, node.TailWidth + outline * 2f),
                Mathf.Max(4f, node.TailLength + outline * 2f));
            _tailFillRect.sizeDelta = new Vector2(
                Mathf.Max(2f, node.TailWidth),
                Mathf.Max(2f, node.TailLength));
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
            _runner.NameBackground = _bubbleSpeakerText.gameObject;
            _runner.SpeakerNameText = _bubbleSpeakerText;
        }

        private void UpdateBubblePosition()
        {
            if (_trackedBubble == null || _bubbleWrapper == null)
                return;

            if (_trackedCharacter == null ||
                _trackedCharacter.transform is not RectTransform characterRect)
            {
                _bubbleWrapper.anchoredPosition = new Vector2(0f, 120f);
                SetTailVisible(false);
                return;
            }

            Vector3[] corners = new Vector3[4];
            characterRect.GetWorldCorners(corners);
            Vector3 worldTarget = (corners[1] + corners[2]) * 0.5f;
            Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                camera, worldTarget);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _bubbleLayer, screen, camera, out Vector2 target);

            Rect safe = _bubbleLayer.rect;
            Vector2 size = _bubbleWrapper.rect.size;
            float margin = Mathf.Max(8f, _trackedBubble.TargetMargin);
            float tailLength = Mathf.Max(2f, _trackedBubble.TailLength);
            Vector2 desired = new Vector2(
                target.x,
                target.y + margin + tailLength + size.y * 0.5f);

            if (desired.y + size.y * 0.5f > safe.yMax - margin)
            {
                desired.y = target.y - margin - tailLength - size.y * 0.5f;
            }

            desired.x = Mathf.Clamp(
                desired.x,
                safe.xMin + size.x * 0.5f + margin,
                safe.xMax - size.x * 0.5f - margin);
            desired.y = Mathf.Clamp(
                desired.y,
                safe.yMin + size.y * 0.5f + margin,
                safe.yMax - size.y * 0.5f - margin);
            _bubbleWrapper.anchoredPosition = desired;
            PositionTail(target - desired, size, _trackedBubble);
        }

        private void PositionTail(
            Vector2 targetFromBubble,
            Vector2 bubbleSize,
            RuntimeSpeechBubbleNode node)
        {
            SetTailVisible(true);
            float halfWidth = bubbleSize.x * 0.5f;
            float halfHeight = bubbleSize.y * 0.5f;
            float tailLength = Mathf.Max(2f, node.TailLength);
            float safeCorner = node.BubbleStyle.CornerRadius +
                node.TailWidth * 0.5f;
            Vector2 position;
            float rotation;

            if (Mathf.Abs(targetFromBubble.x) >
                Mathf.Abs(targetFromBubble.y))
            {
                float y = Mathf.Clamp(
                    targetFromBubble.y,
                    -halfHeight + safeCorner,
               halfHeight - safeCorner);
                if (targetFromBubble.x >= 0f)
                {
                    position = new Vector2(
                        halfWidth + tailLength * 0.5f, y);
                    rotation = 90f;
                }
                else
                {
                    position = new Vector2(
                        -halfWidth - tailLength * 0.5f, y);
                    rotation = -90f;
                }
            }
            else
            {
                float x = Mathf.Clamp(
                    targetFromBubble.x,
                    -halfWidth + safeCorner,
                    halfWidth - safeCorner);
                if (targetFromBubble.y >= 0f)
                {
                    position = new Vector2(
                        x, halfHeight + tailLength * 0.5f);
                    rotation = 180f;
                }
                else
                {
                    position = new Vector2(
                        x, -halfHeight - tailLength * 0.5f);
                    rotation = 0f;
                }
            }

            SetTailTransform(_tailOutlineRect, position, rotation);
            SetTailTransform(_tailFillRect, position, rotation);
        }

        private static void SetTailTransform(
        RectTransform rect,
        Vector2 position,
        float rotation)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void SetTailVisible(bool visible)
        {
            if (_tailOutlineRect != null)
                _tailOutlineRect.gameObject.SetActive(visible);
            if (_tailFillRect != null)
                _tailFillRect.gameObject.SetActive(visible);
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
            TextMeshProUGUI text,
            NovelTextAlignment alignment)
        {
            if (text == null) return;

            /*this allows setting or getting a value from a different enum using another as input
             Basically we ask which value is alignment set to?  based on that set the text alignment to the corresponding one
             */
            text.alignment = alignment switch
            {
                NovelTextAlignment.TopCenter => TextAlignmentOptions.Top,
                NovelTextAlignment.TopRight => TextAlignmentOptions.TopRight,
                NovelTextAlignment.CenterLeft => TextAlignmentOptions.Left,
                NovelTextAlignment.CenterCenter => TextAlignmentOptions.Center,
                NovelTextAlignment.CenterRight => TextAlignmentOptions.Right,
                NovelTextAlignment.BottomLeft => TextAlignmentOptions.BottomLeft,
                NovelTextAlignment.BottomCenter => TextAlignmentOptions.Bottom,
                NovelTextAlignment.BottomRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.TopLeft
            };
        }

        private static void ApplyTextSizing(
            TextMeshProUGUI text,
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
        private static TextMeshProUGUI CreateText(
            GameObject target,
            float size,
            TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = target.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
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
