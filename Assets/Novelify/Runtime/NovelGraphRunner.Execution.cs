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
                        NovelCharacterReference movingTarget = ResolveCharacterReference(
                            move.CharacterReferenceValue, move.CharacterValue, move.Character, move.InstanceID);
                        CharacterInfo moving = ShowCharacter(movingTarget.Character, movingTarget.InstanceID);
                        if (moving != null)
                        {
                            Vector2 offset = move.PositionValue != null
                                ? AsVector2(Evaluate(move.PositionValue), new Vector2(move.OffsetX, move.OffsetY))
                                : new Vector2(move.OffsetX, move.OffsetY);
                            float margin = Mathf.Max(0f, AsFloat(Evaluate(move.MarginValue), move.Margin));
                            float rotation = AsFloat(Evaluate(move.RotationValue), move.Rotation);
                            Vector2 scale = AsVector2(Evaluate(move.ScaleValue), move.Scale);
                            bool normalizedPosition = move.PositionSpace == CharacterPositionSpace.Normalized;
                            Vector2 target = normalizedPosition
                                ? moving.NormalizedToAnchoredPosition(offset, margin)
                                : offset;
                            if (move.Relative) target += moving.Position;
                            if (normalizedPosition)
                                target = moving.ClampToStageBounds(target, margin);
                            if (move is not RuntimeTranslateSpeakerPortraitNode)
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
                        Stage.Hide(hideTarget.Character, hideTarget.InstanceID);
                        break;
                    case RuntimeHideAllCharactersNode _: Stage.HideAll(); break;
                    case RuntimeSetCharacterEmotionNode emotion:
                        NovelCharacterReference emotionTarget = ResolveCharacterReference(
                            emotion.CharacterReferenceValue, emotion.CharacterValue, emotion.Character, emotion.InstanceID);
                        ShowCharacter(emotionTarget.Character, emotionTarget.InstanceID)?.SetEmotion(emotion.Emotion);
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
