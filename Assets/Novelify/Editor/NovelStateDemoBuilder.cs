using System;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Novelify.Editor
{
    /// <summary>Creates the editable sample that documents the state/conditional-flow feature.</summary>
    [InitializeOnLoad]
    internal static class NovelStateDemoBuilder
    {
        internal const string SampleFolder = "Assets/Novelify/Samples/StateAndConditionalFlow";
        internal const string GraphPath = SampleFolder + "/StateAndConditionalFlowDemo.novelgraph";
        internal const string ReactiveChoiceGraphPath = SampleFolder + "/ReactiveChoiceDemo.novelgraph";
        internal const string PersistenceGraphPath = SampleFolder + "/PersistenceCheckpointDemo.novelgraph";
        private const string HokiPath = "Assets/Novelify/Samples/Characters/Hoki.asset";
        private const string DaisyPath = "Assets/Novelify/Samples/Characters/Daisy.asset";

        static NovelStateDemoBuilder() => EditorApplication.delayCall += EnsureSample;

        [MenuItem("Tools/Novelify/Samples/Create State & Conditional Flow Demo")]
        private static void CreateFromMenu()
        {
            BuildSample();
            BuildReactiveChoiceSample();
            BuildPersistenceSample();
            UpdateSampleCharacters();
            UpdatePersistenceSampleOptions();
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(GraphPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        private static void EnsureSample()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnsureSample;
                return;
            }
            BuildSample();
            BuildReactiveChoiceSample();
            BuildPersistenceSample();
            UpdateSampleCharacters();
            UpdatePersistenceSampleOptions();
        }

        // Public so CI or a batch-mode Unity editor can materialize the sample.
        public static void BuildSample()
        {
            if (AssetDatabase.LoadMainAssetAtPath(GraphPath) != null) return;

            EnsureFolder(SampleFolder + "/Variables");
            NovelVariableDefinition playerName = CreateVariable("Player Name", NovelVariableType.String, NovelVariableScope.Profile, "PlayerName");
            NovelVariableDefinition trust = CreateVariable("Trust", NovelVariableType.Integer, NovelVariableScope.Story, "Trust");
            NovelVariableDefinition rapport = CreateVariable("Rapport", NovelVariableType.Float, NovelVariableScope.Story, "Rapport");
            NovelVariableDefinition helped = CreateVariable("Helped Before", NovelVariableType.Boolean, NovelVariableScope.Story, "HelpedBefore");
            NovelCharacter hoki = AssetDatabase.LoadAssetAtPath<NovelCharacter>(HokiPath);
            CreateVariable("Temporary Count", NovelVariableType.Integer, NovelVariableScope.CallLocal, "TemporaryCount");

            NovelGraph graph = GraphDatabase.CreateGraph<NovelGraph>(GraphPath);
            if (graph == null) return;
            graph.UndoBeginRecordGraph("Create state and conditional flow demo");
            try
            {
                var start = Add<StartNode>(graph, 0, 100);
                SetNovelVariableNode setName = AddSet(graph, playerName, NovelVariableType.String, "Reader", 250, 0);
                SetNovelVariableNode setTrust = AddSet(graph, trust, NovelVariableType.Integer, 2, 500, 0);
                SetNovelVariableNode setRapport = AddSet(graph, rapport, NovelVariableType.Float, 0.6f, 750, 0);
                SetNovelVariableNode setHelped = AddSet(graph, helped, NovelVariableType.Boolean, false, 1000, 0);
                var branch = Add<BranchNovelNode>(graph, 1500, 100);

                ConnectFlow(graph, start, setName);
                ConnectFlow(graph, setName, setTrust);
                ConnectFlow(graph, setTrust, setRapport);
                ConnectFlow(graph, setRapport, setHelped);
                ConnectFlow(graph, setHelped, branch);

                GetNovelVariableNode getTrust = AddGet(graph, trust, NovelVariableType.Integer, 250, 500);
                CompareNovelValuesNode trustCheck = AddCompare(graph, NovelVariableType.Integer,
                    RuntimeComparisonOperation.GreaterOrEqual, 500, 500);
                trustCheck.GetInputPortByName("B").TrySetValue(2);
                ConnectValue(graph, getTrust, "Value", trustCheck, "A");

                GetNovelVariableNode getRapport = AddGet(graph, rapport, NovelVariableType.Float, 250, 700);
                CompareNovelValuesNode rapportCheck = AddCompare(graph, NovelVariableType.Float,
                    RuntimeComparisonOperation.GreaterOrEqual, 500, 700);
                rapportCheck.GetInputPortByName("B").TrySetValue(0.5f);
                ConnectValue(graph, getRapport, "Value", rapportCheck, "A");

                var numericAnd = Add<AndNovelValuesNode>(graph, 800, 600);
                ConnectValue(graph, trustCheck, "Result", numericAnd, "A");
                ConnectValue(graph, rapportCheck, "Result", numericAnd, "B");

                GetNovelVariableNode getHelped = AddGet(graph, helped, NovelVariableType.Boolean, 250, 950);
                var notHelped = Add<NotNovelValueNode>(graph, 500, 950);
                ConnectValue(graph, getHelped, "Value", notHelped, "Value");

                GetNovelVariableNode getName = AddGet(graph, playerName, NovelVariableType.String, 250, 1150);
                CompareNovelValuesNode nameCheck = AddCompare(graph, NovelVariableType.String,
                    RuntimeComparisonOperation.Equal, 500, 1150);
                nameCheck.GetInputPortByName("B").TrySetValue("Reader");
                ConnectValue(graph, getName, "Value", nameCheck, "A");

                var identityOr = Add<OrNovelValuesNode>(graph, 800, 1050);
                ConnectValue(graph, notHelped, "Result", identityOr, "A");
                ConnectValue(graph, nameCheck, "Result", identityOr, "B");

                var finalAnd = Add<AndNovelValuesNode>(graph, 1150, 800);
                ConnectValue(graph, numericAnd, "Result", finalAnd, "A");
                ConnectValue(graph, identityOr, "Result", finalAnd, "B");
                ConnectValue(graph, finalAnd, "Result", branch, "Condition");

                ModifyNovelVariableNode addTrust = AddModify(graph, trust, NovelNumericType.Integer,
                    RuntimeVariableModifyOperation.Add, 1, 1800, 0);
                ModifyNovelVariableNode addRapport = AddModify(graph, rapport, NovelNumericType.Float,
                    RuntimeVariableModifyOperation.Add, 0.2f, 2050, 0);
                DialogueNode success = AddDialogue(graph,
                    "All checks passed. Trust and rapport were increased.", 2300, 0, hoki);
                var successEnd = Add<EndNode>(graph, 2600, 0);
                ConnectNamedFlow(graph, branch, "True", addTrust);
                ConnectFlow(graph, addTrust, addRapport);
                ConnectFlow(graph, addRapport, success);
                ConnectFlow(graph, success, successEnd);

                ModifyNovelVariableNode subtractTrust = AddModify(graph, trust, NovelNumericType.Integer,
                    RuntimeVariableModifyOperation.Subtract, 1, 1800, 300);
                DialogueNode fallback = AddDialogue(graph,
                    "A check failed. Trust was reduced and the false branch ran.", 2050, 300, hoki);
                var fallbackEnd = Add<EndNode>(graph, 2350, 300);
                ConnectNamedFlow(graph, branch, "False", subtractTrust);
                ConnectFlow(graph, subtractTrust, fallback);
                ConnectFlow(graph, fallback, fallbackEnd);
            }
            finally
            {
                graph.UndoEndRecordGraph();
            }

            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log($"Novelify created the state and conditional-flow demo at {GraphPath}");
        }

        public static void ValidateSampleForAutomation()
        {
            BuildSample();
            BuildReactiveChoiceSample();
            BuildPersistenceSample();
            UpdateSampleCharacters();
            UpdatePersistenceSampleOptions();
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceUpdate);
            RuntimeNovelGraph runtime = AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>(GraphPath);
            if (runtime == null) throw new InvalidOperationException("The state demo did not import as a RuntimeNovelGraph.");
            if (runtime.AllNodes.Count(node => node is RuntimeSetVariableNode) != 4)
                throw new InvalidOperationException("The state demo must contain four Set Variable nodes.");
            if (runtime.AllNodes.Count(node => node is RuntimeModifyVariableNode) != 3)
                throw new InvalidOperationException("The state demo must contain three Modify Variable nodes.");
            RuntimeBranchNode branch = runtime.AllNodes.OfType<RuntimeBranchNode>().SingleOrDefault();
            if (branch?.Condition is not RuntimeBooleanExpression ||
                string.IsNullOrEmpty(branch.TrueNodeID) || string.IsNullOrEmpty(branch.FalseNodeID))
                throw new InvalidOperationException("The state demo branch or its compiled Boolean expression is incomplete.");
            Debug.Log($"Novelify validated the state and conditional-flow demo ({runtime.AllNodes.Count} runtime nodes).");

            AssetDatabase.ImportAsset(ReactiveChoiceGraphPath, ImportAssetOptions.ForceUpdate);
            RuntimeNovelGraph choiceRuntime = AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>(ReactiveChoiceGraphPath);
            RuntimeChoiceNode choice = choiceRuntime?.AllNodes.OfType<RuntimeChoiceNode>().SingleOrDefault();
            if (choice == null || choice.Choices.Count != 4 ||
                choice.Choices.Count(option => option.StateChanges.Count > 0) != 1 ||
                string.IsNullOrEmpty(choice.UnavailableDestinationNodeID))
                throw new InvalidOperationException("The reactive Choice sample did not compile all four options, its transaction, and fallback.");
            Debug.Log("Novelify validated the reactive Choice demo.");

            AssetDatabase.ImportAsset(PersistenceGraphPath, ImportAssetOptions.ForceUpdate);
            RuntimeNovelGraph persistence = AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>(PersistenceGraphPath);
            if (persistence == null || persistence.AllNodes.Count(node => node is RuntimeCheckpointNode) < 2 ||
                !persistence.AllNodes.OfType<RuntimeCheckpointNode>().Any(node =>
                    node.SaveMode == NovelCheckpointSaveMode.Autosave && node.AutosaveSlotID == "autosave") ||
                persistence.AllNodes.OfType<RuntimeDialogueNode>().Any(node => node.NovelCharacter == null))
                throw new InvalidOperationException("The persistence sample needs two checkpoints, an autosave, and template-character dialogue.");
            Debug.Log("Novelify validated the persistence/checkpoint demo.");
        }

        public static void BuildReactiveChoiceSample()
        {
            if (AssetDatabase.LoadMainAssetAtPath(ReactiveChoiceGraphPath) != null) return;
            EnsureFolder(SampleFolder + "/Variables");
            NovelVariableDefinition coins = CreateVariable("Coins", NovelVariableType.Integer, NovelVariableScope.Story, "Coins");
            NovelVariableDefinition heardRumour = CreateVariable("Heard Rumour", NovelVariableType.Boolean, NovelVariableScope.Story, "HeardRumour");
            NovelVariableDefinition hasKey = CreateVariable("Has Key", NovelVariableType.Boolean, NovelVariableScope.Story, "HasKey");
            NovelChoiceTransactionDefinition buyKey = CreateTransaction(coins, hasKey);
            NovelCharacter daisy = AssetDatabase.LoadAssetAtPath<NovelCharacter>(DaisyPath);

            NovelGraph graph = GraphDatabase.CreateGraph<NovelGraph>(ReactiveChoiceGraphPath);
            if (graph == null) return;
            graph.UndoBeginRecordGraph("Create reactive Choice demo");
            try
            {
                var start = Add<StartNode>(graph, 0, 100);
                SetNovelVariableNode setCoins = AddSet(graph, coins, NovelVariableType.Integer, 10, 250, 0);
                SetNovelVariableNode setRumour = AddSet(graph, heardRumour, NovelVariableType.Boolean, false, 500, 0);
                ChoiceNode choice = Add<ChoiceNode>(graph, 1050, 100);
                choice.GetNodeOptionByName("portCount").TrySetValue(4);
                choice.GetInputPortByName("Speaker").TrySetValue(daisy);

                ConfigureChoice(choice, 0, "hidden-room", "Ask about the hidden room",
                    NovelChoiceUnavailablePolicy.Hide, "You have not heard the rumour.", false, null);
                ConfigureChoice(choice, 1, "buy-key", "Buy the key -- 20 coins",
                    NovelChoiceUnavailablePolicy.Disable, "Need 20 coins.", false, buyKey);
                ConfigureChoice(choice, 2, "ask-past", "Tell me about your past",
                    NovelChoiceUnavailablePolicy.Hide, "Already asked.", true, null);
                ConfigureChoice(choice, 3, "leave", "Leave",
                    NovelChoiceUnavailablePolicy.Hide, string.Empty, false, null);

                GetNovelVariableNode getRumour = AddGet(graph, heardRumour, NovelVariableType.Boolean, 250, 500);
                ConnectValue(graph, getRumour, "Value", choice, "Condition 0");
                GetNovelVariableNode getCoins = AddGet(graph, coins, NovelVariableType.Integer, 250, 700);
                CompareNovelValuesNode canAfford = AddCompare(graph, NovelVariableType.Integer,
                    RuntimeComparisonOperation.GreaterOrEqual, 500, 700);
                canAfford.GetInputPortByName("B").TrySetValue(20);
                ConnectValue(graph, getCoins, "Value", canAfford, "A");
                ConnectValue(graph, canAfford, "Result", choice, "Condition 1");

                ConnectFlow(graph, start, setCoins);
                ConnectFlow(graph, setCoins, setRumour);
                ConnectFlow(graph, setRumour, choice);
                string[] texts =
                {
                    "The hidden room opens only after the rumour is known.",
                    "The purchase spent 20 coins and granted the key atomically.",
                    "This answer can be selected only once per story state.",
                    "The always-available exit prevents an empty menu."
                };
                for (int i = 0; i < texts.Length; ++i)
                {
                    DialogueNode dialogue = AddDialogue(graph, texts[i], 1450, i * 250f, daisy);
                    EndNode end = Add<EndNode>(graph, 1800, i * 250f);
                    ConnectNamedFlow(graph, choice, $"Choice {i}", dialogue);
                    ConnectFlow(graph, dialogue, end);
                }
                DialogueNode fallback = AddDialogue(graph,
                    "No choice was actionable, so the explicit fallback ran.", 1450, 1100, daisy);
                EndNode fallbackEnd = Add<EndNode>(graph, 1800, 1100);
                ConnectNamedFlow(graph, choice, "Fallback", fallback);
                ConnectFlow(graph, fallback, fallbackEnd);
            }
            finally { graph.UndoEndRecordGraph(); }
            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(ReactiveChoiceGraphPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log($"Novelify created the reactive Choice demo at {ReactiveChoiceGraphPath}");
        }

        public static void BuildPersistenceSample()
        {
            if (AssetDatabase.LoadMainAssetAtPath(PersistenceGraphPath) != null) return;
            EnsureFolder(SampleFolder);
            NovelCharacter hoki = AssetDatabase.LoadAssetAtPath<NovelCharacter>(HokiPath);
            NovelGraph graph = GraphDatabase.CreateGraph<NovelGraph>(PersistenceGraphPath);
            if (graph == null) return;
            graph.UndoBeginRecordGraph("Create persistence and checkpoint demo");
            try
            {
                StartNode start = Add<StartNode>(graph, 0, 100);
                ShowCharacterNode show = Add<ShowCharacterNode>(graph, 250, 100);
                show.GetInputPortByName("Character").TrySetValue(hoki);
                show.GetNodeOptionByName("Coordinate Space").TrySetValue(CharacterPositionSpace.Normalized);
                show.GetNodeOptionByName("Position").TrySetValue(new Vector2(-0.35f, -0.05f));

                CheckpointNode arrival = Add<CheckpointNode>(graph, 500, 100);
                arrival.GetInputPortByName("Checkpoint ID").TrySetValue("arrival");
                DialogueNode greeting = AddDialogue(graph,
                    "Hoki reached a stable boundary. Save this slot, close the app, and load it again.", 750, 100, hoki);
                ChoiceNode choice = Add<ChoiceNode>(graph, 1100, 100);
                choice.GetNodeOptionByName("portCount").TrySetValue(2);
                choice.GetInputPortByName("Speaker").TrySetValue(hoki);
                ConfigureChoice(choice, 0, "continue-tour", "Continue the persistence tour",
                    NovelChoiceUnavailablePolicy.Hide, string.Empty, false, null);
                ConfigureChoice(choice, 1, "finish-tour", "Finish here",
                    NovelChoiceUnavailablePolicy.Hide, string.Empty, false, null);

                CheckpointNode afterChoice = Add<CheckpointNode>(graph, 1450, 0);
                afterChoice.GetInputPortByName("Checkpoint ID").TrySetValue("after-choice");
                afterChoice.GetNodeOptionByName("Save Mode").TrySetValue(NovelCheckpointSaveMode.Autosave);
                afterChoice.GetNodeOptionByName("Autosave Slot").TrySetValue("autosave");
                DialogueNode resumed = AddDialogue(graph,
                    "This line is after the choice. Loading its save must not select or reward the choice twice.", 1700, 0, hoki);
                EndNode resumedEnd = Add<EndNode>(graph, 2050, 0);
                DialogueNode finished = AddDialogue(graph,
                    "The second route also keeps Hoki's stage identity and facing in the snapshot.", 1450, 300, hoki);
                EndNode finishedEnd = Add<EndNode>(graph, 1800, 300);

                ConnectFlow(graph, start, show);
                ConnectFlow(graph, show, arrival);
                ConnectFlow(graph, arrival, greeting);
                ConnectFlow(graph, greeting, choice);
                ConnectNamedFlow(graph, choice, "Choice 0", afterChoice);
                ConnectFlow(graph, afterChoice, resumed);
                ConnectFlow(graph, resumed, resumedEnd);
                ConnectNamedFlow(graph, choice, "Choice 1", finished);
                ConnectFlow(graph, finished, finishedEnd);
                ConnectNamedFlow(graph, choice, "Fallback", finished);
            }
            finally { graph.UndoEndRecordGraph(); }
            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(PersistenceGraphPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            NovelGraphCatalogBuilder.RebuildCatalog();
            Debug.Log($"Novelify created the persistence/checkpoint demo at {PersistenceGraphPath}");
        }

        private static void UpdateSampleCharacters()
        {
            ApplyCharacter(GraphPath, AssetDatabase.LoadAssetAtPath<NovelCharacter>(HokiPath));
            ApplyCharacter(ReactiveChoiceGraphPath, AssetDatabase.LoadAssetAtPath<NovelCharacter>(DaisyPath));
        }

        private static void UpdatePersistenceSampleOptions()
        {
            NovelGraph graph = GraphDatabase.LoadGraph<NovelGraph>(PersistenceGraphPath);
            if (graph == null) return;
            CheckpointNode checkpoint = graph.GetNodes().OfType<CheckpointNode>().FirstOrDefault(node =>
            {
                node.GetInputPortByName("Checkpoint ID").TryGetValue(out string id);
                return id == "after-choice";
            });
            if (checkpoint == null) return;
            checkpoint.GetNodeOptionByName("Save Mode").TryGetValue(out NovelCheckpointSaveMode mode);
            checkpoint.GetNodeOptionByName("Autosave Slot").TryGetValue(out string slot);
            if (mode == NovelCheckpointSaveMode.Autosave && slot == "autosave") return;
            graph.UndoBeginRecordGraph("Configure persistence sample autosave");
            try
            {
                checkpoint.GetNodeOptionByName("Save Mode").TrySetValue(NovelCheckpointSaveMode.Autosave);
                checkpoint.GetNodeOptionByName("Autosave Slot").TrySetValue("autosave");
            }
            finally { graph.UndoEndRecordGraph(); }
            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(PersistenceGraphPath, ImportAssetOptions.ForceUpdate);
        }

        private static void ApplyCharacter(string path, NovelCharacter character)
        {
            if (character == null) return;
            NovelGraph graph = GraphDatabase.LoadGraph<NovelGraph>(path);
            if (graph == null) return;
            bool changed = false;
            foreach (DialogueNode dialogue in graph.GetNodes().OfType<DialogueNode>())
            {
                IPort port = dialogue.GetInputPortByName("Speaker");
                port.TryGetValue(out NovelCharacter current);
                if (current == character) continue;
                if (!changed) graph.UndoBeginRecordGraph("Assign template characters to Novelify samples");
                port.TrySetValue(character);
                changed = true;
            }
            foreach (ChoiceNode choice in graph.GetNodes().OfType<ChoiceNode>())
            {
                IPort port = choice.GetInputPortByName("Speaker");
                port.TryGetValue(out NovelCharacter current);
                if (current == character) continue;
                if (!changed) graph.UndoBeginRecordGraph("Assign template characters to Novelify samples");
                port.TrySetValue(character);
                changed = true;
            }
            if (!changed) return;
            graph.UndoEndRecordGraph();
            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void ConfigureChoice(ChoiceNode node, int index, string id, string text,
            NovelChoiceUnavailablePolicy policy, string disabledReason, bool onceOnly,
            NovelChoiceTransactionDefinition transaction)
        {
            INodeOption option = node.GetNodeOptionByName(ChoiceNode.ChoicesOptionID);
            option.TryGetValue(out ChoiceAuthoringList current);
            ChoiceAuthoringList updated = current?.Clone(index + 1) ?? ChoiceAuthoringList.CreateDefault(index + 1);
            ChoiceAuthoringEntry entry = updated.Entries[index];
            entry.ID = id;
            entry.Text = text;
            entry.UnavailablePolicy = policy;
            entry.DisabledReason = disabledReason;
            entry.OnceOnly = onceOnly;
            entry.Transaction = transaction;
            option.TrySetValue(updated);
            node.DefineNode();
        }

        private static NovelChoiceTransactionDefinition CreateTransaction(
            NovelVariableDefinition coins, NovelVariableDefinition hasKey)
        {
            string path = SampleFolder + "/BuyKeyTransaction.asset";
            NovelChoiceTransactionDefinition existing =
                AssetDatabase.LoadAssetAtPath<NovelChoiceTransactionDefinition>(path);
            if (existing != null) return existing;
            NovelChoiceTransactionDefinition transaction =
                ScriptableObject.CreateInstance<NovelChoiceTransactionDefinition>();
            transaction.Changes.Add(new NovelChoiceStateChangeDefinition
            {
                Variable = coins, Operation = NovelChoiceStateOperation.Spend, IntegerValue = 20
            });
            transaction.Changes.Add(new NovelChoiceStateChangeDefinition
            {
                Variable = hasKey, Operation = NovelChoiceStateOperation.Set, BooleanValue = true
            });
            AssetDatabase.CreateAsset(transaction, path);
            return transaction;
        }

        private static T Add<T>(NovelGraph graph, float x, float y) where T : Node, new()
        {
            var node = new T { Position = new Vector2(x, y) };
            graph.AddNode(node);
            return node;
        }

        private static SetNovelVariableNode AddSet(NovelGraph graph, NovelVariableDefinition variable,
            NovelVariableType type, object value, float x, float y)
        {
            SetNovelVariableNode node = Add<SetNovelVariableNode>(graph, x, y);
            node.GetNodeOptionByName("Value Type").TrySetValue(type);
            node.GetInputPortByName("Variable").TrySetValue(variable);
            node.GetInputPortByName("Value").TrySetValue(value);
            return node;
        }

        private static GetNovelVariableNode AddGet(NovelGraph graph, NovelVariableDefinition variable,
            NovelVariableType type, float x, float y)
        {
            GetNovelVariableNode node = Add<GetNovelVariableNode>(graph, x, y);
            node.GetNodeOptionByName("Value Type").TrySetValue(type);
            node.GetInputPortByName("Variable").TrySetValue(variable);
            return node;
        }

        private static CompareNovelValuesNode AddCompare(NovelGraph graph, NovelVariableType type,
            RuntimeComparisonOperation operation, float x, float y)
        {
            CompareNovelValuesNode node = Add<CompareNovelValuesNode>(graph, x, y);
            node.GetNodeOptionByName("Value Type").TrySetValue(type);
            node.GetNodeOptionByName("Operator").TrySetValue(operation);
            return node;
        }

        private static ModifyNovelVariableNode AddModify(NovelGraph graph, NovelVariableDefinition variable,
            NovelNumericType type, RuntimeVariableModifyOperation operation, object amount, float x, float y)
        {
            ModifyNovelVariableNode node = Add<ModifyNovelVariableNode>(graph, x, y);
            node.GetNodeOptionByName("Value Type").TrySetValue(type);
            node.GetNodeOptionByName("Operation").TrySetValue(operation);
            node.GetInputPortByName("Variable").TrySetValue(variable);
            node.GetInputPortByName("Amount").TrySetValue(amount);
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

        private static void ConnectFlow(NovelGraph graph, Node from, Node to) =>
            ConnectNamedFlow(graph, from, "out", to);

        private static void ConnectNamedFlow(NovelGraph graph, Node from, string output, Node to) =>
            graph.Connect(from.GetOutputPortByName(output), to.GetInputPortByName("in"));

        private static void ConnectValue(NovelGraph graph, Node from, string output, Node to, string input) =>
            graph.Connect(from.GetOutputPortByName(output), to.GetInputPortByName(input));

        private static NovelVariableDefinition CreateVariable(string displayName, NovelVariableType type,
            NovelVariableScope scope, string fileName)
        {
            string path = $"{SampleFolder}/Variables/{fileName}.asset";
            NovelVariableDefinition existing = AssetDatabase.LoadAssetAtPath<NovelVariableDefinition>(path);
            if (existing != null) return existing;
            NovelVariableDefinition variable = ScriptableObject.CreateInstance<NovelVariableDefinition>();
            variable.name = displayName;
            variable.DisplayName = displayName;
            variable.Type = type;
            variable.Scope = scope;
            variable.EnsureID();
            AssetDatabase.CreateAsset(variable, path);
            return variable;
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
