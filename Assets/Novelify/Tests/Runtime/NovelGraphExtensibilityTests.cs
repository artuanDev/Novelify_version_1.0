using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Novelify.Tests
{
    public class NovelGraphExtensibilityTests
    {
        private GameObject _runnerObject;
        private NovelGraphRunner _runner;
        private RuntimeNovelGraph _graph;

        private sealed class TestPresentation : INovelPresentation
        {
            public bool IsRevealing { get; private set; }
            public NovelDialoguePresentation LastDialogue { get; private set; }
            public IReadOnlyList<NovelChoicePresentation> LastChoices { get; private set; }
            public int StopCount { get; private set; }

            public void PresentDialogue(NovelDialoguePresentation dialogue)
            {
                LastDialogue = dialogue;
                IsRevealing = !dialogue.Node.ShowTextImmediately && !string.IsNullOrEmpty(dialogue.Text);
            }

            public void PresentChoices(IReadOnlyList<NovelChoicePresentation> choices) =>
                LastChoices = choices;

            public void CompleteReveal()
            {
                IsRevealing = false;
                LastDialogue?.NotifyRevealCompleted();
            }

            public void FinishRevealNaturally() => CompleteReveal();
            public void HideDialogue() { }
            public void ClearChoices() => LastChoices = null;
            public void Stop() => StopCount++;
        }

        [SetUp]
        public void SetUp()
        {
            _runnerObject = new GameObject("Standalone NovelGraphRunner");
            _runner = _runnerObject.AddComponent<NovelGraphRunner>();
            _runner.HideCharactersOnEnd = false;
            _graph = ScriptableObject.CreateInstance<RuntimeNovelGraph>();
            _graph.GraphID = "extensible-runner-test";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_runnerObject);
            Object.DestroyImmediate(_graph);
        }

        [Test]
        public void StandaloneRunnerUsesInjectedPresentationForDialogueAndChoices()
        {
            var presentation = new TestPresentation();
            _runner.Session.UsePresentation(presentation);
            var choice = new RuntimeChoiceNode
            {
                NodeID = "choice",
                SpeakerName = "Daisy",
                DialogueText = "Which path?",
                ShowTextImmediately = true,
                Choices = new List<ChoiceData>
                {
                    new ChoiceData
                    {
                        ChoiceID = "custom-ui-choice",
                        ChoiceText = "Continue",
                        DestinationNodeID = "result"
                    }
                }
            };
            var result = new RuntimeDialogueNode
            {
                NodeID = "result",
                DialogueText = "Custom presentation completed.",
                ShowTextImmediately = true
            };
            _graph.EntryNodeID = choice.NodeID;
            _graph.AllNodes = new List<RuntimeNode> { choice, result };

            _runner.Session.Play(_graph);

            Assert.That(presentation.LastDialogue.Node, Is.SameAs(choice));
            Assert.That(presentation.LastDialogue.SpeakerName, Is.EqualTo("Daisy"));
            Assert.That(presentation.LastChoices.Count, Is.EqualTo(1));
            Assert.That(presentation.LastChoices[0].ChoiceID, Is.EqualTo("custom-ui-choice"));

            Assert.That(_runner.Session.TryChoose("custom-ui-choice", out string error), Is.True, error);
            Assert.That(presentation.LastDialogue.Node, Is.SameAs(result));
            Assert.That(_runner.Session.CurrentNode, Is.SameAs(result));
        }

        [Test]
        public void CustomPresentationControlsRevealCompletion()
        {
            var presentation = new TestPresentation();
            _runner.UsePresentation(presentation);
            var line = new RuntimeDialogueNode
            {
                NodeID = "line",
                DialogueText = "Custom typewriter",
                ShowTextImmediately = false
            };
            _graph.EntryNodeID = line.NodeID;
            _graph.AllNodes = new List<RuntimeNode> { line };

            _runner.Session.Play(_graph);
            Assert.That(_runner.Session.IsTextRevealing, Is.True);

            presentation.FinishRevealNaturally();
            Assert.That(_runner.Session.IsTextRevealing, Is.False);
        }

        [Test]
        public void CustomNodeHandlerCanPauseResumeAndBeUnregistered()
        {
            int builtInEventCount = 0;
            _runner.Session.EventRaised += _ => builtInEventCount++;
            var signal = new RuntimeDialogueEventNode
            {
                NodeID = "gameplay",
                EventName = "built-in",
                NextNodeID = "result"
            };
            var result = new RuntimeDialogueNode
            {
                NodeID = "result",
                DialogueText = "Resumed",
                ShowTextImmediately = true
            };
            _graph.EntryNodeID = signal.NodeID;
            _graph.AllNodes = new List<RuntimeNode> { signal, result };

            IDisposable registration = _runner.Session.RegisterNodeHandler<RuntimeDialogueEventNode>(
                (_, _) => NovelNodeExecutionResult.Pause("result"),
                priority: 100);

            _runner.Session.Play(_graph);

            Assert.That(_runner.Session.IsWaiting, Is.True);
            Assert.That(_runner.Session.IsPausedByNodeHandler, Is.True);
            Assert.That(_runner.Session.CurrentNode, Is.SameAs(signal));
            Assert.That(builtInEventCount, Is.Zero);

            Assert.That(_runner.Session.Resume(out string error), Is.True, error);
            Assert.That(_runner.Session.CurrentNode, Is.SameAs(result));
            Assert.That(_runner.Session.IsWaiting, Is.False);
            Assert.That(_runner.Session.IsPausedByNodeHandler, Is.False);

            registration.Dispose();
            _runner.Session.Play(_graph);
            Assert.That(builtInEventCount, Is.EqualTo(1));
            Assert.That(_runner.Session.CurrentNode, Is.SameAs(result));
        }
    }
}
