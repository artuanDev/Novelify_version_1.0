using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
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

        [Serializable]
        private sealed class TestScaleExpression : RuntimeValueExpression
        {
            [SerializeReference] public RuntimeValueExpression Input;
            public float Factor;
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
        public void LocalizationReachesPresentationChoicesAndHistoryWithSourceFallback()
        {
            var table = ScriptableObject.CreateInstance<NovelLocalizationTable>();
            try
            {
                string lineID = NovelLocalizationKey.Dialogue(_graph.GraphID, "choice");
                table.Lines.Add(new NovelLocalizedLine
                {
                    Key = lineID,
                    SourceText = "Which path?",
                    Translations = new List<NovelLocalizedText>
                    {
                        new NovelLocalizedText { Locale = "es", Text = "¿Qué camino?" }
                    }
                });
                table.Lines.Add(new NovelLocalizedLine
                {
                    Key = NovelLocalizationKey.Choice(lineID, "continue"),
                    SourceText = "Continue",
                    Translations = new List<NovelLocalizedText>
                    {
                        new NovelLocalizedText { Locale = "es", Text = "Continuar" }
                    }
                });
                _runner.LocalizationTable = table;
                _runner.Locale = "es";
                var presentation = new TestPresentation();
                _runner.UsePresentation(presentation);
                var choice = new RuntimeChoiceNode
                {
                    NodeID = "choice",
                    LineID = lineID,
                    SpeakerName = "Narrator",
                    DialogueText = "Which path?",
                    ShowTextImmediately = true,
                    Choices = new List<ChoiceData>
                    {
                        new ChoiceData { ChoiceID = "continue", ChoiceText = "Continue" }
                    }
                };
                _graph.EntryNodeID = choice.NodeID;
                _graph.AllNodes = new List<RuntimeNode> { choice };

                _runner.Session.Play(_graph);

                Assert.That(presentation.LastDialogue.Text, Is.EqualTo("¿Qué camino?"));
                Assert.That(presentation.LastDialogue.SpeakerName, Is.EqualTo("Narrator"));
                Assert.That(presentation.LastChoices[0].Text, Is.EqualTo("Continuar"));
                Assert.That(_runner.History[0].ResolvedText, Is.EqualTo("¿Qué camino?"));
                Assert.That(_runner.History[0].LineID, Is.EqualTo(lineID));
                Assert.That(table.Resolve("missing", "es", "Fallback"), Is.EqualTo("Fallback"));
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
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

        [Test]
        public void CustomValueEvaluatorCanComposeExpressionsAndBeUnregistered()
        {
            var expression = new TestScaleExpression
            {
                Input = new RuntimeConstantExpression { Value = RuntimeValue.From(3f) },
                Factor = 2f
            };
            IDisposable registration = _runner.Session.RegisterValueEvaluator<TestScaleExpression>(
                (context, value) => RuntimeValue.From(
                    context.Evaluate(value.Input).FloatValue * value.Factor));

            RuntimeValue evaluated = _runner.Session.Evaluate(expression);
            Assert.That(evaluated.Kind, Is.EqualTo(RuntimeValueKind.Float));
            Assert.That(evaluated.FloatValue, Is.EqualTo(6f));

            registration.Dispose();
            Assert.That(_runner.Session.Evaluate(expression).Kind, Is.EqualTo(RuntimeValueKind.None));
        }

        [Test]
        public void SkipCanStayEnabledOnAnUnreadStarterLineWhenReadOnlyIsOff()
        {
            NovelPlayerController controller = _runnerObject.AddComponent<NovelPlayerController>();
            controller.AutoPlayOnStart = false;
            controller.ShowGeneratedControls = false;
            controller.Preferences.SkipReadOnly = false;
            var line = new RuntimeDialogueNode
            {
                NodeID = "first-unread-line",
                DialogueText = "A new story begins.",
                ShowTextImmediately = true
            };
            _graph.EntryNodeID = line.NodeID;
            _graph.AllNodes = new List<RuntimeNode> { line };

            controller.SetSkip(true);
            _runner.Session.Play(_graph);

            Assert.That(controller.SkipMode, Is.True);
            Assert.That(_runner.Session.CurrentNode, Is.SameAs(line));
        }

        [UnityTest]
        public IEnumerator GeneratedSkipButtonIsClickableAndShowsItsState()
        {
            NovelPlayerController controller = _runnerObject.AddComponent<NovelPlayerController>();
            controller.AutoPlayOnStart = false;
            yield return null;

            GameObject buttonObject = GameObject.Find("Skip Button");
            Assert.That(buttonObject, Is.Not.Null);
            Button button = buttonObject.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            Assert.That(button.targetGraphic.raycastTarget, Is.True);
            button.onClick.Invoke();
            Assert.That(controller.SkipMode, Is.True);
            Assert.That(button.GetComponentInChildren<Text>().text, Is.EqualTo("Skip: On"));
        }

        [UnityTest]
        public IEnumerator FlashAndShakeWaitThenContinueToNarration()
        {
            var presentation = new TestPresentation();
            _runner.UsePresentation(presentation);
            var flash = new RuntimeScreenFlashNode
            {
                NodeID = "flash",
                NextNodeID = "shake",
                Duration = 0.02f,
                WaitForCompletion = true
            };
            var shake = new RuntimeScreenShakeNode
            {
                NodeID = "shake",
                NextNodeID = "narration",
                Duration = 0.02f,
                Amplitude = 12f,
                WaitForCompletion = true
            };
            var narration = new RuntimeNarrationNode
            {
                NodeID = "narration",
                DialogueText = "After the impact.",
                ShowTextImmediately = true
            };
            _graph.EntryNodeID = flash.NodeID;
            _graph.AllNodes = new List<RuntimeNode> { flash, shake, narration };

            _runner.Session.Play(_graph);
            Assert.That(_runner.Session.IsWaiting, Is.True);
            float timeout = Time.realtimeSinceStartup + 2f;
            while (_runner.Session.CurrentNode != narration && Time.realtimeSinceStartup < timeout)
                yield return null;
            Assert.That(_runner.Session.CurrentNode, Is.SameAs(narration));
            Assert.That(_runner.Session.IsWaiting, Is.False);
            Assert.That(presentation.LastDialogue.Text, Is.EqualTo("After the impact."));
        }
    }
}
