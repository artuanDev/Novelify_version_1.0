using System;
using System.Collections.Generic;
using UnityEngine;

namespace Novelify
{
    public partial class NovelGraphRunner
    {
        internal void StartGraphInternal(RuntimeNovelGraph graph)
        {
            StopGraphInternal();
            _hasStartedGraph = true;
            _stage?.StopMovement();
            _graphCalls.Clear();
            _valueScope = new RuntimeValueScope();
            LoadGraph(graph);
            if (graph == null)
            {
                Debug.LogError("NovelGraphRunner has no RuntimeNovelGraph assigned.", this);
                return;
            }

            _isGraphRunning = true;
            Session.RaiseStarted(graph);
            if (!string.IsNullOrEmpty(graph.EntryNodeID)) ShowNode(graph.EntryNodeID);
            else StopGraphInternal();
        }

        internal void AdvanceGraphInternal()
        {
            if (_currentNode is not RuntimeDialogueNode || _isWaiting || _nodeEnteredFrame == Time.frameCount) return;
            if (_isTextRevealing) { CompleteTextImmediately(); return; }
            if (_textCompletedFrame == Time.frameCount) return;
            if (_currentNode is RuntimeChoiceNode choice && choice.Choices?.Count > 0) return;
            AdvanceCurrentNode();
        }

        private void LoadGraph(RuntimeNovelGraph graph)
        {
            RuntimeGraph = graph;
            _nodeLookup.Clear();
            if (graph?.AllNodes == null) return;
            foreach (RuntimeNode node in graph.AllNodes)
                if (node != null && !string.IsNullOrEmpty(node.NodeID)) _nodeLookup[node.NodeID] = node;
        }

        private void AdvanceCurrentNode()
        {
            if (!string.IsNullOrEmpty(_currentNode?.NextNodeID)) ShowNode(_currentNode.NextNodeID);
            else if (TryReturnFromGraph(out string returnNodeID)) ShowNode(returnNodeID);
            else StopGraphInternal();
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
                    Debug.LogWarning($"NovelGraphRunner could not find node '{nodeID}'.", this);
                    break;
                }
                _currentNode = node;
                Session.RaiseNodeEntered(RuntimeGraph, node);
                StateStore.RecordVisit(RuntimeGraph != null ? RuntimeGraph.GraphID : string.Empty, node.NodeID);
                _textCompletedFrame = -1;
                if (TryExecuteRegisteredNode(node, out NovelNodeExecutionResult customResult))
                {
                    switch (customResult.Kind)
                    {
                        case NovelNodeExecutionKind.Continue:
                            nodeID = string.IsNullOrEmpty(customResult.NextNodeID)
                                ? node.NextNodeID
                                : customResult.NextNodeID;
                            continue;
                        case NovelNodeExecutionKind.Jump:
                            nodeID = customResult.NextNodeID;
                            continue;
                        case NovelNodeExecutionKind.Pause:
                            PauseForExternalHandler(customResult.NextNodeID);
                            return;
                        case NovelNodeExecutionKind.Stop:
                            StopGraphInternal();
                            return;
                    }
                }
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
                        var movingCharacters = new List<CharacterInfo>();
                        CharacterInfo moving = ApplyPortraitTransform(
                            move, PrimaryTransformTarget(move));
                        if (moving != null)
                            movingCharacters.Add(moving);
                        if (move.AdditionalTargets != null &&
                            move.AdditionalTargets.Count > 0)
                        {
                            foreach (RuntimePortraitTransformTarget target in
                                     move.AdditionalTargets)
                            {
                                CharacterInfo additional =
                                    ApplyPortraitTransform(move, target);
                                if (additional != null)
                                    movingCharacters.Add(additional);
                            }
                        }
                        else if (move.TransformSecondCharacter)
                        {
                            CharacterInfo legacySecond =
                                ApplyPortraitTransform(
                                    move, LegacySecondTransformTarget(move));
                            if (legacySecond != null)
                                movingCharacters.Add(legacySecond);
                        }
                        if (move.WaitForCompletion &&
                            movingCharacters.Exists(character =>
                                character != null && character.IsMoving))
                        {
                            _isWaiting = true;
                            _waitCoroutine = StartCoroutine(WaitThenContinue(
                                node, version, 0f,
                                movingCharacters.ToArray()));
                            return;
                        }
                        break;
                    case RuntimeFlipCharacterNode flip:
                        NovelCharacterReference flipTarget = ResolveCharacterReference(
                            flip.CharacterReferenceValue, flip.CharacterValue, flip.Character, flip.InstanceID);
                        CharacterInfo flipping = ShowCharacter(flipTarget.Character, flipTarget.InstanceID);
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
                    case RuntimeSetCharacterFacingNode facing:
                        NovelCharacterReference facingTarget = ResolveCharacterReference(
                            facing.CharacterReferenceValue, facing.CharacterValue, facing.Character, facing.InstanceID);
                        CharacterInfo facingCharacter = ShowCharacter(facingTarget.Character, facingTarget.InstanceID);
                        if (facingCharacter != null)
                        {
                            Vector3 currentScale = facingCharacter.transform.localScale;
                            float sign = facing.Facing == CharacterFacing.Left ? -1f : 1f;
                            currentScale.x = Mathf.Abs(currentScale.x) * sign;
                            facingCharacter.transform.localScale = currentScale;
                        }
                        break;
                    case RuntimeShowCharacterNode show:
                        NovelCharacterReference showTarget = ResolveCharacterReference(
                            show.CharacterReferenceValue, show.CharacterValue, show.Character, show.InstanceID);
                        CharacterInfo shown = ShowCharacter(showTarget.Character, showTarget.InstanceID);
                        if (shown != null)
                        {
                            Vector2 showPosition = AsVector2(Evaluate(show.PositionValue), show.Position);
                            if (show.PositionSpace == CharacterPositionSpace.Normalized)
                                showPosition = shown.ClampToStageBounds(shown.NormalizedToAnchoredPosition(showPosition, 0f), 0f);
                            shown.MoveTo(showPosition, false, 0f);
                            shown.SetEmotion(show.Emotion);
                        }
                        break;
                    case RuntimeHideCharacterNode hide:
                        NovelCharacterReference hideTarget = ResolveCharacterReference(
                            hide.CharacterReferenceValue, hide.CharacterValue, hide.Character, hide.InstanceID);
                        CharacterInfo hiding = Stage.Hide(hideTarget.Character, hideTarget.InstanceID,
                            hide.Transition, hide.Direction, hide.Duration, hide.SlideOffset, hide.Easing);
                        if (hide.WaitForCompletion && hiding != null && hiding.IsMoving)
                        {
                            _isWaiting = true;
                            _waitCoroutine = StartCoroutine(WaitThenContinue(node, version, 0f, hiding));
                            return;
                        }
                        break;
                    case RuntimeHideAllCharactersNode _: Stage.HideAll(); break;
                    case RuntimeSetCharacterEmotionNode emotion:
                        NovelCharacterReference emotionTarget = ResolveCharacterReference(
                            emotion.CharacterReferenceValue, emotion.CharacterValue, emotion.Character, emotion.InstanceID);
                        ShowCharacter(emotionTarget.Character, emotionTarget.InstanceID)?.SetEmotion(emotion.Emotion);
                        break;
                    case RuntimeCreateDialogueBoxNode createDialogue:
                        GeneratedPresentation.CreateDialogueBox(createDialogue);
                        break;
                        
                    case RuntimeCreateDialogueSpeakerBoxNode createSpeaker:
                        GeneratedPresentation.CreateSpeakerBox(createSpeaker);
                        break;
                        
                    case RuntimeChangeDialogueStyleNode changeStyle:
                        GeneratedPresentation.ChangeStyle(changeStyle);
                        break;
                        
                    case RuntimeResetDialogueStyleNode resetStyle:
                        GeneratedPresentation.ResetStyle(resetStyle);
                        break;

                    case RuntimeCreateChoiceLayoutNode createChoices:
                        GeneratedPresentation.CreateChoiceLayout(createChoices);
                        break;
                        
                    case RuntimePlayMusicNode music:
                        ExecuteGeneratedPlayMusic(music);
                        break;
                        
                    case RuntimeStopAudioChannelNode stopChannel:
                        GeneratedPresentation.StopAudio(stopChannel.Channel);
                        break;

                    case RuntimeCreateSpeechBubbleNode createBubble:
                        GeneratedPresentation.CreateSpeechBubble(createBubble);
                        break;

                    case RuntimeChangeSpeechBubbleNode changeBubble:
                        GeneratedPresentation.ChangeSpeechBubble(changeBubble);
                        break;

                    case RuntimeSetBackgroundNode background:
                        if (BeginGeneratedBackground(background, version))
                            return;
                        break;

                    case RuntimeFadeNode fade:
                        if (BeginGeneratedFade(fade, version))
                            return;
                        break;
                    case RuntimeWaitNode wait:
                        if (wait.Duration > 0f && !float.IsInfinity(wait.Duration))
                        {
                            _isWaiting = true;
                            _waitCoroutine = StartCoroutine(WaitThenContinue(node, version, wait.Duration));
                            return;
                        }
                        break;
                    case RuntimeCheckpointNode checkpoint:
                        RequestCheckpoint(checkpoint.CheckpointID,
                            checkpoint.SaveMode == NovelCheckpointSaveMode.Autosave
                                ? checkpoint.AutosaveSlotID
                                : null);
                        break;
                    case RuntimeSetVariableNode setVariable:
                        if (!TryWriteVariable(setVariable.Variable, Evaluate(setVariable.Value), out string setError))
                            Debug.LogError(setError, this);
                        break;
                    case RuntimeModifyVariableNode modifyVariable:
                        if (!TryModifyVariable(modifyVariable, out string modifyError))
                            Debug.LogError(modifyError, this);
                        break;
                    case RuntimeBranchNode branch:
                        nodeID = AsBool(Evaluate(branch.Condition), false)
                            ? branch.TrueNodeID
                            : branch.FalseNodeID;
                        continue;
                    case RuntimeDialogueEventNode signal:
                        OnDialogueEvent?.Invoke(signal.EventName ?? string.Empty);
                        Session.RaiseEvent(signal.EventName ?? string.Empty);
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
                            StopGraphInternal();
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
                            StopGraphInternal();
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
            StopGraphInternal();
        }

        private static RuntimePortraitTransformTarget PrimaryTransformTarget(
            RuntimeTransformSpeakerPortraitNode move) => new()
        {
            Character = move.Character,
            InstanceID = move.InstanceID,
            OffsetX = move.OffsetX,
            OffsetY = move.OffsetY,
            Rotation = move.Rotation,
            Scale = move.Scale,
            Margin = move.Margin,
            Opacity = move.Opacity,
            CharacterValue = move.CharacterValue,
            CharacterReferenceValue = move.CharacterReferenceValue,
            PositionValue = move.PositionValue,
            RotationValue = move.RotationValue,
            ScaleValue = move.ScaleValue,
            MarginValue = move.MarginValue,
            OpacityValue = move.OpacityValue
        };

        private static RuntimePortraitTransformTarget
            LegacySecondTransformTarget(
                RuntimeTransformSpeakerPortraitNode move) => new()
        {
            Character = move.SecondCharacter,
            InstanceID = move.SecondInstanceID,
            OffsetX = move.SecondOffsetX,
            OffsetY = move.SecondOffsetY,
            Rotation = move.SecondRotation,
            Scale = move.SecondScale,
            Margin = move.SecondMargin,
            Opacity = move.SecondOpacity,
            CharacterValue = move.SecondCharacterValue,
            CharacterReferenceValue = move.SecondCharacterReferenceValue,
            PositionValue = move.SecondPositionValue,
            RotationValue = move.SecondRotationValue,
            ScaleValue = move.SecondScaleValue,
            MarginValue = move.SecondMarginValue,
            OpacityValue = move.SecondOpacityValue
        };

        private CharacterInfo ApplyPortraitTransform(
            RuntimeTransformSpeakerPortraitNode move,
            RuntimePortraitTransformTarget transformTarget)
        {
            NovelCharacterReference targetReference = ResolveCharacterReference(
                transformTarget.CharacterReferenceValue,
                transformTarget.CharacterValue,
                transformTarget.Character,
                transformTarget.InstanceID);
            CharacterInfo character = ShowCharacter(
                targetReference.Character, targetReference.InstanceID);
            if (character == null)
                return null;

            Vector2 fallbackPosition = new Vector2(
                transformTarget.OffsetX, transformTarget.OffsetY);
            RuntimeValueExpression positionValue =
                transformTarget.PositionValue;
            Vector2 offset = positionValue != null
                ? AsVector2(Evaluate(positionValue), fallbackPosition)
                : fallbackPosition;
            float margin = Mathf.Max(0f, AsFloat(Evaluate(
                transformTarget.MarginValue), transformTarget.Margin));
            float rotation = AsFloat(Evaluate(
                transformTarget.RotationValue), transformTarget.Rotation);
            Vector2 scale = AsVector2(Evaluate(
                transformTarget.ScaleValue), transformTarget.Scale);
            float opacity = Mathf.Clamp01(AsFloat(Evaluate(
                transformTarget.OpacityValue), transformTarget.Opacity));
            bool normalized = move.PositionSpace == CharacterPositionSpace.Normalized;
            Vector2 target;
            if (normalized && move.Relative)
                target = character.Position +
                         character.NormalizedToAnchoredOffset(offset, margin);
            else
            {
                target = normalized
                    ? character.NormalizedToAnchoredPosition(offset, margin)
                    : offset;
                if (move.Relative)
                    target += character.Position;
            }
            if (normalized)
                target = character.ClampToStageBounds(target, margin);

            if (move is RuntimeTranslateSpeakerPortraitNode)
            {
                character.MoveTo(target, move.SmoothMovement,
                    move.Duration, move.EaseInOut);
                return character;
            }

            character.TransformTo(
                target,
                rotation,
                scale,
                move.SmoothMovement,
                move.Duration,
                move.UseEasingPreset
                    ? move.Easing
                    : move.EaseInOut
                        ? PortraitTweenEasing.EaseInOut
                        : PortraitTweenEasing.None,
                move.CustomEasingCurve,
                move.AnimateOpacity,
                opacity);
            return character;
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
    }
}
