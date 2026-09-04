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

        [Header("Sound settings")]
        public AudioSource TalkSource;
        [Tooltip("Plays the optional sound attached to a node. A dedicated source is created automatically when this is empty.")]
        [FormerlySerializedAs("PlaySound")]
        public AudioSource NodeSoundSource;

        [Header("UI Components")]
        public GameObject DialoguePanel;
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

        private Dictionary<string, RuntimeDialogueNode> _nodeLookup = new Dictionary<string, RuntimeDialogueNode>();
        private RuntimeDialogueNode _currentNode;
        private Coroutine _textRevealCoroutine;
        private Coroutine _mouthAnimationCoroutine;
        private Coroutine _blinkAnimationCoroutine;
        private bool _isTextRevealing;
        private int _textCompletedFrame = -1;
        private char _lastRevealedCharacter;
        private bool _hasRevealedCharacter;

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
                NodeSoundSource = gameObject.AddComponent<AudioSource>();
                NodeSoundSource.playOnAwake = false;
            }
        }

        private void Start()
        {
            foreach (var node in RuntimeGraph.AllNodes)
            {
                _nodeLookup[node.NodeID] = node;
            }  

            if(!string.IsNullOrEmpty(RuntimeGraph.EntryNodeID))
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

            // Choice nodes only advance through their buttons. A click anywhere still
            // completes their unfinished text, but it must not select a choice as well.
            if (_currentNode.Choices.Count > 0)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_currentNode.NextNodeID))
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
            if(!_nodeLookup.ContainsKey(nodeID))
            {
                EndDialogue();
                return;
            }

            StopNodePresentation();

            _currentNode = _nodeLookup[nodeID];
            DialoguePanel.SetActive(true);
            SpeakerNameText.SetText(_currentNode.SpeakerName);

            PlayNodeSound(_currentNode.PlaySound);

            NameBackground.SetActive(_currentNode.SpeakerName != "");

            UpdateSpeakerPortrait(
                _currentNode.PortraitBody,
                _currentNode.PortraitEyes,
                _currentNode.PortraitDetails,
                _currentNode.PortraitMouth
                );

            StartNodePresentation(_currentNode);

            BackgroundChoicesPanel.SetActive(false);

            foreach (Transform child in ChoiceButtonContainer)
            {
                Destroy(child.gameObject);
            }

            if(_currentNode.Choices.Count > 0)
            {
                BackgroundChoicesPanel.SetActive(true);
                foreach(var choice in _currentNode.Choices)
                {
                    Button button = Instantiate(ChoiceButtonPrefab, ChoiceButtonContainer);

                    TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
                    if(buttonText != null)
                    {
                        buttonText.text = choice.ChoiceText;
                    }

                    if(button != null)
                    {
                        button.onClick.AddListener(() =>
                        {
                            if (_isTextRevealing || _textCompletedFrame == Time.frameCount)
                            {
                                CompleteTextImmediately();
                                return;
                            }

                            if (!string.IsNullOrEmpty(choice.DestinationNodeID))
                            {
                                ShowNode(choice.DestinationNodeID);
                            }
                            else
                            {
                                EndDialogue();
                            }
                        });
                    }
                }
            }
        }

        private void EndDialogue()
        {
            StopNodePresentation();
            DialoguePanel.SetActive(false);
            _currentNode = null;

            UpdateSpeakerPortrait(null, null, null, null);

            foreach (Transform child in ChoiceButtonContainer)
            {
                Destroy(child.gameObject);
            }
        }

        private void StartNodePresentation(RuntimeDialogueNode node)
        {
            string dialogue = node.DialogueText ?? string.Empty;
            _hasRevealedCharacter = false;

            if (node.ShowTextImmediately || dialogue.Length == 0)
            {
                DialogueText.maxVisibleCharacters = int.MaxValue;
                DialogueText.SetText(dialogue);
                _isTextRevealing = false;
            }
            else
            {
                DialogueText.SetText(string.Empty);
                _isTextRevealing = true;
                _textRevealCoroutine = StartCoroutine(RevealText(node));

                if (node.AnimateMouth &&
                    Speaker_Mouth != null &&
                    node.PortraitMouth != null &&
                    node.PortraitMouthOpen != null)
                {
                    _mouthAnimationCoroutine = StartCoroutine(AnimateMouth(node));
                }
            }

            if (node.AnimateBlinking &&
                Speaker_Eyes != null &&
                node.PortraitEyes != null &&
                node.PortraitEyesClosed != null)
            {
                _blinkAnimationCoroutine = StartCoroutine(AnimateBlinking(node));
            }
        }

        private IEnumerator RevealText(RuntimeDialogueNode node)
        {
            yield return NovelifyUtilities.ShowTextLetterByLetter(
                node.DialogueText,
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

        private IEnumerator AnimateMouth(RuntimeDialogueNode node)
        {
            bool showOpenMouth = false;
            float baseFrameInterval = Mathf.Max(0.02f, node.MouthFrameInterval);
            float timingVariation = Mathf.Clamp(node.MouthTimingVariation, 0f, 0.75f);
            float pauseChance = Mathf.Clamp01(node.MouthPauseChance);
            float pauseMultiplier = Mathf.Max(1f, node.MouthPauseMultiplier);

            while (_currentNode == node && _isTextRevealing)
            {
                bool isSpeechPause = _hasRevealedCharacter &&
                    (char.IsWhiteSpace(_lastRevealedCharacter) ||
                     char.IsPunctuation(_lastRevealedCharacter));

                if (!_hasRevealedCharacter || isSpeechPause)
                {
                    showOpenMouth = false;
                }
                else
                {
                    // Choosing a state instead of strictly alternating lets the mouth
                    // naturally hold a pose for an extra beat from time to time.
                    showOpenMouth = Random.value < 0.62f;
                }

                Speaker_Mouth.sprite = showOpenMouth
                    ? node.PortraitMouthOpen
                    : node.PortraitMouth;

                float frameInterval = baseFrameInterval * Random.Range(
                    1f - timingVariation,
                    1f + timingVariation);

                if (isSpeechPause || (!showOpenMouth && Random.value < pauseChance))
                {
                    frameInterval *= pauseMultiplier * Random.Range(0.85f, 1.15f);
                }

                yield return new WaitForSeconds(Mathf.Max(0.02f, frameInterval));
            }

            if (_currentNode == node)
            {
                Speaker_Mouth.sprite = node.PortraitMouth;
            }
        }

        private IEnumerator AnimateBlinking(RuntimeDialogueNode node)
        {
            float minimumInterval = Mathf.Max(0.1f, Mathf.Min(
                node.BlinkIntervalMin, node.BlinkIntervalMax));
            float maximumInterval = Mathf.Max(minimumInterval, Mathf.Max(
                node.BlinkIntervalMin, node.BlinkIntervalMax));
            float blinkDuration = Mathf.Max(0.02f, node.BlinkDuration);

            while (_currentNode == node)
            {
                yield return new WaitForSeconds(Random.Range(minimumInterval, maximumInterval));

                if (_currentNode != node)
                {
                    yield break;
                }

                Speaker_Eyes.sprite = node.PortraitEyesClosed;
                yield return new WaitForSeconds(blinkDuration);

                if (_currentNode == node)
                {
                    Speaker_Eyes.sprite = node.PortraitEyes;
                }
            }
        }

        private void CompleteTextImmediately()
        {
            if (!_isTextRevealing || _currentNode == null)
            {
                return;
            }

            if (_textRevealCoroutine != null)
            {
                StopCoroutine(_textRevealCoroutine);
                _textRevealCoroutine = null;
            }

            DialogueText.maxVisibleCharacters = int.MaxValue;
            DialogueText.SetText(_currentNode.DialogueText ?? string.Empty);
            _isTextRevealing = false;
            _textCompletedFrame = Time.frameCount;
            StopMouthAnimation();
            StopTalkAudio();
        }

        private void StopNodePresentation()
        {
            if (_textRevealCoroutine != null)
            {
                StopCoroutine(_textRevealCoroutine);
                _textRevealCoroutine = null;
            }

            StopMouthAnimation();

            if (_blinkAnimationCoroutine != null)
            {
                StopCoroutine(_blinkAnimationCoroutine);
                _blinkAnimationCoroutine = null;
            }

            _isTextRevealing = false;
            StopTalkAudio();
            StopNodeSound();
        }

        private void PlayNodeSound(AudioClip clip)
        {
            if (clip == null || NodeSoundSource == null)
            {
                return;
            }

            NodeSoundSource.Stop();
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
        }

        private void StopMouthAnimation()
        {
            if (_mouthAnimationCoroutine != null)
            {
                StopCoroutine(_mouthAnimationCoroutine);
                _mouthAnimationCoroutine = null;
            }

            if (_currentNode != null && Speaker_Mouth != null)
            {
                Speaker_Mouth.sprite = _currentNode.PortraitMouth;
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

        private void UpdateSpeakerPortrait(
            Sprite portrait_body, Sprite portrait_eyes, Sprite portrait_details, Sprite portrait_mouth)
        {
            SetPortraitLayer(Speaker_Body, portrait_body);
            SetPortraitLayer(Speaker_Eyes, portrait_eyes);
            SetPortraitLayer(Speaker_Details, portrait_details);
            SetPortraitLayer(Speaker_Mouth, portrait_mouth);
        }

        private static void SetPortraitLayer(Image image, Sprite sprite)
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
