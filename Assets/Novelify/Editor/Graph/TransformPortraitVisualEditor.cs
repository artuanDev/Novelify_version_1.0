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
            public float Duration;
            public bool Ease;
            public bool Wait;
            public bool Clamp;
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
                    Mathf.LerpAngle(from.Rotation, to.Rotation, t),
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
        private static Delegate _globalUndoHandler;
        private static bool _globalUndoInstalled;

        private TransformSpeakerPortraitNode _node;
        private Graph _graph;
        private NovelCharacter _character;
        private string _instanceID;
        private PortraitState _start;
        private PortraitState _target;
        private string _startSource;
        private Vector2Int _resolution;
        private bool _targetSeededFromStart;

        private VisualElement _previewHost;
        private VisualElement _screen;
        private VisualElement _safeArea;
        private VisualElement _ghost;
        private VisualElement _targetPortrait;
        private VisualElement _transformFrame;
        private Label _resolutionLabel;
        private Label _coordinateLabel;
        private Label _startSourceLabel;
        private Label _previewTimeLabel;
        private Label _statusLabel;
        private HelpBox _connectedHelp;
        private Vector2Field _positionField;
        private FloatField _rotationField;
        private Vector2Field _scaleField;
        private FloatField _durationField;
        private Toggle _easeToggle;
        private Toggle _waitToggle;
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
            TransformPortraitVisualEditor window = GetWindow<TransformPortraitVisualEditor>(true, "Portrait Tween", true);
            window.minSize = new Vector2(980f, 650f);
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
            if (_node != null) Rebuild();
        }

        private void OnDisable() => EditorApplication.update -= TickPreview;

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
            if (_targetSeededFromStart) _target = _start;

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
            _previewHost.style.minWidth = 480f;
            _previewHost.style.alignItems = Align.Center;
            _previewHost.style.justifyContent = Justify.Center;
            _previewHost.style.paddingLeft = 24f;
            _previewHost.style.paddingRight = 24f;
            _previewHost.style.paddingTop = 24f;
            _previewHost.style.paddingBottom = 24f;
            _previewHost.RegisterCallback<GeometryChangedEvent>(_ => FitScreen());
            parent.Add(_previewHost);

            _screen = new VisualElement();
            _screen.style.position = UnityEngine.UIElements.Position.Relative;
            _screen.style.overflow = Overflow.Hidden;
            _screen.style.backgroundColor = (Color)new Color32(5, 13, 27, 255);
            SetBorder(_screen, Accent, 1f);
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
                    RefreshPortraits();
            });
            _previewHost.Add(_screen);

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
            _screen.Add(_safeArea);

            _ghost = CreatePortraitGroup(0.24f, StartAccent, "START");
            _ghost.pickingMode = PickingMode.Ignore;
            _screen.Add(_ghost);
            _targetPortrait = CreatePortraitGroup(1f, Accent, "TARGET");
            _targetPortrait.pickingMode = PickingMode.Ignore;
            _screen.Add(_targetPortrait);

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

            _clampToggle = new Toggle("Keep target center on screen") { value = true };
            _clampToggle.tooltip = "Constrains normalized target coordinates to the visible -1..+1 screen frame.";
            _clampToggle.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                EditorSnapshot before = CaptureSnapshot();
                before.Clamp = evt.previousValue;
                RecordLocalUndo(before, "Change screen clamp");
            });
            inspector.Add(_clampToggle);

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
                new PortraitState(Vector2.zero, 0f, Vector2.one), true, "Reset transform")) { text = "Reset" };
            resetTransform.style.flexGrow = 1f;
            resetRow.Add(resetStart);
            resetRow.Add(resetTransform);
            inspector.Add(resetRow);

            AddSectionTitle(inspector, "ANIMATION");
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
            _easeToggle = new Toggle("Ease in / out") { value = GetOption("Ease In Out", true) };
            _waitToggle = new Toggle("Wait for completion") { value = GetOption("Wait For Completion", true) };
            _easeToggle.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                EditorSnapshot before = CaptureSnapshot();
                before.Ease = evt.previousValue;
                RecordLocalUndo(before, "Change easing");
            });
            _waitToggle.RegisterValueChangedCallback(evt =>
            {
                if (_updatingFields) return;
                EditorSnapshot before = CaptureSnapshot();
                before.Wait = evt.previousValue;
                RecordLocalUndo(before, "Change wait behavior");
            });
            inspector.Add(_easeToggle);
            inspector.Add(_waitToggle);

            AddSectionTitle(inspector, "VIEW GUIDES");
            _resolutionDropdown = new DropdownField("Preview resolution", ResolutionNames(), 0);
            _resolutionDropdown.tooltip = "Game View (Auto) follows the currently selected Game View resolution live.";
            _resolutionDropdown.RegisterValueChangedCallback(evt => SetResolution(evt.newValue));
            inspector.Add(_resolutionDropdown);
            _safeAreaToggle = new Toggle("Show safe-area guide") { value = true };
            _safeAreaToggle.RegisterValueChangedCallback(_ => RefreshSafeArea());
            inspector.Add(_safeAreaToggle);
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
                "Move: drag the portrait (Shift locks the dominant axis). Scale: pull a corner (Shift scales from center, Ctrl keeps it uniform). Rotate: drag any side (Ctrl snaps to 10 degrees). Space previews the tween. Ctrl+Z/Ctrl+Y use this window's local history only while the mouse is over it.",
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
            _target = new PortraitState(
                ResolvePosition(_node),
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

            return _target.Position == Vector2.zero &&
                   Mathf.Approximately(_target.Rotation, 0f) &&
                   _target.Scale == Vector2.one &&
                   !GetOption("Relative", false) &&
                   !GetOption("Animate Transform", false);
        }

        private void ResolveStartState()
        {
            if (Application.isPlaying)
            {
                foreach (CharacterInfo info in Resources.FindObjectsOfTypeAll<CharacterInfo>())
                {
                    if (info == null || !info.gameObject.scene.IsValid() || info.character != _character ||
                        !string.Equals(info.InstanceID ?? string.Empty, _instanceID, StringComparison.Ordinal)) continue;
                    _start = new PortraitState(info.AnchoredToNormalizedPosition(info.Position), info.Rotation, info.Scale);
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
                    if (space == CharacterPositionSpace.Canvas) position = CanvasToNormalized(position);
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
                    if (space == CharacterPositionSpace.Canvas) position = CanvasToNormalized(position);
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

        private void RefreshAll()
        {
            _updatingFields = true;
            _positionField?.SetValueWithoutNotify(_target.Position);
            _rotationField?.SetValueWithoutNotify(_target.Rotation);
            _scaleField?.SetValueWithoutNotify(_target.Scale);
            _timeline?.SetValueWithoutNotify(1f);
            _updatingFields = false;

            _previewCompleted = false;

            RefreshResolutionLabel();
            if (_startSourceLabel != null)
                _startSourceLabel.text = $"Ghost start: {_startSource}";
            RefreshConnectedHelp();
            FitScreen();
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
            float aspect = _resolution.x / (float)_resolution.y;
            float width = Mathf.Min(maxWidth, maxHeight * aspect);
            float height = width / aspect;
            _screen.style.width = width;
            _screen.style.height = height;
            RefreshSafeArea();
            RefreshPortraits();
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
            float eased = _easeToggle?.value == false ? progress : SmoothStep(progress);
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
            if (_screen == null) return new Vector2(100f, 160f);
            float screenWidth = Mathf.Max(1f, _screen.resolvedStyle.width);
            float screenHeight = Mathf.Max(1f, _screen.resolvedStyle.height);
            float aspect = PortraitAspect();
            float height = screenHeight * 0.58f;
            float width = height * aspect;
            if (width > screenWidth * 0.72f)
            {
                width = screenWidth * 0.72f;
                height = width / Mathf.Max(0.05f, aspect);
            }
            return new Vector2(width, height);
        }

        private void RefreshTransformFrame(float progress)
        {
            if (_transformFrame == null || _screen == null) return;
            bool editableTargetVisible = !_playingPreview && progress >= 0.999f;
            _transformFrame.style.display = editableTargetVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!editableTargetVisible) return;

            Vector2 baseSize = GetPortraitBaseSize();
            Vector2 size = Vector2.Scale(baseSize, Abs(_target.Scale));
            size.x = Mathf.Max(8f, size.x);
            size.y = Mathf.Max(8f, size.y);
            Vector2 center = NormalizedToLocal(_target.Position);
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

        private float PortraitAspect()
        {
            if (_character == null) return 0.65f;
            CharacterPortrait portrait = _character.GetPortrait(CharacterEmotion.Neutral);
            Sprite sprite = portrait.Body ?? portrait.Eyes ?? portrait.Details ?? portrait.Mouth;
            if (sprite == null || sprite.rect.height <= 0f) return 0.65f;
            return Mathf.Clamp(sprite.rect.width / sprite.rect.height, 0.15f, 4f);
        }

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
            Duration = _durationField?.value ?? GetOption("Duration", 0.5f),
            Ease = _easeToggle?.value ?? GetOption("Ease In Out", true),
            Wait = _waitToggle?.value ?? GetOption("Wait For Completion", true),
            Clamp = _clampToggle?.value ?? true
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
                _positionField?.SetValueWithoutNotify(snapshot.Target.Position);
                _rotationField?.SetValueWithoutNotify(snapshot.Target.Rotation);
                _scaleField?.SetValueWithoutNotify(snapshot.Target.Scale);
                _durationField?.SetValueWithoutNotify(snapshot.Duration);
                _easeToggle?.SetValueWithoutNotify(snapshot.Ease);
                _waitToggle?.SetValueWithoutNotify(snapshot.Wait);
                _clampToggle?.SetValueWithoutNotify(snapshot.Clamp);
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

            Vector2 baseSize = GetPortraitBaseSize();
            Vector2 halfSize = Vector2.Scale(baseSize, Abs(_gestureStartState.Scale)) * 0.5f;
            Vector2 center = NormalizedToLocal(_gestureStartState.Position);
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
            Vector2 baseSize = GetPortraitBaseSize();
            Vector2 initialMagnitude = Abs(_gestureStartState.Scale);
            Vector2 initialFullSize = Vector2.Scale(baseSize, initialMagnitude);
            Vector2 center = NormalizedToLocal(_gestureStartState.Position);
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
            SetTarget(new PortraitState(LocalToNormalized(newCenter), _gestureStartState.Rotation, newScale), false);
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
            Vector2 center = NormalizedToLocal(_target.Position);
            Vector2 local = RotateVector(pointer - center, -_target.Rotation);
            Vector2 halfSize = Vector2.Scale(GetPortraitBaseSize(), Abs(_target.Scale)) * 0.5f;
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

        private Vector2 NormalizedToPixels(Vector2 normalized) => new Vector2(
            (normalized.x * 0.5f + 0.5f) * _resolution.x,
            (normalized.y * 0.5f + 0.5f) * _resolution.y);

        private Vector2 CanvasToNormalized(Vector2 canvas) => new Vector2(
            _resolution.x > 0 ? canvas.x / (_resolution.x * 0.5f) : 0f,
            _resolution.y > 0 ? canvas.y / (_resolution.y * 0.5f) : 0f);

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
                TrySetUnconnected(_node.GetInputPortByName("Position"), _target.Position);
                TrySetUnconnected(_node.GetInputPortByName("Rotation"), _target.Rotation);
                TrySetUnconnected(_node.GetInputPortByName("Scale"), _target.Scale);
                _node.GetNodeOptionByName("Coordinate Space")?.TrySetValue(CharacterPositionSpace.Normalized);
                _node.GetNodeOptionByName("Relative")?.TrySetValue(false);
                _node.GetNodeOptionByName("Animate Transform")?.TrySetValue(true);
                _node.GetNodeOptionByName("Duration")?.TrySetValue(Mathf.Max(0f, _durationField.value));
                _node.GetNodeOptionByName("Ease In Out")?.TrySetValue(_easeToggle.value);
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

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);

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
            if (_followGameViewResolution)
            {
                if (TryGetSelectedGameViewResolution(out Vector2Int selected)) _resolution = selected;
            }
            else if (name.StartsWith("2560", StringComparison.Ordinal)) _resolution = new Vector2Int(2560, 1440);
            else if (name.StartsWith("1280", StringComparison.Ordinal)) _resolution = new Vector2Int(1280, 720);
            else if (name.StartsWith("1080", StringComparison.Ordinal)) _resolution = new Vector2Int(1080, 1920);
            else if (name.StartsWith("1920x1200", StringComparison.Ordinal)) _resolution = new Vector2Int(1920, 1200);
            else if (name.StartsWith("1024", StringComparison.Ordinal)) _resolution = new Vector2Int(1024, 1024);
            else _resolution = new Vector2Int(1920, 1080);
            RefreshResolutionLabel();
            FitScreen();
        }

        private void RefreshAutomaticResolution(bool force)
        {
            if (!_followGameViewResolution || _gesture != TransformGesture.None) return;
            double now = EditorApplication.timeSinceStartup;
            if (!force && now < _nextResolutionCheck) return;
            _nextResolutionCheck = now + 0.35d;
            if (!TryGetSelectedGameViewResolution(out Vector2Int selected)) return;
            if (selected == _resolution) return;
            _resolution = selected;
            RefreshResolutionLabel();
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
