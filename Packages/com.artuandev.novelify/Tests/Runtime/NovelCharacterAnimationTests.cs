using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Novelify.Tests
{
    public class NovelCharacterAnimationTests
    {
        private GameObject _host, _stage, _prefab;
        private NovelGraphRunner _runner;
        private NovelCharacter _character;
        private RuntimeNovelGraph _graph;

        [SetUp]
        public void SetUp()
        {
            _stage = new GameObject("Animation stage", typeof(RectTransform));
            _stage.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 600f);
            _prefab = new GameObject("Animation portrait", typeof(RectTransform), typeof(CharacterInfo));
            _prefab.SetActive(false);
            _host = new GameObject("Animation runner");
            _host.SetActive(false);
            _runner = _host.AddComponent<NovelGraphRunner>();
            _runner.CharacterContainer = _stage.transform;
            _runner.PortraitPrefab = _prefab;
            _runner.HideCharactersOnEnd = false;
            _host.SetActive(true);
            _character = ScriptableObject.CreateInstance<NovelCharacter>();
            _graph = ScriptableObject.CreateInstance<RuntimeNovelGraph>();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_stage);
            Object.DestroyImmediate(_prefab);
            Object.DestroyImmediate(_character);
            Object.DestroyImmediate(_graph);
        }

        private void Play(RuntimeAnimateCharacterNode animation, bool withStop = false)
        {
            animation.NodeID = "animation";
            animation.NextNodeID = "line";
            animation.Character = _character;
            _graph.EntryNodeID = animation.NodeID;
            _graph.AllNodes = new List<RuntimeNode>
            {
                animation,
                new RuntimeDialogueNode { NodeID = "line", NextNodeID = withStop ? "stop" : null, ShowTextImmediately = true }
            };
            if (withStop)
            {
                _graph.AllNodes.Add(new RuntimeStopCharacterAnimationNode
                    { NodeID = "stop", NextNodeID = "after", Character = _character, InstanceID = animation.InstanceID });
                _graph.AllNodes.Add(new RuntimeDialogueNode { NodeID = "after", ShowTextImmediately = true });
            }
            _runner.Session.Play(_graph);
        }

        [UnityTest]
        public IEnumerator BounceLoopsOnOneInstanceUntilItsStopNodeRestoresThePose()
        {
            var animation = new RuntimeAnimateCharacterNode
                { Animation = NovelCharacterAnimation.Bounce, InstanceID = "bouncing", Amplitude = 40f, Frequency = 2f, WaitForCompletion = true };
            Play(animation, true);
            CharacterInfo target = _runner.ShowCharacter(_character, "bouncing");
            CharacterInfo other = _runner.ShowCharacter(_character, "other");
            Vector2 origin = target.Position;
            Assert.That(_runner.CurrentNode.NodeID, Is.EqualTo("line"));
            Assert.That(_runner.IsWaiting, Is.False, "An indefinite animation must not block flow.");
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(target.IsAnimating, Is.True);
            Assert.That(target.Position, Is.EqualTo(origin));
            Assert.That(target.GetComponent<RectTransform>().anchoredPosition.y, Is.GreaterThan(origin.y));
            Assert.That(other.IsAnimating, Is.False);
            _runner.Session.Advance();
            Assert.That(_runner.CurrentNode.NodeID, Is.EqualTo("after"));
            Assert.That(target.IsAnimating, Is.False);
            Assert.That(target.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(origin));
        }

        [UnityTest]
        public IEnumerator ShakeRespectsReferenceInputsAndFiniteDurationWaitsForCompletion()
        {
            Play(new RuntimeAnimateCharacterNode
            {
                Animation = NovelCharacterAnimation.Shake,
                CharacterReferenceValue = new RuntimeConstantExpression { Value = RuntimeValue.From(new NovelCharacterReference(_character, "shake")) },
                AmplitudeValue = new RuntimeConstantExpression { Value = RuntimeValue.From(25f) },
                FrequencyValue = new RuntimeConstantExpression { Value = RuntimeValue.From(20f) },
                DurationValue = new RuntimeConstantExpression { Value = RuntimeValue.From(0.25f) },
                WaitForCompletion = true
            });
            CharacterInfo target = _runner.ShowCharacter(_character, "shake");
            Vector2 origin = target.Position;
            Assert.That(_runner.IsWaiting, Is.True);
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.That(Vector2.Distance(target.GetComponent<RectTransform>().anchoredPosition, origin), Is.GreaterThan(0.01f));
            _runner.Session.Advance();
            Assert.That(_runner.CurrentNode.NodeID, Is.EqualTo("animation"));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(_runner.CurrentNode.NodeID, Is.EqualTo("line"));
            Assert.That(_runner.IsWaiting, Is.False);
            Assert.That(target.IsAnimating, Is.False);
            Assert.That(target.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(origin));
        }

        [UnityTest]
        public IEnumerator SwayAndTransformTweenComposeAndStoppingPreservesTheFinalTransform()
        {
            Play(new RuntimeAnimateCharacterNode { Animation = NovelCharacterAnimation.Sway, Amplitude = 12f, Frequency = 1f });
            CharacterInfo target = _runner.ShowCharacter(_character);
            target.TransformTo(new Vector2(90f, 45f), 30f, new Vector2(1.5f, 0.8f), true, 0.15f, PortraitTweenEasing.None);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(target.IsAnimating, Is.True);
            Assert.That(target.Position, Is.EqualTo(new Vector2(90f, 45f)));
            Assert.That(target.Rotation, Is.EqualTo(30f).Within(0.001f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(target.transform.localEulerAngles.z, 30f)), Is.GreaterThan(0.01f));
            _runner.Session.Stop();
            Assert.That(target.IsAnimating, Is.False);
            Assert.That(target.transform.localEulerAngles.z, Is.EqualTo(30f).Within(0.001f));
            Assert.That(target.Scale, Is.EqualTo(new Vector2(1.5f, 0.8f)));
        }

        [UnityTest]
        public IEnumerator ReplacingOrHidingAnAnimationLeavesNoResidualOffset()
        {
            Play(new RuntimeAnimateCharacterNode { Animation = NovelCharacterAnimation.Bounce, Amplitude = 40f });
            CharacterInfo target = _runner.ShowCharacter(_character);
            Vector2 origin = target.Position;
            yield return new WaitForSecondsRealtime(0.1f);
            target.StartSimpleAnimation(NovelCharacterAnimation.Sway, 8f, 1f);
            Assert.That(target.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(origin));
            yield return new WaitForSecondsRealtime(0.1f);
            target.HideImmediately();
            Assert.That(target.IsAnimating, Is.False);
            Assert.That(target.transform.localEulerAngles.z, Is.Zero.Within(0.001f));
            Assert.That(target.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(origin));
        }

        [UnityTest]
        public IEnumerator ScaledAnimationPausesWhileUnscaledAnimationContinuesAndDisableStopsIt()
        {
            _runner.TimeMode = DialogueTimeMode.Scaled;
            Time.timeScale = 0f;
            yield return null;
            Play(new RuntimeAnimateCharacterNode { Animation = NovelCharacterAnimation.Bounce, Amplitude = 40f });
            CharacterInfo target = _runner.ShowCharacter(_character);
            Vector2 origin = target.Position;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(target.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(origin));
            target.TimeMode = DialogueTimeMode.Unscaled;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(target.GetComponent<RectTransform>().anchoredPosition.y, Is.GreaterThan(origin.y));
            _runner.enabled = false;
            Assert.That(target.IsAnimating, Is.False);
            Assert.That(target.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(origin));
        }

        [Test]
        public void StopNodeDoesNotCreateOrRevealItsTarget()
        {
            _graph.EntryNodeID = "stop";
            _graph.AllNodes = new List<RuntimeNode>
            {
                new RuntimeStopCharacterAnimationNode { NodeID = "stop", NextNodeID = "line", Character = _character },
                new RuntimeDialogueNode { NodeID = "line", ShowTextImmediately = true }
            };
            _runner.Session.Play(_graph);
            Assert.That(_runner.AllCharacters.Count, Is.Zero);
            CharacterInfo target = _runner.ShowCharacter(_character);
            target.HideImmediately();
            _runner.Session.Play(_graph);
            Assert.That(target.gameObject.activeSelf, Is.False);
        }
    }
}
