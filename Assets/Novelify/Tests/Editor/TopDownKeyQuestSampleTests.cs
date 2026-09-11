using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Novelify.Tests
{
    public sealed class TopDownKeyQuestSampleTests
    {
        private const string DaisyGraphPath =
            "Assets/Novelify/Samples/TopDownKeyQuest/Graphs/DaisyConversation.novelgraph";
        private const string DoorGraphPath =
            "Assets/Novelify/Samples/TopDownKeyQuest/Graphs/DoorInteraction.novelgraph";
        private const string HasKeyPath =
            "Assets/Novelify/Samples/TopDownKeyQuest/Variables/HasKey.asset";

        [Test]
        public void CompleteQuestChangesDialogueAndRaisesDoorEvent()
        {
            RuntimeNovelGraph daisy = AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>(DaisyGraphPath);
            RuntimeNovelGraph door = AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>(DoorGraphPath);
            NovelVariableDefinition hasKey = AssetDatabase.LoadAssetAtPath<NovelVariableDefinition>(HasKeyPath);
            Assert.That(daisy, Is.Not.Null);
            Assert.That(door, Is.Not.Null);
            Assert.That(hasKey, Is.Not.Null);

            GameObject runnerObject = new GameObject("TopDownKeyQuestSampleTests");
            NovelGraphRunner runner = runnerObject.AddComponent<NovelGraphRunner>();
            runner.Session.UsePresentation(new InstantPresentation());
            bool doorOpened = false;
            runner.Session.EventRaised += eventName => doorOpened |= eventName == "topdown.door.open";

            try
            {
                runner.Session.Play(door);
                StringAssert.Contains("missing a key", CurrentLine(runner));
                AdvanceAsNextFrame(runner);
                StringAssert.Contains("Daisy", CurrentLine(runner));
                AdvanceAsNextFrame(runner);
                Assert.That(runner.Session.IsRunning, Is.False);
                Assert.That(doorOpened, Is.False);

                runner.Session.Play(daisy);
                StringAssert.Contains("cannot hand it", CurrentLine(runner));
                AdvanceAsNextFrame(runner);
                Assert.That(runner.Session.CurrentChoices.Count, Is.EqualTo(3));
                Assert.That(runner.Session.TryChoose("demand-key", out string rudeError), Is.True, rudeError);
                StringAssert.Contains("will not reward", CurrentLine(runner));
                AdvanceAsNextFrame(runner);
                StringAssert.Contains("came out wrong", CurrentLine(runner));
                AdvanceAsNextFrame(runner);
                Assert.That(runner.Session.GetBool(hasKey), Is.False);

                runner.Session.Play(daisy);
                StringAssert.Contains("cannot hand it", CurrentLine(runner));
                AdvanceAsNextFrame(runner);
                Assert.That(runner.Session.TryChoose("promise-return", out string chooseError), Is.True, chooseError);
                StringAssert.Contains("promise I can believe", CurrentLine(runner));
                AdvanceAsNextFrame(runner);
                Assert.That(runner.Session.GetBool(hasKey), Is.True);
                StringAssert.Contains("Thank you", CurrentLine(runner));
                AdvanceAsNextFrame(runner);

                runner.Session.Play(daisy);
                StringAssert.Contains("key already", CurrentLine(runner));
                runner.Session.Stop();

                runner.Session.Play(door);
                StringAssert.Contains("slides into the lock", CurrentLine(runner));
                AdvanceAsNextFrame(runner);
                StringAssert.Contains("way ahead", CurrentLine(runner));
                AdvanceAsNextFrame(runner);
                Assert.That(doorOpened, Is.True);
                Assert.That(runner.Session.IsRunning, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(runnerObject);
            }
        }

        private static string CurrentLine(NovelGraphRunner runner) =>
            (runner.Session.CurrentNode as RuntimeDialogueNode)?.DialogueText ?? string.Empty;

        private static void AdvanceAsNextFrame(NovelGraphRunner runner)
        {
            // EditMode tests do not advance Time.frameCount consistently. Reset only
            // the private debounce marker so each call represents a later player click.
            typeof(NovelGraphRunner).GetField("_nodeEnteredFrame", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(runner, -1);
            runner.Session.Advance();
        }

        private sealed class InstantPresentation : INovelPresentation
        {
            public bool IsRevealing => false;
            public void PresentDialogue(NovelDialoguePresentation dialogue) { }
            public void PresentChoices(IReadOnlyList<NovelChoicePresentation> choices) { }
            public void CompleteReveal() { }
            public void HideDialogue() { }
            public void ClearChoices() { }
            public void Stop() { }
        }
    }
}
