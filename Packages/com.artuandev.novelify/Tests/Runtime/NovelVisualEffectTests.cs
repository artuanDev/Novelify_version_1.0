using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Novelify.Tests
{
    public class NovelVisualEffectTests
    {
        private GameObject _canvas, _host;
        private RectTransform _surface, _panel, _stage;
        private NovelGraphRunner _runner;
        private RuntimeNovelGraph _graph;
        private Camera _camera;
        private RenderTexture _cameraTexture;
        private RenderPipelineAsset _originalPipeline, _originalQualityPipeline;
#if UNITY_EDITOR
        private bool _originalAsyncShaderCompilation;
#endif

        private sealed class CallbackPipelineAsset : RenderPipelineAsset
        {
            protected override RenderPipeline CreatePipeline() => new CallbackPipeline();
        }

        private sealed class CallbackPipeline : RenderPipeline
        {
            protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
            {
                foreach (Camera camera in cameras)
                {
                    BeginCameraRendering(context, camera);
                    EndCameraRendering(context, camera);
                }
                context.Submit();
            }
        }

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            // Pixel checks need the real UI shader, not the editor's compiling placeholder.
            _originalAsyncShaderCompilation = UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            // Manual Camera.Render checks use the built-in pipeline even in URP projects.
            _originalPipeline = GraphicsSettings.defaultRenderPipeline;
            _originalQualityPipeline = QualitySettings.renderPipeline;
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            _camera = new GameObject("Shake camera", typeof(Camera)).GetComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.transform.position = new Vector3(4f, 8f, -10f);
            _cameraTexture = new RenderTexture(128, 128, 16);
            _camera.targetTexture = _cameraTexture;
            _canvas = new GameObject("Effect canvas", typeof(RectTransform), typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.GetComponent<Canvas>().worldCamera = _camera;
            _surface = Rect("Authored presentation", _canvas.transform);
            _surface.anchoredPosition = new Vector2(20f, 30f);
            _panel = Rect("Authored dialogue", _surface);
            _stage = Rect("Authored stage", _surface);
            _host = new GameObject("Effect runner");
            _host.SetActive(false);
            _runner = _host.AddComponent<NovelGraphRunner>();
            _runner.CanvasDialogue = _surface.gameObject;
            _runner.DialoguePanel = _panel.gameObject;
            _runner.CharacterContainer = _stage;
            _runner.ScreenShakeCamera = _camera;
            _host.SetActive(true);
            _graph = ScriptableObject.CreateInstance<RuntimeNovelGraph>();
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = _originalAsyncShaderCompilation;
#endif
            Time.timeScale = 1f;
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_canvas);
            Object.DestroyImmediate(_graph);
            Object.DestroyImmediate(_camera.gameObject);
            Object.DestroyImmediate(_cameraTexture);
            GraphicsSettings.defaultRenderPipeline = _originalPipeline;
            QualitySettings.renderPipeline = _originalQualityPipeline;
        }


        private void PlayEffect(RuntimeNode effect)
        {
            effect.NodeID = "effect";
            effect.NextNodeID = "after";
            _graph.EntryNodeID = "before";
            _graph.AllNodes = new List<RuntimeNode>
            {
                new RuntimeDialogueNode { NodeID = "before", NextNodeID = "effect", DialogueText = "Before", ShowTextImmediately = true },
                effect,
                new RuntimeDialogueNode { NodeID = "after", DialogueText = "After", ShowTextImmediately = true }
            };
            _runner.Session.Play(_graph);
        }

        [UnityTest]
        public IEnumerator BlockingShakeMovesCameraAndScreenSpaceUiAndRestoresBoth()
        {
            PlayEffect(new RuntimeScreenShakeNode { Duration = 0.12f, Amplitude = 30f });
            yield return null;
            Vector2 origin = _surface.anchoredPosition;
            Vector2 panelOrigin = _panel.anchoredPosition;
            Vector2 stageOrigin = _stage.anchoredPosition;
            Vector3 cameraOrigin = _camera.transform.position;
            _runner.Session.Advance();
            Assert.That(_runner.IsWaiting, Is.True);
            Assert.That(_panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(Vector3.Distance(_camera.transform.position, cameraOrigin), Is.GreaterThan(0.001f));
            Assert.That(_camera.enabled, Is.False, "Shake must move the transform even without rendering.");
            Assert.That(Vector2.Distance(_surface.anchoredPosition, origin), Is.GreaterThan(0.01f));
            Assert.That(_panel.anchoredPosition, Is.EqualTo(panelOrigin));
            Assert.That(_stage.anchoredPosition, Is.EqualTo(stageOrigin));
            _runner.Session.Advance();
            Assert.That(_runner.CurrentNode.NodeID, Is.EqualTo("effect"));
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(_runner.CurrentNode.NodeID, Is.EqualTo("after"));
            Assert.That(_runner.IsWaiting, Is.False);
            Assert.That(_surface.anchoredPosition, Is.EqualTo(origin));
            Assert.That(_camera.transform.position, Is.EqualTo(cameraOrigin));
        }

        [UnityTest]
        public IEnumerator ScreenShakeDoesNotAccumulateSubpixelCanvasPositionChanges()
        {
            _surface.anchorMin = Vector2.zero;
            _surface.anchorMax = Vector2.one;
            _surface.sizeDelta = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            Vector2 origin = _surface.anchoredPosition;
            Vector3 cameraOrigin = _camera.transform.position;
            var presentation = _host.GetComponent<NovelGeneratedPresentation>();
            presentation.BeginCameraShake(0.25f, 30f, 25f, _camera, null);
            float deadline = Time.realtimeSinceStartup + 0.25f;
            while (Time.realtimeSinceStartup < deadline)
            {
                // Camera-space canvases can recompute stretched roots with a
                // small rounding difference after the shake has been applied.
                _surface.anchoredPosition += new Vector2(0.0005f, -0.0005f);
                Canvas.ForceUpdateCanvases();
                yield return null;
                Assert.That(Vector2.Distance(_surface.anchoredPosition, origin) * _canvas.GetComponent<Canvas>().scaleFactor, Is.LessThan(43f),
                    "Each frame must apply one shake offset, without accumulating previous offsets.");
            }
            presentation.StopGraphEffects();
            Assert.That(Vector2.Distance(_surface.anchoredPosition, origin), Is.LessThan(0.01f));
            Assert.That(_surface.sizeDelta, Is.EqualTo(Vector2.zero));
            Assert.That(_camera.transform.position, Is.EqualTo(cameraOrigin));
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator ExampleSceneCameraSpaceShakeRestoresStretchedCanvasRoots()
        {
            const string scenePath = "Assets/Novelify/Samples/Scenes/TestScene.unity";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(scenePath) == null)
                Assert.Ignore("The development-project example scene is not installed.");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scenePath,
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            try
            {
                yield return null;
                NovelGraphRunner runner = null;
                Canvas canvas = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out Canvas candidate)) canvas = candidate;
                    foreach (var candidateRunner in root.GetComponentsInChildren<NovelGraphRunner>()) runner = candidateRunner;
                }
                Assert.That(runner, Is.Not.Null);
                Assert.That(canvas, Is.Not.Null);
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
                var presentation = runner.GetComponent<NovelGeneratedPresentation>();
                Camera camera = presentation.ResolveShakeCamera();
                runner.Session.Stop();
                Canvas.ForceUpdateCanvases();
                var roots = new List<(RectTransform Rect, Vector2 Position, Vector2 Size)>();
                foreach (Transform child in canvas.transform)
                    if (child is RectTransform rect) roots.Add((rect, rect.anchoredPosition, rect.sizeDelta));
                Vector3 cameraOrigin = camera.transform.position;
                presentation.BeginCameraShake(0.3f, 24f, 25f, camera, null);
                float deadline = Time.realtimeSinceStartup + 0.3f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    Canvas.ForceUpdateCanvases();
                    foreach (var target in roots)
                        Assert.That(Vector2.Distance(target.Rect.anchoredPosition, target.Position) * canvas.scaleFactor,
                            Is.LessThan(35f), "The example scene must stay within the authored shake amplitude.");
                    yield return null;
                }
                presentation.StopGraphEffects();
                Canvas.ForceUpdateCanvases();
                foreach (var target in roots)
                {
                    Assert.That(Vector2.Distance(target.Rect.anchoredPosition, target.Position), Is.LessThan(0.01f), target.Rect.name);
                    Assert.That(target.Rect.sizeDelta, Is.EqualTo(target.Size), target.Rect.name);
                }
                Assert.That(Vector3.Distance(camera.transform.position, cameraOrigin), Is.LessThan(0.0001f));
            }
            finally { if (scene.IsValid()) UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene); }
            yield return null;
        }
#endif

        [UnityTest]
        public IEnumerator NonBlockingCameraShakeContinuesAndRestoresTheLatestCameraPose()
        {
            PlayEffect(new RuntimeScreenShakeNode { Duration = 1f, Amplitude = 30f, WaitForCompletion = false });
            yield return null;
            Vector2 origin = _surface.anchoredPosition;
            Vector3 cameraOrigin = _camera.transform.position;
            _runner.Session.Advance();
            Assert.That(_runner.CurrentNode.NodeID, Is.EqualTo("after"));
            Assert.That(_runner.IsWaiting, Is.False);
            yield return null;
            Assert.That(Vector3.Distance(_camera.transform.position, cameraOrigin), Is.GreaterThan(0.001f));
            Vector3 movedOrigin = cameraOrigin + new Vector3(2f, -1f, 0f);
            _camera.transform.position = movedOrigin;
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(_camera.transform.position, movedOrigin), Is.GreaterThan(0.001f));
            _runner.Session.Stop();
            Assert.That(_surface.anchoredPosition, Is.EqualTo(origin));
            Assert.That(_camera.transform.position, Is.EqualTo(movedOrigin));
            yield return null;
            Assert.That(_surface.anchoredPosition, Is.EqualTo(origin));
        }

        [UnityTest]
        public IEnumerator ShakeHonorsTheSelectedClockAndDisableCancelsContinuation()
        {
            PlayEffect(new RuntimeScreenShakeNode { Duration = 0.1f, Amplitude = 30f });
            yield return null;
            Vector2 origin = _surface.anchoredPosition;
            Vector3 cameraOrigin = _camera.transform.position;
            _runner.TimeMode = DialogueTimeMode.Scaled;
            Time.timeScale = 0f;
            _runner.Session.Advance();
            Vector3 pausedPose = _camera.transform.position;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(_runner.CurrentNode.NodeID, Is.EqualTo("effect"));
            Assert.That(_camera.transform.position, Is.EqualTo(pausedPose));
            _runner.enabled = false;
            Assert.That(_surface.anchoredPosition, Is.EqualTo(origin));
            Assert.That(_runner.CurrentNode, Is.Null);
            Assert.That(_camera.transform.position, Is.EqualTo(cameraOrigin));
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(_runner.CurrentNode, Is.Null);
        }

        [UnityTest]
        public IEnumerator ReplacingShakeDoesNotAccumulateOffsetsOrInvokeCancelledContinuation()
        {
            NovelGeneratedPresentation presentation = _host.GetComponent<NovelGeneratedPresentation>();
            Vector3 origin = _camera.transform.position;
            bool cancelledCompleted = false;
            bool completed = false;
            presentation.BeginCameraShake(1f, 30f, 25f, _camera, () => cancelledCompleted = true);
            Assert.That(Vector3.Distance(_camera.transform.position, origin), Is.GreaterThan(0.001f));
            presentation.BeginCameraShake(0.1f, 20f, 25f, _camera, () => completed = true);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(_camera.transform.position, Is.EqualTo(origin));
            Assert.That(cancelledCompleted, Is.False);
            Assert.That(completed, Is.True);
        }

        [UnityTest]
        public IEnumerator DisablingPresentationRestoresCameraImmediately()
        {
            NovelGeneratedPresentation presentation = _host.GetComponent<NovelGeneratedPresentation>();
            Vector3 origin = _camera.transform.position;
            bool completed = false;
            presentation.BeginCameraShake(1f, 30f, 25f, _camera, () => completed = true);
            Assert.That(Vector3.Distance(_camera.transform.position, origin), Is.GreaterThan(0.001f));
            presentation.enabled = false;
            Assert.That(_camera.transform.position, Is.EqualTo(origin));
            yield return null;
            Assert.That(_camera.transform.position, Is.EqualTo(origin));
            Assert.That(completed, Is.False);
        }
        [UnityTest]
        public IEnumerator FlashPreservesDialogueAndStopsItsOverlay()
        {
            PlayEffect(new RuntimeScreenFlashNode { Duration = 1f, Color = Color.red });
            yield return null;
            _runner.Session.Advance();
            Assert.That(_panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            yield return null;
            Transform overlay = _canvas.transform.Find("Story UI (runtime)/Flash Overlay");
            Assert.That(overlay.gameObject.activeSelf, Is.True);
            Assert.That(overlay.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0f));
            _runner.Session.Stop();
            Assert.That(overlay.gameObject.activeSelf, Is.False);
            Assert.That(overlay.GetComponent<CanvasGroup>().alpha, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ZeroDurationAndZeroAmplitudeEffectsContinueImmediately()
        {
            PlayEffect(new RuntimeScreenShakeNode { Duration = 1f, Amplitude = 0f });
            yield return null;
            _runner.Session.Advance();
            Assert.That(_runner.CurrentNode.NodeID, Is.EqualTo("after"));
            Assert.That(_runner.IsWaiting, Is.False);
            PlayEffect(new RuntimeScreenFlashNode { Duration = 0f });
            yield return null;
            _runner.Session.Advance();
            Assert.That(_runner.CurrentNode.NodeID, Is.EqualTo("after"));
            Assert.That(_runner.IsWaiting, Is.False);
        }

        [Test]
        public void ShakeCameraSelectionUsesExplicitCanvasAndMainCameraInOrder()
        {
            NovelGeneratedPresentation presentation = _host.GetComponent<NovelGeneratedPresentation>();
            Assert.That(presentation.ResolveShakeCamera(), Is.SameAs(_camera));
            _runner.ScreenShakeCamera = null;
            _canvas.GetComponent<Canvas>().worldCamera = _camera;
            Assert.That(presentation.ResolveShakeCamera(), Is.SameAs(_camera));
            _canvas.GetComponent<Canvas>().worldCamera = null;
            _camera.tag = "MainCamera";
            _camera.enabled = true;
            Assert.That(presentation.ResolveShakeCamera(), Is.SameAs(_camera));
        }

        private Vector2 RenderRedMarkerCenter()
        {
            RenderTexture previous = RenderTexture.active;
            var snapshot = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            try
            {
                _camera.Render();
                RenderTexture.active = _cameraTexture;
                snapshot.ReadPixels(new Rect(0f, 0f, 128f, 128f), 0, 0);
                Color[] pixels = snapshot.GetPixels();
                Vector2 sum = Vector2.zero;
                int count = 0;
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color color = pixels[index];
                    if (color.r < 0.8f || color.g > 0.2f || color.b > 0.2f) continue;
                    sum += new Vector2(index % 128, index / 128);
                    count++;
                }
                Assert.That(count, Is.GreaterThan(0), "Camera-rendered UI marker must be visible.");
                return sum / count;
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(snapshot);
            }
        }

        private static Vector2 ScreenMarkerCenter(Color markerColor, RectTransform marker, int maximumPixels = int.MaxValue)
        {
            if (Application.isBatchMode)
                Assert.Ignore("Overlay pixels require an interactive rendering frame, not batch mode.");
            var snapshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
            try
            {
                snapshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                Color[] pixels = snapshot.GetPixels();
                Vector2 sum = Vector2.zero;
                int count = 0;
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color color = pixels[index];
                    if (Mathf.Abs(color.r - markerColor.r) > 0.15f ||
                        Mathf.Abs(color.g - markerColor.g) > 0.15f ||
                        Mathf.Abs(color.b - markerColor.b) > 0.15f) continue;
                    sum += new Vector2(index % Screen.width, index / Screen.width);
                    count++;
                }
                if (count == 0)
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../Library/ScreenShakeMissingMarker.png"),
                        snapshot.EncodeToPNG());
                Assert.That(count, Is.GreaterThan(0), "Overlay marker must appear in the rendered frame: " +
                    marker.name + " at " + RectTransformUtility.WorldToScreenPoint(null, marker.position) +
                    " in " + Screen.width + "x" + Screen.height + ".");
                Assert.That(count, Is.LessThanOrEqualTo(maximumPixels), "Clipping must still limit the visible marker area.");
                return sum / count;
            }
            finally { Object.DestroyImmediate(snapshot); }
        }

        private static IEnumerator NextRenderedFrame()
        {
            if (Application.isBatchMode)
            {
                yield return null;
                yield return null;
            }
            else yield return new WaitForEndOfFrame();
        }

        private static RectTransform Marker(string name, Transform parent, Color color, Vector2 position)
        {
            RectTransform marker = Rect(name, parent);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(100f, 100f);
            marker.anchoredPosition = position;
            marker.gameObject.AddComponent<UnityEngine.UI.Image>().color = color;
            return marker;
        }

        [UnityTest]
        public IEnumerator OverlayShakeMovesSeparateBackdropAndNestedUiByTheSameScreenDistance()
        {
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var backdrop = new GameObject("Separate backdrop", typeof(RectTransform), typeof(Canvas));
            Canvas backdropCanvas = backdrop.GetComponent<Canvas>();
            backdropCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            backdropCanvas.scaleFactor = 2f;
            RectTransform marker = Marker("Backdrop marker", backdrop.transform, Color.blue, Vector2.zero);
            try
            {
                PlayEffect(new RuntimeScreenShakeNode { Duration = 1f, Amplitude = 35f });
                yield return null;
                Canvas.ForceUpdateCanvases();
                Vector2 backdropOrigin = RectTransformUtility.WorldToScreenPoint(null, marker.position);
                Vector2 dialogueOrigin = RectTransformUtility.WorldToScreenPoint(null, _panel.position);
                Vector2 portraitOrigin = RectTransformUtility.WorldToScreenPoint(null, _stage.position);
                _runner.Session.Advance();
                Canvas.ForceUpdateCanvases();
                Vector2 backdropDelta = RectTransformUtility.WorldToScreenPoint(null, marker.position) - backdropOrigin;
                Vector2 dialogueDelta = RectTransformUtility.WorldToScreenPoint(null, _panel.position) - dialogueOrigin;
                Vector2 portraitDelta = RectTransformUtility.WorldToScreenPoint(null, _stage.position) - portraitOrigin;
                Assert.That(backdropDelta.magnitude, Is.GreaterThan(0.5f));
                Assert.That(Vector2.Distance(backdropDelta, dialogueDelta), Is.LessThan(0.01f));
                Assert.That(Vector2.Distance(portraitDelta, dialogueDelta), Is.LessThan(0.01f));
                _runner.Session.Stop();
                Canvas.ForceUpdateCanvases();
                Assert.That(RectTransformUtility.WorldToScreenPoint(null, marker.position), Is.EqualTo(backdropOrigin));
                Assert.That(RectTransformUtility.WorldToScreenPoint(null, _panel.position), Is.EqualTo(dialogueOrigin));
                Assert.That(RectTransformUtility.WorldToScreenPoint(null, _stage.position), Is.EqualTo(portraitOrigin));
            }
            finally { Object.DestroyImmediate(backdrop); }
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator AuthoredStarterSceneShakeMovesTheActualBackdropAndDialogue()
        {
            const string scenePath = "Assets/NovelifyStarter 1/Starter.unity";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(scenePath) == null)
                Assert.Ignore("The project-specific starter scene is not included in this package installation.");
            var parameters = new UnityEngine.SceneManagement.LoadSceneParameters(
                UnityEngine.SceneManagement.LoadSceneMode.Additive);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scenePath, parameters);
            RuntimeNovelGraph graph = null;
            try
            {
                yield return null;
                NovelGraphRunner starterRunner = null;
                RectTransform backdrop = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out NovelGraphRunner runner)) starterRunner = runner;
                    if (root.name == "Sample backdrop - edit or replace")
                        backdrop = root.transform.Find("Night sky") as RectTransform;
                }
                Assert.That(starterRunner, Is.Not.Null);
                Assert.That(backdrop, Is.Not.Null);
                var player = starterRunner.GetComponent<NovelPlayerController>();
                if (player != null) player.enabled = false;
                graph = Object.Instantiate(starterRunner.RuntimeGraph);
                RuntimeScreenShakeNode shake = null;
                foreach (RuntimeNode node in graph.AllNodes)
                    if (node is RuntimeScreenShakeNode candidate) { shake = candidate; break; }
                Assert.That(shake, Is.Not.Null, "Use the actual imported Screen Shake node from the starter graph.");
                graph.EntryNodeID = shake.NodeID;
                starterRunner.Session.Stop();
                Canvas.ForceUpdateCanvases();
                RectTransform dialogue = starterRunner.DialoguePanel.transform as RectTransform;
                Vector2 backdropOrigin = RectTransformUtility.WorldToScreenPoint(null, backdrop.position);
                Vector2 dialogueOrigin = RectTransformUtility.WorldToScreenPoint(null, dialogue.position);
                Vector3 cameraOrigin = starterRunner.GetComponent<NovelGeneratedPresentation>().ResolveShakeCamera().transform.position;
                starterRunner.Session.Play(graph);
                Canvas.ForceUpdateCanvases();
                Vector2 backdropDelta = RectTransformUtility.WorldToScreenPoint(null, backdrop.position) - backdropOrigin;
                Vector2 dialogueDelta = RectTransformUtility.WorldToScreenPoint(null, dialogue.position) - dialogueOrigin;
                Assert.That(backdropDelta.magnitude, Is.GreaterThan(0.5f), "The starter artwork must visibly move.");
                Assert.That(Vector2.Distance(dialogueDelta, backdropDelta), Is.LessThan(0.01f));
                Assert.That(Vector3.Distance(starterRunner.GetComponent<NovelGeneratedPresentation>().ResolveShakeCamera().transform.position,
                    cameraOrigin), Is.GreaterThan(0.0001f));
                starterRunner.Session.Stop();
                Canvas.ForceUpdateCanvases();
                Assert.That(RectTransformUtility.WorldToScreenPoint(null, backdrop.position), Is.EqualTo(backdropOrigin));
                Assert.That(RectTransformUtility.WorldToScreenPoint(null, dialogue.position), Is.EqualTo(dialogueOrigin));
            }
            finally
            {
                if (graph != null) Object.DestroyImmediate(graph);
                if (scene.IsValid()) UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }
#endif
        [UnityTest]
        public IEnumerator ScreenShakeVisiblyMovesOverlayBackdropPortraitAndDialogueTogether()
        {
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _camera.targetTexture = null;
            _camera.enabled = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            var backdrop = new GameObject("Separate starter backdrop", typeof(RectTransform), typeof(Canvas));
            Canvas backdropCanvas = backdrop.GetComponent<Canvas>();
            backdropCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            backdropCanvas.sortingOrder = -20;
            backdropCanvas.scaleFactor = 2f;
            RectTransform backdropMarker = Marker("Backdrop", backdrop.transform, Color.blue, new Vector2(-100f, 0f));
            RectTransform dialogueMarker = Marker("Dialogue", _surface, Color.red, new Vector2(200f, 0f));
            RectTransform portraitMarker = Marker("Portrait", _stage, Color.green, new Vector2(0f, 200f));
            Vector2 backdropOrigin = backdropMarker.anchoredPosition;
            Vector2 surfaceOrigin = _surface.anchoredPosition;
            Vector2 stageOrigin = _stage.anchoredPosition;
            try
            {
                PlayEffect(new RuntimeScreenShakeNode { Duration = 2f, Amplitude = 35f, WaitForCompletion = false });
                yield return null;
                yield return NextRenderedFrame();
                Vector2 backgroundBefore = ScreenMarkerCenter(Color.blue, backdropMarker);
                Vector2 portraitBefore = ScreenMarkerCenter(Color.green, portraitMarker);
                Vector2 dialogueBefore = ScreenMarkerCenter(Color.red, dialogueMarker);
                _runner.Session.Advance();
                Assert.That(_runner.CurrentNode.NodeID, Is.EqualTo("after"));
                yield return NextRenderedFrame();
                Vector2 backgroundDelta = ScreenMarkerCenter(Color.blue, backdropMarker) - backgroundBefore;
                Vector2 portraitDelta = ScreenMarkerCenter(Color.green, portraitMarker) - portraitBefore;
                Vector2 dialogueDelta = ScreenMarkerCenter(Color.red, dialogueMarker) - dialogueBefore;
                Assert.That(dialogueDelta.magnitude, Is.GreaterThan(0.5f));
                Assert.That(Vector2.Distance(backgroundDelta, dialogueDelta), Is.LessThan(1.5f));
                Assert.That(Vector2.Distance(portraitDelta, dialogueDelta), Is.LessThan(1.5f));
                Assert.That(Vector2.Distance(_surface.anchoredPosition, surfaceOrigin), Is.GreaterThan(0.01f));
                Assert.That(Vector2.Distance(backdropMarker.anchoredPosition, backdropOrigin), Is.GreaterThan(0.01f));
                Assert.That(_stage.anchoredPosition, Is.EqualTo(stageOrigin), "Nested stage must not receive a second offset.");
                _runner.Session.Stop();
                Assert.That(_surface.anchoredPosition, Is.EqualTo(surfaceOrigin));
                Assert.That(backdropMarker.anchoredPosition, Is.EqualTo(backdropOrigin));
                Assert.That(_canvas.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                yield return NextRenderedFrame();
                Assert.That(Vector2.Distance(ScreenMarkerCenter(Color.blue, backdropMarker), backgroundBefore), Is.LessThan(0.1f));
            }
            finally { Object.DestroyImmediate(backdrop); }
        }

        [UnityTest]
        public IEnumerator CameraShakeVisiblyMovesWorldSpaceUi()
        {
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            Canvas canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _camera;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = new Vector2(1000f, 1000f);
            canvasRect.position = _camera.transform.position + _camera.transform.forward * 2f;
            canvasRect.localScale = Vector3.one * 0.01f;
            RectTransform marker = Rect("Camera UI marker", _canvas.transform);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(180f, 180f);
            marker.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.red;
            var overlay = new GameObject("Mixed overlay", typeof(RectTransform), typeof(Canvas));
            overlay.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform overlayMarker = Marker("Mixed overlay marker", overlay.transform, Color.green, Vector2.zero);
            try
            {
                PlayEffect(new RuntimeScreenShakeNode { Duration = 1f, Amplitude = 30f, WaitForCompletion = false });
                yield return null;
                Canvas.ForceUpdateCanvases();
                Vector2 origin = RenderRedMarkerCenter();
                Vector2 overlayOrigin = RectTransformUtility.WorldToScreenPoint(null, overlayMarker.position);
                _runner.Session.Advance();
                Vector2 shaken = RenderRedMarkerCenter();
                Canvas.ForceUpdateCanvases();
                Vector2 cameraDelta = shaken - origin;
                Assert.That(cameraDelta.magnitude, Is.GreaterThan(0.5f));
                var overlayGraphic = overlayMarker.GetComponent<UnityEngine.UI.Graphic>();
                Assert.That(overlayGraphic.materialForRendering, Is.SameAs(overlayGraphic.material));
                _runner.Session.Stop();
                Assert.That(Vector2.Distance(RenderRedMarkerCenter(), origin), Is.LessThan(0.1f));
                Assert.That(RectTransformUtility.WorldToScreenPoint(null, overlayMarker.position), Is.EqualTo(overlayOrigin));
            }
            finally { Object.DestroyImmediate(overlay); }
        }

        [UnityTest]
        public IEnumerator ScreenShakeWorksWithoutACameraForOverlayUi()
        {
            _runner.ScreenShakeCamera = null;
            _canvas.GetComponent<Canvas>().worldCamera = null;
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            PlayEffect(new RuntimeScreenShakeNode { Duration = 0.1f, Amplitude = 30f });
            yield return null;
            Vector2 origin = _surface.anchoredPosition;
            _runner.Session.Advance();
            Assert.That(_runner.IsWaiting, Is.True);
            Assert.That(Vector2.Distance(_surface.anchoredPosition, origin), Is.GreaterThan(0.01f));
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(_runner.CurrentNode.NodeID, Is.EqualTo("after"));
            Assert.That(_runner.IsWaiting, Is.False);
            Assert.That(_surface.anchoredPosition, Is.EqualTo(origin));
        }

        [UnityTest]
        public IEnumerator ScreenShakeMovesClippingAndStyledBoxesTogetherAndRestoresThem()
        {
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _camera.targetTexture = null;
            _camera.enabled = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            RectTransform clip = Rect("Clip viewport", _canvas.transform);
            clip.sizeDelta = new Vector2(300f, 200f);
            clip.anchoredPosition = new Vector2(-250f, 0f);
            clip.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            RectTransform marker = Marker("Clipped marker", clip, Color.red, Vector2.zero);
            marker.sizeDelta = new Vector2(600f, 400f);
            RectTransform boxRect = Rect("Styled marker", _canvas.transform);
            boxRect.sizeDelta = new Vector2(220f, 160f);
            boxRect.anchoredPosition = new Vector2(250f, 0f);
            var box = boxRect.gameObject.AddComponent<NovelRoundedGraphic>();
            NovelBoxStyle style = NovelBoxStyle.DialogueDefault;
            style.FillColor = Color.green;
            style.Opacity = 1f;
            style.OutlineEnabled = false;
            style.CornerRadius = 25f;
            box.Apply(style);
            Material originalBoxMaterial = box.material;
            Vector2 markerPose = marker.anchoredPosition;
            Vector2 boxPose = boxRect.anchoredPosition;
            PlayEffect(new RuntimeScreenShakeNode { Duration = 2f, Amplitude = 35f, WaitForCompletion = false });
            yield return null;
            yield return NextRenderedFrame();
            Vector2 redBefore = ScreenMarkerCenter(Color.red, marker, 63000);
            Vector2 greenBefore = ScreenMarkerCenter(Color.green, boxRect);
            _runner.Session.Advance();
            yield return NextRenderedFrame();
            Vector2 redDelta = ScreenMarkerCenter(Color.red, marker, 63000) - redBefore;
            Vector2 greenDelta = ScreenMarkerCenter(Color.green, boxRect) - greenBefore;
            Assert.That(redDelta.magnitude, Is.GreaterThan(0.5f));
            Assert.That(Vector2.Distance(greenDelta, redDelta), Is.LessThan(1.5f));
            Assert.That(marker.anchoredPosition, Is.EqualTo(markerPose));
            Assert.That(Vector2.Distance(boxRect.anchoredPosition, boxPose), Is.GreaterThan(0.01f));
            Assert.That(box.material, Is.SameAs(originalBoxMaterial));
            _runner.Session.Stop();
            Assert.That(boxRect.anchoredPosition, Is.EqualTo(boxPose));
            yield return NextRenderedFrame();
            Assert.That(Vector2.Distance(ScreenMarkerCenter(Color.red, marker, 63000), redBefore), Is.LessThan(0.1f));
            Assert.That(Vector2.Distance(ScreenMarkerCenter(Color.green, boxRect), greenBefore), Is.LessThan(0.1f));
            Assert.That(box.materialForRendering, Is.SameAs(originalBoxMaterial));
        }

        [UnityTest]
        public IEnumerator CameraShakeMovesTheTransformWithAScriptablePipeline()
        {
            PlayEffect(new RuntimeScreenShakeNode { Duration = 1f, Amplitude = 30f, WaitForCompletion = false });
            yield return null;
            var pipeline = ScriptableObject.CreateInstance<CallbackPipelineAsset>();
            Vector3 origin = _camera.transform.position;
            Vector3 observed = origin;
            int rendered = 0;
            System.Action<ScriptableRenderContext, Camera> observe = (_, camera) =>
            {
                if (camera != _camera) return;
                observed = camera.transform.position;
                rendered++;
            };
            RenderPipelineManager.beginCameraRendering += observe;
            try
            {
                GraphicsSettings.defaultRenderPipeline = pipeline;
                _camera.enabled = true;
                _runner.Session.Advance();
                yield return null;
                yield return null;
                Assert.That(rendered, Is.GreaterThan(0));
                Assert.That(Vector3.Distance(observed, origin), Is.GreaterThan(0.001f));
                Assert.That(Vector3.Distance(_camera.transform.position, origin), Is.GreaterThan(0.001f));
                _runner.Session.Stop();
                Assert.That(_camera.transform.position, Is.EqualTo(origin));
            }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= observe;
                GraphicsSettings.defaultRenderPipeline = null;
                Object.DestroyImmediate(pipeline);
            }
        }
    }
}
