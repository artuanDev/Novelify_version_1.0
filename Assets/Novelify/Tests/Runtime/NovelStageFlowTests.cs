using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Novelify.Tests
{
    public class NovelStageFlowTests
    {
        private GameObject _root, _prefab, _managerObject;
        private NovelManager _manager;
        private NovelCharacter _character;
        private RuntimeNovelGraph _graph;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Stage", typeof(RectTransform));
            _root.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 600f);
            _prefab = new GameObject("Portrait", typeof(RectTransform), typeof(CharacterInfo));
            _prefab.SetActive(false);
            _managerObject = new GameObject("Manager");
            _manager = _managerObject.AddComponent<NovelManager>();
            _manager.CharacterContainer = _root.transform;
            _manager.PortraitPrefab = _prefab;
            _manager.HideCharactersOnEnd = false;
            _character = ScriptableObject.CreateInstance<NovelCharacter>();
            _graph = ScriptableObject.CreateInstance<RuntimeNovelGraph>();
            _manager.RuntimeGraph = _graph;
        }

        [Test]
        public void PortraitTweenTimingSupportsLinearPresetsAndCustomCurves()
        {
            Assert.That(PortraitTweenEasingUtility.Evaluate(PortraitTweenEasing.None, null, 0.25f),
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(PortraitTweenEasingUtility.Evaluate(PortraitTweenEasing.EaseIn, null, 0.5f),
                Is.EqualTo(0.125f).Within(0.0001f));
            Assert.That(PortraitTweenEasingUtility.Evaluate(PortraitTweenEasing.EaseOut, null, 0.5f),
                Is.EqualTo(0.875f).Within(0.0001f));

            AnimationCurve custom = AnimationCurve.Linear(0f, 0f, 1f, 0.5f);
            Assert.That(PortraitTweenEasingUtility.Evaluate(PortraitTweenEasing.Custom, custom, 1f),
                Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void DialogueSpeakerIsPromotedInFrontOfOtherPortraits()
        {
            CharacterInfo speaker = _manager.ShowCharacter(_character, "speaker");
            CharacterInfo other = _manager.ShowCharacter(_character, "other");
            Assert.That(other.transform.GetSiblingIndex(), Is.GreaterThan(speaker.transform.GetSiblingIndex()));

            Play(new RuntimeDialogueNode
            {
                NodeID = "line",
                NovelCharacter = _character,
                InstanceID = "speaker",
                ShowTextImmediately = true
            });

            Assert.That(speaker.transform.GetSiblingIndex(), Is.GreaterThan(other.transform.GetSiblingIndex()));
        }

        [UnityTest]
        public IEnumerator DialogueAppearanceAndHideTransitionsAnimateOpacityAndDirection()
        {
            CharacterInfo info = _manager.ShowCharacter(_character);
            info.Position = Vector2.zero;
            Play(new RuntimeDialogueNode
            {
                NodeID = "enter",
                NovelCharacter = _character,
                ShowTextImmediately = true,
                Appearance = CharacterTransitionMode.FadeAndSlide,
                AppearanceDirection = CharacterTransitionDirection.Left,
                AppearanceDuration = 0.15f,
                AppearanceEasing = PortraitTweenEasing.None
            });

            Assert.That(info.Opacity, Is.Zero.Within(0.001f));
            Assert.That(info.Position.x, Is.LessThan(-700f));
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(info.Opacity, Is.EqualTo(1f).Within(0.01f));
            Assert.That(info.Position.x, Is.Zero.Within(0.01f));

            Play(new RuntimeHideCharacterNode
                {
                    NodeID = "hide", NextNodeID = "after", Character = _character,
                    Transition = CharacterTransitionMode.FadeAndSlide,
                    Direction = CharacterTransitionDirection.Right,
                    Duration = 0.15f,
                    Easing = PortraitTweenEasing.None,
                    WaitForCompletion = true
                },
                new RuntimeDialogueNode { NodeID = "after", ShowTextImmediately = true });

            Assert.That(_manager.IsWaiting, Is.True);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(info.gameObject.activeSelf, Is.False);
            Assert.That(info.Position.x, Is.Zero.Within(0.01f));
            Assert.That(info.Opacity, Is.EqualTo(1f).Within(0.01f));
            Assert.That(_manager.CurrentNode.NodeID, Is.EqualTo("after"));
        }

        [UnityTest]
        public IEnumerator PortraitTweenCanAnimateOnlyTransparency()
        {
            CharacterInfo info = _manager.ShowCharacter(_character);
            Play(new RuntimeTransformSpeakerPortraitNode
                {
                    NodeID = "opacity", NextNodeID = "line", Character = _character,
                    SmoothMovement = false,
                    AnimateOpacity = true,
                    Opacity = 0.25f,
                    Duration = 0.15f,
                    Easing = PortraitTweenEasing.None,
                    UseEasingPreset = true
                },
                new RuntimeDialogueNode { NodeID = "line", ShowTextImmediately = true });

            Assert.That(_manager.IsWaiting, Is.True);
            Assert.That(info.Opacity, Is.EqualTo(1f).Within(0.001f));
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(info.Opacity, Is.EqualTo(0.25f).Within(0.01f));
            Assert.That(_manager.CurrentNode.NodeID, Is.EqualTo("line"));
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1;
            Object.DestroyImmediate(_managerObject);
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_prefab);
            Object.DestroyImmediate(_character);
            Object.DestroyImmediate(_graph);
        }

        private void Play(params RuntimeNode[] nodes)
        {
            _graph.AllNodes = new List<RuntimeNode>(nodes);
            _graph.EntryNodeID = nodes[0].NodeID;
            _manager.PlayGraph(_graph);
        }

        [Test]
        public void GraphCatalogResolvesIdsAndInvalidatesItsLookupWhenRebuilt()
        {
            NovelGraphCatalog catalog = ScriptableObject.CreateInstance<NovelGraphCatalog>();
            RuntimeNovelGraph replacement = ScriptableObject.CreateInstance<RuntimeNovelGraph>();
            try
            {
                catalog.ReplaceEntries(new[]
                {
                    new NovelGraphCatalog.Entry { GraphID = "story", Graph = _graph }
                });
                Assert.That(catalog.GetGraph("story"), Is.SameAs(_graph));

                catalog.ReplaceEntries(new[]
                {
                    new NovelGraphCatalog.Entry { GraphID = "replacement", Graph = replacement }
                });
                Assert.That(catalog.GetGraph("story"), Is.Null);
                Assert.That(catalog.GetGraph("replacement"), Is.SameAs(replacement));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(replacement);
            }
        }

        [Test]
        public void StoryVariablesModifyAndBranchThroughBooleanExpressions()
        {
            NovelVariableDefinition trust = CreateVariable("Trust", NovelVariableType.Integer, NovelVariableScope.Story);
            NovelVariableDefinition helped = CreateVariable("Helped", NovelVariableType.Boolean, NovelVariableScope.Story);
            var events = new List<string>();
            _manager.OnDialogueEvent.AddListener(events.Add);
            try
            {
                RuntimeValueExpression trustValue = new RuntimeVariableExpression { Variable = trust };
                RuntimeValueExpression helpedValue = new RuntimeVariableExpression { Variable = helped };
                Play(
                    new RuntimeSetVariableNode
                    {
                        NodeID = "set-trust", NextNodeID = "set-helped", Variable = trust,
                        Value = Constant(RuntimeValue.From(1))
                    },
                    new RuntimeSetVariableNode
                    {
                        NodeID = "set-helped", NextNodeID = "modify", Variable = helped,
                        Value = Constant(RuntimeValue.From(true))
                    },
                    new RuntimeModifyVariableNode
                    {
                        NodeID = "modify", NextNodeID = "branch", Variable = trust,
                        Operation = RuntimeVariableModifyOperation.Add,
                        Amount = Constant(RuntimeValue.From(2))
                    },
                    new RuntimeBranchNode
                    {
                        NodeID = "branch", TrueNodeID = "success", FalseNodeID = "failure",
                        Condition = new RuntimeBooleanExpression
                        {
                            Operation = RuntimeBooleanOperation.And,
                            A = helpedValue,
                            B = new RuntimeComparisonExpression
                            {
                                Operation = RuntimeComparisonOperation.GreaterOrEqual,
                                ValueKind = RuntimeValueKind.Integer,
                                A = trustValue,
                                B = Constant(RuntimeValue.From(3))
                            }
                        }
                    },
                    new RuntimeDialogueEventNode { NodeID = "success", EventName = "trusted" },
                    new RuntimeDialogueEventNode { NodeID = "failure", EventName = "blocked" });

                CollectionAssert.AreEqual(new[] { "trusted" }, events);
                Assert.That(_manager.StateStore.Get(trust).IntegerValue, Is.EqualTo(3));
                Assert.That(_manager.StateStore.Get(helped).BooleanValue, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(trust);
                Object.DestroyImmediate(helped);
            }
        }

        [Test]
        public void ManagersOwnSeparateStateStoresByDefault()
        {
            NovelVariableDefinition score = CreateVariable("Score", NovelVariableType.Integer, NovelVariableScope.Story);
            GameObject secondObject = new GameObject("Second Manager");
            NovelManager second = secondObject.AddComponent<NovelManager>();
            try
            {
                Assert.That(_manager.StateStore.TrySet(score, RuntimeValue.From(8), out string error), Is.True, error);
                Assert.That(_manager.StateStore.Get(score).IntegerValue, Is.EqualTo(8));
                Assert.That(second.StateStore.Get(score).IntegerValue, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(secondObject);
                Object.DestroyImmediate(score);
            }
        }

        [Test]
        public void FunctionCallLocalVariablesStartFreshForEveryCall()
        {
            NovelVariableDefinition local = CreateVariable("Temporary Count", NovelVariableType.Integer, NovelVariableScope.CallLocal);
            RuntimeNovelFunction function = ScriptableObject.CreateInstance<RuntimeNovelFunction>();
            var events = new List<string>();
            _manager.OnDialogueEvent.AddListener(events.Add);
            try
            {
                function.EntryNodeID = "increment";
                function.AllNodes.Add(new RuntimeModifyVariableNode
                {
                    NodeID = "increment", Variable = local,
                    Operation = RuntimeVariableModifyOperation.Add,
                    Amount = Constant(RuntimeValue.From(1))
                });
                function.Outputs.Add(new RuntimeFunctionOutput
                {
                    Name = "Count",
                    Value = new RuntimeVariableExpression { Variable = local }
                });

                Play(
                    new RuntimeCallNovelFunctionNode { NodeID = "call-one", NextNodeID = "call-two", Function = function },
                    new RuntimeCallNovelFunctionNode { NodeID = "call-two", NextNodeID = "branch", Function = function },
                    new RuntimeBranchNode
                    {
                        NodeID = "branch", TrueNodeID = "isolated", FalseNodeID = "leaked",
                        Condition = new RuntimeComparisonExpression
                        {
                            Operation = RuntimeComparisonOperation.Equal,
                            ValueKind = RuntimeValueKind.Integer,
                            A = new RuntimeFunctionOutputExpression { CallNodeID = "call-two", Name = "Count" },
                            B = Constant(RuntimeValue.From(1))
                        }
                    },
                    new RuntimeDialogueEventNode { NodeID = "isolated", EventName = "isolated" },
                    new RuntimeDialogueEventNode { NodeID = "leaked", EventName = "leaked" });

                CollectionAssert.AreEqual(new[] { "isolated" }, events);
            }
            finally
            {
                Object.DestroyImmediate(function);
                Object.DestroyImmediate(local);
            }
        }

        private static NovelVariableDefinition CreateVariable(
            string name, NovelVariableType type, NovelVariableScope scope)
        {
            NovelVariableDefinition variable = ScriptableObject.CreateInstance<NovelVariableDefinition>();
            variable.DisplayName = name;
            variable.Type = type;
            variable.Scope = scope;
            variable.EnsureID();
            return variable;
        }

        private static RuntimeConstantExpression Constant(RuntimeValue value) =>
            new RuntimeConstantExpression { Value = value };

        [Test]
        public void ReactiveChoicesHideDisableRefreshCommitOnceAndFallback()
        {
            NovelVariableDefinition coins = CreateVariable("Coins", NovelVariableType.Integer, NovelVariableScope.Story);
            NovelVariableDefinition hasKey = CreateVariable("Has Key", NovelVariableType.Boolean, NovelVariableScope.Story);
            GameObject containerObject = new GameObject("Choices", typeof(RectTransform));
            GameObject buttonObject = new GameObject("Choice Button", typeof(RectTransform), typeof(UnityEngine.UI.Button));
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            _manager.ChoiceButtonContainer = containerObject.transform;
            _manager.ChoiceButtonPrefab = buttonObject.GetComponent<UnityEngine.UI.Button>();
            var events = new List<string>();
            _manager.OnDialogueEvent.AddListener(events.Add);
            try
            {
                Assert.That(_manager.StateStore.TrySet(coins, RuntimeValue.From(10), out string stateError), Is.True, stateError);
                var purchase = new ChoiceData
                {
                    ChoiceID = "buy-key",
                    ChoiceText = "Buy the key -- 20 coins",
                    Condition = Constant(RuntimeValue.From(true)),
                    UnavailablePolicy = NovelChoiceUnavailablePolicy.Disable,
                    DisabledReason = "Need 20 coins.",
                    DestinationNodeID = "bought",
                    StateChanges = new List<RuntimeChoiceStateChange>
                    {
                        new RuntimeChoiceStateChange
                        {
                            Variable = coins, Operation = NovelChoiceStateOperation.Spend,
                            Value = Constant(RuntimeValue.From(20))
                        },
                        new RuntimeChoiceStateChange
                        {
                            Variable = hasKey, Operation = NovelChoiceStateOperation.Set,
                            Value = Constant(RuntimeValue.From(true))
                        }
                    }
                };
                var once = new ChoiceData
                {
                    ChoiceID = "past", ChoiceText = "Tell me about your past", OnceOnly = true,
                    Condition = Constant(RuntimeValue.From(true)), DestinationNodeID = "past"
                };
                var choice = new RuntimeChoiceNode
                {
                    NodeID = "choice", UnavailableDestinationNodeID = "fallback",
                    Choices = new List<ChoiceData>
                    {
                        new ChoiceData
                        {
                            ChoiceID = "hidden", ChoiceText = "Ask about the hidden room",
                            Condition = Constant(RuntimeValue.From(false)),
                            UnavailablePolicy = NovelChoiceUnavailablePolicy.Hide, DestinationNodeID = "hidden"
                        },
                        purchase,
                        once,
                        new ChoiceData
                        {
                            ChoiceID = "leave", ChoiceText = "Leave",
                            Condition = Constant(RuntimeValue.From(true)), DestinationNodeID = "leave"
                        }
                    }
                };
                Play(choice,
                    new RuntimeDialogueEventNode { NodeID = "hidden", EventName = "hidden" },
                    new RuntimeDialogueEventNode { NodeID = "bought", EventName = "bought" },
                    new RuntimeDialogueEventNode { NodeID = "past", EventName = "past" },
                    new RuntimeDialogueEventNode { NodeID = "leave", EventName = "leave" },
                    new RuntimeDialogueEventNode { NodeID = "fallback", EventName = "fallback" });

                UnityEngine.UI.Button[] buttons = containerObject.GetComponentsInChildren<UnityEngine.UI.Button>();
                Assert.That(buttons.Length, Is.EqualTo(3), "The hidden first option must not create a button.");
                Assert.That(buttons[0].interactable, Is.False);
                StringAssert.Contains("Need 20 coins", buttons[0].GetComponentInChildren<TextMeshProUGUI>().text);

                buttons[1].onClick.Invoke();
                buttons[1].onClick.Invoke();
                CollectionAssert.AreEqual(new[] { "past" }, events, "Rapid activation must commit only once.");
                Assert.That(_manager.StateStore.HasSelectedChoice("past"), Is.True);

                events.Clear();
                Play(choice,
                    new RuntimeDialogueEventNode { NodeID = "hidden", EventName = "hidden" },
                    new RuntimeDialogueEventNode { NodeID = "bought", EventName = "bought" },
                    new RuntimeDialogueEventNode { NodeID = "past", EventName = "past" },
                    new RuntimeDialogueEventNode { NodeID = "leave", EventName = "leave" },
                    new RuntimeDialogueEventNode { NodeID = "fallback", EventName = "fallback" });
                Assert.That(containerObject.GetComponentsInChildren<UnityEngine.UI.Button>().Length, Is.EqualTo(2),
                    "The once-only option must remain hidden on the next visit.");

                Assert.That(_manager.StateStore.TrySet(coins, RuntimeValue.From(20), out stateError), Is.True, stateError);
                buttons = containerObject.GetComponentsInChildren<UnityEngine.UI.Button>();
                UnityEngine.UI.Button buy = buttons.Single(button =>
                    button.GetComponentInChildren<TextMeshProUGUI>().text.StartsWith("Buy the key"));
                Assert.That(buy.interactable, Is.True, "An open menu must refresh when relevant state changes.");
                buy.onClick.Invoke();
                buy.onClick.Invoke();
                CollectionAssert.AreEqual(new[] { "bought" }, events);
                Assert.That(_manager.StateStore.Get(coins).IntegerValue, Is.Zero);
                Assert.That(_manager.StateStore.Get(hasKey).BooleanValue, Is.True);

                events.Clear();
                Play(new RuntimeChoiceNode
                    {
                        NodeID = "none", UnavailableDestinationNodeID = "fallback",
                        Choices = new List<ChoiceData>
                        {
                            new ChoiceData
                            {
                                ChoiceID = "never", ChoiceText = "Never",
                                Condition = Constant(RuntimeValue.From(false)),
                                UnavailablePolicy = NovelChoiceUnavailablePolicy.Hide
                            }
                        }
                    },
                    new RuntimeDialogueEventNode { NodeID = "fallback", EventName = "fallback" });
                CollectionAssert.AreEqual(new[] { "fallback" }, events);
            }
            finally
            {
                Object.DestroyImmediate(containerObject);
                Object.DestroyImmediate(buttonObject);
                Object.DestroyImmediate(coins);
                Object.DestroyImmediate(hasKey);
            }
        }

        [UnityTest]
        public IEnumerator TranslateCreatesItsTargetMovesAcrossFramesAndBlocksClicksUntilFinished()
        {
            int events = 0;
            _manager.OnDialogueEvent.AddListener(_ => ++events);
            Play(new RuntimeTranslateSpeakerPortraitNode { NodeID = "move", NextNodeID = "event", Character = _character,
                    OffsetX = 300, SmoothMovement = true, Duration = 0.4f },
                new RuntimeDialogueEventNode { NodeID = "event", NextNodeID = "line", EventName = "arrived" },
                new RuntimeDialogueNode { NodeID = "line" });
            CharacterInfo info = _manager.ShowCharacter(_character);
            Assert.That(info.Position.x, Is.EqualTo(0));
            Assert.That(_manager.IsWaiting, Is.True);
            _manager.Advance();
            Assert.That(events, Is.Zero);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(info.Position.x, Is.InRange(0.01f, 299.99f));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(info.Position.x, Is.EqualTo(300).Within(0.001f));
            Assert.That(events, Is.EqualTo(1));
            Assert.That(_manager.IsWaiting, Is.False);
        }

        [UnityTest]
        public IEnumerator NonBlockingMovesRunTogetherAndUseUnscaledTime()
        {
            Time.timeScale = 0;
            Play(new RuntimeTranslateSpeakerPortraitNode { NodeID = "left", NextNodeID = "right", Character = _character,
                    InstanceID = "left", OffsetX = -250, SmoothMovement = true, Duration = 0.3f, WaitForCompletion = false },
                new RuntimeTranslateSpeakerPortraitNode { NodeID = "right", NextNodeID = "line", Character = _character,
                    InstanceID = "right", OffsetX = 250, SmoothMovement = true, Duration = 0.3f, WaitForCompletion = false },
                new RuntimeDialogueNode { NodeID = "line" });
            Assert.That(_manager.IsWaiting, Is.False);
            CharacterInfo left = _manager.ShowCharacter(_character, "left");
            CharacterInfo right = _manager.ShowCharacter(_character, "right");
            Assert.That(left.IsMoving && right.IsMoving, Is.True);
            float deadline = Time.realtimeSinceStartup + 2f;
            while ((left.IsMoving || right.IsMoving) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(left.IsMoving || right.IsMoving, Is.False, "Both moves must finish while timeScale is zero.");
            // RectTransform recalculates anchored/local positions with floating-point rounding.
            Assert.That(left.Position.x, Is.EqualTo(-250).Within(0.001f));
            Assert.That(right.Position.x, Is.EqualTo(250).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator ScaledDialogueClockDeliberatelyPausesTransitions()
        {
            _manager.TimeMode = DialogueTimeMode.Scaled;
            Time.timeScale = 0;
            Play(new RuntimeTranslateSpeakerPortraitNode
                {
                    NodeID = "move", NextNodeID = "line", Character = _character,
                    OffsetX = 200f, SmoothMovement = true, Duration = 0.15f
                },
                new RuntimeDialogueNode { NodeID = "line" });
            CharacterInfo info = _manager.ShowCharacter(_character);

            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(info.Position.x, Is.Zero.Within(0.001f));
            Assert.That(_manager.IsWaiting, Is.True);

            Time.timeScale = 1;
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(info.Position.x, Is.EqualTo(200f).Within(0.001f));
            Assert.That(_manager.CurrentNode.NodeID, Is.EqualTo("line"));
        }

        [Test]
        public void CharacterReferenceTargetsInstanceAndSetFacingIsIdempotent()
        {
            RuntimeValueExpression target = new RuntimeConstantExpression
            {
                Value = RuntimeValue.From(new NovelCharacterReference(_character, "second"))
            };
            Play(new RuntimeShowCharacterNode
                {
                    NodeID = "show", NextNodeID = "face-one", CharacterReferenceValue = target
                },
                new RuntimeSetCharacterFacingNode
                {
                    NodeID = "face-one", NextNodeID = "face-two", CharacterReferenceValue = target,
                    Facing = CharacterFacing.Left
                },
                new RuntimeSetCharacterFacingNode
                {
                    NodeID = "face-two", NextNodeID = "line", CharacterReferenceValue = target,
                    Facing = CharacterFacing.Left
                },
                new RuntimeDialogueNode { NodeID = "line" });

            Assert.That(_manager.SearchAlreadyCreatedCharacter(_character, "second"), Is.True);
            CharacterInfo info = _manager.ShowCharacter(_character, "second");
            Assert.That(info.transform.localScale.x, Is.LessThan(0f));
            Assert.That(_manager.AllCharacters.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WaitCannotBeSkippedAndStoppingCancelsContinuation()
        {
            int events = 0;
            _manager.OnDialogueEvent.AddListener(_ => ++events);
            Play(new RuntimeWaitNode { NodeID = "wait", NextNodeID = "event", Duration = 0.3f },
                new RuntimeDialogueEventNode { NodeID = "event" });
            _manager.Advance();
            Assert.That(_manager.IsWaiting, Is.True);
            yield return null;
            _manager.EndDialogue();
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(events, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AutomaticNodesKeepCharactersVisibleWhenDialogueIsHidden()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(_root.transform, false);
            var panel = new GameObject("Dialogue Panel", typeof(RectTransform));
            panel.transform.SetParent(canvas.transform, false);
            _manager.CharacterContainer = null;
            _manager.CanvasDialogue = panel;
            _manager.DialoguePanel = panel;
            Play(new RuntimeShowCharacterNode { NodeID = "show", NextNodeID = "wait", Character = _character },
                new RuntimeWaitNode { NodeID = "wait", NextNodeID = "line", Duration = 0.2f },
                new RuntimeDialogueNode { NodeID = "line" });
            CharacterInfo info = _manager.ShowCharacter(_character);
            Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(info.gameObject.activeInHierarchy, Is.True);
            Assert.That(info.transform.IsChildOf(panel.transform), Is.False);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(info.gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator ManagerInsideDialoguePanelSurvivesAudioWaitAndMovement()
        {
            GameObject panel = CreateNestedManagerPanel();
            var clip = AudioClip.Create("Regression audio", 44100 * 4, 1, 44100, false);
            try
            {
                Play(new RuntimePlaySoundNode { NodeID = "sound", NextNodeID = "first", ClipSound = clip, Loop = true },
                    new RuntimeDialogueNode { NodeID = "first", NextNodeID = "wait" },
                    new RuntimeWaitNode { NodeID = "wait", NextNodeID = "move", Duration = 0.1f },
                    new RuntimeTranslateSpeakerPortraitNode { NodeID = "move", NextNodeID = "second", Character = _character,
                        SmoothMovement = true, OffsetX = 150, Duration = 0.2f },
                    new RuntimeDialogueNode { NodeID = "second", NextNodeID = "stop" },
                    new RuntimeStopSoundNode { NodeID = "stop", NextNodeID = "last" },
                    new RuntimeDialogueNode { NodeID = "last" });
                Assert.That(_manager.CurrentNode.NodeID, Is.EqualTo("first"));
                Assert.That(_manager.PlaySoundSource.clip, Is.SameAs(clip));
                Assert.That(_manager.PlaySoundSource.isPlaying, Is.True);
                yield return null;
                _manager.Advance();
                Assert.That(_manager.isActiveAndEnabled, Is.True);
                Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.Zero);
                Assert.That(_manager.IsWaiting, Is.True);
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.That(_manager.CurrentNode.NodeID, Is.EqualTo("second"));
                Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
                Assert.That(_manager.PlaySoundSource.isPlaying, Is.True);
                _manager.Advance();
                Assert.That(_manager.CurrentNode.NodeID, Is.EqualTo("last"));
                Assert.That(_manager.PlaySoundSource.isPlaying, Is.False);
                _manager.EndDialogue();
                Assert.That(_manager.isActiveAndEnabled, Is.True);
                Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.Zero);
            }
            finally { Object.DestroyImmediate(clip); }
        }

        private GameObject CreateNestedManagerPanel()
        {
            var panel = new GameObject("DialogueUI", typeof(RectTransform));
            panel.transform.SetParent(_root.transform, false);
            _managerObject.transform.SetParent(panel.transform, false);
            _manager.DialoguePanel = panel;
            return panel;
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator ExampleStoryPlaysMusicAndReachesBothCharacters()
        {
            CreateNestedManagerPanel();
            RuntimeNovelGraph example = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>(
                "Assets/Novelify/Samples/NovelGraphs/ExampleStory.novelgraph");
            Assert.That(example, Is.Not.Null);
            _manager.PlayGraph(example);
            Assert.That(_manager.CurrentNode, Is.TypeOf<RuntimeDialogueNode>());
            Assert.That(((RuntimeDialogueNode)_manager.CurrentNode).NovelCharacter, Is.Null, "The opening line is narration.");
            Assert.That(_manager.PlaySoundSource.clip, Is.Not.Null, "Resolve the MusicToPlay graph variable.");
            Assert.That(_manager.PlaySoundSource.isPlaying, Is.True);
            yield return null;
            _manager.Advance();
            Assert.That(((RuntimeDialogueNode)_manager.CurrentNode).NovelCharacter.name, Is.EqualTo("Hoki"));
            yield return null;
            _manager.Advance();
            Assert.That(_manager.IsWaiting, Is.True, "Hoki must pass through Translate before Daisy's line.");
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.That(((RuntimeDialogueNode)_manager.CurrentNode).NovelCharacter.name, Is.EqualTo("Daisy"));
            Assert.That(_manager.AllCharacters.Count, Is.EqualTo(2));
            Assert.That(_manager.PlaySoundSource.isPlaying, Is.True);
            _manager.Advance();
            Assert.That(_manager.CurrentNode, Is.Null);
            Assert.That(_manager.PlaySoundSource.isPlaying, Is.False);
        }
#endif

        [UnityTest]
        public IEnumerator EventListenersCanStopTheGraphWithoutExecutingFollowingNodes()
        {
            int events = 0;
            _manager.OnDialogueEvent.AddListener(_ => { ++events; _manager.EndDialogue(); });
            Play(new RuntimeDialogueEventNode { NodeID = "first", NextNodeID = "second" },
                new RuntimeDialogueEventNode { NodeID = "second" });
            Assert.That(events, Is.EqualTo(1));
            _manager.enabled = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TransformUsesNormalizedStageBoundsMarginRotationAndScale()
        {
            Play(new RuntimeTransformSpeakerPortraitNode
                {
                    NodeID = "transform",
                    NextNodeID = "line",
                    Character = _character,
                    OffsetX = 0.5f,
                    OffsetY = -0.5f,
                    Margin = 100f,
                    Rotation = 90f,
                    Scale = new Vector2(2f, 0.5f),
                    SmoothMovement = true,
                    Duration = 0.2f
                },
                new RuntimeDialogueNode { NodeID = "line" });

            CharacterInfo info = _manager.ShowCharacter(_character);
            Assert.That(_manager.IsWaiting, Is.True);
            yield return new WaitForSecondsRealtime(0.3f);

            Assert.That(info.Position.x, Is.EqualTo(250f).Within(0.01f));
            Assert.That(info.Position.y, Is.EqualTo(-200f).Within(0.01f));
            Assert.That(Mathf.DeltaAngle(info.Rotation, 90f), Is.Zero.Within(0.01f));
            Assert.That(info.Scale.x, Is.EqualTo(2f).Within(0.01f));
            Assert.That(info.Scale.y, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(_manager.CurrentNode.NodeID, Is.EqualTo("line"));
        }

        [UnityTest]
        public IEnumerator CalledGraphRunsAndReturnsToItsCaller()
        {
            RuntimeNovelGraph child = ScriptableObject.CreateInstance<RuntimeNovelGraph>();
            RuntimeNovelGraph grandchild = ScriptableObject.CreateInstance<RuntimeNovelGraph>();
            var events = new List<string>();
            _manager.OnDialogueEvent.AddListener(events.Add);
            try
            {
                grandchild.EntryNodeID = "grandchild-event";
                grandchild.AllNodes.Add(new RuntimeDialogueEventNode
                    { NodeID = "grandchild-event", EventName = "grandchild" });
                child.EntryNodeID = "child-event";
                child.AllNodes.Add(new RuntimeDialogueEventNode
                    { NodeID = "child-event", NextNodeID = "nested-call", EventName = "child" });
                child.AllNodes.Add(new RuntimeCallNovelPageNode
                    { NodeID = "nested-call", Graph = grandchild });

                Play(new RuntimeDialogueEventNode
                        { NodeID = "before", NextNodeID = "call", EventName = "before" },
                    new RuntimeCallNovelPageNode
                        { NodeID = "call", NextNodeID = "after", Graph = child },
                    new RuntimeDialogueEventNode
                        { NodeID = "after", NextNodeID = "line", EventName = "after" },
                    new RuntimeDialogueNode { NodeID = "line" });

                CollectionAssert.AreEqual(new[] { "before", "child", "grandchild", "after" }, events);
                Assert.That(_manager.CurrentNode.NodeID, Is.EqualTo("line"));
                Assert.That(_manager.RuntimeGraph, Is.SameAs(_graph));
            }
            finally
            {
                Object.DestroyImmediate(child);
                Object.DestroyImmediate(grandchild);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NovelFunctionInputsOutputsMathAndLiveCharacterPositionComposeAtRuntime()
        {
            RuntimeNovelFunction function = ScriptableObject.CreateInstance<RuntimeNovelFunction>();
            try
            {
                var characterInput = new RuntimeFunctionInputExpression { Name = "Target" };
                var deltaInput = new RuntimeFunctionInputExpression { Name = "Delta" };
                var livePosition = new RuntimeCharacterComponentExpression
                {
                    Component = RuntimeCharacterComponent.NormalizedPosition,
                    Character = characterInput,
                    InstanceID = new RuntimeConstantExpression { Value = RuntimeValue.From(string.Empty) }
                };
                function.Inputs.Add(new RuntimeFunctionInput { Name = "Target", DefaultValue = RuntimeValue.None() });
                function.Inputs.Add(new RuntimeFunctionInput { Name = "Delta", DefaultValue = RuntimeValue.From(Vector2.zero) });
                function.EntryNodeID = "function-move";
                function.AllNodes.Add(new RuntimeTransformSpeakerPortraitNode
                {
                    NodeID = "function-move",
                    CharacterValue = characterInput,
                    PositionValue = new RuntimeArithmeticExpression
                    {
                        Operation = RuntimeArithmeticOperation.Add,
                        ValueKind = RuntimeValueKind.Vector2,
                        A = livePosition,
                        B = deltaInput
                    }
                });
                function.Outputs.Add(new RuntimeFunctionOutput { Name = "Final Position", Value = livePosition });

                var call = new RuntimeCallNovelFunctionNode
                {
                    NodeID = "call",
                    NextNodeID = "adjust",
                    Function = function,
                    Arguments = new List<RuntimeFunctionArgument>
                    {
                        new RuntimeFunctionArgument
                        {
                            Name = "Target",
                            Value = new RuntimeConstantExpression { Value = RuntimeValue.From(_character) }
                        },
                        new RuntimeFunctionArgument
                        {
                            Name = "Delta",
                            Value = new RuntimeConstantExpression { Value = RuntimeValue.From(new Vector2(0.5f, 0f)) }
                        }
                    }
                };
                var adjust = new RuntimeTransformSpeakerPortraitNode
                {
                    NodeID = "adjust",
                    NextNodeID = "line",
                    Character = _character,
                    PositionValue = new RuntimeArithmeticExpression
                    {
                        Operation = RuntimeArithmeticOperation.Subtract,
                        ValueKind = RuntimeValueKind.Vector2,
                        A = new RuntimeFunctionOutputExpression { CallNodeID = "call", Name = "Final Position" },
                        B = new RuntimeConstantExpression { Value = RuntimeValue.From(new Vector2(0.25f, 0f)) }
                    }
                };

                Play(call, adjust, new RuntimeDialogueNode { NodeID = "line" });

                CharacterInfo info = _manager.ShowCharacter(_character);
                Assert.That(info.Position.x, Is.EqualTo(100f).Within(0.01f));
                Assert.That(info.Position.y, Is.Zero.Within(0.01f));
                Assert.That(_manager.CurrentNode.NodeID, Is.EqualTo("line"));
            }
            finally
            {
                Object.DestroyImmediate(function);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NestedFunctionCheckpointRoundTripsWithoutReplayingReward()
        {
            string directory = Path.Combine(Path.GetTempPath(), "NovelifyPersistence_" + Guid.NewGuid().ToString("N"));
            NovelVariableDefinition coins = CreateVariable("Coins", NovelVariableType.Integer, NovelVariableScope.Story);
            RuntimeNovelFunction function = ScriptableObject.CreateInstance<RuntimeNovelFunction>();
            NovelGraphCatalog catalog = ScriptableObject.CreateInstance<NovelGraphCatalog>();
            var events = new List<string>();
            try
            {
                _graph.GraphID = "main-story";
                _graph.ContentVersion = "main-v1";
                function.GraphID = "interview-function";
                function.ContentVersion = "function-v1";
                function.EntryNodeID = "checkpoint";
                function.Inputs.Add(new RuntimeFunctionInput { Name = "Question", DefaultValue = RuntimeValue.From("none") });
                function.AllNodes.Add(new RuntimeCheckpointNode
                    { NodeID = "checkpoint", CheckpointID = "inside-interview", NextNodeID = "question-line" });
                function.AllNodes.Add(new RuntimeDialogueNode
                    { NodeID = "question-line", DialogueText = "Choose an answer.", ShowTextImmediately = true });
                catalog.ReplaceEntries(new[]
                {
                    new NovelGraphCatalog.Entry { GraphID = _graph.GraphID, Graph = _graph },
                    new NovelGraphCatalog.Entry { GraphID = function.GraphID, Graph = function }
                });
                catalog.ReplaceAssetEntries(null, new[]
                {
                    new NovelGraphCatalog.VariableEntry { VariableID = coins.ID, Variable = coins }
                });
                _manager.AssetCatalog = catalog;
                _manager.UseSaveStorage(new NovelSaveStorage(directory));
                _manager.OnDialogueEvent.AddListener(events.Add);

                Play(
                    new RuntimeSetVariableNode
                    {
                        NodeID = "initial-coins", NextNodeID = "call", Variable = coins,
                        Value = Constant(RuntimeValue.From(5))
                    },
                    new RuntimeCallNovelFunctionNode
                    {
                        NodeID = "call", NextNodeID = "reward", Function = function,
                        Arguments = new List<RuntimeFunctionArgument>
                        {
                            new RuntimeFunctionArgument { Name = "Question", Value = Constant(RuntimeValue.From("trust")) }
                        }
                    },
                    new RuntimeDialogueEventNode { NodeID = "reward", NextNodeID = "after", EventName = "reward" },
                    new RuntimeDialogueNode { NodeID = "after", DialogueText = "Returned.", ShowTextImmediately = true });

                Assert.That(_manager.CurrentNode.NodeID, Is.EqualTo("question-line"));
                Assert.That(_manager.LatestCheckpoint, Is.Not.Null);
                Assert.That(_manager.LatestCheckpoint.Frames.Count, Is.EqualTo(1));
                Assert.That(_manager.LatestCheckpoint.CurrentScope.Inputs.Single().Value.StringValue, Is.EqualTo("trust"));
                Assert.That(_manager.SaveSlot("interview").Succeeded, Is.True);

                Assert.That(_manager.StateStore.TrySet(coins, RuntimeValue.From(99), out string error), Is.True, error);
                _manager.EndDialogue();
                NovelPersistenceResult loaded = _manager.LoadSlot("interview");
                Assert.That(loaded.Succeeded, Is.True, loaded.ToString());
                Assert.That(_manager.CurrentNode.NodeID, Is.EqualTo("question-line"));
                Assert.That(_manager.StateStore.Get(coins).IntegerValue, Is.EqualTo(5));
                CollectionAssert.IsEmpty(events, "Loading must not replay caller-side rewards.");

                yield return null;
                _manager.Advance();
                CollectionAssert.AreEqual(new[] { "reward" }, events);
                Assert.That(_manager.CurrentNode.NodeID, Is.EqualTo("after"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(function);
                Object.DestroyImmediate(coins);
            }
        }

        [Test]
        public void MissingSavedNodeMigratesToNamedCheckpointAndKeepsCallerFrame()
        {
            RuntimeNovelFunction function = ScriptableObject.CreateInstance<RuntimeNovelFunction>();
            NovelGraphCatalog catalog = ScriptableObject.CreateInstance<NovelGraphCatalog>();
            try
            {
                _graph.GraphID = "main";
                _graph.ContentVersion = "v1";
                function.GraphID = "function";
                function.ContentVersion = "v1";
                function.EntryNodeID = "checkpoint";
                var checkpoint = new RuntimeCheckpointNode
                    { NodeID = "checkpoint", CheckpointID = "safe", NextNodeID = "saved-line" };
                function.AllNodes.Add(checkpoint);
                function.AllNodes.Add(new RuntimeDialogueNode { NodeID = "saved-line", ShowTextImmediately = true });
                catalog.ReplaceEntries(new[]
                {
                    new NovelGraphCatalog.Entry { GraphID = "main", Graph = _graph },
                    new NovelGraphCatalog.Entry { GraphID = "function", Graph = function }
                });
                _manager.AssetCatalog = catalog;
                Play(new RuntimeCallNovelFunctionNode
                    { NodeID = "call", NextNodeID = "caller-line", Function = function },
                    new RuntimeDialogueNode { NodeID = "caller-line", ShowTextImmediately = true });
                Assert.That(_manager.CaptureSnapshot(out NovelSaveData snapshot).Succeeded, Is.True);

                function.ContentVersion = "v2";
                checkpoint.NextNodeID = "fallback-line";
                function.AllNodes.RemoveAll(node => node.NodeID == "saved-line");
                function.AllNodes.Add(new RuntimeDialogueNode { NodeID = "fallback-line", ShowTextImmediately = true });

                NovelPersistenceResult result = _manager.RestoreSnapshot(snapshot);
                Assert.That(result.Status, Is.EqualTo(NovelPersistenceStatus.Migrated), result.ToString());
                Assert.That(_manager.CurrentNode.NodeID, Is.EqualTo("fallback-line"));
                Assert.That(snapshot.Frames.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(function);
            }
        }

        [Test]
        public void CorruptSlotDoesNotReplaceRunningSessionAndBackupRemainsLoadable()
        {
            string directory = Path.Combine(Path.GetTempPath(), "NovelifyStorage_" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new NovelSaveStorage(directory);
                var first = new NovelSaveData { TimestampUtc = "first" };
                var second = new NovelSaveData { TimestampUtc = "second" };
                Assert.That(storage.SaveSlot("slot", first).Succeeded, Is.True);
                Assert.That(storage.SaveSlot("slot", second).Succeeded, Is.True);
                File.WriteAllText(storage.GetSlotPath("slot"), "corrupt");
                NovelPersistenceResult recovered = storage.LoadSlot("slot", out NovelSaveData loaded);
                Assert.That(recovered.Status, Is.EqualTo(NovelPersistenceStatus.RestoredBackup));
                Assert.That(loaded.TimestampUtc, Is.EqualTo("first"));

                _graph.GraphID = "running";
                _graph.ContentVersion = "v1";
                Play(new RuntimeDialogueNode { NodeID = "still-running", ShowTextImmediately = true });
                _manager.UseSaveStorage(storage);
                File.WriteAllText(storage.GetSlotPath("broken"), "corrupt");
                NovelPersistenceResult failed = _manager.LoadSlot("broken");
                Assert.That(failed.Succeeded, Is.False);
                Assert.That(_manager.CurrentNode.NodeID, Is.EqualTo("still-running"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void StoryAndProfilePersistenceAreSeparate()
        {
            NovelVariableDefinition story = CreateVariable("Story", NovelVariableType.Integer, NovelVariableScope.Story);
            NovelVariableDefinition profile = CreateVariable("Profile", NovelVariableType.Boolean, NovelVariableScope.Profile);
            try
            {
                Assert.That(_manager.StateStore.TrySet(story, RuntimeValue.From(7), out string error), Is.True, error);
                Assert.That(_manager.StateStore.TrySet(profile, RuntimeValue.From(true), out error), Is.True, error);
                List<NovelSavedVariable> storyValues = _manager.StateStore.CaptureStoryVariables();
                List<NovelSavedVariable> profileValues = _manager.StateStore.CaptureProfileVariables();
                Assert.That(storyValues.Select(item => item.VariableID), Is.EquivalentTo(new[] { story.ID }));
                Assert.That(profileValues.Select(item => item.VariableID), Is.EquivalentTo(new[] { profile.ID }));
            }
            finally
            {
                Object.DestroyImmediate(story);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SnapshotRestoresCharacterIdentityVisibilityTransformEmotionAndFacing()
        {
            NovelGraphCatalog catalog = ScriptableObject.CreateInstance<NovelGraphCatalog>();
            try
            {
                _graph.GraphID = "stage-story";
                _graph.ContentVersion = "v1";
                catalog.ReplaceEntries(new[] { new NovelGraphCatalog.Entry { GraphID = _graph.GraphID, Graph = _graph } });
                catalog.ReplaceAssetEntries(new[]
                {
                    new NovelGraphCatalog.CharacterEntry { CharacterID = "hoki", Character = _character }
                }, null);
                _manager.AssetCatalog = catalog;
                Play(
                    new RuntimeShowCharacterNode
                    {
                        NodeID = "show", NextNodeID = "face", Character = _character,
                        Position = new Vector2(120f, 45f), PositionSpace = CharacterPositionSpace.Canvas,
                        Emotion = CharacterEmotion.Happy
                    },
                    new RuntimeSetCharacterFacingNode
                    {
                        NodeID = "face", NextNodeID = "checkpoint", Character = _character,
                        Facing = CharacterFacing.Left
                    },
                    new RuntimeCheckpointNode
                    {
                        NodeID = "checkpoint", NextNodeID = "line", CheckpointID = "stage-ready"
                    },
                    new RuntimeDialogueNode
                    {
                        NodeID = "line", NovelCharacter = _character, Emotion = CharacterEmotion.Happy,
                        DialogueText = "Ready.", ShowTextImmediately = true
                    });

                NovelSaveData snapshot = _manager.LatestCheckpoint;
                Assert.That(snapshot.Characters.Count, Is.EqualTo(1));
                CharacterInfo info = _manager.ShowCharacter(_character);
                info.Position = new Vector2(-300f, -200f);
                info.Scale = new Vector2(3f, 2f);
                info.SetEmotion(CharacterEmotion.Angry);
                info.gameObject.SetActive(false);

                Assert.That(_manager.RestoreSnapshot(snapshot).Succeeded, Is.True);
                Assert.That(info.gameObject.activeSelf, Is.True);
                Assert.That(info.Position, Is.EqualTo(new Vector2(120f, 45f)));
                Assert.That(info.Scale.x, Is.LessThan(0f));
                Assert.That(info.Emotion, Is.EqualTo(CharacterEmotion.Happy));
            }
            finally { Object.DestroyImmediate(catalog); }
        }

        [UnityTest]
        public IEnumerator SaveRequestedDuringWaitCompletesAtNextBoundary()
        {
            string directory = Path.Combine(Path.GetTempPath(), "NovelifyDeferred_" + Guid.NewGuid().ToString("N"));
            try
            {
                _graph.GraphID = "deferred-story";
                _graph.ContentVersion = "v1";
                _manager.UseSaveStorage(new NovelSaveStorage(directory));
                Play(
                    new RuntimeWaitNode { NodeID = "wait", NextNodeID = "line", Duration = 0.05f },
                    new RuntimeDialogueNode { NodeID = "line", ShowTextImmediately = true });
                NovelPersistenceResult pending = _manager.SaveSlot("deferred");
                Assert.That(pending.Status, Is.EqualTo(NovelPersistenceStatus.Pending));
                Assert.That(_manager.IsSavePending, Is.True);
                yield return new WaitForSecondsRealtime(0.12f);
                Assert.That(_manager.IsSavePending, Is.False);
                Assert.That(File.Exists(_manager.SaveStorage.GetSlotPath("deferred")), Is.True);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void AutosaveCheckpointWritesReservedSlotAtBoundary()
        {
            string directory = Path.Combine(Path.GetTempPath(), "NovelifyAutosave_" + Guid.NewGuid().ToString("N"));
            try
            {
                _graph.GraphID = "autosave-story";
                _graph.ContentVersion = "v1";
                _manager.UseSaveStorage(new NovelSaveStorage(directory));
                Play(
                    new RuntimeCheckpointNode
                    {
                        NodeID = "checkpoint", NextNodeID = "line", CheckpointID = "chapter-start",
                        SaveMode = NovelCheckpointSaveMode.Autosave, AutosaveSlotID = "autosave"
                    },
                    new RuntimeDialogueNode { NodeID = "line", ShowTextImmediately = true });

                Assert.That(_manager.LatestCheckpoint, Is.Not.Null);
                Assert.That(_manager.LatestCheckpoint.CheckpointID, Is.EqualTo("chapter-start"));
                Assert.That(File.Exists(_manager.SaveStorage.GetSlotPath("autosave")), Is.True);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
