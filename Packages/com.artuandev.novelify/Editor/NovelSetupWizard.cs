using System;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Novelify.Editor
{
    /// <summary>Creates a playable, art-free starter scene from the packaged graph authoring tools.</summary>
    public sealed class NovelSetupWizard : EditorWindow
    {
        private enum StarterKind { Dialogue, VisualNovel }

        private StarterKind _kind = StarterKind.VisualNovel;
        private string _folder = "Assets/NovelifyStarter";
        private string _sourceLocale = "en";
        private string _translationLocale = "es";

        [MenuItem("Window/Novelify/Setup Wizard")]
        public static void Open() => GetWindow<NovelSetupWizard>("Novelify Setup");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Create a playable Novelify starter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates a playable scene, editable story graph, styled sample backdrop, " +
                "and source-formatted translation drafts. No artwork or prefab wiring is required.",
                MessageType.Info);
            _kind = (StarterKind)EditorGUILayout.EnumPopup("Starter", _kind);
            _folder = EditorGUILayout.TextField("Create in", _folder).Trim().Replace('\\', '/');
            _sourceLocale = EditorGUILayout.TextField("Source locale", _sourceLocale).Trim();
            _translationLocale = EditorGUILayout.TextField("Draft translation locale", _translationLocale).Trim();
            if (!IsValidFolder(_folder))
                EditorGUILayout.HelpBox("Choose a folder beneath Assets, such as Assets/NovelifyStarter.",
                    MessageType.Error);
            if (string.IsNullOrEmpty(_sourceLocale))
                EditorGUILayout.HelpBox("Enter a source language code such as en or es.", MessageType.Error);
            if (string.IsNullOrEmpty(_translationLocale) ||
                string.Equals(_sourceLocale, _translationLocale, StringComparison.OrdinalIgnoreCase))
                EditorGUILayout.HelpBox("Choose a different locale for editable translation drafts.",
                    MessageType.Error);

            using (new EditorGUI.DisabledScope(!IsValidFolder(_folder) ||
                                               string.IsNullOrEmpty(_sourceLocale) ||
                                               string.IsNullOrEmpty(_translationLocale) ||
                                               string.Equals(_sourceLocale, _translationLocale,
                                                   StringComparison.OrdinalIgnoreCase)))
                if (GUILayout.Button("Create starter scene", GUILayout.Height(36)))
                    CreateStarter();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("After creation", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. Press Play; click or press Space to advance.");
            EditorGUILayout.LabelField("2. Double-click the .novelgraph to edit the story.");
            EditorGUILayout.LabelField("3. Use Backlog, Auto, Skip and Settings in Play mode.");
            EditorGUILayout.LabelField("4. Edit rich translation drafts in the Localization Workspace.");
        }

        private static bool IsValidFolder(string path)
        {
            if (path == null || !path.StartsWith("Assets/", StringComparison.Ordinal)) return false;
            string[] parts = path.Split('/');
            return parts.Length > 1 && parts.Skip(1).All(part =>
                !string.IsNullOrWhiteSpace(part) && part != "." && part != ".." &&
                part.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) < 0);
        }

        private void CreateStarter()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string folder = AssetDatabase.GenerateUniqueAssetPath(_folder.TrimEnd('/'));
            string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || !EnsureFolderExists(parent))
            {
                EditorUtility.DisplayDialog("Invalid folder",
                    "Could not create the requested folder inside Assets.", "OK");
                return;
            }
            AssetDatabase.CreateFolder(parent, name);

            string graphPath = folder + "/Starter.novelgraph";
            bool withChoices = _kind == StarterKind.VisualNovel;
            var sampleSpeaker = CreateInstance<NovelCharacter>();
            sampleSpeaker.name = "Mara";
            sampleSpeaker.SpeakerName = "Mara";
            AssetDatabase.CreateAsset(sampleSpeaker, folder + "/Mara.asset");
            NovelChoiceStyle choiceStyle = withChoices ? CreateSampleChoiceStyle(folder) : null;
            NovelGraph graph = GraphDatabase.CreateGraph<NovelGraph>(graphPath);
            if (graph == null)
            {
                EditorUtility.DisplayDialog("Novelify setup failed",
                    "Unity could not create the Novel Graph.", "OK");
                return;
            }
            PopulateStarterGraph(graph, withChoices, sampleSpeaker, choiceStyle);
            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate);
            RuntimeNovelGraph runtime = AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>(graphPath);
            if (runtime == null || string.IsNullOrEmpty(runtime.EntryNodeID))
            {
                EditorUtility.DisplayDialog("Novelify setup failed",
                    "The graph was created but did not compile. Check the Console.", "OK");
                return;
            }

            var table = CreateInstance<NovelLocalizationTable>();
            table.SourceGraph = runtime;
            table.SourceLocale = _sourceLocale;
            AssetDatabase.CreateAsset(table, folder + "/StarterLocalization.asset");
            NovelLineIndex.Sync(table, NovelLineIndex.Collect(runtime),
                _translationLocale);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.GetComponent<Camera>().orthographic = true;
            cameraObject.GetComponent<Camera>().backgroundColor =
                new Color(0.035f, 0.055f, 0.11f);
            CreateSampleBackdrop();
            var runnerObject = new GameObject("Novelify Runner");
            NovelGraphRunner runner = runnerObject.AddComponent<NovelGraphRunner>();
            runner.RuntimeGraph = runtime;
            runner.LocalizationTable = table;
            runner.Locale = _sourceLocale;
            runner.HideCharactersOnEnd = true;
            runnerObject.AddComponent<NovelPlayerController>();
            EditorSceneManager.SaveScene(scene, folder + "/Starter.unity");
            Selection.activeGameObject = runnerObject;
            EditorGUIUtility.PingObject(runtime);
            EditorUtility.DisplayDialog("Novelify is ready",
                "Press Play to explore the sample. The scene, graph, character and translation drafts are in " + folder,
                "OK");
        }

        /// <summary>Builds the starter story in an existing graph for the wizard and editor tests.</summary>
        public static void PopulateStarterGraph(NovelGraph graph, bool withChoices,
            NovelCharacter speaker = null, NovelChoiceStyle choiceStyle = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            graph.UndoBeginRecordGraph("Create Novelify starter graph");
            try
            {
                var start = new StartNode();
                var intro = new NarrationNode();
                graph.AddNode(start);
                graph.AddNode(intro);
                intro.GetNodeOptionByName("Dialogue").TrySetValue(
                    new RichDialogueText(
                        "<size=125%><b>THE LAST SIGNAL</b></size>\n" +
                        "The observatory has been dark for years. Tonight, a pale light returns to its window."));
                Connect(graph, start, "out", intro, "in");

                if (withChoices)
                {
                    var choice = new ChoiceNode();
                    var flash = new ScreenFlashNode();
                    var choiceLayout = new CreateChoiceLayoutNode();
                    var first = new DialogueNode();
                    var second = new DialogueNode();
                    var shake = new ScreenShakeNode();
                    var firstEnd = new EndNode();
                    var secondEnd = new EndNode();
                    graph.AddNode(choice);
                    graph.AddNode(flash);
                    graph.AddNode(choiceLayout);
                    graph.AddNode(first);
                    graph.AddNode(second);
                    graph.AddNode(shake);
                    graph.AddNode(firstEnd);
                    graph.AddNode(secondEnd);
                    choice.GetNodeOptionByName("Dialogue").TrySetValue(
                        new RichDialogueText(
                            "A signal pulses across the ridge. <i>What will you do?</i>"));
                    var options = ChoiceAuthoringList.CreateDefault();
                    options.Entries[0].ID = "explore";
                    options.Entries[0].Text = "Follow the signal";
                    options.Entries[1].ID = "stay";
                    options.Entries[1].Text = "Stay and decode it";
                    choice.GetNodeOptionByName(ChoiceNode.ChoicesOptionID).TrySetValue(options);
                    choice.DefineNode();
                    first.GetNodeOptionByName("Dialogue").TrySetValue(
                        new RichDialogueText(
                            "<b>There it is.</b> The light is answering us. " +
                            "<link=\"novelify-wave\">Keep moving.</link>"));
                    second.GetNodeOptionByName("Dialogue").TrySetValue(
                        new RichDialogueText(
                            "<i>Wait.</i> The pattern repeats every seven seconds. " +
                            "Someone wanted us to find it."));
                    if (speaker != null)
                    {
                        first.GetInputPortByName("Speaker").TrySetValue(speaker);
                        second.GetInputPortByName("Speaker").TrySetValue(speaker);
                    }
                    flash.GetInputPortByName("Duration").TrySetValue(0.24f);
                    flash.GetNodeOptionByName("Color").TrySetValue(
                        new Color(0.43f, 0.88f, 0.95f, 0.45f));
                    if (choiceStyle != null)
                        choiceLayout.GetNodeOptionByName("Style Asset").TrySetValue(choiceStyle);
                    choiceLayout.GetNodeOptionByName("Panel Size")
                        .TrySetValue(new Vector2(1100f, 560f));
                    choiceLayout.GetNodeOptionByName("Choice Spacing")
                        .TrySetValue(18f);
                    shake.GetInputPortByName("Duration").TrySetValue(0.18f);
                    shake.GetInputPortByName("Amplitude").TrySetValue(9f);
                    Connect(graph, intro, "out", flash, "in");
                    Connect(graph, flash, "out", choiceLayout, "in");
                    Connect(graph, choiceLayout, "out", choice, "in");
                    Connect(graph, choice, "Choice 0", first, "in");
                    Connect(graph, choice, "Choice 1", second, "in");
                    Connect(graph, first, "out", shake, "in");
                    Connect(graph, shake, "out", firstEnd, "in");
                    Connect(graph, second, "out", secondEnd, "in");
                }
                else
                {
                    var end = new EndNode();
                    graph.AddNode(end);
                    Connect(graph, intro, "out", end, "in");
                }
            }
            finally
            {
                graph.UndoEndRecordGraph();
            }
        }

        private static NovelChoiceStyle CreateSampleChoiceStyle(string folder)
        {
            var style = CreateInstance<NovelChoiceStyle>();
            style.name = "Signal choice style";
            style.FontSize = 28f;
            style.ButtonWidth = 730f;
            style.ButtonHeight = 78f;
            style.HorizontalPadding = 24f;
            style.VerticalPadding = 12f;
            style.ShowPanelBackground = true;
            NovelBoxStyle button = NovelChoiceStyle.DefaultBackground;
            button.FillColor = new Color(0.095f, 0.16f, 0.25f, 0.98f);
            button.CornerRadius = 18f;
            button.OutlineEnabled = true;
            button.OutlineColor = new Color(0.36f, 0.66f, 0.72f, 0.65f);
            button.OutlineThickness = 2f;
            style.Background = button;
            NovelBoxStyle panel = NovelChoiceStyle.DefaultPanelBackground;
            panel.FillColor = new Color(0.035f, 0.06f, 0.11f, 0.94f);
            panel.CornerRadius = 28f;
            style.PanelBackground = panel;
            style.HighlightedTint = new Color(0.74f, 1f, 0.98f);
            style.SelectedTint = style.HighlightedTint;
            AssetDatabase.CreateAsset(style, folder + "/ChoiceStyle.asset");
            return style;
        }

        private static void CreateSampleBackdrop()
        {
            var canvasObject = new GameObject("Sample backdrop - edit or replace",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = -20;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasObject.transform;

            BackdropBlock(root, "Night sky", Vector2.zero, Vector2.one,
                new Color(0.035f, 0.055f, 0.12f));
            BackdropBlock(root, "Distant glow", new Vector2(0f, 0.31f),
                new Vector2(1f, 0.72f), new Color(0.12f, 0.16f, 0.30f));
            BackdropBlock(root, "Horizon", new Vector2(0f, 0.26f),
                new Vector2(1f, 0.37f), new Color(0.16f, 0.24f, 0.37f));

            RectTransform halo = BackdropRect(root, "Signal halo",
                new Vector2(0.70f, 0.50f), new Vector2(0.94f, 0.91f));
            BackdropRound(halo, new Color(0.23f, 0.55f, 0.67f, 0.16f), 280f);
            RectTransform light = BackdropRect(root, "Signal light",
                new Vector2(0.77f, 0.59f), new Vector2(0.88f, 0.78f));
            BackdropRound(light, new Color(0.80f, 0.94f, 0.88f, 0.85f), 180f);

            float[] heights = { 0.34f, 0.26f, 0.40f, 0.31f, 0.45f,
                0.29f, 0.38f, 0.26f, 0.42f, 0.32f, 0.36f };
            for (int i = 0; i < heights.Length; i++)
            {
                float left = i / (float)heights.Length;
                float right = (i + 1.05f) / heights.Length;
                BackdropBlock(root, $"Observatory skyline {i + 1}",
                    new Vector2(left, 0.08f),
                    new Vector2(Mathf.Min(1f, right), heights[i]),
                    i % 3 == 0
                        ? new Color(0.06f, 0.10f, 0.18f)
                        : new Color(0.075f, 0.12f, 0.21f));
            }
            BackdropBlock(root, "Foreground", Vector2.zero,
                new Vector2(1f, 0.11f), new Color(0.025f, 0.045f, 0.09f));
            BackdropBlock(root, "Accent rule", new Vector2(0.055f, 0.88f),
                new Vector2(0.115f, 0.885f), new Color(0.40f, 0.86f, 0.87f));
            BackdropText(root, "Story title", "THE LAST SIGNAL",
                new Vector2(0.055f, 0.79f), new Vector2(0.66f, 0.87f),
                52, new Color(0.94f, 0.97f, 1f));
            BackdropText(root, "Story subtitle", "A PLAYABLE NOVELIFY STORY",
                new Vector2(0.057f, 0.74f), new Vector2(0.54f, 0.79f),
                20, new Color(0.55f, 0.77f, 0.83f));
        }

        private static RectTransform BackdropRect(Transform parent, string name,
            Vector2 minimum, Vector2 maximum)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            RectTransform rect = result.GetComponent<RectTransform>();
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void BackdropBlock(Transform parent, string name,
            Vector2 minimum, Vector2 maximum, Color color)
        {
            Image image = BackdropRect(parent, name, minimum, maximum)
                .gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void BackdropRound(RectTransform rect, Color color, float radius)
        {
            NovelRoundedGraphic graphic = rect.gameObject.AddComponent<NovelRoundedGraphic>();
            NovelBoxStyle style = NovelBoxStyle.DialogueDefault;
            style.FillColor = color;
            style.Opacity = 1f;
            style.CornerRadius = radius;
            graphic.Apply(style);
        }

        private static void BackdropText(Transform parent, string name, string value,
            Vector2 minimum, Vector2 maximum, int size, Color color)
        {
            Text text = BackdropRect(parent, name, minimum, maximum)
                .gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
        }

        private static void Connect(Graph graph, Node from, string output, Node to, string input)
        {
            if (!graph.Connect(from.GetOutputPortByName(output), to.GetInputPortByName(input)))
                throw new InvalidOperationException($"Could not connect {from.GetType().Name} to {to.GetType().Name}.");
        }

        private static bool EnsureFolderExists(string path)
        {
            if (path == "Assets") return true;
            if (!IsValidFolder(path)) return false;
            string current = "Assets";
            foreach (string segment in path.Split('/').Skip(1))
            {
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment);
                if (!AssetDatabase.IsValidFolder(next)) return false;
                current = next;
            }
            return true;
        }
    }
}
