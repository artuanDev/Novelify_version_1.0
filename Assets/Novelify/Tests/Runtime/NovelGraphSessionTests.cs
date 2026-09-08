using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Novelify.Tests
{
    public class NovelGraphSessionTests
    {
        private GameObject _managerObject;
        private NovelManager _manager;
        private RuntimeNovelGraph _graph;

        [SetUp]
        public void SetUp()
        {
            _managerObject = new GameObject("NovelGraphSessionTests");
            _manager = _managerObject.AddComponent<NovelManager>();
            _manager.HideCharactersOnEnd = false;
            _graph = ScriptableObject.CreateInstance<RuntimeNovelGraph>();
            _graph.GraphID = "session-test";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_managerObject);
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void SessionIsStableAndCompatibilityFacadeUsesIt()
        {
            Assert.That(_manager.Session, Is.SameAs(_manager.Session));

            _graph.EntryNodeID = "line";
            _graph.AllNodes = new List<RuntimeNode>
            {
                new RuntimeDialogueNode
                {
                    NodeID = "line",
                    DialogueText = "Ready",
                    ShowTextImmediately = true
                }
            };

            _manager.PlayGraph(_graph);

            Assert.That(_manager.Session.IsRunning, Is.True);
            Assert.That(_manager.Session.CurrentGraph, Is.SameAs(_graph));
            Assert.That(_manager.Session.CurrentNodeID, Is.EqualTo("line"));

            _manager.EndDialogue();
            Assert.That(_manager.Session.IsRunning, Is.False);
        }

        [Test]
        public void SessionRaisesGraphNodeAndDialogueEvents()
        {
            RuntimeNovelGraph started = null;
            RuntimeNovelGraph stopped = null;
            RuntimeNode entered = null;
            RuntimeDialogueNode presented = null;
            string speaker = null;
            _manager.Session.GraphStarted += value => started = value;
            _manager.Session.GraphStopped += value => stopped = value;
            _manager.Session.NodeEntered += (_, node) => entered = node;
            _manager.Session.DialoguePresented += (_, node, name) =>
            {
                presented = node;
                speaker = name;
            };

            var line = new RuntimeDialogueNode
            {
                NodeID = "line",
                SpeakerName = "Hoki",
                DialogueText = "The API is ready.",
                ShowTextImmediately = true
            };
            _graph.EntryNodeID = line.NodeID;
            _graph.AllNodes = new List<RuntimeNode> { line };

            _manager.Session.Play(_graph);

            Assert.That(started, Is.SameAs(_graph));
            Assert.That(entered, Is.SameAs(line));
            Assert.That(presented, Is.SameAs(line));
            Assert.That(speaker, Is.EqualTo("Hoki"));

            _manager.Session.Stop();
            Assert.That(stopped, Is.SameAs(_graph));
        }

        [Test]
        public void SessionReadsAndWritesTypedVariables()
        {
            NovelVariableDefinition score = ScriptableObject.CreateInstance<NovelVariableDefinition>();
            score.DisplayName = "Score";
            score.Type = NovelVariableType.Integer;
            score.Scope = NovelVariableScope.Story;
            try
            {
                Assert.That(_manager.Session.SetVariable(score, 12), Is.True);
                Assert.That(_manager.Session.GetInt(score), Is.EqualTo(12));

                Assert.That(_manager.Session.TrySetVariable(
                    score, RuntimeValue.From("wrong type"), out string error), Is.False);
                StringAssert.Contains("expects Integer", error);
            }
            finally
            {
                Object.DestroyImmediate(score);
            }
        }

        [Test]
        public void SessionCanResolveCatalogIdsAndChooseWithoutCustomUi()
        {
            NovelVariableDefinition trust = ScriptableObject.CreateInstance<NovelVariableDefinition>();
            trust.DisplayName = "Trust";
            trust.Type = NovelVariableType.Integer;
            NovelGraphCatalog catalog = ScriptableObject.CreateInstance<NovelGraphCatalog>();
            try
            {
                catalog.ReplaceEntries(new[]
                {
                    new NovelGraphCatalog.Entry { GraphID = _graph.GraphID, Graph = _graph }
                });
                catalog.ReplaceAssetEntries(null, new[]
                {
                    new NovelGraphCatalog.VariableEntry { VariableID = trust.ID, Variable = trust }
                });
                _manager.AssetCatalog = catalog;

                var choice = new RuntimeChoiceNode
                {
                    NodeID = "choice",
                    DialogueText = "Choose",
                    ShowTextImmediately = true,
                    Choices = new List<ChoiceData>
                    {
                        new ChoiceData
                        {
                            ChoiceID = "accept",
                            ChoiceText = "Accept",
                            DestinationNodeID = "result"
                        }
                    }
                };
                var result = new RuntimeDialogueNode
                {
                    NodeID = "result",
                    DialogueText = "Accepted",
                    ShowTextImmediately = true
                };
                _graph.EntryNodeID = choice.NodeID;
                _graph.AllNodes = new List<RuntimeNode> { choice, result };

                LogAssert.Expect(LogType.Warning, "ChoiceButtonPrefab or ChoiceButtonContainer is missing.");
                Assert.That(_manager.Session.Play(_graph.GraphID), Is.True);
                Assert.That(_manager.Session.TryGetVariable(trust.ID, out NovelVariableDefinition found), Is.True);
                Assert.That(found, Is.SameAs(trust));
                Assert.That(_manager.Session.SetVariable(trust.ID, 9), Is.True);
                Assert.That(_manager.Session.GetInt(trust.ID), Is.EqualTo(9));
                Assert.That(_manager.Session.CurrentChoices.Count, Is.EqualTo(1));

                Assert.That(_manager.Session.TryChoose("accept", out string error), Is.True, error);
                Assert.That(_manager.Session.CurrentNode, Is.SameAs(result));
                Assert.That(_manager.Session.HasChosen("accept"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(trust);
            }
        }
    }
}
