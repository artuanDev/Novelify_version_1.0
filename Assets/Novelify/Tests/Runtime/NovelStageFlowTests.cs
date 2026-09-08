using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
    }
}
