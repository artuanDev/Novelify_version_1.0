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

        private readonly struct GraphCallFrame
        {
            public readonly RuntimeNovelGraph Graph;
            public readonly string ReturnNodeID;

            public GraphCallFrame(RuntimeNovelGraph graph, string returnNodeID)
            {
                Graph = graph;
                ReturnNodeID = returnNodeID;
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
                        CharacterInfo moving = ShowCharacter(move.Character, move.InstanceID);
                        if (moving != null)
                        {
                            Vector2 offset = new Vector2(move.OffsetX, move.OffsetY);
                            Vector2 target = move.PositionIsNormalized
                                ? moving.NormalizedToAnchoredPosition(offset, move.Margin)
                                : offset;
                            if (move.Relative) target += moving.Position;
                            if (move.PositionIsNormalized)
                                target = moving.ClampToStageBounds(target, move.Margin);
                            if (move.PositionIsNormalized)
                            {
                                moving.TransformTo(
                                    target,
                                    move.Rotation,
                                    move.Scale,
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
                        CharacterInfo flipping = ShowCharacter(flip.Character, flip.InstanceID);
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
                        CharacterInfo shown = ShowCharacter(show.Character, show.InstanceID);
                        if (shown != null)
                        {
                            shown.MoveTo(show.Position, false, 0f);
                            shown.SetEmotion(show.Emotion);
                        }
                        break;
                    case RuntimeHideCharacterNode hide: Stage.Hide(hide.Character, hide.InstanceID); break;
                    case RuntimeHideAllCharactersNode _: Stage.HideAll(); break;
                    case RuntimeSetCharacterEmotionNode emotion:
                        ShowCharacter(emotion.Character, emotion.InstanceID)?.SetEmotion(emotion.Emotion);
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
                        _graphCalls.Push(new GraphCallFrame(RuntimeGraph, node.NextNodeID));
                        LoadGraph(call.Graph);
                        nodeID = call.Graph.EntryNodeID;
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
            if (SpeakerNameText != null) SpeakerNameText.SetText(node.SpeakerName ?? string.Empty);
            if (NameBackground != null) NameBackground.SetActive(!string.IsNullOrEmpty(node.SpeakerName));
            if (BackgroundChoicesPanel != null) BackgroundChoicesPanel.SetActive(false);
            StopAudio(NodeSoundSource);
            if (NodeSoundSource != null && node.PlaySound != null)
            {
                NodeSoundSource.clip = node.PlaySound;
                NodeSoundSource.Play();
            }
            _speaker = node.NovelCharacter != null ? ShowCharacter(node.NovelCharacter, node.InstanceID) : null;
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
            yield return NovelifyUtilities.ShowTextLetterByLetter(
                node.DialogueText ?? string.Empty, DialogueText, node.TalkSound, TalkSource,
                node.PitchMinVariation, node.PitchMaxVariation, node.CharactersPerSecond,
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
                if (label != null) label.SetText(choice.ChoiceText ?? string.Empty);
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
            if (PlaySoundSource == null || node.ClipSound == null) return;
            PlaySoundSource.clip = node.ClipSound;
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
