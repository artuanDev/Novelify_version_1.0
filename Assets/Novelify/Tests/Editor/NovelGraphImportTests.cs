using System;
using System.Linq;
using Novelify.Editor;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

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
        public void ExampleStoryImportsMusicThenNarrationHokiTranslateDaisyAndEnd()
        {
            const string path = "Assets/Novelify/NovelGraphs/ExampleStory.novelgraph";
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
            Assert.That(current, Is.TypeOf<RuntimeTranslateSpeakerPortraitNode>());
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
            Add<DialogueEventNode>(); Add<StopSoundNode>();
            RuntimeNovelGraph runtime = Import();
            Assert.That(runtime.AllNodes.OfType<RuntimeShowCharacterNode>().Single().NextNodeID, Is.Null.Or.Empty);
            Assert.That(runtime.AllNodes.OfType<RuntimeHideCharacterNode>().Single().Character, Is.EqualTo(_character));
            Assert.That(runtime.AllNodes.OfType<RuntimeHideAllCharactersNode>().Count(), Is.EqualTo(1));
            Assert.That(runtime.AllNodes.OfType<RuntimeSetCharacterEmotionNode>().Count(), Is.EqualTo(1));
            Assert.That(runtime.AllNodes.OfType<RuntimeWaitNode>().Count(), Is.EqualTo(1));
            Assert.That(runtime.AllNodes.OfType<RuntimeDialogueEventNode>().Count(), Is.EqualTo(1));
            Assert.That(runtime.AllNodes.OfType<RuntimeStopSoundNode>().Count(), Is.EqualTo(1));
        }
    }
}
