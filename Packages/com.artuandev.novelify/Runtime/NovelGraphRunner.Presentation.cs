using System.Collections;
using UnityEngine;

namespace Novelify
{
    public partial class NovelGraphRunner
    {
        private Font _defaultDialogueFont;
        private string _resolvedDialogueText = string.Empty;
        private int _resolvedSoundCueCharacterIndex = -1;

        private string ResolveLocalized(string key, string fallback) =>
            LocalizationTable != null
                ? LocalizationTable.Resolve(key, Locale, fallback)
                : fallback ?? string.Empty;

        private string LineKey(RuntimeDialogueNode node) =>
            !string.IsNullOrEmpty(node?.LineID)
                ? node.LineID
                : NovelLocalizationKey.Dialogue(RuntimeGraph?.GraphID, node?.NodeID);

        private string ResolveChoiceText(RuntimeChoiceNode node, ChoiceData choice)
        {
            string authored = choice.ChoiceText ?? string.Empty;
            string evaluated = AsString(Evaluate(choice.ChoiceTextValue), authored);
            return string.Equals(evaluated, authored, System.StringComparison.Ordinal)
                ? ResolveLocalized(NovelLocalizationKey.Choice(LineKey(node), choice.ChoiceID), authored)
                : evaluated;
        }

        private string ResolveDisabledReason(RuntimeChoiceNode node, ChoiceData choice)
        {
            string authored = choice.DisabledReason ?? string.Empty;
            string evaluated = AsString(Evaluate(choice.DisabledReasonValue), authored);
            return string.Equals(evaluated, authored, System.StringComparison.Ordinal)
                ? ResolveLocalized(NovelLocalizationKey.DisabledReason(LineKey(node), choice.ChoiceID), authored)
                : evaluated;
        }

        private void InitializePresentation()
        {
            if (_customPresentation != null) return;
            if (DialogueText != null)
            {
                _defaultDialogueFont = DialogueText.font;
                DialogueText.supportRichText = true;
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

        private IEnumerator WaitThenContinue(
            RuntimeNode node,
            int version,
            float seconds,
            params CharacterInfo[] movingCharacters)
        {
            // Yield before continuing so the coroutine handle is assigned before completion.
            bool characterIsMoving;
            do
            {
                yield return null;
                seconds -= DialogueDeltaTime;
                characterIsMoving = false;
                if (movingCharacters != null)
                    foreach (CharacterInfo character in movingCharacters)
                        if (character != null && character.IsMoving)
                        {
                            characterIsMoving = true;
                            break;
                        }
            } while (seconds > 0f || characterIsMoving);
            _waitCoroutine = null;
            _isWaiting = false;
            if (version == _flowVersion && _currentNode == node) AdvanceCurrentNode();
        }

        private void ShowDialogueNode(RuntimeDialogueNode node)
        {
            PrepareGeneratedDialogue(node);
            _choiceSelectionCommitted = false;
            _nodeEnteredFrame = Time.frameCount;
            NovelCharacterReference speakerReference = ResolveCharacterReference(
                node.CharacterReferenceValue, node.CharacterValue, node.NovelCharacter, node.InstanceID);
            NovelCharacter speakingCharacter = speakerReference.Character;
            string lineKey = LineKey(node);
            string speakerName = speakingCharacter != null ? speakingCharacter.SpeakerName : node.SpeakerName ?? string.Empty;
            _resolvedDialogueText = ResolveLocalized(lineKey, node.DialogueText);
            _resolvedSoundCueCharacterIndex =
                string.Equals(_resolvedDialogueText, node.DialogueText, System.StringComparison.Ordinal)
                    ? node.PlaySoundCharacterIndex
                    : FindSoundCue(_resolvedDialogueText);
            if (_customPresentation != null)
            {
                _speaker = null;
                _isTextRevealing = false;
                var presentation = new NovelDialoguePresentation(
                    Session,
                    RuntimeGraph,
                    node,
                    speakerReference,
                    speakerName,
                    _resolvedDialogueText,
                    () => OnCustomRevealCompleted(node));
                _customPresentation.PresentDialogue(presentation);
                _isTextRevealing = _customPresentation.IsRevealing;
                if (node is RuntimeChoiceNode customChoice) ShowChoices(customChoice);
                if (_currentNode == node)
                {
                    OnDialogueBoundaryPresented(node, speakerName, _resolvedDialogueText);
                    Session.RaiseDialoguePresented(RuntimeGraph, node, speakerName);
                }
                ScheduleAutomaticBubbleAdvance(node);
                return;
            }

            SetPanelVisible(DialoguePanel, true);

            if (SpeakerNameText != null)
                SpeakerNameText.SetText(speakerName);
            if (node is not RuntimeSpeechBubbleNode)
                GeneratedPresentation.RefreshSpeakerNameLayout();
            if (NameBackground != null)
                NameBackground.SetActive(!string.IsNullOrEmpty(speakerName));

            if (BackgroundChoicesPanel != null) BackgroundChoicesPanel.SetActive(false);
            StopAudio(NodeSoundSource);
            AudioClip nodeClip = AsObject(Evaluate(node.PlaySoundValue), node.PlaySound);
            if (NodeSoundSource != null && nodeClip != null &&
                (_resolvedSoundCueCharacterIndex < 0 || node.ShowTextImmediately))
            {
                NodeSoundSource.clip = nodeClip;
                NodeSoundSource.Play();
            }
            _speaker = speakingCharacter != null ? ShowCharacter(speakingCharacter, speakerReference.InstanceID) : null;

            TrackGeneratedSpeechBubble(node);
            Stage.SetActiveSpeaker(
                _speaker,
                DimInactiveCharacters,
                InactiveCharacterTint,
                SpeakerFocusTransitionDuration);
            Stage.BringToFront(_speaker);
            _speaker?.BeginDialogue(node);
            if (_speaker != null && node.Appearance != CharacterTransitionMode.Instant)
                _speaker.TransitionIn(node.Appearance, node.AppearanceDirection,
                    Mathf.Max(0f, node.AppearanceDuration), node.SlideOffset, node.AppearanceEasing);

            if (DialogueText != null)
            {
                ApplyDialogueFonts(node);
                DialogueText.SetText(_resolvedDialogueText);
                if (node.ShowTextImmediately || string.IsNullOrEmpty(_resolvedDialogueText))
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
                OnDialogueBoundaryPresented(node, speakerName, _resolvedDialogueText);
                Session.RaiseDialoguePresented(RuntimeGraph, node, speakerName);
            }
            ScheduleAutomaticBubbleAdvance(node);
        }

        private void ScheduleAutomaticBubbleAdvance(RuntimeDialogueNode node)
        {
            float delay = node is RuntimeSpeechBubbleNode bubble
                ? bubble.AutoAdvanceDelay
                : node is RuntimeNarrationNode narration
                    ? narration.AutoAdvanceDelay
                    : -1f;
            if (delay < 0f)
                return;
            if (_autoAdvanceCoroutine != null)
                StopCoroutine(_autoAdvanceCoroutine);
            _autoAdvanceCoroutine = StartCoroutine(AutoAdvanceBubble(
                node, _flowVersion, delay));
        }

        private IEnumerator AutoAdvanceBubble(
            RuntimeDialogueNode node,
            int version,
            float delay)
        {
            // Always keep the node alive for at least one rendered frame.
            do
            {
                yield return null;
                delay -= DialogueDeltaTime;
            } while (delay > 0f);

            if (version != _flowVersion || _currentNode != node)
                yield break;
            _autoAdvanceCoroutine = null;
            if (_isTextRevealing)
            {
                CompleteTextImmediately();
                yield return null;
            }
            if (version == _flowVersion && _currentNode == node)
                AdvanceCurrentNode();
        }

        private void ApplyDialogueFonts(RuntimeDialogueNode node)
        {
            if (DialogueText == null || node == null)
            {
                return;
            }

            DialogueText.SetFontAssets(
                node.DialogueFont != null ? node.DialogueFont : _defaultDialogueFont,
                node.DialogueFontAssets);
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
                _resolvedDialogueText, DialogueText,
                character != null ? character.TalkSound : node.TalkSound, TalkSource,
                character != null ? character.PitchMinVariation : node.PitchMinVariation,
                character != null ? character.PitchMaxVariation : node.PitchMaxVariation,
                node.CharactersPerSecond * Mathf.Max(0.25f, TextSpeedMultiplier),
                letter => _speaker?.RevealLetter(letter),
                TimeMode,
                (characterIndex, _) =>
                {
                    if (characterIndex == _resolvedSoundCueCharacterIndex)
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

        private static int FindSoundCue(string text)
        {
            var characters = NovelTextMarkup.Parse(text).Characters;
            for (int index = 0; index < characters.Count; index++)
                if (characters[index].SoundCue) return index;
            return -1;
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
            _generatedPresentation?.StopGraphEffects();
            _stage?.StopSimpleAnimations();
            _currentNode = null;
            HideDialoguePanel();
            if (HideCharactersOnEnd) _stage?.HideAll();
            _stage?.ClearSpeakerFocus();
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
                _resolvedSoundCueCharacterIndex >= DialogueText.maxVisibleCharacters)
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
            if (_autoAdvanceCoroutine != null)
                StopCoroutine(_autoAdvanceCoroutine);
            _autoAdvanceCoroutine = null;
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
