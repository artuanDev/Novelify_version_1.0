using System.Collections;
using UnityEngine;

namespace Novelify
{
    public partial class NovelGraphRunner
    {
        private void InitializePresentation()
        {
            if (_customPresentation != null) return;
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
        private float DialogueDeltaTime =>
            TimeMode == DialogueTimeMode.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;

        private IEnumerator WaitThenContinue(RuntimeNode node, int version, float seconds, CharacterInfo moving = null)
        {
            // Yield before continuing so the coroutine handle is assigned before completion.
            do
            {
                yield return null;
                seconds -= DialogueDeltaTime;
            } while (seconds > 0f || (moving != null && moving.IsMoving));
            _waitCoroutine = null;
            _isWaiting = false;
            if (version == _flowVersion && _currentNode == node) AdvanceCurrentNode();
        }

        private void ShowDialogueNode(RuntimeDialogueNode node)
        {
            _choiceSelectionCommitted = false;
            _nodeEnteredFrame = Time.frameCount;
            NovelCharacterReference speakerReference = ResolveCharacterReference(
                node.CharacterReferenceValue, node.CharacterValue, node.NovelCharacter, node.InstanceID);
            NovelCharacter speakingCharacter = speakerReference.Character;
            string speakerName = speakingCharacter != null ? speakingCharacter.SpeakerName : node.SpeakerName ?? string.Empty;
            if (_customPresentation != null)
            {
                _speaker = null;
                CharacterPortrait = null;
                _isTextRevealing = false;
                var presentation = new NovelDialoguePresentation(
                    Session,
                    RuntimeGraph,
                    node,
                    speakerReference,
                    speakerName,
                    node.DialogueText ?? string.Empty,
                    () => OnCustomRevealCompleted(node));
                _customPresentation.PresentDialogue(presentation);
                _isTextRevealing = _customPresentation.IsRevealing;
                if (node is RuntimeChoiceNode customChoice) ShowChoices(customChoice);
                if (_currentNode == node)
                {
                    OnDialogueBoundaryPresented(node, speakerName, node.DialogueText ?? string.Empty);
                    Session.RaiseDialoguePresented(RuntimeGraph, node, speakerName);
                }
                return;
            }

            SetPanelVisible(DialoguePanel, true);
            if (SpeakerNameText != null) SpeakerNameText.SetText(speakerName);
            if (NameBackground != null) NameBackground.SetActive(!string.IsNullOrEmpty(speakerName));
            if (BackgroundChoicesPanel != null) BackgroundChoicesPanel.SetActive(false);
            StopAudio(NodeSoundSource);
            AudioClip nodeClip = AsObject(Evaluate(node.PlaySoundValue), node.PlaySound);
            if (NodeSoundSource != null && nodeClip != null &&
                (node.PlaySoundCharacterIndex < 0 || node.ShowTextImmediately))
            {
                NodeSoundSource.clip = nodeClip;
                NodeSoundSource.Play();
            }
            _speaker = speakingCharacter != null ? ShowCharacter(speakingCharacter, speakerReference.InstanceID) : null;
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
            if (node is RuntimeChoiceNode choice) ShowChoices(choice);
            if (_currentNode == node)
            {
                OnDialogueBoundaryPresented(node, speakerName, node.DialogueText ?? string.Empty);
                Session.RaiseDialoguePresented(RuntimeGraph, node, speakerName);
            }
        }

        private void OnCustomRevealCompleted(RuntimeDialogueNode node)
        {
            if (_currentNode != node || !_isTextRevealing) return;
            _isTextRevealing = false;
            _textCompletedFrame = Time.frameCount;
            OnSupportedSaveBoundary();
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
                letter => _speaker?.RevealLetter(letter),
                TimeMode,
                (characterIndex, _) =>
                {
                    if (characterIndex == node.PlaySoundCharacterIndex)
                        PlayDialogueCue(node);
                });
            if (_currentNode != node) yield break;
            _textRevealCoroutine = null;
            _isTextRevealing = false;
            _textCompletedFrame = Time.frameCount;
            _speaker?.StopSpeaking();
            StopTalkAudio();
            OnSupportedSaveBoundary();
        }

        private void PlayDialogueCue(RuntimeDialogueNode node)
        {
            AudioClip clip = AsObject(Evaluate(node.PlaySoundValue), node.PlaySound);
            if (NodeSoundSource == null || clip == null) return;
            NodeSoundSource.clip = clip;
            NodeSoundSource.loop = false;
            NodeSoundSource.Play();
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

        internal void StopGraphInternal()
        {
            RuntimeNovelGraph stoppedGraph = RuntimeGraph;
            bool wasRunning = _isGraphRunning;
            _isGraphRunning = false;
            ++_flowVersion;
            _graphCalls.Clear();
            CancelWait();
            StopNodePresentation();
            StopAudio(PlaySoundSource);
            _currentNode = null;
            HideDialoguePanel();
            if (HideCharactersOnEnd) _stage?.HideAll();
            ClearChoiceButtons();
            _customPresentation?.Stop();
            if (wasRunning) Session.RaiseStopped(stoppedGraph);
        }

        private void CancelWait()
        {
            if (_waitCoroutine != null) StopCoroutine(_waitCoroutine);
            _waitCoroutine = null;
            _externallyPaused = false;
            _externalResumeNodeID = null;
            _isWaiting = false;
        }

        private void HideDialoguePanel()
        {
            if (_customPresentation != null)
            {
                _customPresentation.HideDialogue();
                return;
            }
            SetPanelVisible(DialoguePanel, false);
            if (BackgroundChoicesPanel != null) BackgroundChoicesPanel.SetActive(false);
        }

        private static void SetPanelVisible(GameObject panel, bool visible)
        {
            if (panel == null) return;
            // The runner may be a child of this panel. Keep the host active so story
            // coroutines and audio survive while the dialogue itself is hidden.
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.AddComponent<CanvasGroup>();
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (visible && !panel.activeSelf) panel.SetActive(true);
        }

        private void CompleteTextImmediately()
        {
            if (!_isTextRevealing) return;
            if (_customPresentation != null)
            {
                RuntimeDialogueNode node = _currentNode as RuntimeDialogueNode;
                _customPresentation.CompleteReveal();
                OnCustomRevealCompleted(node);
                return;
            }
            if (DialogueText == null) return;
            if (_textRevealCoroutine != null) StopCoroutine(_textRevealCoroutine);
            _textRevealCoroutine = null;
            if (_currentNode is RuntimeDialogueNode dialogue &&
                dialogue.PlaySoundCharacterIndex >= DialogueText.maxVisibleCharacters)
                PlayDialogueCue(dialogue);
            DialogueText.maxVisibleCharacters = int.MaxValue;
            _isTextRevealing = false;
            _textCompletedFrame = Time.frameCount;
            _speaker?.StopSpeaking();
            StopTalkAudio();
            OnSupportedSaveBoundary();
        }

        private void StopNodePresentation()
        {
            if (_customPresentation != null)
            {
                _customPresentation.HideDialogue();
                _isTextRevealing = false;
                return;
            }
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
            _choiceButtons.Clear();
            if (_customPresentation != null)
            {
                _customPresentation.ClearChoices();
                return;
            }
            if (ChoiceButtonContainer == null) return;
            foreach (Transform child in ChoiceButtonContainer)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }
    }
}
