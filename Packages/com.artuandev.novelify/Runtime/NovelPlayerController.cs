using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Novelify
{
    /// <summary>Ready-to-use VN controls. Add beside a NovelGraphRunner; replace it for a custom UI.</summary>
    [AddComponentMenu("Novelify/Player Controller")]
    [RequireComponent(typeof(NovelGraphRunner))]
    [DisallowMultipleComponent]
    public sealed class NovelPlayerController : MonoBehaviour
    {
        public bool AutoPlayOnStart = true;
        public bool ShowGeneratedControls = true;

        public bool AutoMode { get; private set; }
        public bool SkipMode { get; private set; }
        public bool IsOverlayOpen => (_backlogPanel != null && _backlogPanel.activeSelf) ||
                                     (_settingsPanel != null && _settingsPanel.activeSelf);
        public NovelPlayerPreferences Preferences { get; private set; }

        private NovelGraphRunner _runner;
        private GameObject _toolbar;
        private GameObject _backlogPanel;
        private GameObject _settingsPanel;
        private Text _backlogText;
        private ScrollRect _backlogScroll;
        private Text _autoLabel;
        private Text _skipLabel;
        private Text _speedLabel;
        private Text _delayLabel;
        private Text _volumeLabel;
        private Text _readLabel;
        private Button _autoButton;
        private Button _skipButton;
        private RuntimeDialogueNode _line;
        private bool _lineWasRead;
        private bool _lineEntered;
        private bool _wasRevealing;
        private string _skipStopReason;
        private float _readyAt;
#if ENABLE_LEGACY_INPUT_MANAGER
        private bool _hasInputSystemAdapter;
#endif
        private int _overlayClosedFrame = -1;

        private void Awake()
        {
            _runner = GetComponent<NovelGraphRunner>();
            Preferences = NovelPlayerPreferences.Load();
        }

        private void OnEnable()
        {
            _runner ??= GetComponent<NovelGraphRunner>();
            if (_toolbar != null) _toolbar.SetActive(true);
            _runner.Session.NodeEntered += OnNodeEntered;
            _runner.Session.DialoguePresented += OnDialoguePresented;
            _runner.Session.GraphStopped += OnGraphStopped;
        }

        private void OnDisable()
        {
            if (_toolbar != null) _toolbar.SetActive(false);
            CloseOverlays();
            if (_runner == null) return;
            _runner.Session.NodeEntered -= OnNodeEntered;
            _runner.Session.DialoguePresented -= OnDialoguePresented;
            _runner.Session.GraphStopped -= OnGraphStopped;
        }

        private void OnDestroy()
        {
            if (_toolbar != null) Destroy(_toolbar);
            if (_backlogPanel != null) Destroy(_backlogPanel);
            if (_settingsPanel != null) Destroy(_settingsPanel);
        }

        private void Start()
        {
            Preferences ??= NovelPlayerPreferences.Load();
            ApplyPreferences();
            if (ShowGeneratedControls) BuildControls();
            Type adapterType = Type.GetType("Novelify.InputSystem.NovelInputSystemPlayerInput, Novelify.InputSystem");
            if (adapterType != null && typeof(MonoBehaviour).IsAssignableFrom(adapterType))
            {
                if (GetComponent(adapterType) == null) gameObject.AddComponent(adapterType);
#if ENABLE_LEGACY_INPUT_MANAGER
                _hasInputSystemAdapter = true;
#endif
            }
            if (AutoPlayOnStart && !_runner.Session.IsRunning && _runner.RuntimeGraph != null)
                _runner.Session.Play(_runner.RuntimeGraph);
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!_hasInputSystemAdapter) ReadLegacyInput();
#endif
            if (!AutoMode && !SkipMode || IsOverlayOpen || !_runner.Session.IsRunning ||
                _line == null || _runner.Session.CurrentNode != _line) return;
            if (_line is RuntimeChoiceNode)
            {
                if (SkipMode) StopSkip("Choice");
                return;
            }
            if (SkipMode && Preferences.SkipReadOnly && !_lineWasRead)
            {
                StopSkip("Unread");
                return;
            }
            if (_runner.Session.IsTextRevealing)
            {
                _wasRevealing = true;
                if (SkipMode) _runner.Session.Advance();
                return;
            }
            if (_wasRevealing)
            {
                _wasRevealing = false;
                _readyAt = Time.unscaledTime + (SkipMode ? 0.12f : Preferences.AutoDelay);
            }
            if (Time.unscaledTime < _readyAt) return;
            _runner.Session.Advance();
            _readyAt = Time.unscaledTime + (SkipMode ? 0.12f : Preferences.AutoDelay);
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private void ReadLegacyInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { CloseOverlays(); return; }
            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton1)) ToggleBacklog();
            if (Input.GetKeyDown(KeyCode.PageUp)) ScrollBacklog(0.25f);
            if (Input.GetKeyDown(KeyCode.PageDown)) ScrollBacklog(-0.25f);
            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.JoystickButton7)) ToggleSettings();
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.JoystickButton2)) ToggleAuto();
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.JoystickButton3)) ToggleSkip();
            bool pointer = Input.GetMouseButtonDown(0) && !IsInteractiveUIAt(Input.mousePosition);
            for (int index = 0; index < Input.touchCount; index++)
            {
                Touch touch = Input.GetTouch(index);
                if (touch.phase == TouchPhase.Began && !IsInteractiveUIAt(touch.position)) pointer = true;
            }
            if (pointer || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.JoystickButton0)) Advance();
        }
#endif

        public void Advance()
        {
            if (IsOverlayOpen || _overlayClosedFrame == Time.frameCount) return;
            _runner.Session.Advance();
            _readyAt = Time.unscaledTime + (SkipMode ? 0.12f : Preferences?.AutoDelay ?? 2f);
        }

        /// <summary>Ignore button taps but allow taps on decorative dialogue graphics.</summary>
        public bool IsInteractiveUIAt(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;
            var data = new PointerEventData(EventSystem.current) { position = screenPosition };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            foreach (RaycastResult hit in hits)
                if (hit.gameObject != null && hit.gameObject.GetComponentInParent<Selectable>() != null)
                    return true;
            return false;
        }

        public void ToggleAuto() => SetAuto(!AutoMode);

        public void SetAuto(bool enabled)
        {
            AutoMode = enabled;
            if (enabled)
            {
                SkipMode = false;
                _skipStopReason = null;
            }
            _readyAt = Time.unscaledTime + (Preferences?.AutoDelay ?? 2f);
            UpdateLabels();
        }

        public void ToggleSkip() => SetSkip(!SkipMode);

        public void SetSkip(bool enabled)
        {
            SkipMode = enabled;
            _skipStopReason = null;
            if (enabled) AutoMode = false;
            _readyAt = Time.unscaledTime + 0.12f;
            UpdateLabels();
        }

        private void StopSkip(string reason)
        {
            SkipMode = false;
            _skipStopReason = reason;
            UpdateLabels();
        }

        public void ToggleBacklog()
        {
            if (_backlogPanel == null) return;
            bool show = !_backlogPanel.activeSelf;
            CloseOverlays();
            if (!show) return;
            _backlogPanel.SetActive(true);
            _backlogPanel.transform.SetAsLastSibling();
            RefreshBacklog();
            _backlogPanel.GetComponentInChildren<Button>()?.Select();
        }

        public void ToggleSettings()
        {
            if (_settingsPanel == null) return;
            bool show = !_settingsPanel.activeSelf;
            CloseOverlays();
            if (!show) return;
            _settingsPanel.SetActive(true);
            _settingsPanel.transform.SetAsLastSibling();
            _settingsPanel.GetComponentInChildren<Button>()?.Select();
        }

        public void ScrollBacklog(float amount)
        {
            if (_backlogPanel == null || !_backlogPanel.activeSelf || _backlogScroll == null) return;
            _backlogScroll.verticalNormalizedPosition = Mathf.Clamp01(
                _backlogScroll.verticalNormalizedPosition + amount);
        }

        public void CloseOverlays()
        {
            bool wasOpen = IsOverlayOpen;
            if (_backlogPanel != null) _backlogPanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            _readyAt = Time.unscaledTime + (Preferences?.AutoDelay ?? 2f);
            if (wasOpen)
            {
                _overlayClosedFrame = Time.frameCount;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void OnNodeEntered(RuntimeNovelGraph graph, RuntimeNode node)
        {
            _line = node as RuntimeDialogueNode;
            _lineEntered = _line != null;
            _lineWasRead = _line != null && _runner.StateStore.HasReadLine(
                !string.IsNullOrEmpty(_line.LineID) ? _line.LineID :
                NovelLocalizationKey.Dialogue(graph?.GraphID, _line.NodeID));
        }

        private void OnDialoguePresented(RuntimeNovelGraph graph, RuntimeDialogueNode node, string speaker)
        {
            if (!_lineEntered || _line != node)
                _lineWasRead = _runner.StateStore.HasReadLine(
                    !string.IsNullOrEmpty(node.LineID) ? node.LineID :
                    NovelLocalizationKey.Dialogue(graph?.GraphID, node.NodeID));
            _line = node;
            _lineEntered = false;
            _wasRevealing = _runner.Session.IsTextRevealing;
            _readyAt = Time.unscaledTime + (SkipMode ? 0.12f : Preferences?.AutoDelay ?? 2f);
            if (SkipMode && node is RuntimeChoiceNode) StopSkip("Choice");
            else if (SkipMode && Preferences.SkipReadOnly && !_lineWasRead)
                StopSkip("Unread");
        }

        private void OnGraphStopped(RuntimeNovelGraph graph)
        {
            _line = null;
            SetSkip(false);
            SetAuto(false);
        }

        public void ApplyPreferences()
        {
            Preferences ??= NovelPlayerPreferences.Load();
            Preferences.Clamp();
            _runner.TextSpeedMultiplier = Preferences.TextSpeedMultiplier;
            if (_runner.TalkSource != null) _runner.TalkSource.volume = Preferences.TalkVolume;
            Preferences.Save();
            UpdateLabels();
        }

        private void BuildControls()
        {
            Canvas canvas = _runner.CanvasDialogue != null
                ? _runner.CanvasDialogue.GetComponentInParent<Canvas>()
                : GetComponentInParent<Canvas>();
            if (canvas == null) return;
            RectTransform toolbar = MakeRect("Story controls", canvas.transform);
            _toolbar = toolbar.gameObject;
            toolbar.anchorMin = toolbar.anchorMax = new Vector2(1f, 1f);
            toolbar.pivot = new Vector2(1f, 1f);
            toolbar.anchoredPosition = new Vector2(-28f, -24f);
            toolbar.sizeDelta = new Vector2(548f, 60f);
            AddRounded(toolbar, new Color(0.035f, 0.055f, 0.10f, 0.93f), 20f);
            var row = toolbar.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(8, 8, 8, 8);
            row.spacing = 8f;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            row.childControlWidth = true;
            row.childControlHeight = true;
            NewButton(toolbar, "Backlog", ToggleBacklog);
            _autoLabel = NewButton(toolbar, "Auto", ToggleAuto);
            _autoButton = _autoLabel.GetComponentInParent<Button>();
            _skipLabel = NewButton(toolbar, "Skip", ToggleSkip);
            _skipButton = _skipLabel.GetComponentInParent<Button>();
            NewButton(toolbar, "Settings", ToggleSettings);
            toolbar.SetAsLastSibling();

            _backlogPanel = MakePanel("Backlog overlay", canvas.transform,
                "STORY HISTORY", "Backlog", out RectTransform backlogCard);
            RectTransform scrollHost = MakeRect("History Scroll", backlogCard);
            Stretch(scrollHost, new Vector2(0.07f, 0.13f), new Vector2(0.86f, 0.80f));
            var scroll = scrollHost.gameObject.AddComponent<ScrollRect>();
            _backlogScroll = scroll;
            RectTransform viewport = MakeRect("Viewport", scrollHost);
            Stretch(viewport, Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<Image>().color =
                new Color(0.06f, 0.09f, 0.16f, 0.55f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            RectTransform content = MakeRect("History", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 300f);
            _backlogText = MakeText(content, string.Empty, 25, TextAnchor.UpperLeft);
            _backlogText.color = new Color(0.88f, 0.92f, 0.98f);
            _backlogText.horizontalOverflow = HorizontalWrapMode.Wrap;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform scrollUp = MakeRect("Scroll Up", backlogCard);
            Stretch(scrollUp, new Vector2(0.88f, 0.52f), new Vector2(0.96f, 0.63f));
            NewButton(scrollUp, "Up", () => ScrollBacklog(0.25f));
            RectTransform scrollDown = MakeRect("Scroll Down", backlogCard);
            Stretch(scrollDown, new Vector2(0.88f, 0.38f), new Vector2(0.96f, 0.49f));
            NewButton(scrollDown, "Down", () => ScrollBacklog(-0.25f));
            RectTransform backlogHint = MakeRect("Backlog Hint", backlogCard);
            Stretch(backlogHint, new Vector2(0.07f, 0.04f), new Vector2(0.93f, 0.10f));
            Text backlogHintText = MakeText(backlogHint,
                "Scroll with wheel, Page Up / Page Down, or the buttons at right.",
                17, TextAnchor.MiddleLeft);
            backlogHintText.color = new Color(0.60f, 0.70f, 0.81f);
            _backlogPanel.SetActive(false);

            _settingsPanel = MakePanel("Preferences overlay", canvas.transform,
                "READING OPTIONS", "Preferences", out RectTransform settingsCard);
            RectTransform settings = MakeRect("Options", settingsCard);
            Stretch(settings, new Vector2(0.13f, 0.22f), new Vector2(0.87f, 0.78f));
            var column = settings.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 14f;
            column.childForceExpandHeight = true;
            column.childForceExpandWidth = true;
            column.childControlHeight = true;
            column.childControlWidth = true;
            _speedLabel = NewButton(settings, string.Empty, () =>
            {
                Preferences.TextSpeedMultiplier = Preferences.TextSpeedMultiplier >= 4f
                    ? 0.5f : Preferences.TextSpeedMultiplier * 2f;
                ApplyPreferences();
            });
            _delayLabel = NewButton(settings, string.Empty, () =>
            {
                Preferences.AutoDelay = Preferences.AutoDelay >= 4f ? 0.5f : Preferences.AutoDelay * 2f;
                ApplyPreferences();
            });
            _volumeLabel = NewButton(settings, string.Empty, () =>
            {
                Preferences.TalkVolume = Preferences.TalkVolume <= 0f ? 1f :
                    Mathf.Max(0f, Preferences.TalkVolume - 0.25f);
                ApplyPreferences();
            });
            _readLabel = NewButton(settings, string.Empty, () =>
            {
                Preferences.SkipReadOnly = !Preferences.SkipReadOnly;
                ApplyPreferences();
            });
            RectTransform hint = MakeRect("Settings Hint", settingsCard);
            Stretch(hint, new Vector2(0.13f, 0.10f), new Vector2(0.87f, 0.17f));
            Text hintText = MakeText(hint,
                "Select a row to change it. Read-only Skip stops at unread dialogue.",
                17, TextAnchor.MiddleCenter);
            hintText.color = new Color(0.60f, 0.70f, 0.81f);
            _settingsPanel.SetActive(false);
            UpdateLabels();
        }

        private void RefreshBacklog()
        {
            if (_backlogText == null) return;
            var builder = new StringBuilder();
            foreach (NovelHistoryEntryData entry in _runner.History)
            {
                if (entry == null) continue;
                if (builder.Length > 0) builder.Append("\n\n");
                if (!string.IsNullOrEmpty(entry.Speaker))
                    builder.Append("<b>").Append(entry.Speaker).Append("</b>\n");
                builder.Append(entry.ResolvedText);
            }
            _backlogText.text = builder.Length > 0 ? builder.ToString() : "No dialogue yet.";
            Canvas.ForceUpdateCanvases();
            _backlogText.rectTransform.sizeDelta = new Vector2(0f,
                Mathf.Max(300f, _backlogText.preferredHeight + 24f));
            Canvas.ForceUpdateCanvases();
            if (_backlogScroll != null) _backlogScroll.verticalNormalizedPosition = 0f;
        }

        private void UpdateLabels()
        {
            if (_autoLabel != null) _autoLabel.text = AutoMode ? "Auto: On" : "Auto: Off";
            if (_skipLabel != null) _skipLabel.text = SkipMode
                ? "Skip: On" : _skipStopReason == null ? "Skip: Off" : $"Skip: {_skipStopReason}";
            SetToggleAppearance(_autoButton, AutoMode);
            SetToggleAppearance(_skipButton, SkipMode);
            if (Preferences == null) return;
            if (_speedLabel != null) _speedLabel.text = $"Text speed: {Preferences.TextSpeedMultiplier:0.##}x";
            if (_delayLabel != null) _delayLabel.text = $"Auto delay: {Preferences.AutoDelay:0.##}s";
            if (_volumeLabel != null) _volumeLabel.text = $"Talk volume: {Preferences.TalkVolume:P0}";
            if (_readLabel != null) _readLabel.text = Preferences.SkipReadOnly
                ? "Skip: Read text only" : "Skip: All text";
        }

        private GameObject MakePanel(string name, Transform parent,
            string eyebrow, string title, out RectTransform card)
        {
            RectTransform root = MakeRect(name, parent);
            Stretch(root, Vector2.zero, Vector2.one);
            root.gameObject.AddComponent<Image>().color = new Color(0.015f, 0.025f, 0.05f, 0.78f);
            card = MakeRect("Panel", root);
            Stretch(card, new Vector2(0.19f, 0.13f), new Vector2(0.81f, 0.87f));
            AddRounded(card, new Color(0.055f, 0.082f, 0.14f, 0.99f), 30f,
                new Color(0.30f, 0.47f, 0.62f, 0.55f));
            RectTransform eyebrowRect = MakeRect("Eyebrow", card);
            Stretch(eyebrowRect, new Vector2(0.07f, 0.88f), new Vector2(0.70f, 0.93f));
            Text eyebrowText = MakeText(eyebrowRect, eyebrow, 17, TextAnchor.MiddleLeft);
            eyebrowText.color = new Color(0.45f, 0.85f, 0.87f);
            RectTransform heading = MakeRect("Heading", card);
            Stretch(heading, new Vector2(0.07f, 0.80f), new Vector2(0.75f, 0.89f));
            MakeText(heading, title, 37, TextAnchor.MiddleLeft);
            RectTransform close = MakeRect("Close", card);
            Stretch(close, new Vector2(0.82f, 0.83f), new Vector2(0.94f, 0.92f));
            NewButton(close, "Close", CloseOverlays);
            return root.gameObject;
        }

        private static Text NewButton(Transform parent, string label, Action action)
        {
            RectTransform rect = MakeRect(label + " Button", parent);
            Stretch(rect, Vector2.zero, Vector2.one);
            NovelRoundedGraphic graphic = AddRounded(rect,
                new Color(0.12f, 0.18f, 0.28f, 1f), 13f);
            graphic.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.72f, 0.78f, 0.85f, 1f);
            colors.disabledColor = new Color(0.5f, 0.55f, 0.6f, 0.55f);
            button.colors = colors;
            button.onClick.AddListener(() => action?.Invoke());
            RectTransform caption = MakeRect("Label", rect);
            Stretch(caption, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f));
            Text text = MakeText(caption, label, 21, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
            return text;
        }

        private static NovelRoundedGraphic AddRounded(RectTransform rect,
            Color fill, float radius, Color? outline = null)
        {
            var graphic = rect.gameObject.AddComponent<NovelRoundedGraphic>();
            NovelBoxStyle style = NovelBoxStyle.DialogueDefault;
            style.FillColor = fill;
            style.Opacity = 1f;
            style.CornerRadius = radius;
            style.OutlineEnabled = outline.HasValue;
            style.OutlineColor = outline ?? Color.clear;
            style.OutlineThickness = outline.HasValue ? 2f : 0f;
            graphic.Apply(style);
            return graphic;
        }

        private static void SetToggleAppearance(Button button, bool enabled)
        {
            if (button == null || button.targetGraphic is not NovelRoundedGraphic graphic) return;
            NovelBoxStyle style = NovelBoxStyle.DialogueDefault;
            style.FillColor = enabled
                ? new Color(0.09f, 0.43f, 0.47f, 1f)
                : new Color(0.12f, 0.18f, 0.28f, 1f);
            style.Opacity = 1f;
            style.CornerRadius = 13f;
            style.OutlineEnabled = enabled;
            style.OutlineColor = new Color(0.37f, 0.93f, 0.89f, 0.8f);
            style.OutlineThickness = 2f;
            graphic.Apply(style);
            graphic.raycastTarget = true;
        }

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            return result.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Text MakeText(RectTransform parent, string value, int size, TextAnchor alignment)
        {
            Text text = parent.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.text = value;
            text.supportRichText = true;
            text.raycastTarget = false;
            return text;
        }
    }
}
