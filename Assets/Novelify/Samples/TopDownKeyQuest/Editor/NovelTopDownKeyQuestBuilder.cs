using System;
using System.Linq;
using Novelify.Samples.TopDownKeyQuest;
using TMPro;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Novelify.Editor.Samples.TopDownKeyQuest
{
    /// <summary>Rebuilds the complete top-down sample without hand-authored scene YAML.</summary>
    public static class NovelTopDownKeyQuestBuilder
    {
        public const string SampleFolder = "Assets/Novelify/Samples/TopDownKeyQuest";
        public const string ScenePath = SampleFolder + "/Scenes/TopDownKeyQuest.unity";
        public const string DaisyGraphPath = SampleFolder + "/Graphs/DaisyConversation.novelgraph";
        public const string DoorGraphPath = SampleFolder + "/Graphs/DoorInteraction.novelgraph";
        public const string HasKeyPath = SampleFolder + "/Variables/HasKey.asset";

        private const string HokiPath = "Assets/Novelify/Samples/Characters/Hoki.asset";
        private const string DaisyPath = "Assets/Novelify/Samples/Characters/Daisy.asset";
        private const string PortraitPrefabPath = "Assets/Novelify/Samples/Prefabs/PortraitPrefab.prefab";
        private const string ChoicePrefabPath = "Assets/Novelify/Samples/Prefabs/ChoiceButton.prefab";

        [MenuItem("Tools/Novelify/Samples/Create Top-Down Key Quest")]
        public static void BuildSample()
        {
            EnsureFolder(SampleFolder + "/Graphs");
            EnsureFolder(SampleFolder + "/Variables");
            EnsureFolder(SampleFolder + "/Scenes");

            NovelVariableDefinition hasKey = CreateHasKeyVariable();
            NovelCharacter hoki = RequireAsset<NovelCharacter>(HokiPath);
            NovelCharacter daisy = RequireAsset<NovelCharacter>(DaisyPath);
            BuildDaisyGraph(hasKey, hoki, daisy);
            BuildDoorGraph(hasKey, hoki);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            BuildScene(hasKey);
            NovelGraphCatalogBuilder.RebuildCatalog();

            UnityEngine.Object scene = AssetDatabase.LoadMainAssetAtPath(ScenePath);
            Selection.activeObject = scene;
            EditorGUIUtility.PingObject(scene);
            Debug.Log($"Novelify created the playable top-down key quest at {ScenePath}");
        }

        public static void ValidateSampleForAutomation()
        {
            BuildSample();
            RuntimeNovelGraph daisy = RequireAsset<RuntimeNovelGraph>(DaisyGraphPath);
            RuntimeNovelGraph door = RequireAsset<RuntimeNovelGraph>(DoorGraphPath);
            NovelVariableDefinition hasKey = RequireAsset<NovelVariableDefinition>(HasKeyPath);

            RuntimeChoiceNode choice = daisy.AllNodes.OfType<RuntimeChoiceNode>().SingleOrDefault();
            if (choice == null || choice.Choices.Count != 3 ||
                !daisy.AllNodes.OfType<RuntimeSetVariableNode>().Any(node => node.Variable == hasKey))
                throw new InvalidOperationException("Daisy's graph must have three persuasion choices and grant HasKey.");
            RuntimeBranchNode doorBranch = door.AllNodes.OfType<RuntimeBranchNode>().SingleOrDefault();
            if (doorBranch == null || string.IsNullOrEmpty(doorBranch.TrueNodeID) || string.IsNullOrEmpty(doorBranch.FalseNodeID) ||
                !door.AllNodes.OfType<RuntimeDialogueEventNode>().Any(node => node.EventName == TopDownNovelDoor.OpenEvent))
                throw new InvalidOperationException("The door graph must branch on HasKey and emit the open event.");

            Scene loaded = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!loaded.IsValid() || UnityEngine.Object.FindAnyObjectByType<TopDownPlayerController>() == null ||
                UnityEngine.Object.FindAnyObjectByType<TopDownNovelDoor>() == null ||
                UnityEngine.Object.FindObjectsByType<TopDownNovelInteractable>().Length < 2)
                throw new InvalidOperationException("The top-down scene is missing its player, door, or interactables.");
            Debug.Log("Novelify validated the top-down key quest scene and both runtime graphs.");
        }

        private static void BuildDaisyGraph(
            NovelVariableDefinition hasKey, NovelCharacter hoki, NovelCharacter daisy)
        {
            if (AssetDatabase.LoadMainAssetAtPath(DaisyGraphPath) != null) return;
            NovelGraph graph = GraphDatabase.CreateGraph<NovelGraph>(DaisyGraphPath);
            graph.UndoBeginRecordGraph("Create Daisy key conversation");
            try
            {
                StartNode start = Add<StartNode>(graph, 0, 100);
                GetNovelVariableNode getKey = AddGet(graph, hasKey, 0, 500);
                BranchNovelNode branch = Add<BranchNovelNode>(graph, 300, 100);
                ConnectFlow(graph, start, branch);
                ConnectValue(graph, getKey, "Value", branch, "Condition");

                DialogueNode alreadyHelped = AddDialogue(graph,
                    "You have the key already. The old door is waiting for you.", 650, -150, daisy);
                EndNode alreadyEnd = Add<EndNode>(graph, 1000, -150);
                ConnectNamedFlow(graph, branch, "True", alreadyHelped);
                ConnectFlow(graph, alreadyHelped, alreadyEnd);

                DialogueNode greeting = AddDialogue(graph,
                    "I do have the brass key, but I cannot hand it to someone I do not trust.", 650, 200, daisy);
                ChoiceNode choice = Add<ChoiceNode>(graph, 1000, 200);
                choice.GetNodeOptionByName("portCount").TrySetValue(3);
                choice.GetNodeOptionByName("Dialogue").TrySetValue(new RichDialogueText("How will Hoki convince Daisy?"));
                choice.GetInputPortByName("Speaker").TrySetValue(hoki);
                ConfigureChoice(choice, 0, "promise-return", "I only need to pass. I promise I'll bring it straight back.");
                ConfigureChoice(choice, 1, "demand-key", "Give me the key. I don't have time for this.");
                ConfigureChoice(choice, 2, "leave-daisy", "Never mind. I'll look for another way.");
                ConnectNamedFlow(graph, branch, "False", greeting);
                ConnectFlow(graph, greeting, choice);

                DialogueNode convinced = AddDialogue(graph,
                    "All right. That is a promise I can believe. Take the key—and be careful.", 1400, 0, daisy);
                SetNovelVariableNode grantKey = AddSet(graph, hasKey, true, 1750, 0);
                DialogueNode thanks = AddDialogue(graph,
                    "Thank you, Daisy. I'll return it as soon as the door is open.", 2050, 0, hoki);
                EndNode successEnd = Add<EndNode>(graph, 2400, 0);
                ConnectNamedFlow(graph, choice, "Choice 0", convinced);
                ConnectFlow(graph, convinced, grantKey);
                ConnectFlow(graph, grantKey, thanks);
                ConnectFlow(graph, thanks, successEnd);

                DialogueNode refusal = AddDialogue(graph,
                    "Then you can make time. I will not reward a demand like that.", 1400, 300, daisy);
                DialogueNode rethink = AddDialogue(graph,
                    "That came out wrong. I should try speaking to her again.", 1750, 300, hoki);
                EndNode refusalEnd = Add<EndNode>(graph, 2100, 300);
                ConnectNamedFlow(graph, choice, "Choice 1", refusal);
                ConnectFlow(graph, refusal, rethink);
                ConnectFlow(graph, rethink, refusalEnd);

                DialogueNode leave = AddDialogue(graph,
                    "Maybe I should think before I answer.", 1400, 600, hoki);
                EndNode leaveEnd = Add<EndNode>(graph, 1750, 600);
                ConnectNamedFlow(graph, choice, "Choice 2", leave);
                ConnectNamedFlow(graph, choice, "Fallback", leave);
                ConnectFlow(graph, leave, leaveEnd);
            }
            finally { graph.UndoEndRecordGraph(); }
            SaveGraph(graph, DaisyGraphPath);
        }

        private static void BuildDoorGraph(NovelVariableDefinition hasKey, NovelCharacter hoki)
        {
            if (AssetDatabase.LoadMainAssetAtPath(DoorGraphPath) != null) return;
            NovelGraph graph = GraphDatabase.CreateGraph<NovelGraph>(DoorGraphPath);
            graph.UndoBeginRecordGraph("Create conditional door interaction");
            try
            {
                StartNode start = Add<StartNode>(graph, 0, 100);
                GetNovelVariableNode getKey = AddGet(graph, hasKey, 0, 500);
                BranchNovelNode branch = Add<BranchNovelNode>(graph, 300, 100);
                ConnectFlow(graph, start, branch);
                ConnectValue(graph, getKey, "Value", branch, "Condition");

                DialogueNode unlocked = AddDialogue(graph,
                    "The brass key slides into the lock. With a clean click, the old door releases.", 700, -100);
                DialogueNode success = AddDialogue(graph,
                    "That did it. The way ahead is finally clear.", 1050, -100, hoki);
                DialogueEventNode openEvent = Add<DialogueEventNode>(graph, 1400, -100);
                openEvent.GetNodeOptionByName("Event Name").TrySetValue(TopDownNovelDoor.OpenEvent);
                EndNode openEnd = Add<EndNode>(graph, 1750, -100);
                ConnectNamedFlow(graph, branch, "True", unlocked);
                ConnectFlow(graph, unlocked, success);
                ConnectFlow(graph, success, openEvent);
                ConnectFlow(graph, openEvent, openEnd);

                DialogueNode locked = AddDialogue(graph,
                    "The old door refuses to move. Its brass lock is missing a key.", 700, 300);
                DialogueNode response = AddDialogue(graph,
                    "Locked. Daisy might know where the key is.", 1050, 300, hoki);
                EndNode lockedEnd = Add<EndNode>(graph, 1400, 300);
                ConnectNamedFlow(graph, branch, "False", locked);
                ConnectFlow(graph, locked, response);
                ConnectFlow(graph, response, lockedEnd);
            }
            finally { graph.UndoEndRecordGraph(); }
            SaveGraph(graph, DoorGraphPath);
        }

        private static void BuildScene(NovelVariableDefinition hasKey)
        {
            RuntimeNovelGraph daisyGraph = RequireAsset<RuntimeNovelGraph>(DaisyGraphPath);
            RuntimeNovelGraph doorGraph = RequireAsset<RuntimeNovelGraph>(DoorGraphPath);
            Sprite square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject world = new GameObject("WORLD — game-specific objects");

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.25f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.075f);

            CreateWorldBlock("Floor", world.transform, square, Vector2.zero, new Vector2(9.2f, 8.2f),
                new Color(0.12f, 0.2f, 0.2f), -20, false);
            CreateWall(world.transform, square, "West Wall", new Vector2(-4.45f, 0f), new Vector2(.3f, 8.5f));
            CreateWall(world.transform, square, "East Wall", new Vector2(4.45f, 0f), new Vector2(.3f, 8.5f));
            CreateWall(world.transform, square, "South Wall", new Vector2(0f, -4.05f), new Vector2(9.2f, .3f));
            CreateWall(world.transform, square, "North Wall Left", new Vector2(-2.7f, 4.05f), new Vector2(3.8f, .3f));
            CreateWall(world.transform, square, "North Wall Right", new Vector2(2.7f, 4.05f), new Vector2(3.8f, .3f));

            CreateWorldBlock("Rug", world.transform, square, new Vector2(0f, .35f), new Vector2(2.8f, 3.5f),
                new Color(0.22f, 0.12f, 0.15f), -10, false);
            CreateWorldBlock("Table", world.transform, square, new Vector2(2.7f, .4f), new Vector2(1.2f, 1.2f),
                new Color(0.32f, 0.22f, 0.13f), -2, true);
            CreateWorldBlock("Planter", world.transform, square, new Vector2(-3.65f, -2.9f), new Vector2(.9f, .9f),
                new Color(0.18f, 0.36f, 0.23f), -2, true);

            GameObject runnerObject = new GameObject("NOVELIFY — story runtime");
            NovelGraphRunner runner = runnerObject.AddComponent<NovelGraphRunner>();

            GameObject daisyObject = CreateWorldBlock("Daisy — Novelify interaction", world.transform, square,
                new Vector2(-2.75f, .7f), new Vector2(.8f, .8f), new Color(0.95f, 0.58f, 0.67f), 2, false);
            CircleCollider2D daisyTrigger = daisyObject.AddComponent<CircleCollider2D>();
            daisyTrigger.radius = .75f;
            daisyTrigger.isTrigger = true;
            TopDownNovelInteractable daisy = daisyObject.AddComponent<TopDownNovelInteractable>();
            daisy.NovelifyRunner = runner;
            daisy.InteractionGraph = daisyGraph;
            daisy.InteractionLabel = "Talk to Daisy";
            CreateWorldLabel("Daisy", daisyObject.transform, new Vector3(0f, .8f, 0f), Color.white);

            GameObject doorObject = CreateWorldBlock("North Door — Novelify event target", world.transform, square,
                new Vector2(0f, 3.9f), new Vector2(1.45f, .55f), new Color(0.62f, 0.36f, 0.14f), 3, false);
            BoxCollider2D doorCollider = doorObject.AddComponent<BoxCollider2D>();
            TopDownNovelDoor door = doorObject.AddComponent<TopDownNovelDoor>();
            door.NovelifyRunner = runner;
            door.InteractionGraph = doorGraph;
            door.InteractionLabel = "Inspect north door";
            door.BlockingCollider = doorCollider;
            door.WorldLabel = CreateWorldLabel("LOCKED", doorObject.transform, new Vector3(0f, -.75f, 0f),
                new Color(1f, .82f, .38f));

            GameObject playerObject = CreateWorldBlock("Hoki — Player", world.transform, square,
                new Vector2(0f, -2.55f), new Vector2(.7f, .7f), new Color(0.32f, 0.72f, 1f), 5, false);
            Rigidbody2D body = playerObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D playerCollider = playerObject.AddComponent<CircleCollider2D>();
            playerCollider.radius = .44f;
            CreateWorldLabel("Hoki", playerObject.transform, new Vector3(0f, .75f, 0f), Color.white);

            Canvas canvas = CreateCanvas();
            RectTransform stage = CreateRect("Novelify Character Stage", canvas.transform, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            stage.SetAsFirstSibling();
            BuildHUD(canvas.transform, out TextMeshProUGUI objective, out TextMeshProUGUI key,
                out TextMeshProUGUI prompt);
            BuildDialogueUI(canvas.transform, out GameObject dialoguePanel, out GameObject choicePanel,
                out GameObject namePanel, out TextMeshProUGUI speaker, out TextMeshProUGUI dialogueText,
                out RectTransform choices);

            GameObject portraitPrefab = RequireAsset<GameObject>(PortraitPrefabPath);
            GameObject choicePrefab = RequireAsset<GameObject>(ChoicePrefabPath);
            runner.CanvasDialogue = canvas.gameObject;
            runner.PortraitPrefab = portraitPrefab;
            runner.CharacterContainer = stage;
            runner.DialoguePanel = dialoguePanel;
            runner.BackgroundChoicesPanel = choicePanel;
            runner.NameBackground = namePanel;
            runner.SpeakerNameText = speaker;
            runner.DialogueText = dialogueText;
            runner.ChoiceButtonPrefab = choicePrefab.GetComponent<Button>();
            runner.ChoiceButtonContainer = choices;
            runner.HideCharactersOnEnd = true;

            TopDownPlayerController controller = playerObject.AddComponent<TopDownPlayerController>();
            controller.NovelifyRunner = runner;
            controller.InteractionPrompt = prompt;
            TopDownQuestHUD quest = runnerObject.AddComponent<TopDownQuestHUD>();
            quest.NovelifyRunner = runner;
            quest.HasKey = hasKey;
            quest.ObjectiveText = objective;
            quest.KeyText = key;

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(canvas.transform.parent, false);
            dialoguePanel.SetActive(false);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject("UI — Novelify presentation and game HUD",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            return canvas;
        }

        private static void BuildHUD(Transform canvas, out TextMeshProUGUI objective,
            out TextMeshProUGUI key, out TextMeshProUGUI prompt)
        {
            GameObject top = CreatePanel("Top HUD", canvas, new Color(.025f, .035f, .055f, .94f),
                new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -78f), Vector2.zero);
            CreateUIText("Title", top.transform, "NOVELIFY  /  TOP-DOWN KEY QUEST", 24, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(.55f, 1f), new Vector2(28f, 8f), new Vector2(-10f, -8f),
                new Color(.55f, .9f, 1f));
            CreateUIText("Controls", top.transform, "WASD  MOVE     E  INTERACT     SPACE / CLICK  ADVANCE", 18,
                TextAlignmentOptions.Right, new Vector2(.45f, 0f), Vector2.one,
                new Vector2(10f, 8f), new Vector2(-28f, -8f), new Color(.75f, .8f, .85f));

            objective = CreateUIText("Objective", canvas, string.Empty, 20, TextAlignmentOptions.Left,
                new Vector2(.02f, .87f), new Vector2(.72f, .94f), Vector2.zero, Vector2.zero, Color.white);
            key = CreateUIText("Key Status", canvas, string.Empty, 20, TextAlignmentOptions.Right,
                new Vector2(.72f, .87f), new Vector2(.98f, .94f), Vector2.zero, Vector2.zero,
                new Color(1f, .8f, .3f));
            prompt = CreateUIText("Interaction Prompt", canvas, "", 24, TextAlignmentOptions.Center,
                new Vector2(.25f, .30f), new Vector2(.75f, .37f), Vector2.zero, Vector2.zero,
                new Color(1f, .86f, .42f));
            prompt.gameObject.SetActive(false);
        }

        private static void BuildDialogueUI(Transform canvas, out GameObject dialoguePanel,
            out GameObject choicePanel, out GameObject namePanel, out TextMeshProUGUI speaker,
            out TextMeshProUGUI dialogue, out RectTransform choices)
        {
            dialoguePanel = CreatePanel("Novelify Dialogue Panel", canvas, new Color(.025f, .035f, .055f, .97f),
                new Vector2(.1f, .025f), new Vector2(.9f, .27f), Vector2.zero, Vector2.zero);
            namePanel = CreatePanel("Speaker Name Background", dialoguePanel.transform,
                new Color(.08f, .32f, .42f, 1f), new Vector2(.025f, .72f), new Vector2(.34f, .97f),
                Vector2.zero, Vector2.zero);
            speaker = CreateUIText("Speaker Name", namePanel.transform, "Hoki", 25, TextAlignmentOptions.Left,
                Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-8f, 0f), Color.white);
            dialogue = CreateUIText("Dialogue Text", dialoguePanel.transform, "Dialogue appears here.", 27,
                TextAlignmentOptions.TopLeft, new Vector2(.035f, .08f), new Vector2(.63f, .7f),
                Vector2.zero, Vector2.zero, Color.white);

            choicePanel = CreatePanel("Choice Background", dialoguePanel.transform,
                new Color(.055f, .075f, .1f, .96f), new Vector2(.65f, .05f), new Vector2(.98f, .95f),
                Vector2.zero, Vector2.zero);
            choices = CreateRect("Choice Button Container", choicePanel.transform, new Vector2(.05f, .05f),
                new Vector2(.95f, .95f), Vector2.zero, Vector2.zero);
            VerticalLayoutGroup layout = choices.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
        }

        private static GameObject CreateWorldBlock(string name, Transform parent, Sprite sprite, Vector2 position,
            Vector2 size, Color color, int sortingOrder, bool collider)
        {
            GameObject result = new GameObject(name, typeof(SpriteRenderer));
            result.transform.SetParent(parent, false);
            result.transform.position = new Vector3(position.x, position.y, 0f);
            result.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = result.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            if (collider) result.AddComponent<BoxCollider2D>();
            return result;
        }

        private static void CreateWall(Transform parent, Sprite sprite, string name, Vector2 position, Vector2 size)
        {
            CreateWorldBlock(name, parent, sprite, position, size, new Color(.22f, .28f, .31f), 0, true);
        }

        private static TextMeshPro CreateWorldLabel(string text, Transform parent, Vector3 localPosition, Color color)
        {
            GameObject labelObject = new GameObject(text + " Label", typeof(TextMeshPro));
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            labelObject.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
            TextMeshPro label = labelObject.GetComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = 2.4f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.rectTransform.sizeDelta = new Vector2(2.4f, .45f);
            label.renderer.sortingOrder = 20;
            return label;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return panel;
        }

        private static TextMeshProUGUI CreateUIText(string name, Transform parent, string value, float size,
            TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(12f, size * .65f);
            text.fontSizeMax = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            return rect;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static NovelVariableDefinition CreateHasKeyVariable()
        {
            NovelVariableDefinition existing = AssetDatabase.LoadAssetAtPath<NovelVariableDefinition>(HasKeyPath);
            if (existing != null) return existing;
            NovelVariableDefinition variable = ScriptableObject.CreateInstance<NovelVariableDefinition>();
            variable.name = "HasKey";
            variable.DisplayName = "Has Key";
            variable.Type = NovelVariableType.Boolean;
            variable.Scope = NovelVariableScope.Story;
            variable.DefaultBoolean = false;
            variable.EnsureID();
            AssetDatabase.CreateAsset(variable, HasKeyPath);
            return variable;
        }

        private static GetNovelVariableNode AddGet(NovelGraph graph, NovelVariableDefinition variable, float x, float y)
        {
            GetNovelVariableNode node = Add<GetNovelVariableNode>(graph, x, y);
            node.GetNodeOptionByName("Value Type").TrySetValue(NovelVariableType.Boolean);
            node.GetInputPortByName("Variable").TrySetValue(variable);
            return node;
        }

        private static SetNovelVariableNode AddSet(NovelGraph graph, NovelVariableDefinition variable,
            bool value, float x, float y)
        {
            SetNovelVariableNode node = Add<SetNovelVariableNode>(graph, x, y);
            node.GetNodeOptionByName("Value Type").TrySetValue(NovelVariableType.Boolean);
            node.GetInputPortByName("Variable").TrySetValue(variable);
            node.GetInputPortByName("Value").TrySetValue(value);
            return node;
        }

        private static DialogueNode AddDialogue(NovelGraph graph, string text, float x, float y,
            NovelCharacter speaker = null)
        {
            DialogueNode node = Add<DialogueNode>(graph, x, y);
            node.GetNodeOptionByName("Dialogue").TrySetValue(new RichDialogueText(text));
            if (speaker != null) node.GetInputPortByName("Speaker").TrySetValue(speaker);
            return node;
        }

        private static void ConfigureChoice(ChoiceNode choice, int index, string id, string text)
        {
            INodeOption option = choice.GetNodeOptionByName(ChoiceNode.ChoicesOptionID);
            option.TryGetValue(out ChoiceAuthoringList current);
            ChoiceAuthoringList updated = current?.Clone(index + 1) ?? ChoiceAuthoringList.CreateDefault(index + 1);
            updated.Entries[index].ID = id;
            updated.Entries[index].Text = text;
            updated.Entries[index].UnavailablePolicy = NovelChoiceUnavailablePolicy.Hide;
            option.TrySetValue(updated);
            choice.DefineNode();
        }

        private static T Add<T>(NovelGraph graph, float x, float y) where T : Node, new()
        {
            T node = new T { Position = new Vector2(x, y) };
            graph.AddNode(node);
            return node;
        }

        private static void ConnectFlow(NovelGraph graph, Node from, Node to) =>
            ConnectNamedFlow(graph, from, "out", to);

        private static void ConnectNamedFlow(NovelGraph graph, Node from, string output, Node to) =>
            graph.Connect(from.GetOutputPortByName(output), to.GetInputPortByName("in"));

        private static void ConnectValue(NovelGraph graph, Node from, string output, Node to, string input) =>
            graph.Connect(from.GetOutputPortByName(output), to.GetInputPortByName(input));

        private static void SaveGraph(NovelGraph graph, string path)
        {
            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException($"Required asset is missing: {path}");
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            string[] parts = path.Split('/');
            for (int i = 1; i < parts.Length; ++i)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
