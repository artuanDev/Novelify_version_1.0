using System;
using System.Linq;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

namespace Novelify.Editor
{
    internal sealed class NovelAnimationPreviewWindow : EditorWindow
    {
        private IGraphWindow _source;
        private CharacterAnimationNode _node;
        private PreviewRenderUtility _preview;
        private GameObject _stage;
        private CharacterInfo _portrait;
        private NovelCharacter _character, _overrideCharacter;
        private NovelGraphRunner _runner;
        private GameObject _prefab;
        private Vector2 _resolution = new Vector2(1920f, 1080f);
        private Vector2 _basePosition;
        private Vector2 _baseScale = Vector2.one;
        private float _baseRotation;
        private CharacterEmotion _emotion;
        private float _amplitude, _frequency, _duration, _time;
        private float _speed = 1f;
        private bool _playing = true, _loop = true;
        private double _lastUpdate, _nextRead;
        private string _signature;
        private MaterialPropertyBlock _graphicProperties;
        private readonly Dictionary<Graphic, Mesh> _renderMeshes = new();

        internal static void Open(IGraphWindow source, CharacterAnimationNode node)
        {
            var window = GetWindow<NovelAnimationPreviewWindow>();
            window.titleContent = new GUIContent("Animation Preview");
            window.minSize = new Vector2(580f, 450f);
            window._source = source;
            window._node = node;
            window._overrideCharacter = null;
            window._runner = UnityEngine.Object.FindObjectsByType<NovelGraphRunner>(FindObjectsInactive.Include).FirstOrDefault(candidate => candidate.RuntimeGraph != null &&
                    AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(candidate.RuntimeGraph)) ==
                    source.Graph.AssetGuid.ToString());
            window._signature = null;
            window._time = 0f;
            window._playing = true;
            window.RefreshSettings();
            window.Show();
        }

        private void OnEnable()
        {
            _graphicProperties = new MaterialPropertyBlock();
            _lastUpdate = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += ReleasePreview;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= ReleasePreview;
            ReleasePreview();
        }

        private bool HasNode => (_source as EditorWindow) != null && _source?.Graph != null && _node != null &&
            _source.Graph.GetNodes().Any(candidate => candidate.ID == _node.ID);

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Min((float)(now - _lastUpdate), 0.1f);
            _lastUpdate = now;
            if (!HasNode) { ReleasePreview(); return; }
            if (now >= _nextRead)
            {
                _nextRead = now + 0.15d;
                RefreshSettings();
            }
            if (_playing)
            {
                _time += delta * _speed;
                float length = TimelineLength;
                if (_duration > 0f && _time > length)
                {
                    if (_loop) _time %= length;
                    else { _time = length; _playing = false; }
                }
            }
            Repaint();
        }

        private float TimelineLength => _duration > 0f ? _duration : Mathf.Max(5f, _time);

        private void RefreshSettings()
        {
            if (!HasNode) return;
            Graph graph = _source.Graph;
            _amplitude = NovelGraphValues.Resolve<float>(graph, _node.GetInputPortByName("Amplitude"));
            _frequency = NovelGraphValues.Resolve<float>(graph, _node.GetInputPortByName("Frequency"));
            _duration = NovelGraphValues.Resolve<float>(graph, _node.GetInputPortByName("Duration"));
            if (!Finite(_amplitude) || !Finite(_frequency) || !Finite(_duration))
                _amplitude = _frequency = _duration = 0f;
            _amplitude = Mathf.Max(0f, _amplitude);
            _frequency = Mathf.Max(0f, _frequency);
            _duration = Mathf.Max(0f, _duration);
            var reference = NovelGraphValues.Resolve<NovelCharacterReference>(graph,
                _node.GetInputPortByName("Character Reference"));
            NovelCharacter character = _overrideCharacter != null ? _overrideCharacter : reference.Character != null
                ? reference.Character : NovelGraphValues.Resolve<NovelCharacter>(graph, _node.GetInputPortByName("Character"));
            GameObject prefab = _runner != null ? _runner.PortraitPrefab : null;
            Vector2 resolution = _runner != null ? _runner.PresentationReferenceResolution : _resolution;
            resolution.x = Mathf.Clamp(Finite(resolution.x) ? resolution.x : 1920f, 100f, 7680f);
            resolution.y = Mathf.Clamp(Finite(resolution.y) ? resolution.y : 1080f, 100f, 4320f);
            string signature = $"{character?.GetEntityId()}:{prefab?.GetEntityId()}:{resolution}:" +
                $"{_amplitude:R}:{_frequency:R}:{_duration:R}:{_emotion}:{_basePosition}:{_baseScale}:{_baseRotation:R}";
            if (_signature == signature && _portrait != null) return;
            _signature = signature;
            _character = character;
            _prefab = prefab;
            _resolution = resolution;
            _time = 0f;
            BuildPreview();
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void BuildPreview()
        {
            ReleasePreview();
            if (_character == null) return;
            _preview = new PreviewRenderUtility();
            Camera camera = _preview.camera;
            camera.orthographic = true;
            camera.orthographicSize = _resolution.y * 0.5f;
            camera.transform.position = new Vector3(0f, 0f, -2000f);
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 4000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            _stage = new GameObject("Novelify Animation Preview", typeof(RectTransform), typeof(Canvas));
            _stage.hideFlags = HideFlags.HideAndDontSave;
            _preview.AddSingleGO(_stage);
            var canvas = _stage.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            ((RectTransform)_stage.transform).sizeDelta = _resolution;

            GameObject portrait;
            if (_prefab != null) portrait = UnityEngine.Object.Instantiate(_prefab, _stage.transform, false);
            else
            {
                portrait = new GameObject("Portrait", typeof(RectTransform), typeof(CharacterInfo));
                portrait.transform.SetParent(_stage.transform, false);
                ((RectTransform)portrait.transform).sizeDelta = new Vector2(520f, 760f);
                var info = portrait.GetComponent<CharacterInfo>();
                info.Body = Layer("Body", portrait.transform);
                info.Eyes = Layer("Eyes", portrait.transform);
                info.Details = Layer("Details", portrait.transform);
                info.Mouth = Layer("Mouth", portrait.transform);
            }
            portrait.hideFlags = HideFlags.HideAndDontSave;
            portrait.SetActive(true);
            _portrait = portrait.GetComponent<CharacterInfo>() ?? portrait.AddComponent<CharacterInfo>();
            _portrait.TimeMode = DialogueTimeMode.Unscaled;
            // Portrait initialization schedules blinking with Random. Preserve the global state.
            UnityEngine.Random.State random = UnityEngine.Random.state;
            try
            {
                Canvas.ForceUpdateCanvases();
                _portrait.AnchorAtStageBottomCenter();
                _portrait.Initialize(_character);
                _portrait.SetEmotion(_emotion);
            }
            finally { UnityEngine.Random.state = random; }
            _portrait.Position = _basePosition;
            _portrait.Rotation = _baseRotation;
            _portrait.Scale = _baseScale;
            _portrait.Opacity = 1f;
        }

        private static Image Layer(string name, Transform parent)
        {
            var layer = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)layer.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            var image = layer.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private void ReleasePreview()
        {
            foreach (Mesh mesh in _renderMeshes.Values) UnityEngine.Object.DestroyImmediate(mesh);
            _renderMeshes.Clear();
            _portrait = null;
            if (_stage != null) UnityEngine.Object.DestroyImmediate(_stage);
            _stage = null;
            _preview?.Cleanup();
            _preview = null;
        }

        internal Texture RenderPreview(Rect frame)
        {
            _portrait.SampleEditorAnimation(_node.Animation, _amplitude, _frequency, _duration, _time);
            Canvas canvas = _stage.GetComponent<Canvas>();
            canvas.enabled = true;
            foreach (Graphic graphic in _portrait.GetComponentsInChildren<Graphic>()) graphic.SetAllDirty();
            _preview.BeginPreview(frame, GUIStyle.none);
            Canvas.ForceUpdateCanvases();
            // Canvas batches in preview scenes can retain old transform matrices.
            // Draw the real UI meshes with their current matrices to make scrubbing
            // immediate, including sprite UVs, layer colors, scale, and rotation.
            using (var commands = new CommandBuffer { name = "Novelify portrait preview" })
            {
                commands.SetRenderTarget(_preview.camera.targetTexture);
                commands.SetViewport(new Rect(0f, 0f, _preview.camera.targetTexture.width, _preview.camera.targetTexture.height));
                commands.DisableScissorRect();
                commands.SetViewProjectionMatrices(_preview.camera.worldToCameraMatrix,
                    _preview.camera.projectionMatrix);
                foreach (Graphic graphic in _portrait.GetComponentsInChildren<Graphic>())
                {
                    if (!graphic.isActiveAndEnabled) continue;
                    if (!_renderMeshes.TryGetValue(graphic, out Mesh mesh))
                    {
                        Mesh source = graphic.canvasRenderer.GetMesh();
                        if (source == null || source.vertexCount == 0) continue;
                        mesh = UnityEngine.Object.Instantiate(source);
                        mesh.hideFlags = HideFlags.HideAndDontSave;
                        _renderMeshes.Add(graphic, mesh);
                    }
                    if (mesh == null || mesh.vertexCount == 0) continue;
                    _graphicProperties.Clear();
                    _graphicProperties.SetTexture("_MainTex", graphic.mainTexture);
                    Color color = graphic.canvasRenderer.GetColor();
                    color.a *= graphic.canvasRenderer.GetInheritedAlpha();
                    _graphicProperties.SetColor("_Color", color);
                    commands.DrawMesh(mesh, graphic.transform.localToWorldMatrix, graphic.materialForRendering, 0, -1, _graphicProperties);
                }
                canvas.enabled = false;
                try
                {
                    _preview.Render(true, false);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                finally { canvas.enabled = true; }
            }
            return _preview.EndPreview();
        }

        private void OnGUI()
        {
            if (!HasNode)
            {
                EditorGUILayout.HelpBox("Double-click a Bounce, Shake, or Sway Character node to preview it. " +
                    "After scripts reload, reopen the preview from the graph.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField(_node.Title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Amplitude {_amplitude:0.###}   Frequency {_frequency:0.###} Hz   " +
                (_duration > 0f ? $"Duration {_duration:0.###} s" : "Continuous"));
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(_playing ? "Pause" : "Play", EditorStyles.toolbarButton)) _playing = !_playing;
                if (GUILayout.Button("Restart", EditorStyles.toolbarButton)) { _time = 0f; _playing = true; }
                if (GUILayout.Button("Step 1/60 s", EditorStyles.toolbarButton))
                { _playing = false; _time = _duration > 0f ? Mathf.Min(_time + 1f / 60f, TimelineLength) : _time + 1f / 60f; }
                if (GUILayout.Button("Reload Portrait", EditorStyles.toolbarButton)) { _signature = null; RefreshSettings(); }
                _loop = GUILayout.Toggle(_loop, "Loop", EditorStyles.toolbarButton);
            }
            _speed = EditorGUILayout.Slider("Playback speed", _speed, 0.1f, 2f);
            EditorGUI.BeginChangeCheck();
            float scrub = EditorGUILayout.Slider("Time (seconds)", _time, 0f, TimelineLength);
            if (EditorGUI.EndChangeCheck()) { _time = scrub; _playing = false; }

            EditorGUI.BeginChangeCheck();
            _runner = (NovelGraphRunner)EditorGUILayout.ObjectField("Game setup (optional)", _runner, typeof(NovelGraphRunner), true);
            _overrideCharacter = (NovelCharacter)EditorGUILayout.ObjectField("Preview character", _overrideCharacter, typeof(NovelCharacter), false);
            _emotion = (CharacterEmotion)EditorGUILayout.EnumPopup("Preview emotion", _emotion);
            if (_runner == null) _resolution = EditorGUILayout.Vector2Field("Canvas resolution", _resolution);
            _basePosition = EditorGUILayout.Vector2Field("Starting position (canvas)", _basePosition);
            _baseScale = EditorGUILayout.Vector2Field("Starting scale", _baseScale);
            _baseRotation = EditorGUILayout.FloatField("Starting rotation", _baseRotation);
            if (EditorGUI.EndChangeCheck()) { _signature = null; RefreshSettings(); }
            if (_node.GetInputPorts().Any(port => port.IsConnected && port.DataType != null && port.DataType != typeof(Untyped)))
                EditorGUILayout.HelpBox("Connected values use authoring defaults where resolvable. Runtime variables and " +
                    "computed inputs can differ; use Preview character if the target cannot be resolved.", MessageType.Warning);
            EditorGUILayout.HelpBox("Uses the game's CharacterInfo animation code. Game setup supplies its portrait prefab " +
                "and reference resolution. Set the starting transform to match the moment you want to inspect. " +
                "Node value changes refresh automatically. Preview controls never write to your graph or scene.", MessageType.Info);

            Rect area = GUILayoutUtility.GetRect(100f, 100f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (_portrait == null)
            {
                GUI.Label(area, "Assign a Character on the node, or choose a Preview character above.", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            if (Event.current.type != EventType.Repaint || area.width < 1f || area.height < 1f) return;
            float aspect = _resolution.x / _resolution.y;
            float width = Mathf.Min(area.width, area.height * aspect);
            Rect frame = new Rect(area.center.x - width * 0.5f, area.center.y - width / aspect * 0.5f, width, width / aspect);
            GUI.DrawTexture(frame, RenderPreview(frame), ScaleMode.StretchToFill, false);
        }
    }
}

