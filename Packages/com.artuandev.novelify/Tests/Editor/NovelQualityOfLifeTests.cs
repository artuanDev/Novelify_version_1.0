using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Novelify.Editor;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Novelify.Tests
{
    public sealed class NovelQualityOfLifeTests
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private string folder;
        private NovelGraph graph;
        private NovelCharacter character;
        private bool undoEnded;

        [SetUp]
        public void SetUp()
        {
            folder = "Assets/NovelifyQolTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            graph = GraphDatabase.CreateGraph<NovelGraph>(folder + "/Story.novelgraph");
            character = ScriptableObject.CreateInstance<NovelCharacter>();
            character.SpeakerName = "Preview Hero";
            AssetDatabase.CreateAsset(character, folder + "/Character.asset");
            graph.UndoBeginRecordGraph("Build QoL test");
            undoEnded = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (!undoEnded) graph.UndoEndRecordGraph();
            graph.OnDisable();
            AssetDatabase.DeleteAsset(folder);
        }

        private T Add<T>() where T : Node, new()
        {
            var node = new T();
            graph.AddNode(node);
            return node;
        }

        private static Type EditorType(string name) => typeof(NovelGraph).Assembly.GetType("Novelify.Editor." + name, true);

        [TestCase(NovelCharacterAnimation.Bounce)]
        [TestCase(NovelCharacterAnimation.Shake)]
        [TestCase(NovelCharacterAnimation.Sway)]
        public void PreviewMatchesRuntimeAndRestoresAuthoredTransform(NovelCharacterAnimation animation)
        {
            Scene scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var go = new GameObject("Preview", typeof(RectTransform), typeof(CharacterInfo));
            SceneManager.MoveGameObjectToScene(go, scene);
            CharacterInfo info = go.GetComponent<CharacterInfo>();
            try
            {
                info.Position = new Vector2(80f, 40f);
                info.Rotation = 20f;
                const float seconds = 0.237f;
                info.StartSimpleAnimation(animation, 15f, 3f, 1f);
                typeof(CharacterInfo).GetField("_animationElapsed", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(info, seconds - Time.unscaledDeltaTime);
                typeof(CharacterInfo).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(info, null);
                Vector3 position = info.transform.localPosition;
                Quaternion rotation = info.transform.localRotation;
                info.StopSimpleAnimation();
                info.SampleEditorAnimation(animation, 15f, 3f, 1f, seconds);
                Assert.That(info.transform.localPosition, Is.EqualTo(position));
                Assert.That(Quaternion.Angle(info.transform.localRotation, rotation), Is.LessThan(0.001f));
                Assert.That(info.Position, Is.EqualTo(new Vector2(80f, 40f)));
                info.SampleEditorAnimation(animation, 15f, 3f, 1f, 1f);
                Assert.That(info.IsAnimating, Is.False);
                Assert.That(((RectTransform)info.transform).anchoredPosition, Is.EqualTo(new Vector2(80f, 40f)));
                Assert.That(info.transform.localEulerAngles.z, Is.EqualTo(20f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void SamplingRejectsRealSceneObjects()
        {
            var go = new GameObject("Scene portrait", typeof(CharacterInfo));
            try
            {
                Assert.Throws<InvalidOperationException>(() => go.GetComponent<CharacterInfo>()
                    .SampleEditorAnimation(NovelCharacterAnimation.Bounce, 10f, 2f, 0f, 1f));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void SearchFindsDialogueCharacterAndNodeId()
        {
            DialogueNode node = Add<DialogueNode>();
            node.GetNodeOptionByName("Dialogue").TrySetValue(new RichDialogueText("The silver lighthouse"));
            node.GetInputPortByName("Speaker").TrySetValue(character);
            MethodInfo matches = EditorType("NovelGraphToolsWindow").GetMethod("Matches", Static);
            foreach (string search in new[] { "LIGHTHOUSE", "Preview Hero", node.ID.ToString() })
                Assert.That(matches.Invoke(null, new object[] { graph, node, search }), Is.True, search);
            Assert.That(matches.Invoke(null, new object[] { graph, node, "absent phrase" }), Is.False);
        }

        [Test]
        public void BookmarksAndChecksDoNotMutateGraph()
        {
            var node = Add<BounceCharacterNode>();
            node.GetInputPortByName("Amplitude").TrySetValue(0f);
            Type tools = EditorType("NovelGraphToolsWindow");
            MethodInfo toggle = tools.GetMethod("ToggleBookmark", Static);
            MethodInfo bookmarked = tools.GetMethod("IsBookmarked", Static);
            string before = JsonUtility.ToJson(node);
            try
            {
                toggle.Invoke(null, new object[] { graph, node });
                Assert.That(bookmarked.Invoke(null, new object[] { graph, node }), Is.True);
                toggle.Invoke(null, new object[] { graph, node });
                Assert.That(bookmarked.Invoke(null, new object[] { graph, node }), Is.False);
                IEnumerable checks = (IEnumerable)EditorType("NovelGraphChecks").GetMethod("Analyze", Static).Invoke(null, new object[] { graph });
                string[] messages = checks.Cast<object>().Select(check => (string)check.GetType()
                    .GetField("Message", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(check)).ToArray();
                Assert.That(messages.Any(message => message.Contains("Start")), Is.True);
                Assert.That(messages.Any(message => message.Contains("assign a Character")), Is.True);
                Assert.That(messages.Any(message => message.Contains("Amplitude")), Is.True);
                Assert.That(JsonUtility.ToJson(node), Is.EqualTo(before));
            }
            finally
            {
                EditorPrefs.DeleteKey("Novelify.Bookmark." + Application.dataPath + "." + graph.AssetGuid + "." + node.ID);
            }
        }

        [UnityTest]
        public IEnumerator DoubleClickOpensIsolatedPreviewRendersAndSearchFramesDistantNode()
        {
            var texture = new Texture2D(32, 48);
            texture.SetPixels(Enumerable.Repeat(Color.green, 32 * 48).ToArray());
            texture.Apply();
            AssetDatabase.CreateAsset(texture, folder + "/PreviewTexture.asset");
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 32f, 48f), new Vector2(0.5f, 0f));
            AssetDatabase.AddObjectToAsset(sprite, texture);
            character.PortraitBody = sprite;
            EditorUtility.SetDirty(character);
            AssetDatabase.SaveAssetIfDirty(character);
            var start = Add<StartNode>();
            var node = Add<BounceCharacterNode>();
            var end = Add<EndNode>();
            node.Position = new Vector2(8000f, 6000f);
            node.GetInputPortByName("Character").TrySetValue(character);
            Assert.That(graph.Connect(start.GetOutputPortByName("out"), node.GetInputPortByName("in")), Is.True);
            Assert.That(graph.Connect(node.GetOutputPortByName("out"), end.GetInputPortByName("in")), Is.True);
            graph.UndoEndRecordGraph();
            undoEnded = true;
            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(folder + "/Story.novelgraph", ImportAssetOptions.ForceSynchronousImport);
            Assert.That(AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(folder + "/Story.novelgraph")), Is.True);
            yield return null;
            yield return null;
            EditorWindow source = Resources.FindObjectsOfTypeAll<EditorWindow>().First(window =>
                window is IGraphWindow graphWindow && graphWindow.Graph?.AssetGuid == graph.AssetGuid);
            source.position = new Rect(50f, 50f, 1100f, 750f);
            yield return null;
            INode liveNode = ((IGraphWindow)source).Graph.GetNodes().First(candidate => candidate.ID == node.ID);
            EditorType("NovelQualityOfLifeBridge").GetField("_nextScan", Static).SetValue(null, 0d);
            EditorType("NovelQualityOfLifeBridge").GetMethod("Scan", Static).Invoke(null, null);
            var tools = EditorType("NovelGraphToolsWindow");
            Assert.That(tools.GetMethod("FrameNode", Static).Invoke(null, new object[] { source, liveNode }), Is.True);
            yield return null;
            yield return null;
            VisualElement view = source.rootVisualElement.Query<VisualElement>().ToList().First(element =>
                ReferenceEquals(EditorType("NovelQualityOfLifeBridge").GetMethod("NodeAt", Static)
                    .Invoke(null, new object[] { (IGraphWindow)source, element }), liveNode));
            VisualElement graphView = source.rootVisualElement.Query<VisualElement>().ToList().First(element =>
                (bool)EditorType("NovelQualityOfLifeBridge").GetMethod("IsView", Static).Invoke(null, new object[] { element, "GraphView" }));
            Assert.That(graphView.worldBound.Contains(view.worldBound.center), Is.True,
                "Search should bring the distant node into the visible canvas.");
            Assert.That(source.rootVisualElement.ClassListContains("novelify-qol-hook"), Is.True);
            for (int count = 1; count <= 2; count++)
            {
                using (PointerDownEvent click = PointerDownEvent.GetPooled(new Event
                    { type = EventType.MouseDown, button = 0, clickCount = count, mousePosition = view.worldBound.center }))
                {
                    click.target = view;
                    view.SendEvent(click);
                }
                using (PointerUpEvent up = PointerUpEvent.GetPooled(new Event
                    { type = EventType.MouseUp, button = 0, mousePosition = view.worldBound.center }))
                {
                    up.target = view;
                    view.SendEvent(up);
                }
            }

            yield return null;
            EditorWindow previewWindow = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(window => window.GetType() == EditorType("NovelAnimationPreviewWindow"));
            Assert.That(previewWindow, Is.Not.Null, "Double-click should open the animation preview.");
            try
            {
                CharacterInfo portrait = (CharacterInfo)previewWindow.GetType().GetField("_portrait",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(previewWindow);
                Assert.That(portrait, Is.Not.Null);
                Assert.That(UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(portrait.gameObject), Is.True);
                Assert.That(portrait.character, Is.EqualTo(character));
                var rendering = (PreviewRenderUtility)previewWindow.GetType().GetField("_preview",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(previewWindow);
                previewWindow.GetType().GetField("_time", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(previewWindow, 0.25f);
                MethodInfo render = previewWindow.GetType().GetMethod("RenderPreview", BindingFlags.Instance | BindingFlags.NonPublic);
                Texture rendered = (Texture)render.Invoke(previewWindow, new object[] { new Rect(0f, 0f, 640f, 360f) });
                RenderTexture previous = RenderTexture.active;
                var capture = new Texture2D(rendered.width, rendered.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = (RenderTexture)rendered;
                    capture.ReadPixels(new Rect(0f, 0f, rendered.width, rendered.height), 0, 0);
                    capture.Apply();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../Library/NovelifyPreviewRenderDebug.png"), capture.EncodeToPNG());
                    Assert.That(capture.GetPixels().Count(pixel => pixel.g > 0.5f && pixel.r < 0.2f && pixel.b < 0.2f),
                        Is.GreaterThan(100), "The preview must render the character's sprite, not just its background.");
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,
                        "../Library/NovelifyAnimationPreviewVerification.png"), capture.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(capture); }
                previewWindow.GetType().GetField("_amplitude", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(previewWindow, 150f);
                rendered = (Texture)render.Invoke(previewWindow, new object[] { new Rect(0f, 0f, 640f, 360f) });
                Assert.That(((RectTransform)portrait.transform).anchoredPosition.y, Is.EqualTo(150f).Within(0.001f));
                previous = RenderTexture.active;
                capture = new Texture2D(rendered.width, rendered.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = (RenderTexture)rendered;
                    capture.ReadPixels(new Rect(0f, 0f, rendered.width, rendered.height), 0, 0);
                    capture.Apply();
                    var pixels = capture.GetPixels();
                    int minimum = Enumerable.Range(0, pixels.Length).Where(index => pixels[index].g > 0.5f &&
                        pixels[index].r < 0.2f && pixels[index].b < 0.2f).Min(index => index / capture.width);
                    Assert.That(minimum, Is.GreaterThan(20), "A 150-unit bounce must lift the rendered sprite above the stage baseline.");
                }
                finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(capture); }
                liveNode.GetInputPortByName("Amplitude").TrySetValue(41f);
                previewWindow.GetType().GetMethod("RefreshSettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(previewWindow, null);
                Assert.That((float)previewWindow.GetType().GetField("_amplitude", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(previewWindow), Is.EqualTo(41f));
                Assert.That(UnityEngine.Object.FindObjectsByType<CharacterInfo>(FindObjectsInactive.Include).All(info => info.character != character), Is.True);
            }
            finally { previewWindow.Close(); source.Close(); }
        }
    }
}






