using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Novelify
{
    public partial class NovelManager : MonoBehaviour
    {
        public RuntimeNovelGraph RuntimeGraph;

        private readonly Stack<GraphCallFrame> _graphCalls = new Stack<GraphCallFrame>();
        private RuntimeValueScope _valueScope = new RuntimeValueScope();

        [Header("Sound Settings")]
        public AudioSource TalkSource;
        [Tooltip("Sound attached directly to dialogue nodes.")]
        [FormerlySerializedAs("PlaySound")]
        public AudioSource NodeSoundSource;
        [Tooltip("Sound source used by Play Sound nodes.")]
        public AudioSource PlaySoundSource;

        [Header("Character Stage")]
        public GameObject CanvasDialogue;
        public GameObject PortraitPrefab;
        [Tooltip("Optional portrait parent outside the dialogue panel. Defaults to a separate stage under the canvas.")]
        public Transform CharacterContainer;
        public bool HideCharactersOnEnd = true;

        [Header("UI Components")]
        public GameObject DialoguePanel;
        [HideInInspector] public GameObject CharacterPortrait;
        public GameObject BackgroundChoicesPanel;
        public GameObject NameBackground;
        public TextMeshProUGUI SpeakerNameText;
        public TextMeshProUGUI DialogueText;

        [Header("Choice Button UI")]
        public Button ChoiceButtonPrefab;
        public Transform ChoiceButtonContainer;

        [Header("Story Events")]
        [Tooltip("Event nodes send their Event Name to these listeners.")]
        public UnityEvent<string> OnDialogueEvent = new UnityEvent<string>();

        public IReadOnlyDictionary<string, CharacterInfo> AllCharacters => Stage.Characters;
        public bool IsWaiting => _isWaiting;
        public RuntimeNode CurrentNode => _currentNode;
        private NovelCharacterStage _stage;
        private CharacterInfo _speaker;
        private readonly Dictionary<string, RuntimeNode> _nodeLookup = new Dictionary<string, RuntimeNode>();
        private RuntimeNode _currentNode;
        private Coroutine _textRevealCoroutine;
        private Coroutine _waitCoroutine;
        private bool _isTextRevealing, _isWaiting;
        private bool _hasStartedGraph, _ownsContainer;
        private int _nodeEnteredFrame = -1;
        private int _textCompletedFrame = -1;
        private int _flowVersion;
        private const int MaxAutomaticNodesPerTraversal = 1000;
        private const int MaxGraphCallDepth = 128;

        private sealed class RuntimeValueScope
        {
            public readonly Dictionary<string, RuntimeValue> Inputs = new Dictionary<string, RuntimeValue>();
            private readonly Dictionary<string, RuntimeValue> _outputs = new Dictionary<string, RuntimeValue>();

            private static string OutputKey(string nodeID, string name) => (nodeID ?? string.Empty) + "\n" + (name ?? string.Empty);
            public void SetOutput(string nodeID, string name, RuntimeValue value) => _outputs[OutputKey(nodeID, name)] = value ?? RuntimeValue.None();
            public RuntimeValue GetOutput(string nodeID, string name) =>
                _outputs.TryGetValue(OutputKey(nodeID, name), out RuntimeValue value) ? value : RuntimeValue.None();
        }

        private readonly struct GraphCallFrame
        {
            public readonly RuntimeNovelGraph Graph;
            public readonly string ReturnNodeID;
            public readonly RuntimeValueScope Scope;
            public readonly string FunctionCallNodeID;

            public GraphCallFrame(RuntimeNovelGraph graph, string returnNodeID, RuntimeValueScope scope, string functionCallNodeID = null)
            {
                Graph = graph;
                ReturnNodeID = returnNodeID;
                Scope = scope;
                FunctionCallNodeID = functionCallNodeID;
            }
        }

        private void Awake()
        {
            if (DialogueText != null)
            {
                DialogueText.richText = true;
                DialogueText.maxVisibleCharacters = int.MaxValue;
                if (DialogueText.GetComponent<NovelTextEffects>() == null)
                    DialogueText.gameObject.AddComponent<NovelTextEffects>();
            }
            if (NodeSoundSource == null) NodeSoundSource = CreateAudioSource();
            if (PlaySoundSource == null) PlaySoundSource = CreateAudioSource();
        }

        private AudioSource CreateAudioSource()
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        private void Start()
        {
            if (!_hasStartedGraph) PlayGraph(RuntimeGraph);
        }

        public void PlayGraph(RuntimeNovelGraph graph)
        {
            _hasStartedGraph = true;
            EndDialogue();
            _stage?.StopMovement();
            _graphCalls.Clear();
            _valueScope = new RuntimeValueScope();
            LoadGraph(graph);
            if (graph == null)
            {
                Debug.LogError("NovelManager has no RuntimeNovelGraph assigned.", this);
                return;
            }
            if (!string.IsNullOrEmpty(graph.EntryNodeID)) ShowNode(graph.EntryNodeID);
        }

        private void LoadGraph(RuntimeNovelGraph graph)
        {
            RuntimeGraph = graph;
            _nodeLookup.Clear();
            if (graph?.AllNodes == null) return;
            foreach (RuntimeNode node in graph.AllNodes)
                if (node != null && !string.IsNullOrEmpty(node.NodeID)) _nodeLookup[node.NodeID] = node;
        }

        private void Update()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Advance();
        }

        public void Advance()
        {
            if (_currentNode is not RuntimeDialogueNode || _isWaiting || _nodeEnteredFrame == Time.frameCount) return;
            if (_isTextRevealing) { CompleteTextImmediately(); return; }
            if (_textCompletedFrame == Time.frameCount) return;
            if (_currentNode is RuntimeChoiceNode choice && choice.Choices?.Count > 0) return;
            AdvanceCurrentNode();
        }

        private void AdvanceCurrentNode()
        {
            if (!string.IsNullOrEmpty(_currentNode?.NextNodeID)) ShowNode(_currentNode.NextNodeID);
            else if (TryReturnFromGraph(out string returnNodeID)) ShowNode(returnNodeID);
            else EndDialogue();
        }

        private void ShowNode(string nodeID)
        {
            CancelWait();
            StopNodePresentation();
            ClearChoiceButtons();
            int version = ++_flowVersion;
            int automaticNodes = 0;
            while (true)
            {
                if (string.IsNullOrEmpty(nodeID))
                {
                    if (TryReturnFromGraph(out nodeID)) continue;
                    break;
                }
                if (!_nodeLookup.TryGetValue(nodeID, out RuntimeNode node))
                {
                    Debug.LogWarning($"NovelManager could not find node '{nodeID}'.", this);
                    break;
                }
                _currentNode = node;
                _textCompletedFrame = -1;
                if (node is RuntimeDialogueNode dialogue)
                {
                    ShowDialogueNode(dialogue);
                    return;
                }
                if (++automaticNodes > MaxAutomaticNodesPerTraversal)
                {
                    Debug.LogError("Too many automatic nodes were chained. There may be a loop in the graph.", this);
                    break;
                }
                HideDialoguePanel();
                switch (node)
                {
                    case RuntimeTransformSpeakerPortraitNode move:
                        NovelCharacter movingCharacter = AsObject(Evaluate(move.CharacterValue), move.Character);
                        CharacterInfo moving = ShowCharacter(movingCharacter, move.InstanceID);
                        if (moving != null)
                        {
                            Vector2 offset = move.PositionValue != null
                                ? AsVector2(Evaluate(move.PositionValue), new Vector2(move.OffsetX, move.OffsetY))
                                : new Vector2(move.OffsetX, move.OffsetY);
                            float margin = Mathf.Max(0f, AsFloat(Evaluate(move.MarginValue), move.Margin));
                            float rotation = AsFloat(Evaluate(move.RotationValue), move.Rotation);
                            Vector2 scale = AsVector2(Evaluate(move.ScaleValue), move.Scale);
                            Vector2 target = move.PositionIsNormalized
                                ? moving.NormalizedToAnchoredPosition(offset, margin)
                                : offset;
                            if (move.Relative) target += moving.Position;
                            if (move.PositionIsNormalized)
                                target = moving.ClampToStageBounds(target, margin);
                            if (move.PositionIsNormalized)
                            {
                                moving.TransformTo(
                                    target,
                                    rotation,
                                    scale,
                                    move.SmoothMovement,
                                    move.Duration,
                                    move.EaseInOut);
                            }
                            else
                            {
                                moving.MoveTo(
                                    target,
                                    move.SmoothMovement,
                                    move.Duration,
                                    move.EaseInOut);
                            }
                            if (move.WaitForCompletion && moving.IsMoving)
                            {
                                _isWaiting = true;
                                _waitCoroutine = StartCoroutine(WaitThenContinue(node, version, 0f, moving));
                                return;
                            }
                        }
                        break;
                    case RuntimeFlipCharacterNode flip:
                        CharacterInfo flipping = ShowCharacter(AsObject(Evaluate(flip.CharacterValue), flip.Character), flip.InstanceID);
                        if (flipping != null)
                            flipping.gameObject.transform.localScale =
                            new Vector3(
                                flip.FlipX ? flipping.gameObject.transform.localScale.x * -1:
                                    flipping.gameObject.transform.localScale.x,
                                flip.FlipY ? flipping.gameObject.transform.localScale.y * -1 :
                                    flipping.gameObject.transform.localScale.y,
                                flipping.gameObject.transform.localScale.z
                                );

                        break;
                    case RuntimeShowCharacterNode show:
                        CharacterInfo shown = ShowCharacter(AsObject(Evaluate(show.CharacterValue), show.Character), show.InstanceID);
                        if (shown != null)
                        {
                            shown.MoveTo(AsVector2(Evaluate(show.PositionValue), show.Position), false, 0f);
                            shown.SetEmotion(show.Emotion);
                        }
                        break;
                    case RuntimeHideCharacterNode hide:
                        Stage.Hide(AsObject(Evaluate(hide.CharacterValue), hide.Character), hide.InstanceID);
                        break;
                    case RuntimeHideAllCharactersNode _: Stage.HideAll(); break;
                    case RuntimeSetCharacterEmotionNode emotion:
                        ShowCharacter(AsObject(Evaluate(emotion.CharacterValue), emotion.Character), emotion.InstanceID)?.SetEmotion(emotion.Emotion);
                        break;
                    case RuntimeWaitNode wait:
                        if (wait.Duration > 0f && !float.IsInfinity(wait.Duration))
                        {
                            _isWaiting = true;
                            _waitCoroutine = StartCoroutine(WaitThenContinue(node, version, wait.Duration));
                            return;
                        }
                        break;
                    case RuntimeDialogueEventNode signal:
                        OnDialogueEvent?.Invoke(signal.EventName ?? string.Empty);
                        if (version != _flowVersion || !isActiveAndEnabled) return;
                        break;
                    case RuntimePlaySoundNode sound: PlaySound(sound); break;
                    case RuntimeStopSoundNode _: StopAudio(PlaySoundSource); break;
                    case RuntimeCallNovelPageNode call:
                        if (call.Graph == null)
                        {
                            Debug.LogWarning("Call Novel Page has no graph assigned; continuing in the caller.", this);
                            break;
                        }
                        if (_graphCalls.Count >= MaxGraphCallDepth)
                        {
                            Debug.LogError($"Novel Graph call depth exceeded {MaxGraphCallDepth}. Check for recursive Call Novel Page nodes.", this);
                            EndDialogue();
                            return;
                        }
                        _graphCalls.Push(new GraphCallFrame(RuntimeGraph, node.NextNodeID, _valueScope));
                        LoadGraph(call.Graph);
                        nodeID = call.Graph.EntryNodeID;
                        continue;
                    case RuntimeCallNovelFunctionNode callFunction:
                        if (callFunction.Function == null)
                        {
                            Debug.LogWarning("Novel Function node has no compiled function assigned; continuing in the caller.", this);
                            break;
                        }
                        if (_graphCalls.Count >= MaxGraphCallDepth)
                        {
                            Debug.LogError($"Novel Function call depth exceeded {MaxGraphCallDepth}. Check for recursive functions.", this);
                            EndDialogue();
                            return;
                        }
                        var functionScope = new RuntimeValueScope();
                        if (callFunction.Function.Inputs != null)
                        {
                            foreach (RuntimeFunctionInput input in callFunction.Function.Inputs)
                                functionScope.Inputs[input.Name] = input.DefaultValue ?? RuntimeValue.None();
                        }
                        if (callFunction.Arguments != null)
                        {
                            foreach (RuntimeFunctionArgument argument in callFunction.Arguments)
                                functionScope.Inputs[argument.Name] = Evaluate(argument.Value);
                        }
                        _graphCalls.Push(new GraphCallFrame(RuntimeGraph, node.NextNodeID, _valueScope, node.NodeID));
                        _valueScope = functionScope;
                        LoadGraph(callFunction.Function);
                        nodeID = callFunction.Function.EntryNodeID;
                        continue;
                }
                nodeID = node.NextNodeID;
            }
            EndDialogue();
        }

        private bool TryReturnFromGraph(out string nodeID)
        {
            while (_graphCalls.Count > 0)
            {
                GraphCallFrame frame = _graphCalls.Pop();
                if (!string.IsNullOrEmpty(frame.FunctionCallNodeID) && RuntimeGraph is RuntimeNovelFunction function)
                {
                    if (function.Outputs != null)
                    {
                        foreach (RuntimeFunctionOutput output in function.Outputs)
                            frame.Scope.SetOutput(frame.FunctionCallNodeID, output.Name, Evaluate(output.Value));
                    }
                }
                _valueScope = frame.Scope;
                LoadGraph(frame.Graph);
                if (!string.IsNullOrEmpty(frame.ReturnNodeID))
                {
                    nodeID = frame.ReturnNodeID;
                    return true;
                }
            }
            nodeID = null;
            return false;
        }

        private RuntimeValue Evaluate(RuntimeValueExpression expression)
        {
            switch (expression)
            {
                case null:
                    return RuntimeValue.None();
                case RuntimeConstantExpression constant:
                    return constant.Value ?? RuntimeValue.None();
                case RuntimeFunctionInputExpression input:
                    return _valueScope.Inputs.TryGetValue(input.Name ?? string.Empty, out RuntimeValue inputValue)
                        ? inputValue
                        : RuntimeValue.None();
                case RuntimeFunctionOutputExpression output:
                    return _valueScope.GetOutput(output.CallNodeID, output.Name);
                case RuntimeArithmeticExpression arithmetic:
                    return EvaluateArithmetic(arithmetic);
                case RuntimeCharacterComponentExpression character:
                    return EvaluateCharacterComponent(character);
                default:
                    return RuntimeValue.None();
            }
        }

        private RuntimeValue EvaluateArithmetic(RuntimeArithmeticExpression expression)
        {
            RuntimeValue a = Evaluate(expression.A);
            RuntimeValue b = Evaluate(expression.B);
            if (expression.ValueKind == RuntimeValueKind.Vector2)
            {
                Vector2 left = AsVector2(a, Vector2.zero);
                Vector2 right = AsVector2(b, Vector2.zero);
                switch (expression.Operation)
                {
                    case RuntimeArithmeticOperation.Subtract: return RuntimeValue.From(left - right);
                    case RuntimeArithmeticOperation.Multiply: return RuntimeValue.From(Vector2.Scale(left, right));
                    case RuntimeArithmeticOperation.Divide:
                        return RuntimeValue.From(new Vector2(SafeDivide(left.x, right.x), SafeDivide(left.y, right.y)));
                    default: return RuntimeValue.From(left + right);
                }
            }

            float first = AsFloat(a, 0f);
            float second = AsFloat(b, 0f);
            switch (expression.Operation)
            {
                case RuntimeArithmeticOperation.Subtract: return RuntimeValue.From(first - second);
                case RuntimeArithmeticOperation.Multiply: return RuntimeValue.From(first * second);
                case RuntimeArithmeticOperation.Divide: return RuntimeValue.From(SafeDivide(first, second));
                default: return RuntimeValue.From(first + second);
            }
        }

        private RuntimeValue EvaluateCharacterComponent(RuntimeCharacterComponentExpression expression)
        {
            NovelCharacter character = AsObject<NovelCharacter>(Evaluate(expression.Character), null);
            string instanceID = AsString(Evaluate(expression.InstanceID), string.Empty);
            CharacterInfo live = null;
            if (character != null) Stage.TryGet(character, instanceID, out live);

            switch (expression.Component)
            {
                case RuntimeCharacterComponent.Character: return RuntimeValue.From(character);
                case RuntimeCharacterComponent.SpeakerName: return RuntimeValue.From(character != null ? character.SpeakerName : string.Empty);
                case RuntimeCharacterComponent.Body: return RuntimeValue.From(live?.Body != null ? live.Body.sprite : character?.PortraitBody);
                case RuntimeCharacterComponent.Eyes: return RuntimeValue.From(live?.Eyes != null ? live.Eyes.sprite : character?.PortraitEyes);
                case RuntimeCharacterComponent.EyesClosed: return RuntimeValue.From(character?.PortraitEyesClosed);
                case RuntimeCharacterComponent.Details: return RuntimeValue.From(live?.Details != null ? live.Details.sprite : character?.PortraitFaceDetails);
                case RuntimeCharacterComponent.Mouth: return RuntimeValue.From(live?.Mouth != null ? live.Mouth.sprite : character?.PortraitMouth);
                case RuntimeCharacterComponent.MouthOpen: return RuntimeValue.From(character?.PortraitMouthOpen);
                case RuntimeCharacterComponent.NormalizedPosition:
                    return RuntimeValue.From(live != null ? live.AnchoredToNormalizedPosition(live.Position) : Vector2.zero);
                case RuntimeCharacterComponent.CanvasPosition:
                    return RuntimeValue.From(live != null ? live.Position : Vector2.zero);
                case RuntimeCharacterComponent.Rotation:
                    return RuntimeValue.From(live != null ? live.Rotation : 0f);
                case RuntimeCharacterComponent.Scale:
                    return RuntimeValue.From(live != null ? live.Scale : Vector2.one);
                default:
                    return RuntimeValue.None();
            }
        }

        private static float SafeDivide(float numerator, float denominator) =>
            Mathf.Approximately(denominator, 0f) ? 0f : numerator / denominator;

        private static float AsFloat(RuntimeValue value, float fallback) =>
            value?.Kind == RuntimeValueKind.Float ? value.FloatValue :
            value?.Kind == RuntimeValueKind.Integer ? value.IntegerValue : fallback;

        private static Vector2 AsVector2(RuntimeValue value, Vector2 fallback) =>
            value?.Kind == RuntimeValueKind.Vector2 ? value.Vector2Value : fallback;

        private static string AsString(RuntimeValue value, string fallback) =>
            value?.Kind == RuntimeValueKind.String ? value.StringValue ?? string.Empty : fallback;

        private static T AsObject<T>(RuntimeValue value, T fallback) where T : UnityEngine.Object =>
            value?.Kind == RuntimeValueKind.Object && value.ObjectValue is T typed ? typed : fallback;

        private IEnumerator WaitThenContinue(RuntimeNode node, int version, float seconds, CharacterInfo moving = null)
        {
            // Yield before continuing so the coroutine handle is assigned before completion.
            do
            {
                yield return null;
                seconds -= Time.unscaledDeltaTime;
            } while (seconds > 0f || (moving != null && moving.IsMoving));
            _waitCoroutine = null;
            _isWaiting = false;
            if (version == _flowVersion && _currentNode == node) AdvanceCurrentNode();
        }

        private void ShowDialogueNode(RuntimeDialogueNode node)
        {
            _nodeEnteredFrame = Time.frameCount;
            SetPanelVisible(DialoguePanel, true);
            NovelCharacter speakingCharacter = AsObject(Evaluate(node.CharacterValue), node.NovelCharacter);
            string speakerName = speakingCharacter != null ? speakingCharacter.SpeakerName : node.SpeakerName ?? string.Empty;
            if (SpeakerNameText != null) SpeakerNameText.SetText(speakerName);
            if (NameBackground != null) NameBackground.SetActive(!string.IsNullOrEmpty(speakerName));
            if (BackgroundChoicesPanel != null) BackgroundChoicesPanel.SetActive(false);
            StopAudio(NodeSoundSource);
            AudioClip nodeClip = AsObject(Evaluate(node.PlaySoundValue), node.PlaySound);
            if (NodeSoundSource != null && nodeClip != null)
            {
                NodeSoundSource.clip = nodeClip;
                NodeSoundSource.Play();
            }
            _speaker = speakingCharacter != null ? ShowCharacter(speakingCharacter, node.InstanceID) : null;
            CharacterPortrait = _speaker != null ? _speaker.gameObject : null;
            _speaker?.BeginDialogue(node);
            if (DialogueText != null)
            {
                DialogueText.SetText(node.DialogueText ?? string.Empty);
                if (node.ShowTextImmediately || string.IsNullOrEmpty(node.DialogueText))
                    DialogueText.maxVisibleCharacters = int.MaxValue;
                else
                {
                    _isTextRevealing = true;
                    _textRevealCoroutine = StartCoroutine(RevealText(node));
                }
            }
            if (!_isTextRevealing) _speaker?.StopSpeaking();
            if (node is RuntimeChoiceNode choice && choice.Choices?.Count > 0) ShowChoices(choice);
        }

        private IEnumerator RevealText(RuntimeDialogueNode node)
        {
            NovelCharacter character = _speaker != null ? _speaker.character : node.NovelCharacter;
            yield return NovelifyUtilities.ShowTextLetterByLetter(
                node.DialogueText ?? string.Empty, DialogueText,
                character != null ? character.TalkSound : node.TalkSound, TalkSource,
                character != null ? character.PitchMinVariation : node.PitchMinVariation,
                character != null ? character.PitchMaxVariation : node.PitchMaxVariation,
                node.CharactersPerSecond,
                letter => _speaker?.RevealLetter(letter));
            if (_currentNode != node) yield break;
            _textRevealCoroutine = null;
            _isTextRevealing = false;
            _textCompletedFrame = Time.frameCount;
            _speaker?.StopSpeaking();
            StopTalkAudio();
        }

        private void ShowChoices(RuntimeChoiceNode node)
        {
            if (BackgroundChoicesPanel != null) BackgroundChoicesPanel.SetActive(true);
            if (ChoiceButtonPrefab == null || ChoiceButtonContainer == null)
            {
                Debug.LogWarning("ChoiceButtonPrefab or ChoiceButtonContainer is missing.", this);
                return;
            }
            foreach (ChoiceData choice in node.Choices)
            {
                if (choice == null) continue;
                Button button = Instantiate(ChoiceButtonPrefab, ChoiceButtonContainer);
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.SetText(AsString(Evaluate(choice.ChoiceTextValue), choice.ChoiceText ?? string.Empty));
                button.onClick.AddListener(() =>
                {
                    if (_currentNode != node || _isWaiting) return;
                    if (_isTextRevealing) { CompleteTextImmediately(); return; }
                    if (_textCompletedFrame == Time.frameCount) return;
                    if (!string.IsNullOrEmpty(choice.DestinationNodeID)) ShowNode(choice.DestinationNodeID);
                    else if (TryReturnFromGraph(out string returnNodeID)) ShowNode(returnNodeID);
                    else EndDialogue();
                });
            }
        }

        private void PlaySound(RuntimePlaySoundNode node)
        {
            StopAudio(PlaySoundSource);
            AudioClip clip = AsObject(Evaluate(node.ClipValue), node.ClipSound);
            if (PlaySoundSource == null || clip == null) return;
            PlaySoundSource.clip = clip;
            PlaySoundSource.loop = node.Loop;
            PlaySoundSource.volume = Mathf.Clamp01(node.Volume);
            PlaySoundSource.priority = Mathf.Clamp(node.Priority, 0, 256);
            PlaySoundSource.pitch = Mathf.Clamp(node.Pitch, -3f, 3f);
            PlaySoundSource.Play();
        }

        public void EndDialogue()
        {
            ++_flowVersion;
            _graphCalls.Clear();
            CancelWait();
            StopNodePresentation();
            StopAudio(PlaySoundSource);
            _currentNode = null;
            HideDialoguePanel();
            if (HideCharactersOnEnd) _stage?.HideAll();
            ClearChoiceButtons();
        }

        private void CancelWait()
        {
            if (_waitCoroutine != null) StopCoroutine(_waitCoroutine);
            _waitCoroutine = null;
            _isWaiting = false;
        }

        private void HideDialoguePanel()
        {
            SetPanelVisible(DialoguePanel, false);
            if (BackgroundChoicesPanel != null) BackgroundChoicesPanel.SetActive(false);
        }

        private static void SetPanelVisible(GameObject panel, bool visible)
        {
            if (panel == null) return;
            // The sample manager is a child of this panel. Deactivating it stops the
            // story's coroutines and audio through OnDisable, even between nodes.
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.AddComponent<CanvasGroup>();
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (visible && !panel.activeSelf) panel.SetActive(true);
        }

        private void CompleteTextImmediately()
        {
            if (!_isTextRevealing || DialogueText == null) return;
            if (_textRevealCoroutine != null) StopCoroutine(_textRevealCoroutine);
            _textRevealCoroutine = null;
            DialogueText.maxVisibleCharacters = int.MaxValue;
            _isTextRevealing = false;
            _textCompletedFrame = Time.frameCount;
            _speaker?.StopSpeaking();
            StopTalkAudio();
        }

        private void StopNodePresentation()
        {
            if (_textRevealCoroutine != null) StopCoroutine(_textRevealCoroutine);
            _textRevealCoroutine = null;
            _isTextRevealing = false;
            _speaker?.StopSpeaking();
            _speaker = null;
            StopTalkAudio();
            StopAudio(NodeSoundSource);
        }

        private void StopTalkAudio()
        {
            if (TalkSource == null) return;
            TalkSource.Stop();
            TalkSource.pitch = 1f;
        }

        private static void StopAudio(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
            source.loop = false;
        }

        private void ClearChoiceButtons()
        {
            if (ChoiceButtonContainer == null) return;
            foreach (Transform child in ChoiceButtonContainer)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private void OnDisable()
        {
            EndDialogue();
            _stage?.StopMovement();
        }

    }
}
