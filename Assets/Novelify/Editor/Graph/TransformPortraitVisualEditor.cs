using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Novelify.Editor
{
    /// <summary>
    /// Adds small usability affordances to Graph Toolkit views without depending on
    /// its internal view types. Graph Toolkit currently exposes the models publicly,
    /// but keeps NodeView and PortView internal.
    /// </summary>
    [InitializeOnLoad]
    internal static class NovelGraphViewUsabilityBridge
    {
        private const string RootHookClass = "novelify-graph-double-click-hook-v2";
        private const string PortHookClass = "novelify-choice-port-hook-v2";
        private static double _nextScan;
        private static INode _lastClickedNode;
        private static double _lastClickTime;

        static NovelGraphViewUsabilityBridge() => EditorApplication.update += ScanOpenWindows;

        private static void ScanOpenWindows()
        {
            if (EditorApplication.timeSinceStartup < _nextScan) return;
            _nextScan = EditorApplication.timeSinceStartup + 0.5d;

            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                VisualElement root = window.rootVisualElement;
                if (root?.panel == null || window is not IGraphWindow graphWindow ||
                    graphWindow.Graph is not NovelGraph && graphWindow.Graph is not NovelFunctionGraph) continue;

                if (!root.ClassListContains(RootHookClass))
                {
                    root.AddToClassList(RootHookClass);
                    root.RegisterCallback<PointerDownEvent>(
                        evt => OnGraphPointerDown(evt, graphWindow),
                        TrickleDown.TrickleDown);
                }

                HideRedundantChoicePorts(root);
            }
        }

        private static void OnGraphPointerDown(PointerDownEvent evt, IGraphWindow graphWindow)
        {
            if (evt.button != 0 || graphWindow?.Graph == null) return;
            VisualElement element = evt.target as VisualElement;
            while (element != null)
            {
                if (IsViewType(element, "NodeView"))
                {
                    object nodeModel = ReadMember(element, "NodeModel");
                    INode node = FindPublicNode(graphWindow.Graph, nodeModel);
                    double now = EditorApplication.timeSinceStartup;
                    bool isDoubleClick = evt.clickCount >= 2 ||
                                         ReferenceEquals(node, _lastClickedNode) && now - _lastClickTime <= 0.45d;
                    _lastClickedNode = node;
                    _lastClickTime = now;
                    if (isDoubleClick && node is TransformSpeakerPortraitNode transform)
                    {
                        TransformPortraitVisualEditor.Open(transform);
                        evt.StopImmediatePropagation();
                    }
                    return;
                }
                element = element.parent;
            }
        }

        private static INode FindPublicNode(Graph graph, object nodeModel)
        {
            if (graph == null || nodeModel == null) return null;
            foreach (INode node in graph.GetNodes())
            {
                object publicNodeModel = ReadMember(node, "NodeModel") ?? ReadMember(node, "m_Implementation");
                if (ReferenceEquals(publicNodeModel, nodeModel)) return node;

                // Some Graph Toolkit revisions wrap the implementation once more.
                object nestedModel = ReadMember(publicNodeModel, "NodeModel");
                if (ReferenceEquals(nestedModel, nodeModel)) return node;
            }
            return null;
        }

        private static void HideRedundantChoicePorts(VisualElement element)
        {
            if (IsViewType(element, "Port") && !element.ClassListContains(PortHookClass))
            {
                object model = ReadMember(element, "PortModel");
                if (model is IPort port)
                {
                    element.AddToClassList(PortHookClass);
                    HideRedundantChoicePort(element, port);
                }
            }
            for (int i = 0; i < element.hierarchy.childCount; i++)
                HideRedundantChoicePorts(element.hierarchy[i]);
        }

        private static void HideRedundantChoicePort(VisualElement view, IPort port)
        {
            if (port.GetNode() is not ChoiceNode || port.IsConnected) return;
            string name = ReadStringMember(port, "UniqueName") ?? port.Name ?? string.Empty;
            string[] legacyPrefixes =
            {
                "Choice ID ", "Choice Text ", "Unavailable Policy ",
                "Disabled Reason ", "Once Only ", "Transaction "
            };

            foreach (string prefix in legacyPrefixes)
            {
                if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                view.style.display = DisplayStyle.None;
                return;
            }
        }

        private static bool IsViewType(VisualElement view, string typeName)
        {
            Type type = view.GetType();
            while (type != null)
            {
                if (type.Name == typeName && type.Namespace == "Unity.GraphToolkit.Editor") return true;
                type = type.BaseType;
            }
            return false;
        }

        private static object ReadMember(object source, string name)
        {
            if (source == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = source.GetType();
            while (type != null)
            {
                try
                {
                    foreach (PropertyInfo property in type.GetProperties(flags | BindingFlags.DeclaredOnly))
                        if (property.GetIndexParameters().Length == 0 &&
                            (property.Name == name || property.Name.EndsWith("." + name, StringComparison.Ordinal)))
                            return property.GetValue(source);

                    FieldInfo field = type.GetField(name, flags | BindingFlags.DeclaredOnly);
                    if (field != null) return field.GetValue(source);
                }
                catch { /* A view can be between model detach and panel removal. */ }
                type = type.BaseType;
            }
            return null;
        }

        private static string ReadStringMember(object source, string name) => ReadMember(source, name) as string;
    }

    internal sealed class TransformPortraitVisualEditor : EditorWindow
    {
        private enum TransformGesture { None, Move, Scale, Rotate }

        private struct EditorSnapshot
        {
            public PortraitState Target;
            public bool TargetSeededFromStart;
            public float Duration;
            public PortraitTweenEasing Easing;
            public AnimationCurve CustomCurve;
            public bool Wait;
            public bool Clamp;
            public CharacterPositionSpace PositionSpace;
            public bool Relative;
            public bool AnimateTransform;
            public float Margin;
            public float MarginOpacity;
            public string InstanceID;
            public GameObject UIPreviewSource;
            public bool UIPreviewVisible;
        }

        private readonly struct HistoryEntry
        {
            public readonly EditorSnapshot Snapshot;
            public readonly string Label;

            public HistoryEntry(EditorSnapshot snapshot, string label)
            {
                Snapshot = snapshot;
                Label = label;
            }
        }

        private readonly struct PortraitState
        {
            public readonly Vector2 Position;
            public readonly float Rotation;
            public readonly Vector2 Scale;

            public PortraitState(Vector2 position, float rotation, Vector2 scale)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }

            public static PortraitState Lerp(PortraitState from, PortraitState to, float t) =>
                new PortraitState(
                    Vector2.LerpUnclamped(from.Position, to.Position, t),
                    from.Rotation + Mathf.DeltaAngle(from.Rotation, to.Rotation) * t,
                    Vector2.LerpUnclamped(from.Scale, to.Scale, t));
        }

        private static readonly Color Background = new Color32(12, 19, 32, 255);
        private static readonly Color Panel = new Color32(24, 33, 48, 255);
        private static readonly Color PanelRaised = new Color32(31, 43, 61, 255);
        private static readonly Color Border = new Color32(71, 85, 105, 255);
        private static readonly Color Text = new Color32(226, 232, 240, 255);
        private static readonly Color Muted = new Color32(148, 163, 184, 255);
        private static readonly Color Accent = new Color32(56, 189, 248, 255);
        private static readonly Color StartAccent = new Color32(167, 139, 250, 255);
        private static readonly Color SafeAccent = new Color32(52, 211, 153, 190);
        private const string UIPreviewSourcePrefsPrefix =
            "Novelify.TransformPortraitVisualEditor.GlobalUIPreviewSource.";
        private const string UIPreviewVisiblePrefsPrefix =
            "Novelify.TransformPortraitVisualEditor.GlobalUIPreviewVisible.";
        private const string MarginOpacityPrefsPrefix =
            "Novelify.TransformPortraitVisualEditor.MarginOpacity.";
        private const string ViewportZoomPrefsPrefix =
            "Novelify.TransformPortraitVisualEditor.ViewportZoom.";
        private static Delegate _globalUndoHandler;
        private static bool _globalUndoInstalled;
        private static readonly Dictionary<Sprite, Rect> SpriteAlphaBoundsCache = new Dictionary<Sprite, Rect>();

        private TransformSpeakerPortraitNode _node;
        private Graph _graph;
        private NovelCharacter _character;
        private string _instanceID;
        private PortraitState _start;
        private PortraitState _target;
        private Vector2 _authoredPosition;
        private string _startSource;
        private Vector2Int _resolution;
        private bool _targetSeededFromStart;
        private PortraitTweenEasing _easing;
        private AnimationCurve _customCurve;
        private CharacterPositionSpace _positionSpace;
        private bool _relative;
        private bool _animateTransform;
        private float _margin;
        [SerializeField] private float _marginOpacity = 0.35f;
        [SerializeField] private float _viewportZoom = 1f;
        private Vector2 _portraitCanvasSize;
        private Vector2 _stageCanvasSize;
        private string _portraitSizeSource;
        private Rect _portraitContentRect = new Rect(0f, 0f, 1f, 1f);

        private VisualElement _previewHost;
        private VisualElement _screen;
        private VisualElement _gameViewport;
        private VisualElement _safeArea;
        private VisualElement _ghost;
        private VisualElement _targetPortrait;
        private VisualElement _uiPreviewOverlay;
        private VisualElement _transformFrame;
        private Label _resolutionLabel;
        private Label _coordinateLabel;
        private Label _marginGuideLabel;
        private Label _startSourceLabel;
        private Label _previewTimeLabel;
        private Label _statusLabel;
        private HelpBox _connectedHelp;
        private Vector2Field _positionField;
        private FloatField _rotationField;
        private Vector2Field _scaleField;
        private FloatField _durationField;
        private ObjectField _UIPreview;
        [SerializeField] private GameObject _uiPreviewSource;
        private Toggle _uiPreviewVisibleToggle;
        [SerializeField] private bool _uiPreviewVisible = true;
        [SerializeField] private bool _usesStandardWindowChrome;
        private DropdownField _easingDropdown;
        private CurveField _customCurveField;
        private VisualElement _customCurveContainer;
        private Toggle _waitToggle;
        private DropdownField _positionSpaceDropdown;
        private Toggle _relativeToggle;
        private Toggle _animateTransformToggle;
        private FloatField _marginField;
        private Slider _marginOpacitySlider;
        private Slider _viewportZoomSlider;
        private TextField _instanceIDField;
        private Label _portraitSizeLabel;
        private Toggle _safeAreaToggle;
        private Toggle _clampToggle;
        private DropdownField _resolutionDropdown;
        private Slider _timeline;
        private Button _previewButton;
        private Button _undoButton;
        private Button _redoButton;

        private bool _updatingFields;
        private TransformGesture _gesture;
        private PortraitState _gestureStartState;
        private Vector2 _gestureStartPointer;
        private Vector2 _scaleHandleDirection;
        private Vector2 _fixedScaleCorner;
        private float _gestureStartAngle;
        private bool _moveAxisLocked;
        private bool _moveHorizontal;
        private bool _gestureUndoRecorded;
        private bool _followGameViewResolution = true;
        private double _nextResolutionCheck;
        private bool _playingPreview;
        private bool _previewPaused;
        private bool _previewCompleted;
        private double _previewStartTime;
        private float _previewStartProgress;
        private readonly List<HistoryEntry> _undoHistory = new List<HistoryEntry>();
        private readonly List<HistoryEntry> _redoHistory = new List<HistoryEntry>();
        private bool _historyReady;
        private bool _applyingHistory;
        private double _lastLocalShortcutTime = -1d;

        static TransformPortraitVisualEditor() => InstallGlobalUndoHandler();

        private static void InstallGlobalUndoHandler()
        {
            try
            {
                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                FieldInfo field = typeof(EditorApplication).GetField("globalEventHandler", flags);
                MethodInfo callback = typeof(TransformPortraitVisualEditor).GetMethod(
                    nameof(HandleGlobalEditorEvent), flags);
                if (field != null && typeof(Delegate).IsAssignableFrom(field.FieldType))
                {
                    _globalUndoHandler = Delegate.CreateDelegate(field.FieldType, callback);
                    Delegate current = field.GetValue(null) as Delegate;
                    field.SetValue(null, Delegate.Combine(current, _globalUndoHandler));
                    _globalUndoInstalled = true;
                    return;
                }

                EventInfo eventInfo = typeof(EditorApplication).GetEvent("globalEventHandler", flags);
                if (eventInfo?.EventHandlerType == null) return;
                _globalUndoHandler = Delegate.CreateDelegate(eventInfo.EventHandlerType, callback);
                eventInfo.AddEventHandler(null, _globalUndoHandler);
                _globalUndoInstalled = true;
            }
            catch
            {
                // The window-level UI Toolkit handler below remains as a fallback
                // if a future Unity release removes this internal event router.
                _globalUndoInstalled = false;
            }
        }

        private static void HandleGlobalEditorEvent()
        {
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.KeyDown || (!evt.control && !evt.command)) return;
            bool undo = evt.keyCode == KeyCode.Z && !evt.shift;
            bool redo = evt.keyCode == KeyCode.Y || evt.keyCode == KeyCode.Z && evt.shift;
            if (!undo && !redo) return;

            if (EditorWindow.mouseOverWindow is not TransformPortraitVisualEditor window) return;
            window._lastLocalShortcutTime = EditorApplication.timeSinceStartup;
            if (redo) window.PerformLocalRedo();
            else window.PerformLocalUndo();
            evt.Use();
        }

        public static void Open(TransformSpeakerPortraitNode node)
        {
            if (node == null) return;
            GameObject rememberedUIPreview = null;
            bool rememberedUIPreviewVisible = true;
            bool foundExistingWindow = false;
            TransformPortraitVisualEditor window = null;
            foreach (TransformPortraitVisualEditor existing in
                     Resources.FindObjectsOfTypeAll<TransformPortraitVisualEditor>())
            {
                foundExistingWindow = true;
                if (existing._uiPreviewSource != null)
                    rememberedUIPreview = existing._uiPreviewSource;
                rememberedUIPreviewVisible = existing._uiPreviewVisible;
                if (existing._usesStandardWindowChrome && window == null)
                    window = existing;
                else
                    existing.Close();
            }
            if (rememberedUIPreview != null)
                SaveUIPreviewSource(rememberedUIPreview);
            if (foundExistingWindow)
                EditorPrefs.SetBool(GetUIPreviewVisiblePrefsKey(), rememberedUIPreviewVisible);

            if (window == null)
            {
                window = CreateWindow<TransformPortraitVisualEditor>();
                window._usesStandardWindowChrome = true;
            }
            window.minSize = new Vector2(720f, 480f);
            window._node = node;
            window._graph = node.Graph;
            window._followGameViewResolution = true;
            window.titleContent = new GUIContent("Portrait Tween", EditorGUIUtility.IconContent("Animation.Play").image);
            window.Rebuild();
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            EditorApplication.update -= TickPreview;
            EditorApplication.update += TickPreview;
            EditorApplication.delayCall -= RebuildAfterDomainReload;
            if (_node != null) EditorApplication.delayCall += RebuildAfterDomainReload;
        }

        private void OnDisable()
        {
            EditorApplication.update -= TickPreview;
            EditorApplication.delayCall -= RebuildAfterDomainReload;
        }

        private void RebuildAfterDomainReload()
        {
            if (this == null || _node == null) return;
            try
            {
                Rebuild();
            }
            catch (NullReferenceException)
            {
                rootVisualElement.Clear();
                rootVisualElement.style.backgroundColor = Background;
                HelpBox staleNode = new HelpBox(
                    "The graph was reloaded while this composer was open. Double-click the Transform Speaker Portrait node again to reconnect it.",
                    HelpBoxMessageType.Warning);
                staleNode.style.marginLeft = 16f;
                staleNode.style.marginRight = 16f;
                staleNode.style.marginTop = 16f;
                rootVisualElement.Add(staleNode);
            }
        }

        private void OnFocus() => RefreshAutomaticResolution(true);

        private void Rebuild()
        {
            if (_node == null) return;
            _historyReady = false;
            _graph = _node.Graph;
            ResolveAuthoredValues();
            bool untouchedDefaultTarget = HasUntouchedDefaultTarget();
            if (!TryGetSelectedGameViewResolution(out _resolution))
                _resolution = new Vector2Int(1920, 1080);
            ResolveStartState();
            _targetSeededFromStart = untouchedDefaultTarget;
            _target = new PortraitState(
                _targetSeededFromStart
                    ? _start.Position
                    : AuthoredToVisualPosition(_authoredPosition),
                _target.Rotation,
                _target.Scale);
            ResolvePortraitCanvasSize();
            _marginOpacity = EditorPrefs.GetFloat(GetMarginOpacityPrefsKey(), _marginOpacity);
            _viewportZoom = Mathf.Clamp(
                EditorPrefs.GetFloat(GetViewportZoomPrefsKey(), _viewportZoom),
                0.15f,
                2.5f);
            GameObject savedUIPreview = LoadUIPreviewSource();
            if (savedUIPreview != null)
                _uiPreviewSource = savedUIPreview;
            else if (_uiPreviewSource != null)
                SaveUIPreviewSource(_uiPreviewSource);
            string visibilityKey = GetUIPreviewVisiblePrefsKey();
            if (EditorPrefs.HasKey(visibilityKey))
                _uiPreviewVisible = LoadUIPreviewVisibility();
            else
                EditorPrefs.SetBool(visibilityKey, _uiPreviewVisible);

            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = Background;
            rootVisualElement.style.color = Text;
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            BuildHeader();
            BuildWorkspace();
            BuildFooter();
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnWindowKeyDown, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnWindowKeyDown, TrickleDown.TrickleDown);
            RefreshAll();
            _undoHistory.Clear();
            _redoHistory.Clear();
            _historyReady = true;
            RefreshHistoryButtons();
        }

        private void BuildHeader()
        {
            VisualElement header = new VisualElement();
            header.style.height = 58f;
            header.style.flexShrink = 0f;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 18f;
            header.style.paddingRight = 18f;
            header.style.backgroundColor = Panel;
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = Border;

            VisualElement titles = new VisualElement();
            titles.style.flexGrow = 1f;
            Label title = new Label("Portrait Tween Composer");
            title.style.fontSize = 17f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            Label subtitle = new Label("Compose the target visually, preview the motion, then confirm it on the node.");
            subtitle.style.fontSize = 11f;
            subtitle.style.color = Muted;
            titles.Add(title);
            titles.Add(subtitle);
            header.Add(titles);

            _resolutionLabel = Badge(string.Empty, Accent);
            header.Add(_resolutionLabel);
            rootVisualElement.Add(header);
        }

        private void BuildWorkspace()
        {
            VisualElement workspace = new VisualElement();
            workspace.style.flexGrow = 1f;
            workspace.style.flexDirection = FlexDirection.Row;
            workspace.style.minHeight = 0f;
            rootVisualElement.Add(workspace);

            BuildPreview(workspace);
            BuildInspector(workspace);
        }

        private void BuildPreview(VisualElement parent)
        {
            _previewHost = new VisualElement();
            _previewHost.style.flexGrow = 1f;
            _previewHost.style.minWidth = 280f;
            _previewHost.style.alignItems = Align.Center;
            _previewHost.style.justifyContent = Justify.Center;
            _previewHost.style.paddingLeft = 24f;
            _previewHost.style.paddingRight = 24f;
            _previewHost.style.paddingTop = 24f;
            _previewHost.style.paddingBottom = 24f;
            _previewHost.style.overflow = Overflow.Hidden;
            _previewHost.RegisterCallback<GeometryChangedEvent>(_ => FitScreen());
            _previewHost.RegisterCallback<WheelEvent>(OnViewportWheel, TrickleDown.TrickleDown);
            parent.Add(_previewHost);

            _screen = new VisualElement();
            _screen.style.position = UnityEngine.UIElements.Position.Relative;
            _screen.style.overflow = Overflow.Visible;
            SetBorder(_screen, Border, 1f);
            _screen.focusable = true;
            _screen.tooltip = "Drag the portrait to move it. Pull corners to scale and drag a side to rotate.";
            _screen.RegisterCallback<PointerDownEvent>(OnStagePointerDown);
            _screen.RegisterCallback<PointerMoveEvent>(OnStagePointerMove);
            _screen.RegisterCallback<PointerUpEvent>(OnStagePointerUp);
            _screen.RegisterCallback<PointerCaptureOutEvent>(_ => EndGesture());
            _screen.RegisterCallback<KeyDownEvent>(OnStageKeyDown);
            _screen.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (evt.newRect.width > 0f && evt.newRect.height > 0f)
                {
                    ApplyViewportLayout(evt.newRect.width, evt.newRect.height);
                    if (_uiPreviewSource != null)
                        SetUIPreviewSource(_uiPreviewSource, false);

                    RefreshPortraits();
                }
            });
            _previewHost.Add(_screen);

            _gameViewport = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            _gameViewport.style.position = UnityEngine.UIElements.Position.Absolute;
            _gameViewport.style.overflow = Overflow.Hidden;
            _gameViewport.style.backgroundColor = (Color)new Color32(5, 13, 27, 255);
            SetBorder(_gameViewport, Accent, 1f);
            _screen.Add(_gameViewport);

            AddGridLine(true, 0.25f, 0.25f);
            AddGridLine(true, 0.5f, 0.55f);
            AddGridLine(true, 0.75f, 0.25f);
            AddGridLine(false, 0.25f, 0.25f);
            AddGridLine(false, 0.5f, 0.55f);
            AddGridLine(false, 0.75f, 0.25f);
            AddEdgeLabel("(-1, +1)", 7f, 5f, true);
            AddEdgeLabel("(+1, +1)", 7f, 5f, false);
            AddEdgeLabel("(-1, -1)", 7f, 20f, true, true);
            AddEdgeLabel("(+1, -1)", 7f, 20f, false, true);

            _marginGuideLabel = Badge(string.Empty, Muted);
            _marginGuideLabel.style.position = UnityEngine.UIElements.Position.Absolute;
            _marginGuideLabel.style.left = Length.Percent(50f);
            _marginGuideLabel.style.translate = new Translate(Length.Percent(-50f), 0f);
            _marginGuideLabel.style.top = 5f;
            _marginGuideLabel.pickingMode = PickingMode.Ignore;
            _screen.Add(_marginGuideLabel);

            _safeArea = new VisualElement();
            _safeArea.style.position = UnityEngine.UIElements.Position.Absolute;
            _safeArea.style.borderLeftWidth = 1f;
            _safeArea.style.borderRightWidth = 1f;
            _safeArea.style.borderTopWidth = 1f;
            _safeArea.style.borderBottomWidth = 1f;
            _safeArea.style.borderLeftColor = SafeAccent;
            _safeArea.style.borderRightColor = SafeAccent;
            _safeArea.style.borderTopColor = SafeAccent;
            _safeArea.style.borderBottomColor = SafeAccent;
            _safeArea.pickingMode = PickingMode.Ignore;
            _gameViewport.Add(_safeArea);

            _ghost = CreatePortraitGroup(0.24f, StartAccent, "START");
            _ghost.pickingMode = PickingMode.Ignore;
            _screen.Add(_ghost);
            _targetPortrait = CreatePortraitGroup(1f, Accent, "TARGET");
            _targetPortrait.pickingMode = PickingMode.Ignore;
            _screen.Add(_targetPortrait);

            // Additive preview: this displays the selected scene UI on top of
            // the existing portrait preview without modifying it.
            _uiPreviewOverlay = CreateUIPreviewOverlay();
            _screen.Add(_uiPreviewOverlay);

            _transformFrame = CreateTransformFrame();
            _screen.Add(_transformFrame);

            _coordinateLabel = Badge(string.Empty, Accent);
            _coordinateLabel.style.position = UnityEngine.UIElements.Position.Absolute;
            _coordinateLabel.style.left = 8f;
            _coordinateLabel.style.bottom = 8f;
            _screen.Add(_coordinateLabel);
        }

        private void BuildInspector(VisualElement parent)
        {
            ScrollView inspector = new ScrollView();
            inspector.style.width = 330f;
            inspector.style.flexShrink = 0f;
            inspector.style.backgroundColor = Panel;
            inspector.style.borderLeftWidth = 1f;
            inspector.style.borderLeftColor = Border;
            inspector.contentContainer.style.paddingLeft = 14f;
            inspector.contentContainer.style.paddingRight = 14f;
            inspector.contentContainer.style.paddingTop = 14f;
            inspector.contentContainer.style.paddingBottom = 14f;
            parent.Add(inspector);

            AddSectionTitle(inspector, "TARGET TRANSFORM");
            if (_targetSeededFromStart)
            {
                HelpBox visualStart = new HelpBox(
                    "This node still has its untouched default transform, so the visual target begins at the incoming START position. The node itself is not changed until you click Confirm Tween.",
                    HelpBoxMessageType.Info);
                visualStart.style.marginBottom = 8f;
                inspector.Add(visualStart);
            }
            _positionField = new Vector2Field("Position (-1 to +1)") { value = _target.Position };
            _positionField.tooltip = "Normalized screen coordinates. Center is (0, 0).";
            _positionField.RegisterValueChangedCallback(evt => SetTarget(new PortraitState(evt.newValue, _target.Rotation, _target.Scale)));
            inspector.Add(_positionField);

            _rotationField = new FloatField("Rotation") { value = _target.Rotation };
            _rotationField.tooltip = "Clockwise/counter-clockwise Z rotation in degrees.";
            _rotationField.RegisterValueChangedCallback(evt => SetTarget(new PortraitState(_target.Position, evt.newValue, _target.Scale)));
            inspector.Add(_rotationField);

            _scaleField = new Vector2Field("Scale") { value = _target.Scale };
            _scaleField.tooltip = "Independent horizontal and vertical portrait scale.";
            _scaleField.RegisterValueChangedCallback(evt => SetTarget(new PortraitState(_target.Position, _target.Rotation, evt.newValue)));
            inspector.Add(_scaleField);

            _marginField = new FloatField("Off-screen margin") { value = _margin };
            _marginField.tooltip = "Extra canvas units beyond every game-screen edge. The shaded bands in the stage show this reachable margin.";
            _marginField.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                EditorSnapshot before = CaptureSnapshot();
                before.Margin = Mathf.Max(0f, evt.previousValue);
                RecordLocalUndo(before, "Change off-screen margin");
                SetMargin(evt.newValue);
            });
            inspector.Add(_marginField);

            _clampToggle = new Toggle("Keep target center on screen") { value = true };
            _clampToggle.tooltip = "Constrains the target center to the screen plus the authored off-screen margin.";
            _clampToggle.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                EditorSnapshot before = CaptureSnapshot();
                before.Clamp = evt.previousValue;
                RecordLocalUndo(before, "Change screen clamp");
            });
            inspector.Add(_clampToggle);

            AddSectionTitle(inspector, "NODE SETTINGS");
            _positionSpaceDropdown = new DropdownField(
                "Coordinate space",
                new List<string> { "Normalized", "Canvas" },
                _positionSpace == CharacterPositionSpace.Canvas ? 1 : 0);
            _positionSpaceDropdown.tooltip = "Controls how the visual target is written back to the node. The stage remains a visual, resolution-independent preview.";
            _positionSpaceDropdown.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                RecordLocalUndo("Change coordinate space");
                _positionSpace = evt.newValue == "Canvas"
                    ? CharacterPositionSpace.Canvas
                    : CharacterPositionSpace.Normalized;
                RefreshPositionFieldLabel();
            });
            inspector.Add(_positionSpaceDropdown);

            _relativeToggle = new Toggle("Relative to incoming position") { value = _relative };
            _relativeToggle.tooltip = "When enabled, Confirm Tween stores the displacement from START instead of an absolute position.";
            _relativeToggle.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                EditorSnapshot before = CaptureSnapshot();
                before.Relative = evt.previousValue;
                RecordLocalUndo(before, "Change relative positioning");
                _relative = evt.newValue;
                RefreshPositionFieldLabel();
            });
            inspector.Add(_relativeToggle);

            _instanceIDField = new TextField("Instance ID") { value = _instanceID ?? string.Empty };
            _instanceIDField.tooltip = "Targets an additional instance of this character. A connected Character Reference supplies its own instance ID.";
            bool referenceConnected = _node.GetInputPortByName("Character Reference")?.IsConnected == true;
            _instanceIDField.SetEnabled(!referenceConnected);
            _instanceIDField.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                EditorSnapshot before = CaptureSnapshot();
                before.InstanceID = evt.previousValue;
                RecordLocalUndo(before, "Change instance ID");
                _instanceID = evt.newValue ?? string.Empty;
                ResolveStartState();
                if (_targetSeededFromStart)
                    _target = new PortraitState(_start.Position, _target.Rotation, _target.Scale);
                ResolvePortraitCanvasSize();
                RefreshAll();
            });
            inspector.Add(_instanceIDField);

            AddSectionTitle(inspector, "QUICK PLACEMENT");
            DropdownField preset = new DropdownField("Preset", new List<string>
            {
                "Choose…", "Center", "Left", "Right", "Top", "Bottom", "Off-screen left", "Off-screen right"
            }, 0);
            preset.RegisterValueChangedCallback(evt =>
            {
                ApplyPreset(evt.newValue);
                preset.SetValueWithoutNotify("Choose…");
            });
            inspector.Add(preset);

            VisualElement nudge = Row();
            nudge.Add(new Label("Nudge") { style = { minWidth = 105f, unityTextAlign = TextAnchor.MiddleLeft } });
            nudge.Add(new Button(() => Nudge(Vector2.left)) { text = "←", tooltip = "Nudge left" });
            nudge.Add(new Button(() => Nudge(Vector2.up)) { text = "↑", tooltip = "Nudge up" });
            nudge.Add(new Button(() => Nudge(Vector2.down)) { text = "↓", tooltip = "Nudge down" });
            nudge.Add(new Button(() => Nudge(Vector2.right)) { text = "→", tooltip = "Nudge right" });
            inspector.Add(nudge);

            VisualElement resetRow = Row();
            Button resetStart = new Button(() => SetTarget(_start, true, "Set target to start")) { text = "Target = Start" };
            resetStart.style.flexGrow = 1f;
            Button resetTransform = new Button(() => SetTarget(
                new PortraitState(Vector2.zero, 0f, Vector2.one), true, "Reset transform"))
            { text = "Reset" };
            resetTransform.style.flexGrow = 1f;
            resetRow.Add(resetStart);
            resetRow.Add(resetTransform);
            inspector.Add(resetRow);

            AddSectionTitle(inspector, "UI PREVIEW");

            _UIPreview = new ObjectField("UI to preview (global)")
            {
                objectType = typeof(GameObject),
                value = _uiPreviewSource,
                allowSceneObjects = true
            };
            _UIPreview.tooltip =
                "Assign any active scene UI GameObject below a Canvas. This project-wide selection is reused by every portrait tween node.";

            _UIPreview.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                GameObject selectedObject = evt.newValue as GameObject;

                if (selectedObject != null)
                {
                    bool isValid =
                        selectedObject.GetComponent<RectTransform>() != null &&
                        selectedObject.GetComponentInParent<Canvas>(true) != null &&
                        !EditorUtility.IsPersistent(selectedObject) &&
                        selectedObject.scene.IsValid();

                    if (!isValid)
                    {
                        _UIPreview.SetValueWithoutNotify(_uiPreviewSource);
                        Debug.LogWarning(
                            "Please assign a UI GameObject from the current scene.");
                        return;
                    }
                }

                RecordLocalUndo("Change global UI preview");
                SetUIPreviewSource(selectedObject);
            });

            inspector.Add(_UIPreview);
            _uiPreviewVisibleToggle = new Toggle("Show UI preview")
            {
                value = _uiPreviewVisible
            };
            _uiPreviewVisibleToggle.tooltip =
                "Show or hide the selected scene UI in the preview.";
            _uiPreviewVisibleToggle.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                RecordLocalUndo("Toggle global UI preview");
                SetUIPreviewVisibility(evt.newValue);
            });
            inspector.Add(_uiPreviewVisibleToggle);
            HelpBox uiPreviewHelp = new HelpBox(
                "The selected UI object and visibility are shared by every Transform Speaker Portrait node in this Unity project.",
                HelpBoxMessageType.Info);
            uiPreviewHelp.style.marginTop = 5f;
            inspector.Add(uiPreviewHelp);
            SetUIPreviewSource(_uiPreviewSource, false);

            AddSectionTitle(inspector, "ANIMATION");
            _animateTransformToggle = new Toggle("Animate transform") { value = _animateTransform };
            _animateTransformToggle.tooltip = "Disable to apply the transform immediately at runtime. The composer preview remains available for positioning.";
            _animateTransformToggle.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                EditorSnapshot before = CaptureSnapshot();
                before.AnimateTransform = evt.previousValue;
                RecordLocalUndo(before, "Change animation mode");
                _animateTransform = evt.newValue;
                RefreshAnimationControls();
            });
            inspector.Add(_animateTransformToggle);

            _durationField = new FloatField("Duration (seconds)") { value = GetOption("Duration", 0.5f) };
            _durationField.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                EditorSnapshot before = CaptureSnapshot();
                before.Duration = evt.previousValue;
                RecordLocalUndo(before, "Change duration");
                _durationField.SetValueWithoutNotify(Mathf.Clamp(evt.newValue, 0f, 60f));
            });
            inspector.Add(_durationField);

            _easingDropdown = new DropdownField("Timing", EasingNames(), EasingToIndex(_easing));
            _easingDropdown.tooltip = "None is linear. Presets change timing only; Custom exposes an editable 0-to-1 curve.";
            _easingDropdown.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                EditorSnapshot before = CaptureSnapshot();
                before.Easing = EasingFromName(evt.previousValue);
                RecordLocalUndo(before, "Change timing preset");
                _easing = EasingFromName(evt.newValue);
                RefreshEasingControls();
                RestartPreviewAtCurrentPosition();
            });
            inspector.Add(_easingDropdown);

            _customCurveContainer = new VisualElement();
            _customCurveContainer.style.marginTop = 5f;
            _customCurveField = new CurveField("Custom curve")
            {
                value = CloneCurve(_customCurve),
                ranges = new Rect(0f, 0f, 1f, 1f)
            };
            _customCurveField.tooltip = "Double-click the curve to add points. The endpoints remain fixed at (0,0) and (1,1).";
            _customCurveField.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                EditorSnapshot before = CaptureSnapshot();
                before.CustomCurve = CloneCurve(evt.previousValue);
                RecordLocalUndo(before, "Edit custom timing curve");
                _customCurve = SanitizeCustomCurve(evt.newValue);
                _customCurveField.SetValueWithoutNotify(CloneCurve(_customCurve));
                RestartPreviewAtCurrentPosition();
            });
            _customCurveContainer.Add(_customCurveField);
            Label curveHint = new Label("Time runs left to right; transform progress runs bottom to top. Double-click to add keys.");
            curveHint.style.whiteSpace = WhiteSpace.Normal;
            curveHint.style.fontSize = 10f;
            curveHint.style.color = Muted;
            _customCurveContainer.Add(curveHint);
            inspector.Add(_customCurveContainer);

            _waitToggle = new Toggle("Wait for completion") { value = GetOption("Wait For Completion", true) };
            _waitToggle.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                EditorSnapshot before = CaptureSnapshot();
                before.Wait = evt.previousValue;
                RecordLocalUndo(before, "Change wait behavior");
            });
            inspector.Add(_waitToggle);

            AddSectionTitle(inspector, "VIEW GUIDES");
            _resolutionDropdown = new DropdownField("Preview resolution", ResolutionNames(), 0);
            _resolutionDropdown.tooltip = "Game View (Auto) follows the currently selected Game View resolution live.";
            _resolutionDropdown.RegisterValueChangedCallback(evt => SetResolution(evt.newValue));
            inspector.Add(_resolutionDropdown);
            _viewportZoomSlider = new Slider("Viewport zoom", 0.15f, 2.5f)
            {
                value = _viewportZoom,
                showInputField = true
            };
            _viewportZoomSlider.tooltip = "Zooms the editor camera without changing the node. Use the mouse wheel over the viewport or reset to 100%.";
            _viewportZoomSlider.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                SetViewportZoom(evt.newValue);
            });
            inspector.Add(_viewportZoomSlider);
            Button resetZoom = new Button(() => SetViewportZoom(1f)) { text = "Reset viewport zoom (100%)" };
            resetZoom.tooltip = "Fits the game viewport at its normal editor scale.";
            inspector.Add(resetZoom);
            _safeAreaToggle = new Toggle("Show safe-area guide") { value = true };
            _safeAreaToggle.RegisterValueChangedCallback(_ => RefreshSafeArea());
            inspector.Add(_safeAreaToggle);
            _marginOpacitySlider = new Slider("Margin opacity", 0f, 1f)
            {
                value = _marginOpacity,
                showInputField = true
            };
            _marginOpacitySlider.tooltip = "0 is fully transparent; 1 is fully opaque. This only changes the composer guide, never the game.";
            _marginOpacitySlider.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                EditorSnapshot before = CaptureSnapshot();
                before.MarginOpacity = evt.previousValue;
                RecordLocalUndo(before, "Change margin opacity");
                _marginOpacity = Mathf.Clamp01(evt.newValue);
                EditorPrefs.SetFloat(GetMarginOpacityPrefsKey(), _marginOpacity);
                RefreshMarginArea();
            });
            inspector.Add(_marginOpacitySlider);
            _portraitSizeLabel = new Label();
            _portraitSizeLabel.style.whiteSpace = WhiteSpace.Normal;
            _portraitSizeLabel.style.color = Muted;
            _portraitSizeLabel.style.marginTop = 5f;
            inspector.Add(_portraitSizeLabel);
            _startSourceLabel = new Label();
            _startSourceLabel.style.whiteSpace = WhiteSpace.Normal;
            _startSourceLabel.style.color = Muted;
            _startSourceLabel.style.marginTop = 5f;
            inspector.Add(_startSourceLabel);

            if (_character == null)
            {
                HelpBox missingCharacter = new HelpBox(
                    "Assign a Character or Character Reference to this node to display its real portrait layers.",
                    HelpBoxMessageType.Warning);
                missingCharacter.style.marginTop = 8f;
                inspector.Add(missingCharacter);
            }

            _connectedHelp = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            _connectedHelp.style.marginTop = 10f;
            inspector.Add(_connectedHelp);

            HelpBox controls = new HelpBox(
                "Move: drag the portrait (Shift locks the dominant axis). Scale: pull a corner (Shift scales from center, Ctrl keeps it uniform). Rotate: drag any side (Ctrl snaps to 10 degrees). The inner cyan rectangle is the game viewport; shaded outer bands are the node's off-screen margin. Space previews the tween. Ctrl+Z/Ctrl+Y use this window's local history only while the mouse is over it.",
                HelpBoxMessageType.Info);
            controls.style.marginTop = 10f;
            inspector.Add(controls);
        }

        private void BuildFooter()
        {
            VisualElement footer = new VisualElement();
            footer.style.height = 78f;
            footer.style.flexShrink = 0f;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.paddingLeft = 16f;
            footer.style.paddingRight = 16f;
            footer.style.backgroundColor = PanelRaised;
            footer.style.borderTopWidth = 1f;
            footer.style.borderTopColor = Border;

            _previewButton = new Button(TogglePreview) { text = "▶ Preview" };
            _previewButton.style.width = 100f;
            _previewButton.style.height = 32f;
            footer.Add(_previewButton);
            Button stop = new Button(StopPreview) { text = "■ Stop", tooltip = "Return preview to the starting transform" };
            stop.style.width = 72f;
            stop.style.height = 32f;
            footer.Add(stop);

            _undoButton = new Button(PerformLocalUndo) { text = "Undo", tooltip = "Undo the last tween-editor action (Ctrl+Z while hovering this window)." };
            _undoButton.style.width = 55f;
            _undoButton.style.height = 32f;
            footer.Add(_undoButton);
            _redoButton = new Button(PerformLocalRedo) { text = "Redo", tooltip = "Redo the last tween-editor action (Ctrl+Y or Ctrl+Shift+Z)." };
            _redoButton.style.width = 55f;
            _redoButton.style.height = 32f;
            footer.Add(_redoButton);

            _timeline = new Slider(0f, 1f) { value = 0f };
            _timeline.style.flexGrow = 1f;
            _timeline.style.marginLeft = 12f;
            _timeline.style.marginRight = 8f;
            _timeline.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                _playingPreview = false;
                _previewPaused = false;
                RefreshPreviewPose(evt.newValue);
                RefreshPreviewControls();
            });
            footer.Add(_timeline);
            _previewTimeLabel = new Label("0.00 / 0.50 s");
            _previewTimeLabel.style.width = 94f;
            _previewTimeLabel.style.color = Muted;
            footer.Add(_previewTimeLabel);

            _statusLabel = new Label();
            _statusLabel.style.width = 180f;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.color = Muted;
            footer.Add(_statusLabel);

            Button apply = new Button(SaveToNode) { text = "Confirm Tween" };
            apply.style.width = 130f;
            apply.style.height = 34f;
            apply.style.unityFontStyleAndWeight = FontStyle.Bold;
            footer.Add(apply);
            rootVisualElement.Add(footer);
        }

        private void ResolveAuthoredValues()
        {
            _character = ResolveCharacter(_node);
            _instanceID = ResolveInstanceID(_node);
            _positionSpace = GetOption(_node, "Coordinate Space", CharacterPositionSpace.Normalized);
            _relative = GetOption(_node, "Relative", false);
            _animateTransform = GetOption(_node, "Animate Transform", false);
            _margin = Mathf.Max(0f, ResolveMargin(_node));
            _easing = ResolveEasing(_node);
            _customCurve = SanitizeCustomCurve(GetOption(
                _node,
                "Custom Easing Curve",
                AnimationCurve.Linear(0f, 0f, 1f, 1f)));
            _authoredPosition = ResolvePosition(_node);
            _target = new PortraitState(
                _authoredPosition,
                ResolveRotation(_node),
                ResolveScale(_node));
        }

        private bool HasUntouchedDefaultTarget()
        {
            IPort position = _node.GetInputPortByName("Position");
            IPort rotation = _node.GetInputPortByName("Rotation");
            IPort scale = _node.GetInputPortByName("Scale");
            if (position?.IsConnected == true || rotation?.IsConnected == true || scale?.IsConnected == true)
                return false;

            return _authoredPosition == Vector2.zero &&
                   Mathf.Approximately(_target.Rotation, 0f) &&
                   _target.Scale == Vector2.one &&
                   !_relative &&
                   !_animateTransform;
        }

        private void ResolveStartState()
        {
            if (Application.isPlaying)
            {
                foreach (CharacterInfo info in Resources.FindObjectsOfTypeAll<CharacterInfo>())
                {
                    if (info == null || !info.gameObject.scene.IsValid() || info.character != _character ||
                        !string.Equals(info.InstanceID ?? string.Empty, _instanceID, StringComparison.Ordinal)) continue;
                    _start = new PortraitState(CanvasToNormalized(info.Position), info.Rotation, info.Scale);
                    _startSource = "Live character in Play Mode";
                    return;
                }
            }

            if (TryFindPreviousAuthoredState(out PortraitState state, out string source))
            {
                _start = state;
                _startSource = source;
                return;
            }

            _start = new PortraitState(Vector2.zero, 0f, Vector2.one);
            _startSource = "Default screen center (no earlier matching character transform was found)";
        }

        private bool TryFindPreviousAuthoredState(out PortraitState state, out string source)
            => TryFindPreviousAuthoredState(_node, new HashSet<INode>(), out state, out source);

        private bool TryFindPreviousAuthoredState(
            INode origin,
            HashSet<INode> visited,
            out PortraitState state,
            out string source)
        {
            state = default;
            source = null;
            if (_graph == null || origin == null) return false;

            var queue = new Queue<INode>();
            EnqueuePredecessors(origin, queue);
            int remaining = 256;

            while (queue.Count > 0 && remaining-- > 0)
            {
                INode candidate = queue.Dequeue();
                if (candidate == null || !visited.Add(candidate)) continue;

                if (candidate is TransformSpeakerPortraitNode transform && SameTarget(transform))
                {
                    Vector2 position = ResolvePosition(transform);
                    CharacterPositionSpace space = GetOption(transform, "Coordinate Space", CharacterPositionSpace.Normalized);
                    float sourceMargin = Mathf.Max(0f, ResolveMargin(transform));
                    position = space == CharacterPositionSpace.Canvas
                        ? CanvasToNormalized(position)
                        : SourceNormalizedToVisual(position, sourceMargin);
                    bool relative = GetOption(transform, "Relative", false);
                    string previousSource = null;
                    if (relative)
                    {
                        PortraitState previous = new PortraitState(Vector2.zero, 0f, Vector2.one);
                        TryFindPreviousAuthoredState(
                            transform,
                            new HashSet<INode>(visited),
                            out previous,
                            out previousSource);
                        position += previous.Position;
                    }
                    state = new PortraitState(
                        position,
                        ResolveRotation(transform),
                        ResolveScale(transform));
                    source = relative
                        ? $"Previous relative Transform Portrait node, resolved after {previousSource ?? "screen center"}"
                        : "Previous Transform Portrait node";
                    return true;
                }

                if (candidate is ShowCharacterNode show && SameTarget(show))
                {
                    Vector2 position = GetOption(show, "Position", Vector2.zero);
                    CharacterPositionSpace space = GetOption(show, "Coordinate Space", CharacterPositionSpace.Canvas);
                    position = space == CharacterPositionSpace.Canvas
                        ? CanvasToNormalized(position)
                        : SourceNormalizedToVisual(position, 0f);
                    state = new PortraitState(position, 0f, Vector2.one);
                    source = "Previous Show Character node";
                    return true;
                }

                EnqueuePredecessors(candidate, queue);
            }
            return false;
        }

        private static void EnqueuePredecessors(INode node, Queue<INode> queue)
        {
            IPort input = node?.GetInputPortByName("in");
            if (input == null) return;
            var connected = new List<IPort>();
            input.GetConnectedPorts(connected);
            foreach (IPort port in connected)
            {
                INode predecessor = port?.GetNode();
                if (predecessor != null) queue.Enqueue(predecessor);
            }
        }

        private bool SameTarget(CharacterActionNode node) =>
            ResolveCharacter(node) == _character &&
            string.Equals(ResolveInstanceID(node), _instanceID, StringComparison.Ordinal);

        private NovelCharacter ResolveCharacter(CharacterActionNode node)
        {
            NovelCharacterReference reference = NovelGraphValues.Resolve<NovelCharacterReference>(
                _graph, node.GetInputPortByName("Character Reference"));
            if (reference.Character != null) return reference.Character;
            return NovelGraphValues.Resolve<NovelCharacter>(_graph, node.GetInputPortByName("Character"));
        }

        private string ResolveInstanceID(CharacterActionNode node)
        {
            NovelCharacterReference reference = NovelGraphValues.Resolve<NovelCharacterReference>(
                _graph, node.GetInputPortByName("Character Reference"));
            return reference.Character != null
                ? reference.InstanceID ?? string.Empty
                : GetOption(node, "Instance ID", string.Empty) ?? string.Empty;
        }

        private Vector2 ResolvePosition(TransformSpeakerPortraitNode node)
        {
            IPort port = node.GetInputPortByName("Position");
            if (port == null) return new Vector2(
                GetOption(node, "OffsetX", 0f), GetOption(node, "OffsetY", 0f));
            Vector2 value = NovelGraphValues.Resolve<Vector2>(_graph, port);
            Vector2 legacy = new Vector2(
                GetOption(node, "OffsetX", 0f), GetOption(node, "OffsetY", 0f));
            return !port.IsConnected && value == Vector2.zero && legacy != Vector2.zero ? legacy : value;
        }

        private float ResolveRotation(TransformSpeakerPortraitNode node)
        {
            IPort port = node.GetInputPortByName("Rotation");
            float legacy = GetOption(node, "Rotation", 0f);
            if (port == null) return legacy;
            float value = NovelGraphValues.Resolve<float>(_graph, port);
            return !port.IsConnected && Mathf.Approximately(value, 0f) && !Mathf.Approximately(legacy, 0f)
                ? legacy
                : value;
        }

        private Vector2 ResolveScale(TransformSpeakerPortraitNode node)
        {
            IPort port = node.GetInputPortByName("Scale");
            Vector2 legacy = GetOption(node, "Scale", Vector2.one);
            if (port == null) return legacy;
            Vector2 value = NovelGraphValues.Resolve<Vector2>(_graph, port);
            return !port.IsConnected && value == Vector2.one && legacy != Vector2.one ? legacy : value;
        }

        private float ResolveMargin(TransformSpeakerPortraitNode node)
        {
            IPort port = node.GetInputPortByName("Margin");
            float legacy = GetOption(node, "Margin", 0f);
            if (port == null) return legacy;
            float value = NovelGraphValues.Resolve<float>(_graph, port);
            return !port.IsConnected && Mathf.Approximately(value, 0f) && !Mathf.Approximately(legacy, 0f)
                ? legacy
                : value;
        }

        private void RefreshAll()
        {
            _updatingFields = true;
            _positionField?.SetValueWithoutNotify(_target.Position);
            _rotationField?.SetValueWithoutNotify(_target.Rotation);
            _scaleField?.SetValueWithoutNotify(_target.Scale);
            _marginField?.SetValueWithoutNotify(_margin);
            _positionSpaceDropdown?.SetValueWithoutNotify(
                _positionSpace == CharacterPositionSpace.Canvas ? "Canvas" : "Normalized");
            _relativeToggle?.SetValueWithoutNotify(_relative);
            _animateTransformToggle?.SetValueWithoutNotify(_animateTransform);
            _instanceIDField?.SetValueWithoutNotify(_instanceID ?? string.Empty);
            _marginOpacitySlider?.SetValueWithoutNotify(_marginOpacity);
            _viewportZoomSlider?.SetValueWithoutNotify(_viewportZoom);
            _easingDropdown?.SetValueWithoutNotify(EasingName(_easing));
            _customCurveField?.SetValueWithoutNotify(CloneCurve(_customCurve));
            _UIPreview?.SetValueWithoutNotify(_uiPreviewSource);
            _uiPreviewVisibleToggle?.SetValueWithoutNotify(_uiPreviewVisible);
            _timeline?.SetValueWithoutNotify(1f);
            _updatingFields = false;

            _previewCompleted = false;

            RefreshResolutionLabel();
            RefreshPositionFieldLabel();
            RefreshEasingControls();
            RefreshAnimationControls();
            RefreshPortraitSizeLabel();
            if (_startSourceLabel != null)
                _startSourceLabel.text = $"Ghost start: {_startSource}";
            RefreshConnectedHelp();
            FitScreen();

            // Rebuild after FitScreen so text sizes use the actual displayed
            // Game View scale rather than the raw Canvas pixel size.
            if (_uiPreviewSource != null)
                SetUIPreviewSource(_uiPreviewSource, false);

            RefreshSafeArea();
            RefreshPreviewPose(1f);
            RefreshPreviewControls();
            SetStatus(
                _targetSeededFromStart
                    ? "Visual target starts at the incoming pose. The node remains unchanged until you confirm."
                    : "Target ready. Preview, then confirm.",
                false);
        }

        private void RefreshConnectedHelp()
        {
            var connected = new List<string>();
            if (_node.GetInputPortByName("Position")?.IsConnected == true) connected.Add("Position");
            if (_node.GetInputPortByName("Rotation")?.IsConnected == true) connected.Add("Rotation");
            if (_node.GetInputPortByName("Scale")?.IsConnected == true) connected.Add("Scale");
            if (_node.GetInputPortByName("Margin")?.IsConnected == true) connected.Add("Margin");
            if (_node.GetInputPortByName("Margin")?.IsConnected == true) connected.Add("Margin");
            if (_node.GetInputPortByName("Character Reference")?.IsConnected == true) connected.Add("Character Reference / Instance ID");
            bool visible = connected.Count > 0;
            _connectedHelp.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
                _connectedHelp.text = $"Connected value ports are controlled by the graph and will not be overwritten: {string.Join(", ", connected)}.";
        }

        private void FitScreen()
        {
            if (_previewHost == null || _screen == null || _resolution.y <= 0) return;
            Rect available = _previewHost.contentRect;
            float maxWidth = Mathf.Max(100f, available.width - 48f);
            float maxHeight = Mathf.Max(100f, available.height - 48f);
            float outerWidth = Mathf.Max(1f, _resolution.x + _margin * 2f);
            float outerHeight = Mathf.Max(1f, _resolution.y + _margin * 2f);
            float aspect = outerWidth / outerHeight;
            float width = Mathf.Min(maxWidth, maxHeight * aspect) * _viewportZoom;
            float height = width / aspect;
            _screen.style.width = width;
            _screen.style.height = height;
            ApplyViewportLayout(width, height);
            RefreshMarginArea();
            RefreshSafeArea();
            RefreshPortraits();
            if (_uiPreviewSource != null)
                SetUIPreviewSource(_uiPreviewSource, false);
        }

        private void ApplyViewportLayout(float outerVisualWidth, float outerVisualHeight)
        {
            if (_gameViewport == null) return;
            float outerCanvasWidth = Mathf.Max(1f, _resolution.x + _margin * 2f);
            float outerCanvasHeight = Mathf.Max(1f, _resolution.y + _margin * 2f);
            float left = outerVisualWidth * _margin / outerCanvasWidth;
            float top = outerVisualHeight * _margin / outerCanvasHeight;
            _gameViewport.style.left = left;
            _gameViewport.style.top = top;
            _gameViewport.style.width = outerVisualWidth * _resolution.x / outerCanvasWidth;
            _gameViewport.style.height = outerVisualHeight * _resolution.y / outerCanvasHeight;
        }

        private void OnViewportWheel(WheelEvent evt)
        {
            if (Mathf.Approximately(evt.delta.y, 0f)) return;
            SetViewportZoom(_viewportZoom * (evt.delta.y > 0f ? 0.9f : 1.1f));
            evt.StopImmediatePropagation();
        }

        private void SetViewportZoom(float zoom)
        {
            _viewportZoom = Mathf.Clamp(zoom, 0.15f, 2.5f);
            _viewportZoomSlider?.SetValueWithoutNotify(_viewportZoom);
            EditorPrefs.SetFloat(GetViewportZoomPrefsKey(), _viewportZoom);
            FitScreen();
        }

        private void RefreshMarginArea()
        {
            if (_screen == null) return;
            float alpha = _margin > 0f ? Mathf.Clamp01(_marginOpacity) : 0f;
            _screen.style.backgroundColor = new Color(0.11f, 0.16f, 0.24f, alpha);
            if (_marginGuideLabel != null)
            {
                _marginGuideLabel.style.display = _margin > 0f ? DisplayStyle.Flex : DisplayStyle.None;
                _marginGuideLabel.text = $"OFF-SCREEN MARGIN  +{_margin:0.#}";
                _marginGuideLabel.style.opacity = Mathf.Lerp(0.45f, 1f, alpha);
            }
        }

        private void RefreshSafeArea()
        {
            if (_safeArea == null) return;
            _safeArea.style.display = _safeAreaToggle?.value == false ? DisplayStyle.None : DisplayStyle.Flex;
            float left = 5f, right = 5f, top = 5f, bottom = 5f;
            if (Application.isPlaying && Screen.width > 0 && Screen.height > 0)
            {
                Rect safe = Screen.safeArea;
                left = safe.xMin / Screen.width * 100f;
                right = (Screen.width - safe.xMax) / Screen.width * 100f;
                bottom = safe.yMin / Screen.height * 100f;
                top = (Screen.height - safe.yMax) / Screen.height * 100f;
            }
            _safeArea.style.left = Length.Percent(left);
            _safeArea.style.right = Length.Percent(right);
            _safeArea.style.top = Length.Percent(top);
            _safeArea.style.bottom = Length.Percent(bottom);
        }

        private void RefreshPortraits()
        {
            if (_screen == null || _ghost == null || _targetPortrait == null) return;
            ApplyVisualState(_ghost, _start);
            float progress = _timeline?.value ?? 0f;
            RefreshPreviewPose(progress);
        }

        private void RefreshPreviewPose(float progress)
        {
            progress = Mathf.Clamp01(progress);
            float eased = PortraitTweenEasingUtility.Evaluate(_easing, _customCurve, progress);
            PortraitState pose = PortraitState.Lerp(_start, _target, eased);
            // The screen receives its real dimensions one layout pass after the
            // window is built. Reapply START here so it can never remain at the
            // UI Toolkit default top-left position after an early zero-size pass.
            ApplyVisualState(_ghost, _start);
            ApplyVisualState(_targetPortrait, pose);
            RefreshGhostVisibility();
            RefreshTransformFrame(progress);

            if (_coordinateLabel != null)
            {
                Vector2 pixels = NormalizedToPixels(pose.Position);
                _coordinateLabel.text = $"Target  X {pose.Position.x:0.###}  Y {pose.Position.y:0.###}   •   {pixels.x:0}, {pixels.y:0} px";
            }
            if (_previewTimeLabel != null)
            {
                float duration = Mathf.Max(0f, _durationField?.value ?? 0f);
                _previewTimeLabel.text = $"{duration * progress:0.00} / {duration:0.00} s";
            }
            Repaint();
        }

        private void ApplyVisualState(VisualElement portrait, PortraitState state)
        {
            if (portrait == null || _screen == null) return;
            float screenWidth = _screen.resolvedStyle.width;
            float screenHeight = _screen.resolvedStyle.height;
            if (screenWidth <= 0f || screenHeight <= 0f) return;

            Vector2 baseSize = GetPortraitBaseSize();
            portrait.style.width = baseSize.x;
            portrait.style.height = baseSize.y;
            portrait.style.left = (screenWidth - baseSize.x) * 0.5f + state.Position.x * screenWidth * 0.5f;
            portrait.style.top = (screenHeight - baseSize.y) * 0.5f - state.Position.y * screenHeight * 0.5f;
            portrait.style.rotate = new Rotate(new Angle(state.Rotation, AngleUnit.Degree));
            portrait.style.scale = new Scale(new Vector3(state.Scale.x, state.Scale.y, 1f));
        }

        private Vector2 GetPortraitBaseSize()
        {
            Vector2 viewportSize = GetPreviewScreenSize();
            Vector2 stageSize = new Vector2(
                Mathf.Max(1f, _stageCanvasSize.x),
                Mathf.Max(1f, _stageCanvasSize.y));
            Vector2 nativeSize = new Vector2(
                Mathf.Max(1f, _portraitCanvasSize.x),
                Mathf.Max(1f, _portraitCanvasSize.y));
            return new Vector2(
                nativeSize.x / stageSize.x * viewportSize.x,
                nativeSize.y / stageSize.y * viewportSize.y);
        }

        private Vector2 GetPortraitContentBaseSize()
        {
            Vector2 baseSize = GetPortraitBaseSize();
            return new Vector2(
                Mathf.Max(1f, baseSize.x * _portraitContentRect.width),
                Mathf.Max(1f, baseSize.y * _portraitContentRect.height));
        }

        private Vector2 GetPortraitContentOffset()
        {
            Vector2 baseSize = GetPortraitBaseSize();
            Vector2 center = _portraitContentRect.center;
            return new Vector2(
                (center.x - 0.5f) * baseSize.x,
                (center.y - 0.5f) * baseSize.y);
        }

        private void GetPortraitContentGeometry(
            PortraitState state,
            out Vector2 center,
            out Vector2 size)
        {
            Vector2 rootCenter = NormalizedToLocal(state.Position);
            Vector2 scaledOffset = Vector2.Scale(GetPortraitContentOffset(), state.Scale);
            center = rootCenter + RotateVector(scaledOffset, state.Rotation);
            size = Vector2.Scale(GetPortraitContentBaseSize(), Abs(state.Scale));
        }

        private Vector2 ContentCenterToRootCenter(
            Vector2 contentCenter,
            Vector2 scale,
            float rotation)
        {
            Vector2 scaledOffset = Vector2.Scale(GetPortraitContentOffset(), scale);
            return contentCenter - RotateVector(scaledOffset, rotation);
        }

        private void RefreshTransformFrame(float progress)
        {
            if (_transformFrame == null || _screen == null) return;
            bool editableTargetVisible = !_playingPreview && progress >= 0.999f;
            _transformFrame.style.display = editableTargetVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!editableTargetVisible) return;

            GetPortraitContentGeometry(_target, out Vector2 center, out Vector2 size);
            size.x = Mathf.Max(8f, size.x);
            size.y = Mathf.Max(8f, size.y);
            _transformFrame.style.width = size.x;
            _transformFrame.style.height = size.y;
            _transformFrame.style.left = center.x - size.x * 0.5f;
            _transformFrame.style.top = center.y - size.y * 0.5f;
            _transformFrame.style.rotate = new Rotate(new Angle(_target.Rotation, AngleUnit.Degree));
        }

        private void RefreshGhostVisibility()
        {
            if (_ghost == null) return;
            bool previewMode = _playingPreview || _previewPaused;
            _ghost.style.display = previewMode ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private VisualElement CreatePortraitGroup(float opacity, Color accent, string tag)
        {
            VisualElement group = new VisualElement();
            group.style.position = UnityEngine.UIElements.Position.Absolute;
            group.style.opacity = opacity;
            group.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(50f), 0f);

            CharacterPortrait portrait = _character != null
                ? _character.GetPortrait(CharacterEmotion.Neutral)
                : default;
            AddPortraitLayer(group, portrait.Body);
            AddPortraitLayer(group, portrait.Eyes);
            AddPortraitLayer(group, portrait.Details);
            AddPortraitLayer(group, portrait.Mouth);

            Label label = Badge(tag, accent);
            label.style.position = UnityEngine.UIElements.Position.Absolute;
            label.style.left = 4f;
            label.style.top = 4f;
            label.pickingMode = PickingMode.Ignore;
            group.Add(label);

            if (_character == null)
            {
                Label missing = new Label("No character assigned");
                missing.style.position = UnityEngine.UIElements.Position.Absolute;
                missing.style.left = 10f;
                missing.style.right = 10f;
                missing.style.top = Length.Percent(45f);
                missing.style.unityTextAlign = TextAnchor.MiddleCenter;
                missing.style.color = accent;
                group.Add(missing);
            }
            return group;
        }

        private VisualElement CreateTransformFrame()
        {
            VisualElement frame = new VisualElement();
            frame.style.position = UnityEngine.UIElements.Position.Absolute;
            frame.style.overflow = Overflow.Visible;
            frame.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(50f), 0f);
            SetBorder(frame, Accent, 1.5f);

            AddRotationSide(frame, true, true);
            AddRotationSide(frame, true, false);
            AddRotationSide(frame, false, true);
            AddRotationSide(frame, false, false);
            AddScaleHandle(frame, new Vector2(-1f, -1f));
            AddScaleHandle(frame, new Vector2(1f, -1f));
            AddScaleHandle(frame, new Vector2(-1f, 1f));
            AddScaleHandle(frame, new Vector2(1f, 1f));
            return frame;
        }

        private void AddRotationSide(VisualElement frame, bool horizontal, bool first)
        {
            VisualElement side = new VisualElement();
            side.style.position = UnityEngine.UIElements.Position.Absolute;
            side.tooltip = "Drag this side to rotate. Hold Ctrl to snap to 10 degrees.";
            if (horizontal)
            {
                side.style.left = 12f;
                side.style.right = 12f;
                side.style.height = 14f;
                if (first) side.style.top = -7f;
                else side.style.bottom = -7f;
            }
            else
            {
                side.style.top = 12f;
                side.style.bottom = 12f;
                side.style.width = 14f;
                if (first) side.style.left = -7f;
                else side.style.right = -7f;
            }

            VisualElement grip = new VisualElement();
            grip.style.position = UnityEngine.UIElements.Position.Absolute;
            grip.style.width = 8f;
            grip.style.height = 8f;
            grip.style.borderTopLeftRadius = 4f;
            grip.style.borderTopRightRadius = 4f;
            grip.style.borderBottomLeftRadius = 4f;
            grip.style.borderBottomRightRadius = 4f;
            grip.style.backgroundColor = Accent;
            grip.pickingMode = PickingMode.Ignore;
            if (horizontal)
            {
                grip.style.left = Length.Percent(50f);
                grip.style.marginLeft = -4f;
                grip.style.top = 3f;
            }
            else
            {
                grip.style.top = Length.Percent(50f);
                grip.style.marginTop = -4f;
                grip.style.left = 3f;
            }
            side.Add(grip);
            side.RegisterCallback<PointerDownEvent>(BeginRotate);
            frame.Add(side);
        }

        private void AddScaleHandle(VisualElement frame, Vector2 direction)
        {
            VisualElement handle = new VisualElement();
            handle.style.position = UnityEngine.UIElements.Position.Absolute;
            handle.style.width = 12f;
            handle.style.height = 12f;
            handle.style.backgroundColor = Text;
            SetBorder(handle, Accent, 2f);
            handle.tooltip = "Drag to scale. Shift: from center. Ctrl: uniform. Combine both modifiers if needed.";
            if (direction.x < 0f) handle.style.left = -6f;
            else handle.style.right = -6f;
            if (direction.y < 0f) handle.style.top = -6f;
            else handle.style.bottom = -6f;
            handle.RegisterCallback<PointerDownEvent>(evt => BeginScale(evt, direction));
            frame.Add(handle);
        }

        private static void AddPortraitLayer(VisualElement parent, Sprite sprite)
        {
            if (sprite == null) return;
            var image = new Image
            {
                sprite = sprite,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            image.style.position = UnityEngine.UIElements.Position.Absolute;
            image.style.left = 0f;
            image.style.right = 0f;
            image.style.top = 0f;
            image.style.bottom = 0f;
            parent.Add(image);
        }

        private VisualElement CreateUIPreviewOverlay()
        {
            VisualElement overlay = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };

            overlay.style.position = UnityEngine.UIElements.Position.Absolute;
            overlay.style.left = 0f;
            overlay.style.top = 0f;
            overlay.style.width = 0f;
            overlay.style.height = 0f;
            overlay.style.overflow = Overflow.Visible;
            overlay.style.transformOrigin = new TransformOrigin(
                Length.Percent(0f),
                Length.Percent(0f),
                0f);
            overlay.style.display = DisplayStyle.None;

            return overlay;
        }

        private static string GetUIPreviewPrefsKey() =>
            UIPreviewSourcePrefsPrefix + Hash128.Compute(Application.dataPath);

        private static string GetUIPreviewVisiblePrefsKey() =>
            UIPreviewVisiblePrefsPrefix + Hash128.Compute(Application.dataPath);

        private static string GetMarginOpacityPrefsKey() =>
            MarginOpacityPrefsPrefix + Hash128.Compute(Application.dataPath);

        private static string GetViewportZoomPrefsKey() =>
            ViewportZoomPrefsPrefix + Hash128.Compute(Application.dataPath);

        private static void SaveUIPreviewSource(GameObject source)
        {
            string key = GetUIPreviewPrefsKey();
            if (string.IsNullOrEmpty(key))
                return;

            if (source == null)
            {
                EditorPrefs.DeleteKey(key);
                return;
            }

            GlobalObjectId globalObjectId =
                GlobalObjectId.GetGlobalObjectIdSlow(source);
            string serializedID = globalObjectId.ToString();

            if (string.IsNullOrEmpty(serializedID))
                EditorPrefs.DeleteKey(key);
            else
                EditorPrefs.SetString(key, serializedID);
        }

        private GameObject LoadUIPreviewSource()
        {
            string key = GetUIPreviewPrefsKey();
            if (string.IsNullOrEmpty(key) || !EditorPrefs.HasKey(key))
                return null;

            string serializedID = EditorPrefs.GetString(key);
            if (!GlobalObjectId.TryParse(
                    serializedID,
                    out GlobalObjectId globalObjectId))
            {
                EditorPrefs.DeleteKey(key);
                return null;
            }

            GameObject source =
                GlobalObjectId.GlobalObjectIdentifierToObjectSlow(
                    globalObjectId) as GameObject;

            if (source == null)
                return null;

            bool valid = source.GetComponent<RectTransform>() != null &&
                         source.GetComponentInParent<Canvas>(true) != null &&
                         !EditorUtility.IsPersistent(source) &&
                         source.scene.IsValid();

            if (!valid)
            {
                EditorPrefs.DeleteKey(key);
                return null;
            }

            return source;
        }

        private void SetUIPreviewVisibility(bool visible)
        {
            _uiPreviewVisible = visible;
            EditorPrefs.SetBool(GetUIPreviewVisiblePrefsKey(), visible);
            if (_uiPreviewOverlay == null)
                return;

            _uiPreviewOverlay.style.display =
                visible &&
                _uiPreviewSource != null &&
                _uiPreviewSource.activeInHierarchy
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        private static bool LoadUIPreviewVisibility() =>
            EditorPrefs.GetBool(GetUIPreviewVisiblePrefsKey(), true);

        private void SetUIPreviewSource(GameObject source, bool persist = true)
        {
            _uiPreviewSource = source;
            if (persist) SaveUIPreviewSource(source);

            if (_uiPreviewOverlay == null)
                return;

            _uiPreviewOverlay.Clear();
            _uiPreviewOverlay.style.display = DisplayStyle.None;

            if (source == null || !source.activeInHierarchy)
                return;

            Canvas canvas = source.GetComponentInParent<Canvas>(true);
            Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
            RectTransform canvasRect = rootCanvas != null
                ? rootCanvas.GetComponent<RectTransform>()
                : null;

            RectTransform sourceRect =
                source.GetComponent<RectTransform>();

            if (canvasRect == null || sourceRect == null)
            {
                Debug.LogWarning(
                    "The selected UI object must be under a Canvas and have a RectTransform.");
                return;
            }

            Canvas.ForceUpdateCanvases();

            Vector2 previewSize = GetPreviewScreenSize();
            float scaleX = previewSize.x / canvasRect.rect.width;
            float scaleY = previewSize.y / canvasRect.rect.height;
            Rect viewportRect = GetGameViewportRect();

            _uiPreviewOverlay.style.left = viewportRect.x;
            _uiPreviewOverlay.style.top = viewportRect.y;
            _uiPreviewOverlay.style.width = canvasRect.rect.width;
            _uiPreviewOverlay.style.height = canvasRect.rect.height;
            _uiPreviewOverlay.style.scale = new Scale(
                new Vector3(scaleX, scaleY, 1f));

            AddUIElementsRecursively(
                _uiPreviewOverlay,
                source.transform,
                canvasRect,
                1f);

            SetUIPreviewVisibility(
                _uiPreviewVisibleToggle == null ||
                _uiPreviewVisibleToggle.value);
        }

        private void AddUIElementsRecursively(
            VisualElement parent,
            Transform current,
            RectTransform canvasRect,
            float parentOpacity)
        {
            if (!current.gameObject.activeInHierarchy)
                return;

            float opacity = parentOpacity;
            CanvasGroup canvasGroup =
                current.GetComponent<CanvasGroup>();

            if (canvasGroup != null)
                opacity *= canvasGroup.alpha;

            RectTransform currentRect =
                current.GetComponent<RectTransform>();

            if (currentRect != null)
            {
                UnityEngine.UI.Image sourceImage =
                    current.GetComponent<UnityEngine.UI.Image>();

                if (sourceImage != null &&
                    sourceImage.enabled)
                {
                    VisualElement previewImage =
                        CreateSceneUIImage(sourceImage, opacity);

                    if (previewImage != null)
                    {
                        ApplyCanvasRectTransform(
                            previewImage,
                            currentRect,
                            canvasRect);
                        parent.Add(previewImage);
                    }
                }

                UnityEngine.UI.RawImage sourceRawImage =
                    current.GetComponent<UnityEngine.UI.RawImage>();

                if (sourceRawImage != null &&
                    sourceRawImage.enabled &&
                    sourceRawImage.texture != null)
                {
                    Image previewImage = new Image
                    {
                        image = sourceRawImage.texture,
                        scaleMode = ScaleMode.StretchToFill,
                        tintColor = sourceRawImage.color,
                        pickingMode = PickingMode.Ignore
                    };

                    previewImage.style.opacity = opacity;
                    ApplyCanvasRectTransform(
                        previewImage,
                        currentRect,
                        canvasRect);
                    parent.Add(previewImage);
                }

                UnityEngine.UI.Text sourceText =
                    current.GetComponent<UnityEngine.UI.Text>();

                if (sourceText != null && sourceText.enabled)
                {
                    Label previewText = new Label(sourceText.text)
                    {
                        pickingMode = PickingMode.Ignore
                    };

                    previewText.style.color = sourceText.color;
                    previewText.style.fontSize = sourceText.fontSize;
                    previewText.style.unityTextAlign = sourceText.alignment;
                    previewText.style.whiteSpace = WhiteSpace.Normal;
                    previewText.style.opacity = opacity;

                    ApplyCanvasRectTransform(
                        previewText,
                        currentRect,
                        canvasRect);
                    parent.Add(previewText);
                }

                Component sourceTMPText =
                    GetOptionalTMPText(current);

                if (sourceTMPText != null &&
                    GetComponentBool(sourceTMPText, "enabled", true))
                {
                    Label previewText = new Label(
                        GetComponentString(sourceTMPText, "text"))
                    {
                        pickingMode = PickingMode.Ignore
                    };

                    previewText.style.color = GetComponentColor(
                        sourceTMPText,
                        "color",
                        Color.white);
                    previewText.style.fontSize = Mathf.Max(
                        1f,
                        GetComponentFloat(
                            sourceTMPText,
                            "fontSize",
                            14f));
                    previewText.style.unityTextAlign =
                        ConvertTextAlignment(
                            GetComponentString(
                                sourceTMPText,
                                "alignment"));
                    previewText.style.whiteSpace = WhiteSpace.Normal;
                    previewText.style.opacity = opacity;

                    ApplyCanvasRectTransform(
                        previewText,
                        currentRect,
                        canvasRect);
                    parent.Add(previewText);
                }
            }

            foreach (Transform child in current)
            {
                AddUIElementsRecursively(
                    parent,
                    child,
                    canvasRect,
                    opacity);
            }
        }

        private VisualElement CreateSceneUIImage(
            UnityEngine.UI.Image sourceImage,
            float opacity)
        {
            if (sourceImage == null || !sourceImage.enabled)
                return null;

            if (sourceImage.sprite != null &&
                sourceImage.type == UnityEngine.UI.Image.Type.Sliced)
            {
                Sprite sprite = sourceImage.sprite;
                Vector4 border = sprite.border;

                VisualElement slicedImage = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };

                // UI Toolkit's Image element has no Sliced scale mode. Use
                // the sprite as a background and copy the source sprite's
                // border values so each uGUI Image.Type.Sliced is rendered
                // with the same 9-slice behavior.
                slicedImage.style.backgroundImage =
                    new StyleBackground(sprite);
                slicedImage.style.unitySliceLeft =
                    Mathf.RoundToInt(border.x);
                slicedImage.style.unitySliceRight =
                    Mathf.RoundToInt(border.z);
                slicedImage.style.unitySliceBottom =
                    Mathf.RoundToInt(border.y);
                slicedImage.style.unitySliceTop =
                    Mathf.RoundToInt(border.w);
                slicedImage.style.unitySliceScale = 1f;
                slicedImage.style.unitySliceType = SliceType.Sliced;
                slicedImage.style.unityBackgroundImageTintColor =
                    sourceImage.color;
                slicedImage.style.opacity = opacity;

                return slicedImage;
            }

            if (sourceImage.sprite != null)
            {
                Image previewImage = new Image
                {
                    sprite = sourceImage.sprite,
                    scaleMode = sourceImage.preserveAspect
                        ? ScaleMode.ScaleToFit
                        : ScaleMode.StretchToFill,
                    tintColor = sourceImage.color,
                    pickingMode = PickingMode.Ignore
                };

                previewImage.style.opacity = opacity;
                return previewImage;
            }

            if (sourceImage.color.a > 0f)
            {
                VisualElement previewPanel = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };

                previewPanel.style.backgroundColor = sourceImage.color;
                previewPanel.style.opacity = opacity;
                return previewPanel;
            }

            return null;
        }

        private Rect GetGameViewportRect()
        {
            float screenWidth = _screen != null ? _screen.resolvedStyle.width : 0f;
            float screenHeight = _screen != null ? _screen.resolvedStyle.height : 0f;
            if (screenWidth <= 0f || screenHeight <= 0f)
                return new Rect(0f, 0f, Mathf.Max(1f, _resolution.x), Mathf.Max(1f, _resolution.y));

            float outerWidth = Mathf.Max(1f, _resolution.x + _margin * 2f);
            float outerHeight = Mathf.Max(1f, _resolution.y + _margin * 2f);
            float left = screenWidth * _margin / outerWidth;
            float top = screenHeight * _margin / outerHeight;
            return new Rect(
                left,
                top,
                screenWidth * _resolution.x / outerWidth,
                screenHeight * _resolution.y / outerHeight);
        }

        private Vector2 GetPreviewScreenSize()
        {
            if (_screen != null)
            {
                Rect viewport = GetGameViewportRect();
                float width = viewport.width;
                float height = viewport.height;

                if (width > 0f && height > 0f)
                    return new Vector2(width, height);
            }

            Rect available = _previewHost != null
                ? _previewHost.contentRect
                : Rect.zero;

            float maxWidth = Mathf.Max(100f, available.width - 48f);
            float maxHeight = Mathf.Max(100f, available.height - 48f);
            float aspect = _resolution.y > 0
                ? _resolution.x / (float)_resolution.y
                : 16f / 9f;

            float widthFromAspect = Mathf.Min(
                maxWidth,
                maxHeight * aspect);

            if (widthFromAspect <= 0f)
                widthFromAspect = _resolution.x;

            return new Vector2(
                widthFromAspect,
                widthFromAspect / aspect);
        }

        private static readonly Type OptionalTMPTextType =
            Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro") ??
            Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro.Runtime");

        private static Component GetOptionalTMPText(Transform current)
        {
            return OptionalTMPTextType == null
                ? null
                : current.GetComponent(OptionalTMPTextType);
        }

        private static object GetComponentMember(
            Component component,
            string memberName)
        {
            if (component == null)
                return null;

            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            Type type = component.GetType();

            PropertyInfo property = type.GetProperty(
                memberName,
                flags);

            if (property != null && property.CanRead)
                return property.GetValue(component);

            FieldInfo field = type.GetField(
                memberName,
                flags);

            return field?.GetValue(component);
        }

        private static string GetComponentString(
            Component component,
            string memberName)
        {
            return GetComponentMember(component, memberName)?.ToString()
                ?? string.Empty;
        }

        private static float GetComponentFloat(
            Component component,
            string memberName,
            float fallback)
        {
            object value = GetComponentMember(component, memberName);

            try
            {
                return value == null
                    ? fallback
                    : Convert.ToSingle(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool GetComponentBool(
            Component component,
            string memberName,
            bool fallback)
        {
            object value = GetComponentMember(component, memberName);

            try
            {
                return value == null
                    ? fallback
                    : Convert.ToBoolean(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static Color GetComponentColor(
            Component component,
            string memberName,
            Color fallback)
        {
            object value = GetComponentMember(component, memberName);
            return value is Color color ? color : fallback;
        }

        private static TextAnchor ConvertTextAlignment(string alignment)
        {
            bool centered = alignment.Contains("Center");
            bool right = alignment.Contains("Right");
            bool middle = alignment.Contains("Middle") ||
                          alignment.Contains("Midline");
            bool bottom = alignment.Contains("Bottom");

            if (bottom)
            {
                if (right) return TextAnchor.LowerRight;
                if (centered) return TextAnchor.LowerCenter;
                return TextAnchor.LowerLeft;
            }

            if (middle)
            {
                if (right) return TextAnchor.MiddleRight;
                if (centered) return TextAnchor.MiddleCenter;
                return TextAnchor.MiddleLeft;
            }

            if (right) return TextAnchor.UpperRight;
            if (centered) return TextAnchor.UpperCenter;
            return TextAnchor.UpperLeft;
        }

        private static void ApplyCanvasRectTransform(
            VisualElement element,
            RectTransform source,
            RectTransform canvasRect)
        {
            if (canvasRect.rect.width <= 0f ||
                canvasRect.rect.height <= 0f)
            {
                return;
            }

            Vector3[] corners = new Vector3[4];
            source.GetWorldCorners(corners);

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 local =
                    canvasRect.InverseTransformPoint(corners[i]);

                minX = Mathf.Min(minX, local.x);
                maxX = Mathf.Max(maxX, local.x);
                minY = Mathf.Min(minY, local.y);
                maxY = Mathf.Max(maxY, local.y);
            }

            Rect canvasBounds = canvasRect.rect;

            // Keep the original Canvas coordinate system. The overlay itself
            // is scaled once to fit _screen, so every child—including text—
            // must remain in the original Canvas units here.
            float left = minX - canvasBounds.xMin;
            float top = canvasBounds.yMax - maxY;
            float width = maxX - minX;
            float height = maxY - minY;

            element.style.position =
                UnityEngine.UIElements.Position.Absolute;
            element.style.left = left;
            element.style.top = top;
            element.style.width = width;
            element.style.height = height;
            element.style.rotate = new Rotate(
                new Angle(
                    source.localEulerAngles.z,
                    AngleUnit.Degree));
        }

        private void ResolvePortraitCanvasSize()
        {
            _stageCanvasSize = new Vector2(
                Mathf.Max(1, _resolution.x),
                Mathf.Max(1, _resolution.y));
            CharacterInfo layout = null;

            foreach (CharacterInfo info in Resources.FindObjectsOfTypeAll<CharacterInfo>())
            {
                if (info == null || !info.gameObject.scene.IsValid() || info.character != _character ||
                    !string.Equals(info.InstanceID ?? string.Empty, _instanceID ?? string.Empty, StringComparison.Ordinal))
                    continue;
                layout = info;
                _portraitSizeSource = "live CharacterInfo layout";
                if (info.transform.parent is RectTransform parentRect && IsUsableSize(parentRect.rect.size))
                    _stageCanvasSize = parentRect.rect.size;
                break;
            }

            if (layout == null)
            {
                foreach (NovelGraphRunner runner in Resources.FindObjectsOfTypeAll<NovelGraphRunner>())
                {
                    if (runner == null || !runner.gameObject.scene.IsValid() || runner.PortraitPrefab == null)
                        continue;
                    layout = runner.PortraitPrefab.GetComponent<CharacterInfo>();
                    if (layout == null) continue;
                    _portraitSizeSource = $"portrait prefab '{runner.PortraitPrefab.name}'";
                    if (TryGetRunnerStageSize(runner, out Vector2 stageSize))
                        _stageCanvasSize = stageSize;
                    break;
                }
            }

            if (layout != null && TryGetPortraitLayoutSize(layout, out Vector2 layoutSize))
            {
                _portraitCanvasSize = layoutSize;
                ResolvePortraitContentRect();
                return;
            }

            CharacterPortrait portrait = _character != null
                ? _character.GetPortrait(CharacterEmotion.Neutral)
                : default;
            Sprite sprite = portrait.Body ?? portrait.Eyes ?? portrait.Details ?? portrait.Mouth;
            if (sprite != null && IsUsableSize(sprite.rect.size))
            {
                _portraitCanvasSize = sprite.rect.size;
                _portraitSizeSource = $"native sprite '{sprite.name}' (no CharacterInfo layout found)";
                ResolvePortraitContentRect();
                return;
            }

            _portraitCanvasSize = new Vector2(100f, 160f);
            _portraitSizeSource = "fallback size (no portrait layout found)";
            _portraitContentRect = new Rect(0f, 0f, 1f, 1f);
        }

        private void ResolvePortraitContentRect()
        {
            _portraitContentRect = new Rect(0f, 0f, 1f, 1f);
            if (_character == null || !IsUsableSize(_portraitCanvasSize)) return;
            CharacterPortrait portrait = _character.GetPortrait(CharacterEmotion.Neutral);
            Sprite[] sprites = { portrait.Body, portrait.Eyes, portrait.Details, portrait.Mouth };
            var visited = new HashSet<Sprite>();
            bool found = false;
            Rect combined = default;

            foreach (Sprite sprite in sprites)
            {
                if (sprite == null || !visited.Add(sprite) ||
                    !TryReadSpriteAlphaBounds(sprite, out Rect alphaBounds))
                    continue;

                Vector2 spriteSize = sprite.rect.size;
                if (!IsUsableSize(spriteSize)) continue;
                float fit = Mathf.Min(
                    _portraitCanvasSize.x / spriteSize.x,
                    _portraitCanvasSize.y / spriteSize.y);
                Vector2 fitted = spriteSize * fit;
                Vector2 inset = (_portraitCanvasSize - fitted) * 0.5f;
                Rect visible = new Rect(
                    (inset.x + alphaBounds.xMin * fitted.x) / _portraitCanvasSize.x,
                    (inset.y + (1f - alphaBounds.yMax) * fitted.y) / _portraitCanvasSize.y,
                    alphaBounds.width * fitted.x / _portraitCanvasSize.x,
                    alphaBounds.height * fitted.y / _portraitCanvasSize.y);

                combined = found
                    ? Rect.MinMaxRect(
                        Mathf.Min(combined.xMin, visible.xMin),
                        Mathf.Min(combined.yMin, visible.yMin),
                        Mathf.Max(combined.xMax, visible.xMax),
                        Mathf.Max(combined.yMax, visible.yMax))
                    : visible;
                found = true;
            }

            if (!found) return;
            const float handlePadding = 0.006f;
            float xMin = Mathf.Clamp01(combined.xMin - handlePadding);
            float yMin = Mathf.Clamp01(combined.yMin - handlePadding);
            float xMax = Mathf.Clamp01(combined.xMax + handlePadding);
            float yMax = Mathf.Clamp01(combined.yMax + handlePadding);
            if (xMax > xMin && yMax > yMin)
                _portraitContentRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static bool TryReadSpriteAlphaBounds(Sprite sprite, out Rect bounds)
        {
            bounds = new Rect(0f, 0f, 1f, 1f);
            if (sprite == null || sprite.texture == null) return false;
            if (SpriteAlphaBoundsCache.TryGetValue(sprite, out bounds)) return true;
            if (sprite.packed && sprite.packingRotation != SpritePackingRotation.None) return false;

            RenderTexture temporary = null;
            Texture2D readback = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                Rect source = sprite.textureRect;
                float sampleScale = Mathf.Min(1f, 512f / Mathf.Max(source.width, source.height));
                int width = Mathf.Max(1, Mathf.RoundToInt(source.width * sampleScale));
                int height = Mathf.Max(1, Mathf.RoundToInt(source.height * sampleScale));
                temporary = RenderTexture.GetTemporary(
                    width,
                    height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear);
                Vector2 uvScale = new Vector2(
                    source.width / sprite.texture.width,
                    source.height / sprite.texture.height);
                Vector2 uvOffset = new Vector2(
                    source.x / sprite.texture.width,
                    source.y / sprite.texture.height);
                Graphics.Blit(sprite.texture, temporary, uvScale, uvOffset);
                RenderTexture.active = temporary;
                readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readback.Apply(false, false);
                Color32[] pixels = readback.GetPixels32();

                int minX = width, minY = height, maxX = -1, maxY = -1;
                for (int y = 0; y < height; y++)
                {
                    int row = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        if (pixels[row + x].a <= 3) continue;
                        minX = Mathf.Min(minX, x);
                        minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                    }
                }

                if (maxX < minX || maxY < minY) return false;
                bounds = Rect.MinMaxRect(
                    minX / (float)width,
                    minY / (float)height,
                    (maxX + 1f) / width,
                    (maxY + 1f) / height);
                SpriteAlphaBoundsCache[sprite] = bounds;
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                if (readback != null) DestroyImmediate(readback);
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static bool TryGetPortraitLayoutSize(CharacterInfo info, out Vector2 size)
        {
            size = Vector2.zero;
            if (info == null || info.transform is not RectTransform root) return false;
            UnityEngine.UI.Image[] images = info.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            if (images == null || images.Length == 0) return false;

            bool found = false;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var corners = new Vector3[4];
            foreach (UnityEngine.UI.Image image in images)
            {
                if (image == null || image.transform is not RectTransform rect) continue;
                rect.GetWorldCorners(corners);
                for (int i = 0; i < corners.Length; i++)
                {
                    Vector3 point = root.InverseTransformPoint(corners[i]);
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                    found = true;
                }
            }

            size = maximum - minimum;
            return found && IsUsableSize(size);
        }

        private static bool TryGetRunnerStageSize(NovelGraphRunner runner, out Vector2 size)
        {
            size = Vector2.zero;
            if (runner == null) return false;
            if (runner.CharacterContainer is RectTransform stageRect && IsUsableSize(stageRect.rect.size))
            {
                size = stageRect.rect.size;
                return true;
            }

            Canvas canvas = runner.CanvasDialogue != null
                ? runner.CanvasDialogue.GetComponentInParent<Canvas>(true)
                : null;
            RectTransform canvasRect = canvas != null ? canvas.rootCanvas.GetComponent<RectTransform>() : null;
            if (canvasRect == null || !IsUsableSize(canvasRect.rect.size)) return false;
            size = canvasRect.rect.size;
            return true;
        }

        private static bool IsUsableSize(Vector2 size) =>
            size.x > 0.01f && size.y > 0.01f &&
            !float.IsNaN(size.x) && !float.IsNaN(size.y) &&
            !float.IsInfinity(size.x) && !float.IsInfinity(size.y);

        private void SetTarget(PortraitState state, bool recordUndo = true, string undoLabel = "Change target transform")
        {
            if (_updatingFields) return;
            if (recordUndo) RecordLocalUndo(undoLabel);
            Vector2 position = state.Position;
            if (_clampToggle?.value != false)
            {
                position.x = Mathf.Clamp(position.x, -1f, 1f);
                position.y = Mathf.Clamp(position.y, -1f, 1f);
            }
            Vector2 scale = new Vector2(
                Mathf.Clamp(state.Scale.x, -10f, 10f),
                Mathf.Clamp(state.Scale.y, -10f, 10f));
            _target = new PortraitState(position, Mathf.Repeat(state.Rotation + 180f, 360f) - 180f, scale);
            _targetSeededFromStart = false;
            _playingPreview = false;
            _previewPaused = false;
            _previewCompleted = false;
            _updatingFields = true;
            _positionField?.SetValueWithoutNotify(_target.Position);
            _rotationField?.SetValueWithoutNotify(_target.Rotation);
            _scaleField?.SetValueWithoutNotify(_target.Scale);
            _timeline?.SetValueWithoutNotify(1f);
            _updatingFields = false;
            RefreshPreviewPose(1f);
            RefreshPreviewControls();
            SetStatus("Target changed — preview before confirming.", false);
        }

        private EditorSnapshot CaptureSnapshot() => new EditorSnapshot
        {
            Target = _target,
            TargetSeededFromStart = _targetSeededFromStart,
            Duration = _durationField?.value ?? GetOption("Duration", 0.5f),
            Easing = _easing,
            CustomCurve = CloneCurve(_customCurve),
            Wait = _waitToggle?.value ?? GetOption("Wait For Completion", true),
            Clamp = _clampToggle?.value ?? true,
            PositionSpace = _positionSpace,
            Relative = _relative,
            AnimateTransform = _animateTransform,
            Margin = _margin,
            MarginOpacity = _marginOpacity,
            InstanceID = _instanceID,
            UIPreviewSource = _uiPreviewSource,
            UIPreviewVisible = _uiPreviewVisible
        };

        private void RecordLocalUndo(string label) => RecordLocalUndo(CaptureSnapshot(), label);

        private void RecordLocalUndo(EditorSnapshot snapshot, string label)
        {
            if (!_historyReady || _applyingHistory) return;
            _undoHistory.Add(new HistoryEntry(snapshot, label));
            if (_undoHistory.Count > 100) _undoHistory.RemoveAt(0);
            _redoHistory.Clear();
            RefreshHistoryButtons();
        }

        private void PerformLocalUndo()
        {
            if (_gesture != TransformGesture.None)
            {
                SetStatus("Finish the current drag before undoing.", true);
                return;
            }
            if (_undoHistory.Count == 0)
            {
                SetStatus("Nothing to undo in the tween editor.", true);
                return;
            }

            int index = _undoHistory.Count - 1;
            HistoryEntry entry = _undoHistory[index];
            _undoHistory.RemoveAt(index);
            _redoHistory.Add(new HistoryEntry(CaptureSnapshot(), entry.Label));
            ApplySnapshot(entry.Snapshot);
            RefreshHistoryButtons();
            SetStatus($"Undo: {entry.Label}", false);
        }

        private void PerformLocalRedo()
        {
            if (_gesture != TransformGesture.None)
            {
                SetStatus("Finish the current drag before redoing.", true);
                return;
            }
            if (_redoHistory.Count == 0)
            {
                SetStatus("Nothing to redo in the tween editor.", true);
                return;
            }

            int index = _redoHistory.Count - 1;
            HistoryEntry entry = _redoHistory[index];
            _redoHistory.RemoveAt(index);
            _undoHistory.Add(new HistoryEntry(CaptureSnapshot(), entry.Label));
            ApplySnapshot(entry.Snapshot);
            RefreshHistoryButtons();
            SetStatus($"Redo: {entry.Label}", false);
        }

        private void ApplySnapshot(EditorSnapshot snapshot)
        {
            _applyingHistory = true;
            _updatingFields = true;
            try
            {
                _target = snapshot.Target;
                _targetSeededFromStart = snapshot.TargetSeededFromStart;
                _positionField?.SetValueWithoutNotify(snapshot.Target.Position);
                _rotationField?.SetValueWithoutNotify(snapshot.Target.Rotation);
                _scaleField?.SetValueWithoutNotify(snapshot.Target.Scale);
                _durationField?.SetValueWithoutNotify(snapshot.Duration);
                _easing = snapshot.Easing;
                _customCurve = CloneCurve(snapshot.CustomCurve);
                _easingDropdown?.SetValueWithoutNotify(EasingName(_easing));
                _customCurveField?.SetValueWithoutNotify(CloneCurve(_customCurve));
                _waitToggle?.SetValueWithoutNotify(snapshot.Wait);
                _clampToggle?.SetValueWithoutNotify(snapshot.Clamp);
                _positionSpace = snapshot.PositionSpace;
                _relative = snapshot.Relative;
                _animateTransform = snapshot.AnimateTransform;
                _margin = Mathf.Max(0f, snapshot.Margin);
                _marginOpacity = Mathf.Clamp01(snapshot.MarginOpacity);
                _instanceID = snapshot.InstanceID ?? string.Empty;
                _positionSpaceDropdown?.SetValueWithoutNotify(
                    _positionSpace == CharacterPositionSpace.Canvas ? "Canvas" : "Normalized");
                _relativeToggle?.SetValueWithoutNotify(_relative);
                _animateTransformToggle?.SetValueWithoutNotify(_animateTransform);
                _marginField?.SetValueWithoutNotify(_margin);
                _marginOpacitySlider?.SetValueWithoutNotify(_marginOpacity);
                _instanceIDField?.SetValueWithoutNotify(_instanceID);
                _uiPreviewVisible = snapshot.UIPreviewVisible;
                _UIPreview?.SetValueWithoutNotify(snapshot.UIPreviewSource);
                _uiPreviewVisibleToggle?.SetValueWithoutNotify(_uiPreviewVisible);
                SetUIPreviewSource(snapshot.UIPreviewSource);
                _timeline?.SetValueWithoutNotify(1f);
                _playingPreview = false;
                _previewPaused = false;
                _previewCompleted = false;
            }
            finally
            {
                _updatingFields = false;
                _applyingHistory = false;
            }
            RefreshEasingControls();
            RefreshAnimationControls();
            RefreshPositionFieldLabel();
            EditorPrefs.SetFloat(GetMarginOpacityPrefsKey(), _marginOpacity);
            ResolveStartState();
            if (_targetSeededFromStart)
                _target = new PortraitState(_start.Position, _target.Rotation, _target.Scale);
            _positionField?.SetValueWithoutNotify(_target.Position);
            ResolvePortraitCanvasSize();
            if (_startSourceLabel != null)
                _startSourceLabel.text = $"Ghost start: {_startSource}";
            RefreshPortraitSizeLabel();
            FitScreen();
            RefreshPreviewPose(1f);
            RefreshPreviewControls();
        }

        private void RefreshHistoryButtons()
        {
            _undoButton?.SetEnabled(_undoHistory.Count > 0);
            _redoButton?.SetEnabled(_redoHistory.Count > 0);
        }

        private void ApplyPreset(string preset)
        {
            Vector2 position = preset switch
            {
                "Center" => Vector2.zero,
                "Left" => new Vector2(-0.65f, 0f),
                "Right" => new Vector2(0.65f, 0f),
                "Top" => new Vector2(0f, 0.65f),
                "Bottom" => new Vector2(0f, -0.65f),
                "Off-screen left" => new Vector2(-1.35f, 0f),
                "Off-screen right" => new Vector2(1.35f, 0f),
                _ => _target.Position
            };
            if (preset == "Choose…") return;
            EditorSnapshot before = CaptureSnapshot();
            RecordLocalUndo(before, $"Apply {preset} preset");
            if (preset.StartsWith("Off-screen", StringComparison.Ordinal) && _clampToggle != null)
                _clampToggle.SetValueWithoutNotify(false);
            SetTarget(new PortraitState(position, _target.Rotation, _target.Scale), false);
        }

        private void Nudge(Vector2 direction, float amount = 0.025f) =>
            SetTarget(new PortraitState(_target.Position + direction * amount, _target.Rotation, _target.Scale), true, "Nudge target");

        private void OnStagePointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || _screen == null) return;
            Vector2 pointer = _screen.WorldToLocal(evt.position);
            if (!IsInsideTarget(pointer)) return;
            PrepareForEditing();
            _screen.Focus();
            _gesture = TransformGesture.Move;
            _gestureStartState = _target;
            _gestureStartPointer = pointer;
            _moveAxisLocked = false;
            _gestureUndoRecorded = false;
            _screen.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void BeginScale(PointerDownEvent evt, Vector2 direction)
        {
            if (evt.button != 0 || _screen == null) return;
            PrepareForEditing();
            _screen.Focus();
            _gesture = TransformGesture.Scale;
            _gestureStartState = _target;
            _gestureStartPointer = _screen.WorldToLocal(evt.position);
            _scaleHandleDirection = direction;
            _gestureUndoRecorded = false;

            GetPortraitContentGeometry(_gestureStartState, out Vector2 center, out Vector2 fullSize);
            Vector2 halfSize = fullSize * 0.5f;
            _fixedScaleCorner = center + RotateVector(Vector2.Scale(-direction, halfSize), _gestureStartState.Rotation);
            _screen.CapturePointer(evt.pointerId);
            evt.StopImmediatePropagation();
        }

        private void BeginRotate(PointerDownEvent evt)
        {
            if (evt.button != 0 || _screen == null) return;
            PrepareForEditing();
            _screen.Focus();
            _gesture = TransformGesture.Rotate;
            _gestureStartState = _target;
            _gestureStartPointer = _screen.WorldToLocal(evt.position);
            _gestureUndoRecorded = false;
            Vector2 center = NormalizedToLocal(_gestureStartState.Position);
            _gestureStartAngle = PointerAngle(_gestureStartPointer - center);
            _screen.CapturePointer(evt.pointerId);
            evt.StopImmediatePropagation();
        }

        private void OnStagePointerMove(PointerMoveEvent evt)
        {
            if (_gesture == TransformGesture.None || !_screen.HasPointerCapture(evt.pointerId)) return;
            Vector2 pointer = _screen.WorldToLocal(evt.position);
            if (!_gestureUndoRecorded && (pointer - _gestureStartPointer).sqrMagnitude > 0.01f)
            {
                string label = _gesture == TransformGesture.Move ? "Move target" :
                    _gesture == TransformGesture.Scale ? "Scale target" : "Rotate target";
                RecordLocalUndo(label);
                _gestureUndoRecorded = true;
            }
            switch (_gesture)
            {
                case TransformGesture.Move: UpdateMove(pointer, evt.shiftKey); break;
                case TransformGesture.Scale: UpdateScale(pointer, evt.shiftKey, evt.ctrlKey || evt.commandKey); break;
                case TransformGesture.Rotate: UpdateRotation(pointer, evt.ctrlKey || evt.commandKey); break;
            }
            evt.StopPropagation();
        }

        private void UpdateMove(Vector2 pointer, bool constrainAxis)
        {
            Vector2 delta = pointer - _gestureStartPointer;
            if (constrainAxis)
            {
                if (!_moveAxisLocked && delta.sqrMagnitude >= 9f)
                {
                    _moveHorizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
                    _moveAxisLocked = true;
                }
                if (_moveAxisLocked)
                {
                    if (_moveHorizontal) delta.y = 0f;
                    else delta.x = 0f;
                }
            }
            else _moveAxisLocked = false;

            Vector2 normalizedDelta = new Vector2(
                delta.x / Mathf.Max(1f, _screen.resolvedStyle.width) * 2f,
                -delta.y / Mathf.Max(1f, _screen.resolvedStyle.height) * 2f);
            SetTarget(new PortraitState(
                _gestureStartState.Position + normalizedDelta,
                _gestureStartState.Rotation,
                _gestureStartState.Scale), false);
        }

        private void UpdateScale(Vector2 pointer, bool fromCenter, bool uniform)
        {
            Vector2 baseSize = GetPortraitContentBaseSize();
            Vector2 initialMagnitude = Abs(_gestureStartState.Scale);
            Vector2 initialFullSize = Vector2.Scale(baseSize, initialMagnitude);
            GetPortraitContentGeometry(_gestureStartState, out Vector2 center, out _);
            Vector2 newFullSize;
            Vector2 newCenter;

            if (fromCenter)
            {
                Vector2 local = RotateVector(pointer - center, -_gestureStartState.Rotation);
                if (uniform)
                {
                    Vector2 initialHalfVector = Vector2.Scale(_scaleHandleDirection, initialFullSize * 0.5f);
                    float factor = Vector2.Dot(local, initialHalfVector) /
                                   Mathf.Max(0.0001f, initialHalfVector.sqrMagnitude);
                    factor = Mathf.Max(0.02f, factor);
                    newFullSize = initialFullSize * factor;
                }
                else
                {
                    newFullSize = new Vector2(
                        Mathf.Max(8f, _scaleHandleDirection.x * local.x * 2f),
                        Mathf.Max(8f, _scaleHandleDirection.y * local.y * 2f));
                }
                newCenter = center;
            }
            else
            {
                Vector2 local = RotateVector(pointer - _fixedScaleCorner, -_gestureStartState.Rotation);
                if (uniform)
                {
                    Vector2 initialDiagonal = Vector2.Scale(_scaleHandleDirection, initialFullSize);
                    float factor = Vector2.Dot(local, initialDiagonal) /
                                   Mathf.Max(0.0001f, initialDiagonal.sqrMagnitude);
                    factor = Mathf.Max(0.02f, factor);
                    newFullSize = initialFullSize * factor;
                }
                else
                {
                    newFullSize = new Vector2(
                        Mathf.Max(8f, _scaleHandleDirection.x * local.x),
                        Mathf.Max(8f, _scaleHandleDirection.y * local.y));
                }
                newCenter = _fixedScaleCorner + RotateVector(
                    Vector2.Scale(_scaleHandleDirection, newFullSize * 0.5f),
                    _gestureStartState.Rotation);
            }

            Vector2 sign = new Vector2(SignNotZero(_gestureStartState.Scale.x), SignNotZero(_gestureStartState.Scale.y));
            Vector2 newScale = Vector2.Scale(new Vector2(
                newFullSize.x / Mathf.Max(1f, baseSize.x),
                newFullSize.y / Mathf.Max(1f, baseSize.y)), sign);
            Vector2 rootCenter = ContentCenterToRootCenter(
                newCenter,
                newScale,
                _gestureStartState.Rotation);
            SetTarget(new PortraitState(LocalToNormalized(rootCenter), _gestureStartState.Rotation, newScale), false);
        }

        private void UpdateRotation(Vector2 pointer, bool snap)
        {
            Vector2 center = NormalizedToLocal(_gestureStartState.Position);
            float angle = _gestureStartState.Rotation +
                          Mathf.DeltaAngle(_gestureStartAngle, PointerAngle(pointer - center));
            if (snap) angle = Mathf.Round(angle / 10f) * 10f;
            SetTarget(new PortraitState(_gestureStartState.Position, angle, _gestureStartState.Scale), false);
        }

        private void OnStagePointerUp(PointerUpEvent evt)
        {
            if (_gesture == TransformGesture.None) return;
            if (_screen.HasPointerCapture(evt.pointerId)) _screen.ReleasePointer(evt.pointerId);
            EndGesture();
            evt.StopPropagation();
        }

        private void EndGesture()
        {
            _gesture = TransformGesture.None;
            _moveAxisLocked = false;
            _gestureUndoRecorded = false;
        }

        private void PrepareForEditing()
        {
            _playingPreview = false;
            _previewPaused = false;
            _previewCompleted = false;
            _updatingFields = true;
            _timeline?.SetValueWithoutNotify(1f);
            _updatingFields = false;
            RefreshPreviewPose(1f);
            RefreshPreviewControls();
        }

        private bool IsInsideTarget(Vector2 pointer)
        {
            GetPortraitContentGeometry(_target, out Vector2 center, out Vector2 size);
            Vector2 local = RotateVector(pointer - center, -_target.Rotation);
            Vector2 halfSize = size * 0.5f;
            return Mathf.Abs(local.x) <= Mathf.Max(6f, halfSize.x) &&
                   Mathf.Abs(local.y) <= Mathf.Max(6f, halfSize.y);
        }

        private void OnWindowKeyDown(KeyDownEvent evt)
        {
            bool actionKey = evt.ctrlKey || evt.commandKey;
            bool undo = actionKey && evt.keyCode == KeyCode.Z && !evt.shiftKey;
            bool redo = actionKey && (evt.keyCode == KeyCode.Y || evt.keyCode == KeyCode.Z && evt.shiftKey);
            if ((undo || redo) && ReferenceEquals(EditorWindow.mouseOverWindow, this))
            {
                double now = EditorApplication.timeSinceStartup;
                if (!_globalUndoInstalled || now - _lastLocalShortcutTime > 0.05d)
                {
                    _lastLocalShortcutTime = now;
                    if (redo) PerformLocalRedo();
                    else PerformLocalUndo();
                }
                evt.StopImmediatePropagation();
                return;
            }

            if (evt.keyCode != KeyCode.Space || _gesture != TransformGesture.None) return;
            TogglePreview();
            evt.StopImmediatePropagation();
        }

        private static Vector2 Abs(Vector2 value) =>
            new Vector2(Mathf.Abs(value.x), Mathf.Abs(value.y));

        private static float SignNotZero(float value) => value < 0f ? -1f : 1f;

        private static Vector2 RotateVector(Vector2 value, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(value.x * cosine - value.y * sine, value.x * sine + value.y * cosine);
        }

        private static float PointerAngle(Vector2 offset) =>
            Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;

        private void OnStageKeyDown(KeyDownEvent evt)
        {
            float amount = evt.shiftKey ? 0.1f : 0.025f;
            Vector2 direction;
            switch (evt.keyCode)
            {
                case KeyCode.LeftArrow: direction = Vector2.left; break;
                case KeyCode.RightArrow: direction = Vector2.right; break;
                case KeyCode.UpArrow: direction = Vector2.up; break;
                case KeyCode.DownArrow: direction = Vector2.down; break;
                default: return;
            }
            Nudge(direction, amount);
            evt.StopPropagation();
        }

        private Vector2 LocalToNormalized(Vector2 local)
        {
            float width = Mathf.Max(1f, _screen.resolvedStyle.width);
            float height = Mathf.Max(1f, _screen.resolvedStyle.height);
            return new Vector2((local.x / width - 0.5f) * 2f, (0.5f - local.y / height) * 2f);
        }

        private Vector2 NormalizedToLocal(Vector2 normalized)
        {
            float width = Mathf.Max(1f, _screen.resolvedStyle.width);
            float height = Mathf.Max(1f, _screen.resolvedStyle.height);
            return new Vector2((normalized.x * 0.5f + 0.5f) * width, (0.5f - normalized.y * 0.5f) * height);
        }

        private Vector2 NormalizedToPixels(Vector2 normalized)
        {
            Vector2 canvas = Vector2.Scale(normalized, GetCanvasExtent(_margin));
            return canvas + new Vector2(_resolution.x * 0.5f, _resolution.y * 0.5f);
        }

        private Vector2 GetCanvasExtent(float margin) => new Vector2(
            Mathf.Max(1f, _resolution.x * 0.5f + Mathf.Max(0f, margin)),
            Mathf.Max(1f, _resolution.y * 0.5f + Mathf.Max(0f, margin)));

        private Vector2 CanvasToNormalized(Vector2 canvas)
        {
            Vector2 extent = GetCanvasExtent(_margin);
            return new Vector2(canvas.x / extent.x, canvas.y / extent.y);
        }

        private Vector2 SourceNormalizedToVisual(Vector2 normalized, float sourceMargin)
        {
            Vector2 sourceExtent = GetCanvasExtent(sourceMargin);
            Vector2 currentExtent = GetCanvasExtent(_margin);
            return new Vector2(
                normalized.x * sourceExtent.x / currentExtent.x,
                normalized.y * sourceExtent.y / currentExtent.y);
        }

        private Vector2 AuthoredToVisualPosition(Vector2 authored)
        {
            Vector2 position = _positionSpace == CharacterPositionSpace.Canvas
                ? CanvasToNormalized(authored)
                : authored;
            return _relative ? _start.Position + position : position;
        }

        private Vector2 VisualToAuthoredPosition(Vector2 visual)
        {
            Vector2 value = _relative ? visual - _start.Position : visual;
            return _positionSpace == CharacterPositionSpace.Canvas
                ? Vector2.Scale(value, GetCanvasExtent(_margin))
                : value;
        }

        private void SetMargin(float value)
        {
            Vector2 authored = VisualToAuthoredPosition(_target.Position);
            _margin = Mathf.Max(0f, value);
            _marginField?.SetValueWithoutNotify(_margin);
            ResolveStartState();
            _target = new PortraitState(
                _targetSeededFromStart ? _start.Position : AuthoredToVisualPosition(authored),
                _target.Rotation,
                _target.Scale);
            FitScreen();
            RefreshPositionFieldLabel();
            RefreshPreviewPose(_timeline?.value ?? 1f);
        }

        private void TogglePreview()
        {
            float duration = Mathf.Max(0f, _durationField?.value ?? 0f);
            if (_playingPreview)
            {
                _playingPreview = false;
                _previewPaused = true;
                RefreshGhostVisibility();
                RefreshPreviewControls();
                return;
            }

            bool timelineEnded = _timeline.value >= 0.999f;
            float progress = _previewPaused && !timelineEnded ? _timeline.value : 0f;
            _updatingFields = true;
            _timeline.SetValueWithoutNotify(progress);
            _updatingFields = false;
            if (timelineEnded) RefreshPreviewPose(0f);
            if (duration <= 0f)
            {
                _timeline.SetValueWithoutNotify(1f);
                RefreshPreviewPose(1f);
                SetStatus("Zero-duration tween: target applies instantly.", false);
                return;
            }

            _previewStartProgress = progress;
            _previewStartTime = EditorApplication.timeSinceStartup;
            _playingPreview = true;
            _previewPaused = false;
            _previewCompleted = false;
            RefreshPreviewPose(progress);
            RefreshPreviewControls();
        }

        private void StopPreview()
        {
            _playingPreview = false;
            _previewPaused = false;
            _previewCompleted = false;
            _updatingFields = true;
            _timeline?.SetValueWithoutNotify(0f);
            _updatingFields = false;
            RefreshPreviewPose(0f);
            RefreshPreviewControls();
        }

        private void TickPreview()
        {
            RefreshAutomaticResolution(false);
            if (!_playingPreview || _timeline == null) return;
            float duration = Mathf.Max(0.0001f, _durationField?.value ?? 0.5f);
            float elapsed = (float)(EditorApplication.timeSinceStartup - _previewStartTime);
            float progress = _previewStartProgress + elapsed / duration;
            progress = Mathf.Clamp01(progress);
            _updatingFields = true;
            _timeline.SetValueWithoutNotify(progress);
            _updatingFields = false;
            RefreshPreviewPose(progress);
            if (progress < 1f) return;
            _playingPreview = false;
            _previewPaused = false;
            _previewCompleted = true;
            RefreshGhostVisibility();
            RefreshTransformFrame(1f);
            RefreshPreviewControls();
            SetStatus("Preview complete. Confirm when it looks right.", false);
        }

        private void RefreshPreviewControls()
        {
            if (_previewButton == null) return;
            _previewButton.text = _playingPreview ? "Ⅱ Pause" :
                _previewPaused ? "▶ Resume" :
                _previewCompleted ? "↻ Replay" : "▶ Preview";
        }

        private void SaveToNode()
        {
            if (_node == null || _graph == null) return;
            _graph.UndoBeginRecordGraph("Compose Portrait Tween");
            try
            {
                _authoredPosition = VisualToAuthoredPosition(_target.Position);
                TrySetUnconnected(_node.GetInputPortByName("Position"), _authoredPosition);
                TrySetUnconnected(_node.GetInputPortByName("Rotation"), _target.Rotation);
                TrySetUnconnected(_node.GetInputPortByName("Scale"), _target.Scale);
                TrySetUnconnected(_node.GetInputPortByName("Margin"), _margin);
                _node.GetNodeOptionByName("Coordinate Space")?.TrySetValue(_positionSpace);
                _node.GetNodeOptionByName("Relative")?.TrySetValue(_relative);
                _node.GetNodeOptionByName("Animate Transform")?.TrySetValue(_animateTransform);
                _node.GetNodeOptionByName("Instance ID")?.TrySetValue(_instanceID ?? string.Empty);
                _node.GetNodeOptionByName("Duration")?.TrySetValue(Mathf.Max(0f, _durationField.value));
                _node.GetNodeOptionByName("Easing")?.TrySetValue(_easing);
                _node.GetNodeOptionByName("Custom Easing Curve")?.TrySetValue(CloneCurve(_customCurve));
                _node.GetNodeOptionByName("Ease In Out")?.TrySetValue(_easing != PortraitTweenEasing.None);
                _node.GetNodeOptionByName("Wait For Completion")?.TrySetValue(_waitToggle.value);
            }
            finally
            {
                _graph.UndoEndRecordGraph();
            }
            GraphDatabase.SaveGraph(_graph);
            SetStatus("Tween saved to the node.", false);
        }

        private static void TrySetUnconnected<T>(IPort port, T value)
        {
            if (port?.IsConnected != true) port?.TrySetValue(value);
        }

        private void SetStatus(string message, bool warning)
        {
            if (_statusLabel == null) return;
            _statusLabel.text = message;
            _statusLabel.style.color = warning ? (Color)new Color32(251, 191, 36, 255) : SafeAccent;
        }

        private static List<string> EasingNames() => new List<string>
        {
            "None (Linear)", "Ease In", "Ease Out", "Ease In / Out",
            "Anticipation", "Overshoot", "Bounce", "Custom Curve"
        };

        private static int EasingToIndex(PortraitTweenEasing easing) => easing switch
        {
            PortraitTweenEasing.EaseIn => 1,
            PortraitTweenEasing.EaseOut => 2,
            PortraitTweenEasing.EaseInOut => 3,
            PortraitTweenEasing.Anticipation => 4,
            PortraitTweenEasing.Overshoot => 5,
            PortraitTweenEasing.Bounce => 6,
            PortraitTweenEasing.Custom => 7,
            _ => 0
        };

        private static string EasingName(PortraitTweenEasing easing)
        {
            List<string> names = EasingNames();
            return names[EasingToIndex(easing)];
        }

        private static PortraitTweenEasing EasingFromName(string name) => name switch
        {
            "Ease In" => PortraitTweenEasing.EaseIn,
            "Ease Out" => PortraitTweenEasing.EaseOut,
            "Ease In / Out" => PortraitTweenEasing.EaseInOut,
            "Anticipation" => PortraitTweenEasing.Anticipation,
            "Overshoot" => PortraitTweenEasing.Overshoot,
            "Bounce" => PortraitTweenEasing.Bounce,
            "Custom Curve" => PortraitTweenEasing.Custom,
            _ => PortraitTweenEasing.None
        };

        private static PortraitTweenEasing ResolveEasing(INode node)
        {
            PortraitTweenEasing easing = GetOption(node, "Easing", PortraitTweenEasing.EaseInOut);
            bool legacyEaseInOut = GetOption(node, "Ease In Out", true);
            return easing == PortraitTweenEasing.EaseInOut && !legacyEaseInOut
                ? PortraitTweenEasing.None
                : easing;
        }

        private void RefreshEasingControls()
        {
            if (_customCurveContainer != null)
                _customCurveContainer.style.display = _easing == PortraitTweenEasing.Custom
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        private void RefreshAnimationControls()
        {
            if (_durationField != null)
                _durationField.tooltip = _animateTransform
                    ? "Transform time in real-time seconds."
                    : "Stored on the node, but runtime applies the transform immediately while Animate Transform is disabled.";
        }

        private void RefreshPositionFieldLabel()
        {
            if (_positionField == null) return;
            _positionField.label = "Visual target (-1 to +1)";
            Vector2 authored = VisualToAuthoredPosition(_target.Position);
            string mode = _positionSpace == CharacterPositionSpace.Canvas ? "canvas units" : "normalized units";
            string relative = _relative ? "relative displacement" : "absolute position";
            _positionField.tooltip =
                $"The composer always shows the absolute target visually. Confirm Tween writes {authored.x:0.###}, {authored.y:0.###} as a {relative} in {mode}.";
        }

        private void RefreshPortraitSizeLabel()
        {
            if (_portraitSizeLabel == null) return;
            _portraitSizeLabel.text =
                $"Portrait size: {_portraitCanvasSize.x:0.#} × {_portraitCanvasSize.y:0.#} canvas units from {_portraitSizeSource}.";
        }

        private void RestartPreviewAtCurrentPosition()
        {
            _playingPreview = false;
            _previewPaused = false;
            _previewCompleted = false;
            RefreshPreviewPose(_timeline?.value ?? 1f);
            RefreshPreviewControls();
            SetStatus($"Timing changed to {EasingName(_easing)} — preview before confirming.", false);
        }

        private static AnimationCurve CloneCurve(AnimationCurve curve)
        {
            AnimationCurve clone = curve != null && curve.length > 0
                ? new AnimationCurve(curve.keys)
                : AnimationCurve.Linear(0f, 0f, 1f, 1f);
            if (curve != null)
            {
                clone.preWrapMode = curve.preWrapMode;
                clone.postWrapMode = curve.postWrapMode;
            }
            return clone;
        }

        private static AnimationCurve SanitizeCustomCurve(AnimationCurve curve)
        {
            AnimationCurve sanitized = CloneCurve(curve);
            var keys = new List<Keyframe>(sanitized.keys);
            for (int i = 0; i < keys.Count; i++)
            {
                Keyframe key = keys[i];
                key.time = Mathf.Clamp01(key.time);
                key.value = Mathf.Clamp01(key.value);
                keys[i] = key;
            }

            keys.Sort((left, right) => left.time.CompareTo(right.time));
            if (keys.Count == 0 || keys[0].time > 0.0001f)
                keys.Insert(0, new Keyframe(0f, 0f));
            else
            {
                Keyframe first = keys[0];
                first.time = 0f;
                first.value = 0f;
                keys[0] = first;
            }
            if (keys[keys.Count - 1].time < 0.9999f)
                keys.Add(new Keyframe(1f, 1f));
            else
            {
                int lastIndex = keys.Count - 1;
                Keyframe last = keys[lastIndex];
                last.time = 1f;
                last.value = 1f;
                keys[lastIndex] = last;
            }

            return new AnimationCurve(keys.ToArray())
            {
                preWrapMode = sanitized.preWrapMode,
                postWrapMode = sanitized.postWrapMode
            };
        }

        private static bool TryGetSelectedGameViewResolution(out Vector2Int resolution)
        {
            resolution = default;
            try
            {
                Type gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (gameView == null) return false;

                object view = GetMainGameView(gameView);
                if (view == null) return false;
                const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                object selectedSize = gameView.GetProperty("currentGameViewSize", instanceFlags)?.GetValue(view);
                if (selectedSize == null) return false;

                Type sizeType = selectedSize.GetType();
                int width = Convert.ToInt32(sizeType.GetProperty("width", instanceFlags)?.GetValue(selectedSize) ?? 0);
                int height = Convert.ToInt32(sizeType.GetProperty("height", instanceFlags)?.GetValue(selectedSize) ?? 0);
                string kind = sizeType.GetProperty("sizeType", instanceFlags)?.GetValue(selectedSize)?.ToString();

                // Fixed Resolution entries contain the exact authored output size.
                // Aspect-ratio entries contain values such as 16x9, so only those
                // need the rendered target size as a derived fallback.
                if (string.Equals(kind, "FixedResolution", StringComparison.OrdinalIgnoreCase) &&
                    width > 0 && height > 0)
                {
                    resolution = new Vector2Int(width, height);
                    return true;
                }

                object target = gameView.GetProperty("targetRenderSize", instanceFlags)?.GetValue(view);
                if (target is Vector2 renderSize && renderSize.x > 0f && renderSize.y > 0f)
                {
                    resolution = new Vector2Int(Mathf.RoundToInt(renderSize.x), Mathf.RoundToInt(renderSize.y));
                    return true;
                }
                if (target is Vector2Int integerSize && integerSize.x > 0 && integerSize.y > 0)
                {
                    resolution = integerSize;
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static object GetMainGameView(Type gameViewType)
        {
            const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Type playModeView = Type.GetType("UnityEditor.PlayModeView,UnityEditor");
            if (playModeView != null)
            {
                foreach (MethodInfo method in playModeView.GetMethods(staticFlags))
                {
                    if (method.Name != "GetMainPlayModeView" || method.GetParameters().Length != 0) continue;
                    object main = method.Invoke(null, null);
                    if (main != null && gameViewType.IsInstanceOfType(main)) return main;
                }
            }

            UnityEngine.Object[] views = Resources.FindObjectsOfTypeAll(gameViewType);
            return views != null && views.Length > 0 ? views[0] : null;
        }

        private List<string> ResolutionNames() => new List<string>
        {
            "Game View (Auto)",
            "1920x1080 (16:9)",
            "2560x1440 (16:9)",
            "1280x720 (16:9)",
            "1080x1920 (Portrait)",
            "1920x1200 (16:10)",
            "1024x1024 (Square)"
        };

        private void SetResolution(string name)
        {
            _followGameViewResolution = name.StartsWith("Game View", StringComparison.Ordinal);
            Vector2Int next = _resolution;
            if (_followGameViewResolution)
            {
                if (TryGetSelectedGameViewResolution(out Vector2Int selected)) next = selected;
            }
            else if (name.StartsWith("2560", StringComparison.Ordinal)) next = new Vector2Int(2560, 1440);
            else if (name.StartsWith("1280", StringComparison.Ordinal)) next = new Vector2Int(1280, 720);
            else if (name.StartsWith("1080", StringComparison.Ordinal)) next = new Vector2Int(1080, 1920);
            else if (name.StartsWith("1920x1200", StringComparison.Ordinal)) next = new Vector2Int(1920, 1200);
            else if (name.StartsWith("1024", StringComparison.Ordinal)) next = new Vector2Int(1024, 1024);
            else next = new Vector2Int(1920, 1080);
            ApplyResolution(next);
        }

        private void RefreshAutomaticResolution(bool force)
        {
            if (!_followGameViewResolution || _gesture != TransformGesture.None) return;
            double now = EditorApplication.timeSinceStartup;
            if (!force && now < _nextResolutionCheck) return;
            _nextResolutionCheck = now + 0.35d;
            if (!TryGetSelectedGameViewResolution(out Vector2Int selected)) return;
            if (selected == _resolution) return;
            ApplyResolution(selected);
        }

        private void ApplyResolution(Vector2Int resolution)
        {
            Vector2 authored = VisualToAuthoredPosition(_target.Position);
            _resolution = resolution;
            ResolveStartState();
            _target = new PortraitState(
                _targetSeededFromStart ? _start.Position : AuthoredToVisualPosition(authored),
                _target.Rotation,
                _target.Scale);
            ResolvePortraitCanvasSize();
            RefreshResolutionLabel();
            RefreshPortraitSizeLabel();
            RefreshPositionFieldLabel();
            FitScreen();
        }

        private void RefreshResolutionLabel()
        {
            if (_resolutionLabel == null) return;
            string source = _followGameViewResolution ? "Game View Auto  |  " : string.Empty;
            _resolutionLabel.text = $"{source}{_resolution.x} x {_resolution.y}  |  {AspectLabel(_resolution)}";
        }

        private static string AspectLabel(Vector2Int resolution)
        {
            int divisor = GreatestCommonDivisor(resolution.x, resolution.y);
            return divisor > 0 ? $"{resolution.x / divisor}:{resolution.y / divisor}" : "Unknown";
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            a = Mathf.Abs(a);
            b = Mathf.Abs(b);
            while (b != 0)
            {
                int next = a % b;
                a = b;
                b = next;
            }
            return a;
        }

        private void AddGridLine(bool vertical, float fraction, float opacity)
        {
            VisualElement line = new VisualElement();
            line.style.position = UnityEngine.UIElements.Position.Absolute;
            line.style.backgroundColor = new Color(0.39f, 0.55f, 0.72f, opacity);
            line.pickingMode = PickingMode.Ignore;
            if (vertical)
            {
                line.style.left = Length.Percent(fraction * 100f);
                line.style.top = 0f;
                line.style.bottom = 0f;
                line.style.width = 1f;
            }
            else
            {
                line.style.top = Length.Percent(fraction * 100f);
                line.style.left = 0f;
                line.style.right = 0f;
                line.style.height = 1f;
            }
            _screen.Add(line);
        }

        private void AddEdgeLabel(string text, float side, float offset, bool left, bool bottom = false)
        {
            Label label = new Label(text);
            label.style.position = UnityEngine.UIElements.Position.Absolute;
            label.style.fontSize = 9f;
            label.style.color = new Color(0.58f, 0.68f, 0.8f, 0.75f);
            if (left) label.style.left = side;
            else label.style.right = side;
            if (bottom) label.style.bottom = offset;
            else label.style.top = offset;
            label.pickingMode = PickingMode.Ignore;
            _screen.Add(label);
        }

        private static void AddSectionTitle(VisualElement parent, string title)
        {
            Label label = new Label(title);
            label.style.fontSize = 10f;
            label.style.letterSpacing = 0.7f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = Accent;
            label.style.marginTop = parent.childCount == 0 ? 0f : 18f;
            label.style.marginBottom = 7f;
            parent.Add(label);
        }

        private static VisualElement Row()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4f;
            row.style.marginBottom = 4f;
            return row;
        }

        private static Label Badge(string text, Color color)
        {
            Label badge = new Label(text);
            badge.style.fontSize = 10f;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.color = color;
            badge.style.backgroundColor = new Color(color.r, color.g, color.b, 0.13f);
            badge.style.paddingLeft = 8f;
            badge.style.paddingRight = 8f;
            badge.style.paddingTop = 4f;
            badge.style.paddingBottom = 4f;
            badge.style.borderTopLeftRadius = 4f;
            badge.style.borderTopRightRadius = 4f;
            badge.style.borderBottomLeftRadius = 4f;
            badge.style.borderBottomRightRadius = 4f;
            return badge;
        }

        private static void SetBorder(VisualElement element, Color color, float width)
        {
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
        }

        private T GetOption<T>(string name, T fallback) => GetOption(_node, name, fallback);

        private static T GetOption<T>(INode node, string name, T fallback)
        {
            INodeOption option = node?.GetNodeOptionByName(name);
            return option != null && option.TryGetValue(out T value) ? value : fallback;
        }
    }
}
