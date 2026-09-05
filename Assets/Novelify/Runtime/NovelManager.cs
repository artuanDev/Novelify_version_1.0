using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Novelify
{
    public class NovelManager : MonoBehaviour
    {
        public RuntimeNovelGraph RuntimeGraph;

        [Header("Sound Settings")]
        public AudioSource TalkSource;

        [Tooltip("Sound attached directly to dialogue nodes.")]
        [FormerlySerializedAs("PlaySound")]
        public AudioSource NodeSoundSource;

        [Tooltip("Sound source used by RuntimePlaySoundNode.")]
        public AudioSource PlaySoundSource;

        [Header("UI Components")]
        public GameObject DialoguePanel;
        public GameObject CharacterPortrait;
        public GameObject BackgroundChoicesPanel;
        public GameObject NameBackground;

        public TextMeshProUGUI SpeakerNameText;
        public Image Speaker_Body;
        public Image Speaker_Eyes;
        public Image Speaker_Details;
        public Image Speaker_Mouth;
        public TextMeshProUGUI DialogueText;

        [Header("Choice Button UI")]
        public Button ChoiceButtonPrefab;
        public Transform ChoiceButtonContainer;

        private readonly Dictionary<string, RuntimeNode> _nodeLookup =
            new Dictionary<string, RuntimeNode>();

        private RuntimeNode _currentNode;

        private Coroutine _textRevealCoroutine;
        private Coroutine _mouthAnimationCoroutine;
        private Coroutine _blinkAnimationCoroutine;

        private bool _isTextRevealing;
        private int _textCompletedFrame = -1;
        private char _lastRevealedCharacter;
        private bool _hasRevealedCharacter;

        private const int MaxAutomaticNodesPerTraversal = 1000;

        private void Awake()
        {
            if (DialogueText != null)
            {
                DialogueText.richText = true;
                DialogueText.maxVisibleCharacters = int.MaxValue;

                if (DialogueText.GetComponent<NovelTextEffects>() == null)
                {
                    DialogueText.gameObject.AddComponent<NovelTextEffects>();
                }
            }

            if (NodeSoundSource == null)
            {
                NodeSoundSource =
                    gameObject.AddComponent<AudioSource>();

                NodeSoundSource.playOnAwake = false;
            }

            if (PlaySoundSource == null)
            {
                PlaySoundSource =
                    gameObject.AddComponent<AudioSource>();

                PlaySoundSource.playOnAwake = false;
            }
        }

        private void Start()
        {
            if (RuntimeGraph == null)
            {
                Debug.LogError(
                    "NovelManager has no RuntimeNovelGraph assigned.",
                    this);

                EndDialogue();
                return;
            }

            _nodeLookup.Clear();

            foreach (RuntimeNode node in RuntimeGraph.AllNodes)
            {
                if (node == null ||
                    string.IsNullOrEmpty(node.NodeID))
                {
                    continue;
                }

                _nodeLookup[node.NodeID] = node;
            }

            if (!string.IsNullOrEmpty(
                    RuntimeGraph.EntryNodeID))
            {
                ShowNode(RuntimeGraph.EntryNodeID);
            }
            else
            {
                EndDialogue();
            }
        }

        private void Update()
        {
            if (Mouse.current == null ||
                !Mouse.current.leftButton.wasPressedThisFrame ||
                _currentNode == null)
            {
                return;
            }

            if (_isTextRevealing)
            {
                CompleteTextImmediately();
                return;
            }

            if (_currentNode is RuntimeChoiceNode choiceNode &&
                choiceNode.Choices != null &&
                choiceNode.Choices.Count > 0)
            {
                return;
            }

            AdvanceCurrentNode();
        }

        private void AdvanceCurrentNode()
        {
            if (_currentNode == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(
                    _currentNode.NextNodeID))
            {
                ShowNode(_currentNode.NextNodeID);
            }
            else
            {
                EndDialogue();
            }
        }

        private void ShowNode(string nodeID)
        {
            if (!_nodeLookup.TryGetValue(
                    nodeID,
                    out RuntimeNode node) ||
                node == null)
            {
                Debug.LogWarning(
                    $"NovelManager could not find node '{nodeID}'.",
                    this);

                EndDialogue();
                return;
            }

            StopNodePresentation();

            int automaticNodesProcessed = 0;

            while (node != null)
            {
                _currentNode = node;
                _textCompletedFrame = -1;
                ClearChoiceButtons();

                if (node is RuntimeDialogueNode dialogueNode)
                {
                    ShowDialogueNode(dialogueNode);
                    return;
                }

                if (node is RuntimePlaySoundNode soundNode)
                {
                    ShowPlaySoundNode(soundNode);
                }
                else if(node is RuntimeTranslateSpeakerPortraitNode translateSpeakerPortraitNode)
                {
                    ShowTranslatePortraitNode(translateSpeakerPortraitNode);
                }
                else
                {
                    // All other non-dialogue nodes are instant.
                    HideDialoguePanel();
                }

                automaticNodesProcessed++;

                if (automaticNodesProcessed >
                    MaxAutomaticNodesPerTraversal)
                {
                    Debug.LogError(
                        "Too many automatic nodes were chained. " +
                        "There may be a loop in the graph.",
                        this);

                    EndDialogue();
                    return;
                }

                if (string.IsNullOrEmpty(
                        node.NextNodeID))
                {
                    EndDialogue();
                    return;
                }

                if (!_nodeLookup.TryGetValue(
                        node.NextNodeID,
                        out node))
                {
                    Debug.LogWarning(
                        $"NovelManager could not find node " +
                        $"'{node.NextNodeID}'.",
                        this);

                    EndDialogue();
                    return;
                }
            }

            EndDialogue();
        }

        private void ShowDialogueNode(
            RuntimeDialogueNode node)
        {
            if (DialoguePanel != null)
            {
                DialoguePanel.SetActive(true);
            }

            if (SpeakerNameText != null)
            {
                SpeakerNameText.SetText(
                    node.SpeakerName ?? string.Empty);
            }

            if (NameBackground != null)
            {
                NameBackground.SetActive(
                    !string.IsNullOrEmpty(
                        node.SpeakerName));
            }

            PlayPlaySoundNode(node.PlaySound);

            UpdateSpeakerPortrait(
                node.PortraitBody,
                node.PortraitEyes,
                node.PortraitDetails,
                node.PortraitMouth);

            StartNodePresentation(node);

            if (BackgroundChoicesPanel != null)
            {
                BackgroundChoicesPanel.SetActive(false);
            }

            if (node is RuntimeChoiceNode choiceNode &&
                choiceNode.Choices != null &&
                choiceNode.Choices.Count > 0)
            {
                ShowChoices(choiceNode);
            }
        }

        private void ShowPlaySoundNode(
            RuntimePlaySoundNode node)
        {
            HideDialoguePanel();

            if (PlaySoundSource == null ||
                node.ClipSound == null)
            {
                return;
            }

            PlaySoundSource.Stop();
            PlaySoundSource.clip = node.ClipSound;
            PlaySoundSource.loop = node.Loop;
            PlaySoundSource.volume = node.Volume;
            PlaySoundSource.priority = node.Priority;
            PlaySoundSource.pitch = node.Pitch;
            PlaySoundSource.Play();
        }

        private void ShowTranslatePortraitNode(
            RuntimeTranslateSpeakerPortraitNode node)
        {
            if( !CharacterPortrait.activeSelf ) return;
            CharacterPortrait.transform.position = new Vector2(node.OffsetX, node.OffsetY);
        }

        private void ShowChoices(RuntimeChoiceNode node)
        {
            if (BackgroundChoicesPanel != null)
            {
                BackgroundChoicesPanel.SetActive(true);
            }

            if (ChoiceButtonPrefab == null ||
                ChoiceButtonContainer == null)
            {
                Debug.LogWarning(
                    "ChoiceButtonPrefab or " +
                    "ChoiceButtonContainer is missing.",
                    this);

                return;
            }

            foreach (ChoiceData choice in node.Choices)
            {
                ChoiceData selectedChoice = choice;

                Button button = Instantiate(
                    ChoiceButtonPrefab,
                    ChoiceButtonContainer);

                TextMeshProUGUI buttonText =
                    button.GetComponentInChildren<
                        TextMeshProUGUI>();

                if (buttonText != null)
                {
                    buttonText.text =
                        selectedChoice.ChoiceText ??
                        string.Empty;
                }

                button.onClick.AddListener(() =>
                {
                    if (_isTextRevealing)
                    {
                        CompleteTextImmediately();
                        return;
                    }

                    if (_textCompletedFrame ==
                        Time.frameCount)
                    {
                        return;
                    }

                    if (!string.IsNullOrEmpty(
                            selectedChoice.DestinationNodeID))
                    {
                        ShowNode(
                            selectedChoice.DestinationNodeID);
                    }
                    else
                    {
                        EndDialogue();
                    }
                });
            }
        }

        private void EndDialogue()
        {
            StopNodePresentation();

            if (PlaySoundSource != null)
            {
                PlaySoundSource.Stop();
                PlaySoundSource.clip = null;
                PlaySoundSource.loop = false;
            }

            _currentNode = null;

            HideDialoguePanel();
            UpdateSpeakerPortrait(null, null, null, null);
            ClearChoiceButtons();
        }

        private void HideDialoguePanel()
        {
            if (DialoguePanel != null)
            {
                DialoguePanel.SetActive(false);
            }

            if (BackgroundChoicesPanel != null)
            {
                BackgroundChoicesPanel.SetActive(false);
            }
        }

        private void StartNodePresentation(
            RuntimeDialogueNode node)
        {
            _hasRevealedCharacter = false;

            if (DialogueText == null)
            {
                _isTextRevealing = false;
                return;
            }

            string dialogue =
                node.DialogueText ?? string.Empty;

            if (node.ShowTextImmediately ||
                dialogue.Length == 0)
            {
                DialogueText.maxVisibleCharacters =
                    int.MaxValue;

                DialogueText.SetText(dialogue);
                _isTextRevealing = false;
            }
            else
            {
                DialogueText.SetText(string.Empty);
                _isTextRevealing = true;

                _textRevealCoroutine =
                    StartCoroutine(RevealText(node));

                if (node.AnimateMouth &&
                    Speaker_Mouth != null &&
                    node.PortraitMouth != null &&
                    node.PortraitMouthOpen != null)
                {
                    _mouthAnimationCoroutine =
                        StartCoroutine(AnimateMouth(node));
                }
            }

            if (node.AnimateBlinking &&
                Speaker_Eyes != null &&
                node.PortraitEyes != null &&
                node.PortraitEyesClosed != null)
            {
                _blinkAnimationCoroutine =
                    StartCoroutine(AnimateBlinking(node));
            }
        }

        private IEnumerator RevealText(
            RuntimeDialogueNode node)
        {
            yield return NovelifyUtilities
                .ShowTextLetterByLetter(
                    node.DialogueText ?? string.Empty,
                    DialogueText,
                    node.TalkSound,
                    TalkSource,
                    node.PitchMinVariation,
                    node.PitchMaxVariation,
                    node.CharactersPerSecond,
                    letter =>
                    {
                        if (_currentNode == node)
                        {
                            _lastRevealedCharacter = letter;
                            _hasRevealedCharacter = true;
                        }
                    });

            if (_currentNode != node)
            {
                yield break;
            }

            _textRevealCoroutine = null;
            _isTextRevealing = false;

            StopMouthAnimation();
            StopTalkAudio();
        }

        private IEnumerator AnimateMouth(
            RuntimeDialogueNode node)
        {
            bool showOpenMouth = false;

            float baseFrameInterval =
                Mathf.Max(
                    0.02f,
                    node.MouthFrameInterval);

            float timingVariation =
                Mathf.Clamp(
                    node.MouthTimingVariation,
                    0f,
                    0.75f);

            float pauseChance =
                Mathf.Clamp01(
                    node.MouthPauseChance);

            float pauseMultiplier =
                Mathf.Max(
                    1f,
                    node.MouthPauseMultiplier);

            while (_currentNode == node &&
                   _isTextRevealing)
            {
                bool isSpeechPause =
                    _hasRevealedCharacter &&
                    (char.IsWhiteSpace(
                        _lastRevealedCharacter) ||
                     char.IsPunctuation(
                        _lastRevealedCharacter));

                if (!_hasRevealedCharacter ||
                    isSpeechPause)
                {
                    showOpenMouth = false;
                }
                else
                {
                    showOpenMouth =
                        Random.value < 0.62f;
                }

                Speaker_Mouth.sprite =
                    showOpenMouth
                        ? node.PortraitMouthOpen
                        : node.PortraitMouth;

                float frameInterval =
                    baseFrameInterval *
                    Random.Range(
                        1f - timingVariation,
                        1f + timingVariation);

                if (isSpeechPause ||
                    (!showOpenMouth &&
                     Random.value < pauseChance))
                {
                    frameInterval *=
                        pauseMultiplier *
                        Random.Range(0.85f, 1.15f);
                }

                yield return new WaitForSeconds(
                    Mathf.Max(
                        0.02f,
                        frameInterval));
            }

            if (_currentNode == node &&
                Speaker_Mouth != null)
            {
                Speaker_Mouth.sprite =
                    node.PortraitMouth;
            }
        }

        private IEnumerator AnimateBlinking(
            RuntimeDialogueNode node)
        {
            float minimumInterval =
                Mathf.Max(
                    0.1f,
                    Mathf.Min(
                        node.BlinkIntervalMin,
                        node.BlinkIntervalMax));

            float maximumInterval =
                Mathf.Max(
                    minimumInterval,
                    Mathf.Max(
                        node.BlinkIntervalMin,
                        node.BlinkIntervalMax));

            float blinkDuration =
                Mathf.Max(
                    0.02f,
                    node.BlinkDuration);

            while (_currentNode == node)
            {
                yield return new WaitForSeconds(
                    Random.Range(
                        minimumInterval,
                        maximumInterval));

                if (_currentNode != node)
                {
                    yield break;
                }

                Speaker_Eyes.sprite =
                    node.PortraitEyesClosed;

                yield return new WaitForSeconds(
                    blinkDuration);

                if (_currentNode == node)
                {
                    Speaker_Eyes.sprite =
                        node.PortraitEyes;
                }
            }
        }

        private void CompleteTextImmediately()
        {
            RuntimeDialogueNode node =
                _currentNode as RuntimeDialogueNode;

            if (!_isTextRevealing ||
                node == null ||
                DialogueText == null)
            {
                return;
            }

            if (_textRevealCoroutine != null)
            {
                StopCoroutine(
                    _textRevealCoroutine);

                _textRevealCoroutine = null;
            }

            DialogueText.maxVisibleCharacters =
                int.MaxValue;

            DialogueText.SetText(
                node.DialogueText ?? string.Empty);

            _isTextRevealing = false;
            _textCompletedFrame = Time.frameCount;

            StopMouthAnimation();
            StopTalkAudio();
        }

        private void StopNodePresentation()
        {
            if (_textRevealCoroutine != null)
            {
                StopCoroutine(
                    _textRevealCoroutine);

                _textRevealCoroutine = null;
            }

            StopMouthAnimation();

            if (_blinkAnimationCoroutine != null)
            {
                StopCoroutine(
                    _blinkAnimationCoroutine);

                _blinkAnimationCoroutine = null;
            }

            _isTextRevealing = false;

            StopTalkAudio();
            StopNodeSound();
        }

        private void PlayPlaySoundNode(AudioClip clip)
        {
            if (NodeSoundSource == null)
            {
                return;
            }

            NodeSoundSource.Stop();
            NodeSoundSource.clip = null;
            NodeSoundSource.loop = false;

            if (clip == null)
            {
                return;
            }

            NodeSoundSource.clip = clip;
            NodeSoundSource.Play();
        }

        private void StopNodeSound()
        {
            if (NodeSoundSource == null)
            {
                return;
            }

            NodeSoundSource.Stop();
            NodeSoundSource.clip = null;
            NodeSoundSource.loop = false;
        }

        private void StopMouthAnimation()
        {
            if (_mouthAnimationCoroutine != null)
            {
                StopCoroutine(
                    _mouthAnimationCoroutine);

                _mouthAnimationCoroutine = null;
            }

            RuntimeDialogueNode node =
                _currentNode as RuntimeDialogueNode;

            if (node != null &&
                Speaker_Mouth != null)
            {
                Speaker_Mouth.sprite =
                    node.PortraitMouth;
            }
        }

        private void StopTalkAudio()
        {
            if (TalkSource == null)
            {
                return;
            }

            TalkSource.Stop();
            TalkSource.pitch = 1f;
        }

        private void ClearChoiceButtons()
        {
            if (ChoiceButtonContainer == null)
            {
                return;
            }

            foreach (Transform child in ChoiceButtonContainer)
            {
                Destroy(child.gameObject);
            }
        }

        private void UpdateSpeakerPortrait(
            Sprite portraitBody,
            Sprite portraitEyes,
            Sprite portraitDetails,
            Sprite portraitMouth)
        {
            SetPortraitLayer(
                Speaker_Body,
                portraitBody);

            SetPortraitLayer(
                Speaker_Eyes,
                portraitEyes);

            SetPortraitLayer(
                Speaker_Details,
                portraitDetails);

            SetPortraitLayer(
                Speaker_Mouth,
                portraitMouth);
        }

        private static void SetPortraitLayer(
            Image image,
            Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.enabled = sprite != null;
        }
    }
}