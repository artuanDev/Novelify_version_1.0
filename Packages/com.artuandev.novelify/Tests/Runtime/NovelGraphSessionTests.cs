using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        public void SessionIsStableAndControlsPlayback()
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

            _manager.Session.Play(_graph);

            Assert.That(_manager.Session.IsRunning, Is.True);
            Assert.That(_manager.Session.CurrentGraph, Is.SameAs(_graph));
            Assert.That(_manager.Session.CurrentNodeID, Is.EqualTo("line"));

            _manager.Session.Stop();
            Assert.That(_manager.Session.IsRunning, Is.False);
        }

        [Test]
        public void ContentProviderLoadsAndStartsChapterByID()
        {
            _graph.EntryNodeID = "line";
            _graph.AllNodes = new List<RuntimeNode>
            {
                new RuntimeDialogueNode { NodeID = "line", DialogueText = "Loaded", ShowTextImmediately = true }
            };
            _manager.Session.UseContentProvider(new TestContentProvider(_graph));

            bool started = _manager.Session.PlayChapterAsync(_graph.GraphID).GetAwaiter().GetResult();

            Assert.That(started, Is.True);
            Assert.That(_manager.Session.CurrentGraph, Is.SameAs(_graph));
            Assert.That(_manager.Session.CurrentNodeID, Is.EqualTo("line"));
        }

        [Test]
        public void CancelledChapterLoadDoesNotReplaceCurrentGraph()
        {
            _graph.EntryNodeID = "line";
            _graph.AllNodes = new List<RuntimeNode>
            {
                new RuntimeDialogueNode { NodeID = "line", DialogueText = "Current", ShowTextImmediately = true }
            };
            _manager.Session.Play(_graph);
            _manager.Session.UseContentProvider(new TestContentProvider(_graph));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<System.OperationCanceledException>(() =>
                _manager.Session.PlayChapterAsync(_graph.GraphID, cancellation.Token).GetAwaiter().GetResult());
            Assert.That(_manager.Session.CurrentGraph, Is.SameAs(_graph));
            Assert.That(_manager.Session.CurrentNodeID, Is.EqualTo("line"));
        }

        [Test]
        public void CustomSaveProviderReceivesSlotAndProfileOperations()
        {
            _graph.EntryNodeID = "line";
            _graph.AllNodes = new List<RuntimeNode>
            {
                new RuntimeDialogueNode { NodeID = "line", DialogueText = "Ready", ShowTextImmediately = true }
            };
            var provider = new TestSaveProvider();
            _manager.Session.UseSaveProvider(provider);
            _manager.Session.Play(_graph);

            Assert.That(_manager.Session.Save("slot_1").Succeeded, Is.True);
            Assert.That(provider.Snapshot, Is.Not.Null);
            Assert.That(provider.ListSlots(), Does.Contain("slot_1"));
            Assert.That(_manager.Session.SaveProfile().Succeeded, Is.True);
            Assert.That(provider.Profile, Is.Not.Null);
            Assert.That(_manager.Session.Load("slot_1").Succeeded, Is.True);
            provider.Snapshot = null;
            _manager.Session.Checkpoint("checkpoint", "auto");
            Assert.That(provider.Snapshot, Is.Not.Null);
        }

        [Test]
        public void LoadAsyncHydratesSavedChapterBeforeRestoring()
        {
            _graph.EntryNodeID = "line";
            _graph.AllNodes = new List<RuntimeNode>
            {
                new RuntimeDialogueNode { NodeID = "line", DialogueText = "Saved", ShowTextImmediately = true }
            };
            var saves = new TestSaveProvider();
            _manager.Session.UseSaveProvider(saves);
            _manager.Session.Play(_graph);
            Assert.That(_manager.Session.Save("slot_1").Succeeded, Is.True);
            saves.Snapshot.CheckpointGraphID = "removed-optional-checkpoint";
            _manager.Session.Stop();
            _manager.Session.UseContentProvider(new TestContentProvider(_graph));

            NovelPersistenceResult result = _manager.Session.LoadAsync("slot_1").GetAwaiter().GetResult();

            Assert.That(result.Succeeded, Is.True, result.ToString());
            Assert.That(_manager.Session.CurrentNodeID, Is.EqualTo("line"));
        }

        [Test]
        public void ReadOnlySkipStopsAtUnreadLineButAllowsPreviouslyReadLine()
        {
            var controller = _managerObject.AddComponent<NovelPlayerController>();
            controller.Preferences.SkipReadOnly = true;
            _graph.EntryNodeID = "line";
            _graph.AllNodes = new List<RuntimeNode>
            {
                new RuntimeDialogueNode { NodeID = "line", DialogueText = "Read me", ShowTextImmediately = true }
            };

            controller.SetSkip(true);
            _manager.Session.Play(_graph);
            Assert.That(controller.SkipMode, Is.False);

            _manager.Session.Stop();
            controller.SetSkip(true);
            _manager.Session.Play(_graph);
            Assert.That(controller.SkipMode, Is.True);
        }

        [Test]
        public void SkipStopsAtChoices()
        {
            var controller = _managerObject.AddComponent<NovelPlayerController>();
            _graph.EntryNodeID = "choice";
            _graph.AllNodes = new List<RuntimeNode>
            {
                new RuntimeChoiceNode
                {
                    NodeID = "choice", DialogueText = "Choose", ShowTextImmediately = true,
                    Choices = new List<ChoiceData>
                    {
                        new ChoiceData { ChoiceID = "continue", ChoiceText = "Continue" }
                    }
                }
            };
            controller.SetSkip(true);

            _manager.Session.Play(_graph);

            Assert.That(controller.SkipMode, Is.False);
            Assert.That(_manager.Session.CurrentNodeID, Is.EqualTo("choice"));
        }

        [Test]
        public void PlayerPreferencesClampUnsafeValues()
        {
            var preferences = new NovelPlayerPreferences
            {
                TextSpeedMultiplier = -2f,
                AutoDelay = 99f,
                TalkVolume = 3f
            };

            preferences.Clamp();

            Assert.That(preferences.TextSpeedMultiplier, Is.EqualTo(0.25f));
            Assert.That(preferences.AutoDelay, Is.EqualTo(10f));
            Assert.That(preferences.TalkVolume, Is.EqualTo(1f));
        }

        private sealed class TestContentProvider : INovelContentProvider
        {
            private readonly RuntimeNovelGraph _graph;
            private bool _loaded;
            public TestContentProvider(RuntimeNovelGraph graph) => _graph = graph;
            public bool TryGetLoadedGraph(string chapterID, out RuntimeNovelGraph graph)
            {
                graph = _loaded && chapterID == _graph.GraphID ? _graph : null;
                return graph != null;
            }
            public Task<RuntimeNovelGraph> LoadGraphAsync(string chapterID, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                _loaded = chapterID == _graph.GraphID;
                TryGetLoadedGraph(chapterID, out RuntimeNovelGraph graph);
                return Task.FromResult(graph);
            }
        }

        private sealed class TestSaveProvider : INovelSaveProvider
        {
            public NovelSaveData Snapshot;
            public NovelProfileSaveData Profile;
            public NovelPersistenceResult SaveSlot(string slotID, NovelSaveData snapshot)
            {
                Snapshot = snapshot;
                return new NovelPersistenceResult(NovelPersistenceStatus.Success);
            }
            public NovelPersistenceResult LoadSlot(string slotID, out NovelSaveData snapshot)
            {
                snapshot = Snapshot;
                return new NovelPersistenceResult(snapshot == null
                    ? NovelPersistenceStatus.NotFound : NovelPersistenceStatus.Success);
            }
            public NovelPersistenceResult SaveProfile(NovelProfileSaveData profile)
            {
                Profile = profile;
                return new NovelPersistenceResult(NovelPersistenceStatus.Success);
            }
            public NovelPersistenceResult LoadProfile(out NovelProfileSaveData profile)
            {
                profile = Profile;
                return new NovelPersistenceResult(profile == null
                    ? NovelPersistenceStatus.NotFound : NovelPersistenceStatus.Success);
            }
            public IReadOnlyList<string> ListSlots() => Snapshot == null
                ? System.Array.Empty<string>() : new[] { "slot_1" };
            public NovelPersistenceResult DeleteSlot(string slotID)
            {
                Snapshot = null;
                return new NovelPersistenceResult(NovelPersistenceStatus.Success);
            }
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
