using System;
using System.Linq;
using Novelify.Editor;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Novelify.Tests
{
    public class NovelGraphImportTests
    {
        private string _folder;
        private NovelGraph _graph;
        private NovelCharacter _character;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/NovelifyTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", _folder.Substring("Assets/".Length));
            _character = ScriptableObject.CreateInstance<NovelCharacter>();
            _character.SpeakerName = "Test Speaker";
            AssetDatabase.CreateAsset(_character, _folder + "/Character.asset");
            _graph = GraphDatabase.CreateGraph<NovelGraph>(_folder + "/Story.novelgraph");
            _graph.UndoBeginRecordGraph("Build test graph");
        }

        [TearDown]
        public void TearDown()
        {
            if (_graph != null) _graph.OnDisable();
            AssetDatabase.DeleteAsset(_folder);
        }

        private T Add<T>() where T : Node, new()
        {
            var node = new T();
            _graph.AddNode(node);
            return node;
        }

        private void Connect(Node from, Node to) =>
            Assert.That(_graph.Connect(from.GetOutputPortByName("out"), to.GetInputPortByName("in")), Is.True);

        private RuntimeNovelGraph Import()
        {
            _graph.UndoEndRecordGraph();
            GraphDatabase.SaveGraph(_graph);
            AssetDatabase.ImportAsset(_folder + "/Story.novelgraph", ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>(_folder + "/Story.novelgraph");
        }

        [Test]
        public void CharacterPassThroughAndMovementOptionsSurviveImport()
        {
            StartNode start = Add<StartNode>();
            DialogueNode dialogue = Add<DialogueNode>();
            TranslateSpeakerPortraitNode translate = Add<TranslateSpeakerPortraitNode>();
            ShowCharacterNode show = Add<ShowCharacterNode>();
            EndNode end = Add<EndNode>();
            dialogue.GetInputPortByName("Speaker").TrySetValue(_character);
            _graph.Connect(dialogue.GetOutputPortByName("Current Speaker"), translate.GetInputPortByName("Character"));
            _graph.Connect(translate.GetOutputPortByName("Character"), show.GetInputPortByName("Character"));
            translate.GetNodeOptionByName("Smooth Movement").TrySetValue(true);
            translate.GetNodeOptionByName("Duration").TrySetValue(0.75f);
            translate.GetNodeOptionByName("OffsetX").TrySetValue(240f);
            translate.GetNodeOptionByName("Instance ID").TrySetValue("second");
            Connect(start, dialogue); Connect(dialogue, translate); Connect(translate, show); Connect(show, end);
            RuntimeNovelGraph runtime = Import();
            var move = runtime.AllNodes.OfType<RuntimeTranslateSpeakerPortraitNode>().Single();
            Assert.That(move.Character, Is.EqualTo(_character));
            Assert.That(move.InstanceID, Is.EqualTo("second"));
            Assert.That(move.SmoothMovement, Is.True);
            Assert.That(move.Duration, Is.EqualTo(0.75f));
            Assert.That(move.OffsetX, Is.EqualTo(240f));
            Assert.That(runtime.AllNodes.OfType<RuntimeShowCharacterNode>().Single().Character, Is.EqualTo(_character));
            Assert.That(runtime.AllNodes.Any(node => node.NodeID == runtime.EntryNodeID), Is.True);
            foreach (RuntimeNode node in runtime.AllNodes)
                if (!string.IsNullOrEmpty(node.NextNodeID))
                    Assert.That(runtime.AllNodes.Any(next => next.NodeID == node.NextNodeID), Is.True);
        }

        [Test]
        public void ChoiceExtensionCompilesConditionsStableIdsPoliciesTransactionsAndFallback()
        {
            NovelVariableDefinition coins = ScriptableObject.CreateInstance<NovelVariableDefinition>();
            coins.name = "Coins";
            coins.DisplayName = "Coins";
            coins.Type = NovelVariableType.Integer;
            coins.Scope = NovelVariableScope.Story;
            coins.EnsureID();
            AssetDatabase.CreateAsset(coins, _folder + "/Coins.asset");
            NovelChoiceTransactionDefinition transaction = ScriptableObject.CreateInstance<NovelChoiceTransactionDefinition>();
            transaction.Changes.Add(new NovelChoiceStateChangeDefinition
            {
                Variable = coins,
                Operation = NovelChoiceStateOperation.Spend,
                IntegerValue = 20
            });
            AssetDatabase.CreateAsset(transaction, _folder + "/BuyKey.asset");

            StartNode start = Add<StartNode>();
            ChoiceNode choice = Add<ChoiceNode>();
            EndNode purchaseEnd = Add<EndNode>();
            EndNode leaveEnd = Add<EndNode>();
            EndNode fallbackEnd = Add<EndNode>();
            ChoiceAuthoringList choices = ChoiceAuthoringList.CreateDefault();
            choices.Entries[0].ID = "buy-key";
            choices.Entries[0].Text = "Buy the key -- 20 coins";
            choices.Entries[0].UnavailablePolicy = NovelChoiceUnavailablePolicy.Disable;
            choices.Entries[0].DisabledReason = "Need 20 coins.";
            choices.Entries[0].OnceOnly = true;
            choices.Entries[0].Transaction = transaction;
            choices.Entries[1].Text = "Leave";
            choice.GetNodeOptionByName(ChoiceNode.ChoicesOptionID).TrySetValue(choices);
            choice.DefineNode();
            choice.GetInputPortByName("Condition 0").TrySetValue(false);
            Assert.That(_graph.Connect(start.GetOutputPortByName("out"), choice.GetInputPortByName("in")), Is.True);
            Assert.That(_graph.Connect(choice.GetOutputPortByName("Choice 0"), purchaseEnd.GetInputPortByName("in")), Is.True);
            Assert.That(_graph.Connect(choice.GetOutputPortByName("Choice 1"), leaveEnd.GetInputPortByName("in")), Is.True);
            Assert.That(_graph.Connect(choice.GetOutputPortByName("Fallback"), fallbackEnd.GetInputPortByName("in")), Is.True);

            RuntimeChoiceNode runtime = Import().AllNodes.OfType<RuntimeChoiceNode>().Single();
            ChoiceData purchase = runtime.Choices[0];
            Assert.That(purchase.ChoiceID, Is.EqualTo("buy-key"));
            Assert.That(purchase.Condition, Is.TypeOf<RuntimeConstantExpression>());
            Assert.That(((RuntimeConstantExpression)purchase.Condition).Value.BooleanValue, Is.False);
            Assert.That(purchase.UnavailablePolicy, Is.EqualTo(NovelChoiceUnavailablePolicy.Disable));
            Assert.That(purchase.DisabledReason, Is.EqualTo("Need 20 coins."));
            Assert.That(purchase.OnceOnly, Is.True);
            Assert.That(purchase.StateChanges.Single().Operation, Is.EqualTo(NovelChoiceStateOperation.Spend));
            Assert.That(((RuntimeConstantExpression)purchase.StateChanges.Single().Value).Value.IntegerValue, Is.EqualTo(20));
            Assert.That(runtime.Choices[1].ChoiceID, Is.Not.Null.And.Not.Empty, "Blank IDs must derive a stable fallback ID.");
            Assert.That(runtime.UnavailableDestinationNodeID, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ChoiceDropdownUsesItsIdForTheOutputNameAndRuntimeChoice()
        {
            StartNode start = Add<StartNode>();
            ChoiceNode choice = Add<ChoiceNode>();
            EndNode end = Add<EndNode>();
            ChoiceAuthoringList choices = ChoiceAuthoringList.CreateDefault();
            choices.Entries[0].ID = "mall";
            choices.Entries[0].Text = "Let's go to the mall";
            Assert.That(choice.GetNodeOptionByName(ChoiceNode.ChoicesOptionID).TrySetValue(choices), Is.True);
            choice.DefineNode();

            Assert.That(choice.GetOutputPortByName("Choice 0").DisplayName, Is.EqualTo("mall"));
            Assert.That(choice.GetInputPortByName("Condition 0"), Is.Not.Null,
                "Only the non-redundant dynamic availability input should remain per choice.");
            Assert.That(_graph.Connect(start.GetOutputPortByName("out"), choice.GetInputPortByName("in")), Is.True);
            Assert.That(_graph.Connect(choice.GetOutputPortByName("Choice 0"), end.GetInputPortByName("in")), Is.True);

            RuntimeChoiceNode runtime = Import().AllNodes.OfType<RuntimeChoiceNode>().Single();
            Assert.That(runtime.Choices[0].ChoiceID, Is.EqualTo("mall"));
            Assert.That(runtime.Choices[0].ChoiceText, Is.EqualTo("Let's go to the mall"));
        }

        [Test]
        public void DialogueSoundMarkerCompilesToItsVisibleCharacterIndex()
        {
            StartNode start = Add<StartNode>();
            SimpleDialogueNode dialogue = Add<SimpleDialogueNode>();
            dialogue.GetNodeOptionByName("Dialogue").TrySetValue(
                new RichDialogueText("Hello <b>dear</b> <link=\"novelify-sound\">friend</link>."));
            Connect(start, dialogue);

            RuntimeDialogueNode runtime = Import().AllNodes.OfType<RuntimeDialogueNode>().Single();
            Assert.That(runtime.PlaySoundCharacterIndex, Is.EqualTo(11));
        }

        [Test]
        public void RandomNumberNodeCompilesAsANumericExpression()
        {
            StartNode start = Add<StartNode>();
            TransformSpeakerPortraitNode transform = Add<TransformSpeakerPortraitNode>();
            RandomNumberNode random = Add<RandomNumberNode>();
            EndNode end = Add<EndNode>();
            random.GetNodeOptionByName("Number Type").TrySetValue(NovelNumericType.Float);
            random.DefineNode();
            random.GetInputPortByName("Minimum").TrySetValue(-15f);
            random.GetInputPortByName("Maximum").TrySetValue(15f);
            transform.GetInputPortByName("Character").TrySetValue(_character);
            Assert.That(_graph.Connect(random.GetOutputPortByName("Result"),
                transform.GetInputPortByName("Rotation")), Is.True);
            Connect(start, transform);
            Connect(transform, end);

            RuntimeTransformSpeakerPortraitNode runtime = Import().AllNodes
                .OfType<RuntimeTransformSpeakerPortraitNode>().Single();
            var expression = runtime.RotationValue as RuntimeRandomNumberExpression;
            Assert.That(expression, Is.Not.Null);
            Assert.That(expression.ValueKind, Is.EqualTo(RuntimeValueKind.Float));
            Assert.That(((RuntimeConstantExpression)expression.Minimum).Value.FloatValue, Is.EqualTo(-15f));
            Assert.That(((RuntimeConstantExpression)expression.Maximum).Value.FloatValue, Is.EqualTo(15f));
        }

        [Test]
        public void TransformSpeakerPortraitOptionsSurviveImport()
        {
            StartNode start = Add<StartNode>();
            TransformSpeakerPortraitNode transform = Add<TransformSpeakerPortraitNode>();
            DialogueNode dialogue = Add<DialogueNode>();
            transform.GetInputPortByName("Character").TrySetValue(_character);
            transform.GetInputPortByName("Position").TrySetValue(new Vector2(0.75f, -0.25f));
            transform.GetInputPortByName("Rotation").TrySetValue(35f);
            transform.GetInputPortByName("Scale").TrySetValue(new Vector2(1.5f, 0.8f));
            transform.GetInputPortByName("Margin").TrySetValue(120f);
            transform.GetNodeOptionByName("Animate Transform").TrySetValue(true);
            Connect(start, transform);
            Connect(transform, dialogue);

            RuntimeNovelGraph runtime = Import();
            RuntimeTransformSpeakerPortraitNode result = runtime.AllNodes
                .OfType<RuntimeTransformSpeakerPortraitNode>()
                .Single(node => node is not RuntimeTranslateSpeakerPortraitNode);

            Assert.That(result.Character, Is.EqualTo(_character));
            Assert.That(result.PositionIsNormalized, Is.True);
            Assert.That(result.OffsetX, Is.EqualTo(0.75f));
            Assert.That(result.OffsetY, Is.EqualTo(-0.25f));
            Assert.That(result.Rotation, Is.EqualTo(35f));
            Assert.That(result.Scale, Is.EqualTo(new Vector2(1.5f, 0.8f)));
            Assert.That(result.Margin, Is.EqualTo(120f));
            Assert.That(result.SmoothMovement, Is.True);
            Assert.That(result.PositionValue, Is.TypeOf<RuntimeConstantExpression>());
        }

        [Test]
        public void AuthoredGraphAndNodeIdsRemainStableAcrossReimport()
        {
            StartNode start = Add<StartNode>();
            DialogueNode dialogue = Add<DialogueNode>();
            EndNode end = Add<EndNode>();
            Connect(start, dialogue);
            Connect(dialogue, end);

            RuntimeNovelGraph firstImport = Import();
            string graphID = firstImport.GraphID;
            string[] nodeIDs = firstImport.AllNodes.Select(node => node.NodeID).OrderBy(id => id).ToArray();
            string entryID = firstImport.EntryNodeID;

            AssetDatabase.ImportAsset(_folder + "/Story.novelgraph", ImportAssetOptions.ForceUpdate);
            RuntimeNovelGraph secondImport = AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>(_folder + "/Story.novelgraph");

            Assert.That(graphID, Is.Not.Null.And.Not.Empty);
            Assert.That(secondImport.GraphID, Is.EqualTo(graphID));
            Assert.That(secondImport.EntryNodeID, Is.EqualTo(entryID));
            CollectionAssert.AreEqual(nodeIDs, secondImport.AllNodes.Select(node => node.NodeID).OrderBy(id => id).ToArray());
            Assert.That(secondImport.SchemaVersion, Is.EqualTo(RuntimeNovelGraph.CurrentSchemaVersion));
            Assert.That(secondImport.ContentVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void SharedValueOutputCanFeedBothArithmeticOperands()
        {
            StartNode start = Add<StartNode>();
            TransformSpeakerPortraitNode transform = Add<TransformSpeakerPortraitNode>();
            AddFloatNode add = Add<AddFloatNode>();
            EndNode end = Add<EndNode>();
            IVariable amount = _graph.CreateVariable("Amount", typeof(float), 12f, VariableKind.Local);
            IVariableNode amountNode = _graph.AddVariableNode(amount, Vector2.zero);

            IPort amountOutput = amountNode.GetOutputPorts().Single();
            Assert.That(_graph.Connect(amountOutput, add.GetInputPortByName("A")), Is.True);
            Assert.That(_graph.Connect(amountOutput, add.GetInputPortByName("B")), Is.True);
            Assert.That(_graph.Connect(add.GetOutputPortByName("Result"), transform.GetInputPortByName("Rotation")), Is.True);
            transform.GetInputPortByName("Character").TrySetValue(_character);
            Connect(start, transform);
            Connect(transform, end);

            RuntimeTransformSpeakerPortraitNode result = Import().AllNodes.OfType<RuntimeTransformSpeakerPortraitNode>().Single();
            var expression = result.RotationValue as RuntimeArithmeticExpression;
            Assert.That(expression, Is.Not.Null);
            Assert.That(((RuntimeConstantExpression)expression.A).Value.FloatValue, Is.EqualTo(12f));
            Assert.That(((RuntimeConstantExpression)expression.B).Value.FloatValue, Is.EqualTo(12f));
        }

        [Test]
        public void CyclicExpressionProducesAClearImportDiagnostic()
        {
            StartNode start = Add<StartNode>();
            TransformSpeakerPortraitNode transform = Add<TransformSpeakerPortraitNode>();
            AddFloatNode first = Add<AddFloatNode>();
            AddFloatNode second = Add<AddFloatNode>();
            EndNode end = Add<EndNode>();
            transform.GetInputPortByName("Character").TrySetValue(_character);
            Assert.That(_graph.Connect(first.GetOutputPortByName("Result"), second.GetInputPortByName("A")), Is.True);
            Assert.That(_graph.Connect(second.GetOutputPortByName("Result"), first.GetInputPortByName("A")), Is.True);
            Assert.That(_graph.Connect(first.GetOutputPortByName("Result"), transform.GetInputPortByName("Rotation")), Is.True);
            Connect(start, transform);
            Connect(transform, end);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Cyclic value expression detected"));
            Import();
        }

        [Test]
        public void CharacterReferenceAndCoordinateSpaceCompileExplicitly()
        {
            StartNode start = Add<StartNode>();
            MakeNovelCharacterReferenceNode make = Add<MakeNovelCharacterReferenceNode>();
            ShowCharacterNode show = Add<ShowCharacterNode>();
            EndNode end = Add<EndNode>();
            make.GetInputPortByName("Character").TrySetValue(_character);
            make.GetInputPortByName("Instance ID").TrySetValue("second");
            show.GetNodeOptionByName("Coordinate Space").TrySetValue(CharacterPositionSpace.Normalized);
            Assert.That(_graph.Connect(make.GetOutputPortByName("Character Reference"),
                show.GetInputPortByName("Character Reference")), Is.True);
            Connect(start, show);
            Connect(show, end);

            RuntimeShowCharacterNode result = Import().AllNodes.OfType<RuntimeShowCharacterNode>().Single();
            Assert.That(result.PositionSpace, Is.EqualTo(CharacterPositionSpace.Normalized));
            Assert.That(result.CharacterReferenceValue, Is.TypeOf<RuntimeMakeCharacterReferenceExpression>());
        }

        [Test]
        public void VectorMathAndCharacterSplitCompileIntoLiveExpressions()
        {
            StartNode start = Add<StartNode>();
            SplitNovelCharacterNode split = Add<SplitNovelCharacterNode>();
            SubtractVector2Node subtract = Add<SubtractVector2Node>();
            TransformSpeakerPortraitNode transform = Add<TransformSpeakerPortraitNode>();
            EndNode end = Add<EndNode>();

            split.GetInputPortByName("Character").TrySetValue(_character);
            subtract.GetInputPortByName("B").TrySetValue(new Vector2(0.1f, 0f));
            _graph.Connect(split.GetOutputPortByName("Character"), transform.GetInputPortByName("Character"));
            _graph.Connect(split.GetOutputPortByName("Position (Normalized)"), subtract.GetInputPortByName("A"));
            _graph.Connect(subtract.GetOutputPortByName("Result"), transform.GetInputPortByName("Position"));
            Connect(start, transform);
            Connect(transform, end);

            RuntimeNovelGraph runtime = Import();
            RuntimeTransformSpeakerPortraitNode result = runtime.AllNodes.OfType<RuntimeTransformSpeakerPortraitNode>().Single();
            Assert.That(result.CharacterValue, Is.TypeOf<RuntimeCharacterComponentExpression>());
            Assert.That(result.PositionValue, Is.TypeOf<RuntimeArithmeticExpression>());
            Assert.That(runtime.AllNodes.Count, Is.EqualTo(2),
                "Only Transform and End should be emitted; pure value nodes are expressions.");
        }

        [Test]
        public void NovelFunctionAssetExposesInputsOutputsAndCompilesAsCallableNode()
        {
            string functionPath = _folder + "/MoveTarget.novelfunction";
            NovelFunctionGraph function = GraphDatabase.CreateGraph<NovelFunctionGraph>(functionPath);
            function.UndoBeginRecordGraph("Build function");
            try
            {
                function.EnsureFlowInterface();
                Assert.That(function.GetVariables().Any(variable =>
                    variable.Name == NovelFunctionGraph.EnterVariableName && variable.VariableKind == VariableKind.Input), Is.True);
                Assert.That(function.GetVariables().Any(variable =>
                    variable.Name == NovelFunctionGraph.ContinueVariableName && variable.VariableKind == VariableKind.Output), Is.True);

                IVariable target = function.CreateInterfaceVariable("Target", typeof(NovelCharacter), _character, VariableKind.Input);
                IVariable destination = function.CreateInterfaceVariable("Destination", typeof(Vector2), Vector2.zero, VariableKind.Input);
                IVariable finalPosition = function.CreateInterfaceVariable("Final Position", typeof(Vector2), Vector2.zero, VariableKind.Output);
                var start = new StartNode();
                var transform = new TransformSpeakerPortraitNode();
                var split = new SplitNovelCharacterNode();
                var end = new EndNode();
                function.AddNode(start);
                function.AddNode(transform);
                function.AddNode(split);
                function.AddNode(end);
                IVariableNode targetNode = function.AddVariableNode(target, Vector2.zero);
                IVariableNode destinationNode = function.AddVariableNode(destination, Vector2.zero);
                IVariableNode outputNode = function.AddVariableNode(finalPosition, Vector2.zero);

                Assert.That(function.Connect(start.GetOutputPortByName("out"), transform.GetInputPortByName("in")), Is.True);
                Assert.That(function.Connect(transform.GetOutputPortByName("out"), end.GetInputPortByName("in")), Is.True);
                Assert.That(function.Connect(targetNode.GetOutputPorts().Single(), transform.GetInputPortByName("Character")), Is.True);
                Assert.That(function.Connect(targetNode.GetOutputPorts().Single(), split.GetInputPortByName("Character")), Is.True);
                Assert.That(function.Connect(destinationNode.GetOutputPorts().Single(), transform.GetInputPortByName("Position")), Is.True);
                Assert.That(function.Connect(split.GetOutputPortByName("Position (Normalized)"), outputNode.GetInputPorts().Single()), Is.True);

                function.UndoEndRecordGraph();
                GraphDatabase.SaveGraph(function);
                AssetDatabase.ImportAsset(functionPath, ImportAssetOptions.ForceUpdate);
                RuntimeNovelFunction compiledFunction = AssetDatabase.LoadAssetAtPath<RuntimeNovelFunction>(functionPath);
                Assert.That(compiledFunction, Is.Not.Null);
                Assert.That(compiledFunction.Inputs.Select(input => input.Name), Is.EquivalentTo(new[] { "Target", "Destination" }));
                Assert.That(compiledFunction.Outputs.Single().Name, Is.EqualTo("Final Position"));

                _graph.UndoEndRecordGraph();
                GraphDatabase.SaveGraph(_graph);
                _graph = GraphDatabase.LoadGraph<NovelGraph>(_folder + "/Story.novelgraph");
                _graph.UndoBeginRecordGraph("Add function call");
                var graphStart = new StartNode();
                var graphEnd = new EndNode();
                _graph.AddNode(graphStart);
                _graph.AddNode(graphEnd);
                INode call = _graph.AddSubgraphNode(function, Vector2.zero);
                IPort targetPort = call.GetInputPorts().Single(port => port.DisplayName == "Target");
                IPort destinationPort = call.GetInputPorts().Single(port => port.DisplayName == "Destination");
                IPort enterPort = call.GetInputPorts().Single(port => port.DisplayName == "Enter");
                IPort continuePort = call.GetOutputPorts().Single(port => port.DisplayName == "Continue");
                targetPort.TrySetValue(_character);
                destinationPort.TrySetValue(new Vector2(0.5f, 0f));
                Assert.That(_graph.Connect(graphStart.GetOutputPortByName("out"), enterPort), Is.True);
                Assert.That(_graph.Connect(continuePort, graphEnd.GetInputPortByName("in")), Is.True);

                RuntimeNovelGraph compiledGraph = Import();
                RuntimeCallNovelFunctionNode runtimeCall = compiledGraph.AllNodes.OfType<RuntimeCallNovelFunctionNode>().Single();
                Assert.That(runtimeCall.Function, Is.SameAs(compiledFunction));
                Assert.That(runtimeCall.Arguments.Select(argument => argument.Name), Is.EquivalentTo(new[] { "Target", "Destination" }));
                RuntimeFunctionArgument targetArgument = runtimeCall.Arguments.Single(argument => argument.Name == "Target");
                Assert.That(targetArgument.Value, Is.TypeOf<RuntimeConstantExpression>());
                Assert.That(((RuntimeConstantExpression)targetArgument.Value).Value.ObjectValue, Is.SameAs(_character));
                Assert.That(runtimeCall.NextNodeID, Is.Not.Null.And.Not.Empty);
            }
            finally
            {
                if (function != null) function.OnDisable();
            }
        }

        [Test]
        public void JumpResolvesItsLabelAndContinuesFromThere()
        {
            StartNode start = Add<StartNode>();
            JumpNode jump = Add<JumpNode>();
            LabelNode label = Add<LabelNode>();
            DialogueNode destination = Add<DialogueNode>();
            jump.GetInputPortByName("Label").TrySetValue("Ending");
            label.GetInputPortByName("Label").TrySetValue("ending");
            Connect(start, jump);
            Connect(label, destination);

            RuntimeNovelGraph runtime = Import();
            var lookup = runtime.AllNodes.ToDictionary(node => node.NodeID);
            RuntimeNode jumpRuntime = lookup[runtime.EntryNodeID];
            RuntimeNode labelRuntime = lookup[jumpRuntime.NextNodeID];

            Assert.That(lookup[labelRuntime.NextNodeID], Is.TypeOf<RuntimeDialogueNode>());
        }

        [Test]
        public void ExampleStoryImportsMusicThenNarrationHokiTranslateDaisyAndEnd()
        {
            const string path = "Assets/Novelify/Samples/NovelGraphs/ExampleStory.novelgraph";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            RuntimeNovelGraph runtime = AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>(path);
            Assert.That(runtime, Is.Not.Null);
            var lookup = runtime.AllNodes.ToDictionary(node => node.NodeID);
            RuntimeNode current = lookup[runtime.EntryNodeID];
            Assert.That(current, Is.TypeOf<RuntimePlaySoundNode>());
            Assert.That(((RuntimePlaySoundNode)current).ClipSound, Is.Not.Null);
            current = lookup[current.NextNodeID];
            Assert.That(current, Is.TypeOf<RuntimeDialogueNode>());
            Assert.That(((RuntimeDialogueNode)current).NovelCharacter, Is.Null);
            current = lookup[current.NextNodeID];
            Assert.That(((RuntimeDialogueNode)current).NovelCharacter.name, Is.EqualTo("Hoki"));
            current = lookup[current.NextNodeID];
            Assert.That(current, Is.TypeOf<RuntimeTransformSpeakerPortraitNode>());
            Assert.That(((RuntimeTransformSpeakerPortraitNode)current).OffsetX, Is.EqualTo(-0.5f));
            current = lookup[current.NextNodeID];
            Assert.That(((RuntimeDialogueNode)current).NovelCharacter.name, Is.EqualTo("Daisy"));
            current = lookup[current.NextNodeID];
            Assert.That(current.NextNodeID, Is.Null.Or.Empty);
        }

        [Test]
        public void AllUtilityNodeTypesImportAndCharacterOutputsNeverBecomeStoryFlow()
        {
            StartNode start = Add<StartNode>();
            ShowCharacterNode show = Add<ShowCharacterNode>();
            show.GetInputPortByName("Character").TrySetValue(_character);
            HideCharacterNode hide = Add<HideCharacterNode>();
            _graph.Connect(show.GetOutputPortByName("Character"), hide.GetInputPortByName("Character"));
            Connect(start, show);
            Add<HideAllCharactersNode>(); Add<SetCharacterEmotionNode>(); Add<WaitNode>();
            CheckpointNode checkpoint = Add<CheckpointNode>();
            checkpoint.GetInputPortByName("Checkpoint ID").TrySetValue("chapter-one");
            checkpoint.GetNodeOptionByName("Save Mode").TrySetValue(NovelCheckpointSaveMode.Autosave);
            checkpoint.GetNodeOptionByName("Autosave Slot").TrySetValue("chapter_autosave");
            Add<DialogueEventNode>(); Add<StopSoundNode>();
            RuntimeNovelGraph runtime = Import();
            Assert.That(runtime.AllNodes.OfType<RuntimeShowCharacterNode>().Single().NextNodeID, Is.Null.Or.Empty);
            Assert.That(runtime.AllNodes.OfType<RuntimeHideCharacterNode>().Single().Character, Is.EqualTo(_character));
            Assert.That(runtime.AllNodes.OfType<RuntimeHideAllCharactersNode>().Count(), Is.EqualTo(1));
            Assert.That(runtime.AllNodes.OfType<RuntimeSetCharacterEmotionNode>().Count(), Is.EqualTo(1));
            Assert.That(runtime.AllNodes.OfType<RuntimeWaitNode>().Count(), Is.EqualTo(1));
            RuntimeCheckpointNode runtimeCheckpoint = runtime.AllNodes.OfType<RuntimeCheckpointNode>().Single();
            Assert.That(runtimeCheckpoint.CheckpointID, Is.EqualTo("chapter-one"));
            Assert.That(runtimeCheckpoint.SaveMode, Is.EqualTo(NovelCheckpointSaveMode.Autosave));
            Assert.That(runtimeCheckpoint.AutosaveSlotID, Is.EqualTo("chapter_autosave"));
            Assert.That(runtime.AllNodes.OfType<RuntimeDialogueEventNode>().Count(), Is.EqualTo(1));
            Assert.That(runtime.AllNodes.OfType<RuntimeStopSoundNode>().Count(), Is.EqualTo(1));
        }
    }
}
