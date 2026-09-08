using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Novelify.Editor
{
    [ScriptedImporter(11, NovelGraph.AssetExtension)]
    public class NovelGraphImporter : ScriptedImporter
    {
        protected Graph _editorGraph;
        protected AssetImportContext _context;
        private Dictionary<INode, string> _nodeIDMap;
        private HashSet<string> _choiceIDs;
        private HashSet<string> _expressionDiagnostics;
        private string _expressionDiagnosticIdentity;
        private static readonly HashSet<string> LoggedExpressionDiagnostics = new HashSet<string>(StringComparer.Ordinal);

        public override void OnImportAsset(AssetImportContext ctx)
        {
            NovelGraph editorGraph =
                GraphDatabase.LoadGraphForImporter<NovelGraph>(
                    ctx.assetPath);

            ImportGraph(ctx, editorGraph, ScriptableObject.CreateInstance<RuntimeNovelGraph>());
        }

        protected void ImportGraph(AssetImportContext ctx, Graph editorGraph, RuntimeNovelGraph runtimeGraph)
        {
            _context = ctx;

            if (editorGraph == null)
            {
                ctx.LogImportError($"Novelify could not deserialize the editor graph at '{ctx.assetPath}'.");
                return;
            }

            _editorGraph = editorGraph;

            runtimeGraph.GraphID = AssetDatabase.AssetPathToGUID(ctx.assetPath);
            runtimeGraph.SchemaVersion = RuntimeNovelGraph.CurrentSchemaVersion;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string sourcePath = Path.Combine(projectRoot, ctx.assetPath);
            runtimeGraph.ContentVersion = File.Exists(sourcePath)
                ? Hash128.Compute(File.ReadAllText(sourcePath)).ToString()
                : string.Empty;
            _expressionDiagnosticIdentity = ctx.assetPath + "|" + runtimeGraph.ContentVersion;

            var nodeIDMap = new Dictionary<INode, string>();
            _nodeIDMap = nodeIDMap;
            _choiceIDs = new HashSet<string>(StringComparer.Ordinal);
            _expressionDiagnostics = new HashSet<string>(StringComparer.Ordinal);

            foreach (INode node in editorGraph.GetNodes())
            {
                nodeIDMap[node] = node.ID.ToString();
            }

            if (runtimeGraph is RuntimeNovelFunction function)
                ProcessFunctionInterface(function);

            var labelNodeIDs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (LabelNode labelNode in editorGraph.GetNodes().OfType<LabelNode>())
            {
                string label = GetPortValue<string>(labelNode.GetInputPortByName("Label"))?.Trim();
                if (string.IsNullOrEmpty(label))
                {
                    _context?.LogImportWarning("Label node has an empty Label.");
                }
                else if (!labelNodeIDs.TryAdd(label, nodeIDMap[labelNode]))
                {
                    _context?.LogImportWarning($"Duplicate label '{label}'. Labels must be unique.");
                }
            }

            StartNode startNode =
                editorGraph.GetNodes()
                    .OfType<StartNode>()
                    .FirstOrDefault();

            INode entryNode = null;
            if (startNode != null)
            {
                entryNode = NovelGraphValues.FlowDestination(editorGraph, startNode.GetOutputPortByName("out"));
            }
            else if (runtimeGraph is RuntimeNovelFunction)
            {
                IVariable enter = editorGraph.GetVariables().FirstOrDefault(variable =>
                    variable.VariableKind == VariableKind.Input && variable.Name == NovelFunctionGraph.EnterVariableName);
                var nodes = new List<IVariableNode>();
                enter?.GetNodes(nodes);
                IPort flowOutput = nodes.SelectMany(node => node.GetOutputPorts()).FirstOrDefault();
                entryNode = NovelGraphValues.FlowDestination(editorGraph, flowOutput);
            }

            if (entryNode != null && nodeIDMap.ContainsKey(entryNode))
            {
                runtimeGraph.EntryNodeID = nodeIDMap[entryNode];
            }

            foreach (INode editorNode in editorGraph.GetNodes())
            {
                if (editorNode is StartNode || editorNode is IVariableNode || IsValueNode(editorNode))
                {
                    continue;
                }

                RuntimeNode runtimeNode;

                if (editorNode is ISubgraphNode subgraphNode &&
                    editorNode is not CallNovelPageNode &&
                    subgraphNode.GetSubgraph() is NovelFunctionGraph functionGraph)
                {
                    runtimeNode = ProcessFunctionCallNode(editorNode, functionGraph, nodeIDMap);
                }
                else if (editorNode is SimpleDialogueNode dialogueNode)
                {
                    var dialogueRuntimeNode =
                        new RuntimeDialogueNode
                        {
                            NodeID = nodeIDMap[editorNode]
                        };

                    ProcessDialogueNode(
                        dialogueNode,
                        dialogueRuntimeNode,
                        nodeIDMap);

                    runtimeNode = dialogueRuntimeNode;
                }
                else if (editorNode is ChoiceNode choiceNode)
                {
                    var choiceRuntimeNode =
                        new RuntimeChoiceNode
                        {
                            NodeID = nodeIDMap[editorNode]
                        };

                    ProcessChoiceNode(
                        choiceNode,
                        choiceRuntimeNode,
                        nodeIDMap);

                    runtimeNode = choiceRuntimeNode;
                }
                else if (editorNode is PlaySoundNode playSoundNode)
                {
                    var soundRuntimeNode =
                        new RuntimePlaySoundNode
                        {
                            NodeID = nodeIDMap[editorNode]
                        };

                    ProcessPlaySoundNode(
                        playSoundNode,
                        soundRuntimeNode,
                        nodeIDMap);

                    runtimeNode = soundRuntimeNode;
                }
                else if (editorNode is TranslateSpeakerPortraitNode legacyTranslateNode)
                {
                    var runtimeTranslateSpeakerPortrait =
                        new RuntimeTranslateSpeakerPortraitNode
                        {
                            NodeID = nodeIDMap[editorNode]
                        };

                    ProcessTransformSpeakerNode(
                        legacyTranslateNode,
                        runtimeTranslateSpeakerPortrait,
                        nodeIDMap);

                    runtimeNode = runtimeTranslateSpeakerPortrait;
                }
                else if (editorNode is TransformSpeakerPortraitNode transformSpeakerPortraitNode)
                {
                    var runtimeTransformSpeakerPortrait =
                        new RuntimeTransformSpeakerPortraitNode
                        {
                            NodeID = nodeIDMap[editorNode]
                        };

                    ProcessTransformSpeakerNode(
                        transformSpeakerPortraitNode,
                        runtimeTransformSpeakerPortrait,
                        nodeIDMap);

                    runtimeNode = runtimeTransformSpeakerPortrait;
                }
                else if (editorNode is FlipCharacterNode flipCharacterNode)
                {
                    var runtimeFlipCharacterNode =
                        new RuntimeFlipCharacterNode
                        {
                            NodeID = nodeIDMap[editorNode]
                        };

                    ProcessFlipCharacterNode(
                        flipCharacterNode,
                        runtimeFlipCharacterNode,
                        nodeIDMap);

                    runtimeNode = runtimeFlipCharacterNode;
                }
                else if (editorNode is SetCharacterFacingNode setFacingNode)
                {
                    var runtimeSetFacingNode = new RuntimeSetCharacterFacingNode
                    {
                        NodeID = nodeIDMap[editorNode]
                    };
                    ProcessSetCharacterFacingNode(setFacingNode, runtimeSetFacingNode, nodeIDMap);
                    runtimeNode = runtimeSetFacingNode;
                }
                else if (editorNode is BranchNovelNode branchNode)
                {
                    runtimeNode = new RuntimeBranchNode
                    {
                        NodeID = nodeIDMap[editorNode],
                        Condition = BuildExpression(branchNode.GetInputPortByName("Condition")),
                        TrueNodeID = GetDestinationID(branchNode.GetOutputPortByName("True"), nodeIDMap),
                        FalseNodeID = GetDestinationID(branchNode.GetOutputPortByName("False"), nodeIDMap)
                    };
                }
                else
                {
                    runtimeNode = CreateUtilityNode(editorNode);
                    runtimeNode.NodeID = nodeIDMap[editorNode];
                    if (editorNode is JumpNode jumpNode)
                    {
                        string label = GetPortValue<string>(jumpNode.GetInputPortByName("Label"))?.Trim();
                        if (string.IsNullOrEmpty(label) || !labelNodeIDs.TryGetValue(label, out string targetNodeID))
                        {
                            _context?.LogImportWarning($"Jump target '{label}' was not found.");
                        }
                        else
                        {
                            runtimeNode.NextNodeID = targetNodeID;
                        }
                    }
                    else
                    {
                        runtimeNode.NextNodeID = GetNextNodeID(editorNode, nodeIDMap);
                    }
                }

                runtimeGraph.AllNodes.Add(runtimeNode);
            }

            ctx.AddObjectToAsset("RuntimeData", runtimeGraph);
            ctx.SetMainObject(runtimeGraph);
        }

        private void ProcessDialogueNode(
            SimpleDialogueNode node,
            RuntimeDialogueNode runtimeNode,
            Dictionary<INode, string> nodeIDMap)
        {
            SetSpeaker(node, runtimeNode);
            SetPresentationOptions(node, runtimeNode);

            runtimeNode.NextNodeID =
                GetNextNodeID(node, nodeIDMap);
        }

        private void ProcessChoiceNode(
            ChoiceNode node,
            RuntimeChoiceNode runtimeNode,
            Dictionary<INode, string> nodeIDMap)
        {
            SetSpeaker(node, runtimeNode);
            SetPresentationOptions(node, runtimeNode);
            runtimeNode.UnavailableDestinationNodeID =
                GetDestinationID(node.GetOutputPortByName("Fallback"), nodeIDMap);

            IEnumerable<IPort> choiceOutputPorts =
                node.GetOutputPorts()
                    .Where(port =>
                        port.Name.StartsWith("Choice "));

            foreach (IPort outputPort in choiceOutputPorts)
            {
                string index =
                    outputPort.Name.Substring("Choice ".Length);

                IPort textPort =
                    node.GetInputPortByName(
                        $"Choice Text {index}");

                string choiceID = GetPortValue<string>(node.GetInputPortByName($"Choice ID {index}"))?.Trim();
                if (string.IsNullOrEmpty(choiceID)) choiceID = $"{nodeIDMap[node]}:{index}";
                if (!_choiceIDs.Add(choiceID))
                    _context?.LogImportError($"Duplicate Choice ID '{choiceID}'. Choice IDs must be unique within a graph.");

                var choiceData = new ChoiceData
                {
                    ChoiceID = choiceID,
                    ChoiceText =
                        GetPortValue<string>(textPort),

                    ChoiceTextValue = BuildExpression(textPort),

                    Condition = BuildExpression(node.GetInputPortByName($"Condition {index}")),

                    UnavailablePolicy = GetPortValue<NovelChoiceUnavailablePolicy>(
                        node.GetInputPortByName($"Unavailable Policy {index}")),

                    DisabledReason = GetPortValue<string>(node.GetInputPortByName($"Disabled Reason {index}")),

                    DisabledReasonValue = BuildExpression(node.GetInputPortByName($"Disabled Reason {index}")),

                    OnceOnly = GetPortValue<bool>(node.GetInputPortByName($"Once Only {index}")),

                    DestinationNodeID = GetDestinationID(outputPort, nodeIDMap)
                };

                NovelChoiceTransactionDefinition transaction = GetPortValue<NovelChoiceTransactionDefinition>(
                    node.GetInputPortByName($"Transaction {index}"));
                if (transaction != null)
                {
                    string transactionPath = AssetDatabase.GetAssetPath(transaction);
                    if (!string.IsNullOrEmpty(transactionPath)) _context?.DependsOnSourceAsset(transactionPath);
                    foreach (NovelChoiceStateChangeDefinition change in transaction.Changes ??
                             Enumerable.Empty<NovelChoiceStateChangeDefinition>())
                    {
                        if (change?.Variable == null)
                        {
                            _context?.LogImportError($"Choice '{choiceID}' has a transaction entry without a Variable.");
                            continue;
                        }
                        bool numeric = change.Variable.Type is NovelVariableType.Integer or NovelVariableType.Float;
                        if (change.Operation != NovelChoiceStateOperation.Set && !numeric)
                        {
                            _context?.LogImportError(
                                $"Choice '{choiceID}' uses {change.Operation} on non-numeric variable '{change.Variable.Name}'.");
                            continue;
                        }
                        RuntimeValue value = change.CreateValue();
                        if (change.Operation == NovelChoiceStateOperation.Spend &&
                            ((value.Kind == RuntimeValueKind.Integer && value.IntegerValue < 0) ||
                             (value.Kind == RuntimeValueKind.Float && value.FloatValue < 0f)))
                        {
                            _context?.LogImportError($"Choice '{choiceID}' has a negative Spend amount.");
                            continue;
                        }
                        choiceData.StateChanges.Add(new RuntimeChoiceStateChange
                        {
                            Variable = change.Variable,
                            Operation = change.Operation,
                            Value = new RuntimeConstantExpression { Value = value }
                        });
                    }
                }

                runtimeNode.Choices.Add(choiceData);
            }

            bool reactive = runtimeNode.Choices.Any(choice => choice.OnceOnly || choice.StateChanges.Count > 0 ||
                choice.Condition is not RuntimeConstantExpression condition ||
                condition.Value?.Kind != RuntimeValueKind.Boolean || !condition.Value.BooleanValue);
            if (reactive && string.IsNullOrEmpty(runtimeNode.UnavailableDestinationNodeID))
                _context?.LogImportWarning("Reactive Choice has no Fallback connection for an all-unavailable menu.");
        }

        private void ProcessPlaySoundNode(
            PlaySoundNode node,
            RuntimePlaySoundNode runtimeNode,
            Dictionary<INode, string> nodeIDMap)
        {
            runtimeNode.Loop =
                GetOptionValue(
                    node.GetNodeOptionByName("Loop"),
                    false);

            runtimeNode.Priority = GetOptionValue(node.GetNodeOptionByName("Priority"), 1);
            runtimeNode.Volume = GetOptionValue(node.GetNodeOptionByName("Volume"), 1.0f);
            runtimeNode.Pitch = GetOptionValue(node.GetNodeOptionByName("Pitch"), 1.0f);

            IPort loopPort =
                node.GetInputPortByName("Loop");

            if (loopPort != null)
            {
                runtimeNode.Loop =
                    GetPortValue<bool>(loopPort);
            }

            // This is the exact input port shown in your graph.
            runtimeNode.ClipSound =
                GetPortValue<AudioClip>(
                    node.GetInputPortByName("AudioToPlay"));
            runtimeNode.ClipValue = BuildExpression(node.GetInputPortByName("AudioToPlay"));

            // Fallback names for future variations.
            if (runtimeNode.ClipSound == null)
            {
                runtimeNode.ClipSound =
                    GetFirstPortValue<AudioClip>(
                        node,
                        "Clip Sound",
                        "Sound",
                        "Audio Clip",
                        "Clip");
            }

            if (runtimeNode.ClipSound == null)
            {
                runtimeNode.ClipSound =
                    GetFirstOptionValue<AudioClip>(
                        node,
                        "AudioToPlay",
                        "Clip Sound",
                        "Sound",
                        "Audio Clip",
                        "Clip");
            }

            if (runtimeNode.ClipSound == null && IsMissingConstant(runtimeNode.ClipValue))
            {
                Debug.LogWarning(
                    "PlaySoundNode could not resolve an AudioClip " +
                    "from its AudioToPlay input.",
                    this);
            }

            runtimeNode.NextNodeID =
                GetNextNodeID(node, nodeIDMap);
        }

        private void ProcessTransformSpeakerNode(
            CharacterActionNode node,
            RuntimeTransformSpeakerPortraitNode runtimeNode,
            Dictionary<INode, string> nodeIDMap)
        {
            CharacterPositionSpace positionSpace = node is TransformSpeakerPortraitNode
                ? GetOptionValue(node.GetNodeOptionByName("Coordinate Space"), CharacterPositionSpace.Normalized)
                : CharacterPositionSpace.Canvas;
            runtimeNode.PositionSpace = positionSpace;
            runtimeNode.PositionIsNormalized = positionSpace == CharacterPositionSpace.Normalized;
            runtimeNode.Character = GetPortValue<NovelCharacter>(node.GetInputPortByName("Character"));
            runtimeNode.CharacterValue = BuildExpression(node.GetInputPortByName("Character"));
            runtimeNode.CharacterReferenceValue = BuildExpression(node.GetInputPortByName("Character Reference"));
            runtimeNode.InstanceID = GetOptionValue(node.GetNodeOptionByName("Instance ID"), string.Empty);

            if (node is TransformSpeakerPortraitNode)
            {
                IPort positionPort = node.GetInputPortByName("Position");
                IPort rotationPort = node.GetInputPortByName("Rotation");
                IPort scalePort = node.GetInputPortByName("Scale");
                IPort marginPort = node.GetInputPortByName("Margin");
                Vector2 legacyPosition = new Vector2(
                    GetOptionValue(node.GetNodeOptionByName("OffsetX"), 0f),
                    GetOptionValue(node.GetNodeOptionByName("OffsetY"), 0f));
                float legacyRotation = GetOptionValue(node.GetNodeOptionByName("Rotation"), 0f);
                Vector2 legacyScale = GetOptionValue(node.GetNodeOptionByName("Scale"), Vector2.one);
                float legacyMargin = GetOptionValue(node.GetNodeOptionByName("Margin"), 0f);
                Vector2 position = GetPortValue<Vector2>(positionPort);
                float rotation = GetPortValue<float>(rotationPort);
                Vector2 scale = GetPortValue<Vector2>(scalePort);
                float margin = GetPortValue<float>(marginPort);

                bool useLegacyPosition = !positionPort.IsConnected && position == Vector2.zero && legacyPosition != Vector2.zero;
                bool useLegacyRotation = !rotationPort.IsConnected && Mathf.Approximately(rotation, 0f) && !Mathf.Approximately(legacyRotation, 0f);
                bool useLegacyScale = !scalePort.IsConnected && scale == Vector2.one && legacyScale != Vector2.one;
                bool useLegacyMargin = !marginPort.IsConnected && Mathf.Approximately(margin, 0f) && !Mathf.Approximately(legacyMargin, 0f);

                runtimeNode.PositionValue = useLegacyPosition ? Constant(legacyPosition) : BuildExpression(positionPort);
                runtimeNode.RotationValue = useLegacyRotation ? Constant(legacyRotation) : BuildExpression(rotationPort);
                runtimeNode.ScaleValue = useLegacyScale ? Constant(legacyScale) : BuildExpression(scalePort);
                runtimeNode.MarginValue = useLegacyMargin ? Constant(legacyMargin) : BuildExpression(marginPort);
                if (useLegacyPosition) position = legacyPosition;
                if (useLegacyRotation) rotation = legacyRotation;
                if (useLegacyScale) scale = legacyScale;
                if (useLegacyMargin) margin = legacyMargin;
                runtimeNode.OffsetX = position.x;
                runtimeNode.OffsetY = position.y;
                runtimeNode.Rotation = rotation;
                runtimeNode.Scale = scale;
                runtimeNode.Margin = Mathf.Max(0f, margin);
            }
            else
            {
                runtimeNode.OffsetX = GetOptionValue(node.GetNodeOptionByName("OffsetX"), 0f);
                runtimeNode.OffsetY = GetOptionValue(node.GetNodeOptionByName("OffsetY"), 0f);
                runtimeNode.Rotation = GetOptionValue(node.GetNodeOptionByName("Rotation"), 0f);
                runtimeNode.Scale = GetOptionValue(node.GetNodeOptionByName("Scale"), Vector2.one);
                runtimeNode.Margin = Mathf.Max(0f, GetOptionValue(node.GetNodeOptionByName("Margin"), 0f));
            }
            runtimeNode.SmoothMovement = node is TranslateSpeakerPortraitNode
                ? GetOptionValue(node.GetNodeOptionByName("Smooth Movement"), false)
                : GetOptionValue(node.GetNodeOptionByName("Animate Transform"), false);
            runtimeNode.Duration = Mathf.Max(0f, GetOptionValue(node.GetNodeOptionByName("Duration"), 0.5f));
            runtimeNode.WaitForCompletion = GetOptionValue(node.GetNodeOptionByName("Wait For Completion"), true);
            runtimeNode.EaseInOut = GetOptionValue(node.GetNodeOptionByName("Ease In Out"), true);
            runtimeNode.Relative = GetOptionValue(node.GetNodeOptionByName("Relative"), false);

            if (runtimeNode.Character == null && IsMissingConstant(runtimeNode.CharacterValue) &&
                IsMissingCharacterReference(runtimeNode.CharacterReferenceValue))
                _context?.LogImportWarning("Transform Speaker Portrait needs a Character or Character Reference input.");

            runtimeNode.NextNodeID =
                GetNextNodeID(node, nodeIDMap);
        }

        private void ProcessFlipCharacterNode(
            FlipCharacterNode node,
            RuntimeFlipCharacterNode runtimeNode,
            Dictionary<INode, string> nodeIDMap)
        {
            runtimeNode.Character = GetPortValue<NovelCharacter>(node.GetInputPortByName("Character"));
            runtimeNode.CharacterValue = BuildExpression(node.GetInputPortByName("Character"));
            runtimeNode.CharacterReferenceValue = BuildExpression(node.GetInputPortByName("Character Reference"));

            runtimeNode.FlipX = GetOptionValue(node.GetNodeOptionByName("FlipX"),true);
            runtimeNode.FlipY = GetOptionValue(node.GetNodeOptionByName("FlipY"),false);

            runtimeNode.InstanceID =
                GetOptionValue(
                    node.GetNodeOptionByName("Instance ID"),
                    string.Empty);

            if (runtimeNode.Character == null && IsMissingConstant(runtimeNode.CharacterValue) &&
                IsMissingCharacterReference(runtimeNode.CharacterReferenceValue))
                _context?.LogImportWarning(
                    "Flip Character needs a Character or Character Reference input.");
            runtimeNode.NextNodeID = GetNextNodeID(node, nodeIDMap);
        }

        private void ProcessSetCharacterFacingNode(
            SetCharacterFacingNode node,
            RuntimeSetCharacterFacingNode runtimeNode,
            Dictionary<INode, string> nodeIDMap)
        {
            runtimeNode.Character = GetPortValue<NovelCharacter>(node.GetInputPortByName("Character"));
            runtimeNode.CharacterValue = BuildExpression(node.GetInputPortByName("Character"));
            runtimeNode.CharacterReferenceValue = BuildExpression(node.GetInputPortByName("Character Reference"));
            runtimeNode.InstanceID = GetOptionValue(node.GetNodeOptionByName("Instance ID"), string.Empty);
            runtimeNode.Facing = GetOptionValue(node.GetNodeOptionByName("Facing"), CharacterFacing.Right);
            if (runtimeNode.Character == null && IsMissingConstant(runtimeNode.CharacterValue) &&
                IsMissingCharacterReference(runtimeNode.CharacterReferenceValue))
                _context?.LogImportWarning("Set Facing needs a Character or Character Reference input.");
            runtimeNode.NextNodeID = GetNextNodeID(node, nodeIDMap);
        }

        private string GetNextNodeID(
            INode node,
            Dictionary<INode, string> nodeIDMap)
        {
            return GetDestinationID(node.GetOutputPortByName("out"), nodeIDMap);
        }

        private string GetDestinationID(IPort output, Dictionary<INode, string> nodeIDMap)
        {
            var connected = new List<IPort>();
            output?.GetConnectedPorts(connected);
            if (connected.Count > 1)
            {
                _context?.LogImportWarning($"{output.GetNode().GetType().Name}: '{output.Name}' has multiple story destinations. " +
                    "Connect utility nodes in sequence, or use a Choice node for branching. Only one continuation can run.");
            }
            INode destination = NovelGraphValues.FlowDestination(_editorGraph, output);
            if (destination is IVariableNode)
                return null;
            return destination != null && nodeIDMap.TryGetValue(destination, out string id) ? id : null;
        }

        private RuntimeNode CreateUtilityNode(INode node)
        {
            NovelCharacter character = node is CharacterActionNode
                ? GetPortValue<NovelCharacter>(node.GetInputPortByName("Character")) : null;
            string instanceID = GetOptionValue(node.GetNodeOptionByName("Instance ID"), string.Empty);
            CharacterEmotion emotion = GetOptionValue(node.GetNodeOptionByName("Emotion"), CharacterEmotion.Neutral);
            switch (node)
            {
                case ShowCharacterNode _:
                    return new RuntimeShowCharacterNode { Character = character, InstanceID = instanceID, Emotion = emotion,
                        CharacterValue = BuildExpression(node.GetInputPortByName("Character")),
                        CharacterReferenceValue = BuildExpression(node.GetInputPortByName("Character Reference")),
                        Position = GetOptionValue(node.GetNodeOptionByName("Position"), Vector2.zero),
                        PositionSpace = GetOptionValue(node.GetNodeOptionByName("Coordinate Space"), CharacterPositionSpace.Canvas) };
                case HideCharacterNode _:
                    return new RuntimeHideCharacterNode { Character = character, InstanceID = instanceID,
                        CharacterValue = BuildExpression(node.GetInputPortByName("Character")),
                        CharacterReferenceValue = BuildExpression(node.GetInputPortByName("Character Reference")) };
                case HideAllCharactersNode _: return new RuntimeHideAllCharactersNode();
                case SetCharacterEmotionNode _:
                    return new RuntimeSetCharacterEmotionNode { Character = character, InstanceID = instanceID, Emotion = emotion,
                        CharacterValue = BuildExpression(node.GetInputPortByName("Character")),
                        CharacterReferenceValue = BuildExpression(node.GetInputPortByName("Character Reference")) };
                case WaitNode _:
                    return new RuntimeWaitNode { Duration = Mathf.Max(0f, GetOptionValue(node.GetNodeOptionByName("Duration"), 1f)) };
                case CheckpointNode _:
                    string checkpointID = GetPortValue<string>(node.GetInputPortByName("Checkpoint ID"))?.Trim();
                    if (string.IsNullOrEmpty(checkpointID))
                        _context?.LogImportWarning("Checkpoint has an empty Checkpoint ID; it cannot be used as a migration fallback.");
                    NovelCheckpointSaveMode saveMode = GetOptionValue(
                        node.GetNodeOptionByName("Save Mode"), NovelCheckpointSaveMode.SnapshotOnly);
                    string autosaveSlot = GetOptionValue(node.GetNodeOptionByName("Autosave Slot"), "autosave")?.Trim();
                    if (saveMode == NovelCheckpointSaveMode.Autosave && !NovelSaveStorage.IsValidSlotID(autosaveSlot))
                        _context?.LogImportError("Checkpoint Autosave Slot must use only letters, numbers, '-' or '_', up to 64 characters.");
                    return new RuntimeCheckpointNode
                    {
                        CheckpointID = checkpointID ?? string.Empty,
                        SaveMode = saveMode,
                        AutosaveSlotID = string.IsNullOrEmpty(autosaveSlot) ? "autosave" : autosaveSlot
                    };
                case DialogueEventNode _:
                    return new RuntimeDialogueEventNode { EventName = GetOptionValue(node.GetNodeOptionByName("Event Name"), string.Empty) };
                case StopSoundNode _: return new RuntimeStopSoundNode();
                case SetNovelVariableNode setVariable:
                {
                    NovelVariableDefinition variable = GetPortValue<NovelVariableDefinition>(setVariable.GetInputPortByName("Variable"));
                    ValidateVariablePort(variable, GetOptionValue(setVariable.GetNodeOptionByName("Value Type"), NovelVariableType.Boolean), "Set Variable");
                    return new RuntimeSetVariableNode
                    {
                        Variable = variable,
                        Value = BuildExpression(setVariable.GetInputPortByName("Value"))
                    };
                }
                case ModifyNovelVariableNode modifyVariable:
                {
                    NovelVariableDefinition variable = GetPortValue<NovelVariableDefinition>(modifyVariable.GetInputPortByName("Variable"));
                    NovelVariableType expectedType = GetOptionValue(modifyVariable.GetNodeOptionByName("Value Type"), NovelNumericType.Integer) == NovelNumericType.Integer
                        ? NovelVariableType.Integer
                        : NovelVariableType.Float;
                    ValidateVariablePort(variable, expectedType, "Modify Variable");
                    return new RuntimeModifyVariableNode
                    {
                        Variable = variable,
                        Operation = GetOptionValue(modifyVariable.GetNodeOptionByName("Operation"), RuntimeVariableModifyOperation.Add),
                        Amount = BuildExpression(modifyVariable.GetInputPortByName("Amount"))
                    };
                }
                case CallNovelPageNode call:
                    RuntimeNovelGraph calledGraph = GetPortValue<RuntimeNovelGraph>(
                        call.GetInputPortByName(CallNovelPageNode.GraphPortName));
                    if (calledGraph == null)
                        _context?.LogImportWarning("Call Novel Page needs a Novel Graph input.");
                    else if (AssetDatabase.GetAssetPath(calledGraph) == _context?.assetPath)
                        _context?.LogImportWarning("Call Novel Page references its own graph. Runtime recursion is limited, but this is usually accidental.");
                    return new RuntimeCallNovelPageNode { Graph = calledGraph };
                default: return new RuntimeNode();
            }
        }

        private void ProcessFunctionInterface(RuntimeNovelFunction function)
        {
            foreach (IVariable variable in _editorGraph.GetVariables())
            {
                if (variable.VariableKind == VariableKind.Input && variable.Name != NovelFunctionGraph.EnterVariableName)
                {
                    function.Inputs.Add(new RuntimeFunctionInput
                    {
                        Name = variable.Name,
                        DefaultValue = ConstantFromVariable(variable).Value
                    });
                }
                else if (variable.VariableKind == VariableKind.Output && variable.Name != NovelFunctionGraph.ContinueVariableName)
                {
                    var nodes = new List<IVariableNode>();
                    variable.GetNodes(nodes);
                    IPort valuePort = nodes.SelectMany(node => node.GetInputPorts()).FirstOrDefault();
                    function.Outputs.Add(new RuntimeFunctionOutput
                    {
                        Name = variable.Name,
                        Value = valuePort != null ? BuildExpression(valuePort) : ConstantFromVariable(variable)
                    });
                }
            }
        }

        private RuntimeCallNovelFunctionNode ProcessFunctionCallNode(
            INode node,
            NovelFunctionGraph functionGraph,
            Dictionary<INode, string> nodeIDMap)
        {
            string path = AssetDatabase.GUIDToAssetPath(functionGraph.AssetGuid.ToString());
            if (!string.IsNullOrEmpty(path))
                _context?.DependsOnSourceAsset(path);

            RuntimeNovelFunction function = AssetDatabase.LoadAssetAtPath<RuntimeNovelFunction>(path);
            if (function == null)
                _context?.LogImportWarning($"Novel Function '{functionGraph.Name}' has not produced runtime data yet. Reimport it, then reimport this graph.");

            IVariable continueVariable = functionGraph.GetVariables().FirstOrDefault(variable =>
                variable.VariableKind == VariableKind.Output &&
                variable.Name == NovelFunctionGraph.ContinueVariableName);

            var runtimeNode = new RuntimeCallNovelFunctionNode
            {
                NodeID = nodeIDMap[node],
                Function = function,
                NextNodeID = GetDestinationID(GetSubgraphPort(node, continueVariable, PortDirection.Output), nodeIDMap)
            };

            foreach (IVariable variable in functionGraph.GetVariables())
            {
                if (variable.VariableKind != VariableKind.Input || variable.Name == NovelFunctionGraph.EnterVariableName)
                    continue;
                runtimeNode.Arguments.Add(new RuntimeFunctionArgument
                {
                    Name = variable.Name,
                    Value = BuildExpression(GetSubgraphPort(node, variable, PortDirection.Input))
                });
            }

            return runtimeNode;
        }

        private static bool IsValueNode(INode node) =>
            node is FloatBinaryNode || node is Vector2BinaryNode || node is SplitNovelCharacterNode ||
            node is MakeNovelCharacterReferenceNode || node is SplitNovelCharacterReferenceNode ||
            node is GetNovelVariableNode || node is CompareNovelValuesNode ||
            node is AndNovelValuesNode || node is OrNovelValuesNode || node is NotNovelValueNode;

        private void ValidateVariablePort(NovelVariableDefinition variable, NovelVariableType expectedType, string nodeName)
        {
            if (variable == null)
            {
                _context?.LogImportError($"{nodeName} needs a Variable definition.");
                return;
            }
            if (variable.Type != expectedType)
                _context?.LogImportError($"{nodeName} is configured for {expectedType}, but variable '{variable.Name}' is {variable.Type}.");
        }

        private static bool IsMissingConstant(RuntimeValueExpression expression) =>
            expression is RuntimeConstantExpression constant &&
            (constant.Value == null || constant.Value.Kind == RuntimeValueKind.None || constant.Value.ObjectValue == null);

        private static bool IsMissingCharacterReference(RuntimeValueExpression expression) =>
            expression is RuntimeConstantExpression constant &&
            (constant.Value == null || constant.Value.Kind == RuntimeValueKind.None ||
             (constant.Value.Kind == RuntimeValueKind.CharacterReference &&
              constant.Value.CharacterReferenceValue.Character == null));

        private RuntimeValueExpression BuildExpression(IPort input) =>
            BuildExpression(input, new HashSet<IPort>());

        private RuntimeValueExpression BuildExpression(IPort port, HashSet<IPort> activePath)
        {
            if (port == null)
                return new RuntimeConstantExpression { Value = RuntimeValue.None() };

            if (!activePath.Add(port))
            {
                INode cycleNode = port.GetNode();
                string message =
                    $"Cyclic value expression detected at '{cycleNode?.Title ?? cycleNode?.GetType().Name ?? "unknown node"}' port '{port.Name}'.";
                string diagnosticKey = _expressionDiagnosticIdentity + "|" + message;
                if ((_expressionDiagnostics == null || _expressionDiagnostics.Add(message)) &&
                    LoggedExpressionDiagnostics.Add(diagnosticKey))
                    _context?.LogImportError(message);
                return new RuntimeConstantExpression { Value = RuntimeValue.None() };
            }

            try
            {
                return BuildExpressionOnActivePath(port, activePath);
            }
            finally
            {
                activePath.Remove(port);
            }
        }

        private RuntimeValueExpression BuildExpressionOnActivePath(IPort port, HashSet<IPort> activePath)
        {

            if (port.Direction == PortDirection.Input)
            {
                var connected = new List<IPort>();
                port.GetConnectedPorts(connected);
                if (connected.Count > 0)
                    return BuildExpression(connected[0], activePath);
                return ConstantFromPort(port);
            }

            INode node = port.GetNode();
            if (node is IVariableNode variableNode)
            {
                return variableNode.Variable.VariableKind == VariableKind.Input
                    ? new RuntimeFunctionInputExpression { Name = variableNode.Variable.Name }
                    : ConstantFromVariable(variableNode.Variable);
            }

            if (node is FloatBinaryNode || node is Vector2BinaryNode)
            {
                return new RuntimeArithmeticExpression
                {
                    Operation = GetArithmeticOperation(node),
                    ValueKind = node is FloatBinaryNode ? RuntimeValueKind.Float : RuntimeValueKind.Vector2,
                    A = BuildExpression(node.GetInputPortByName("A"), activePath),
                    B = BuildExpression(node.GetInputPortByName("B"), activePath)
                };
            }

            if (node is GetNovelVariableNode getVariable)
            {
                NovelVariableDefinition variable = GetPortValue<NovelVariableDefinition>(getVariable.GetInputPortByName("Variable"));
                NovelVariableType expectedType = GetOptionValue(getVariable.GetNodeOptionByName("Value Type"), NovelVariableType.Boolean);
                ValidateVariablePort(variable, expectedType, "Get Variable");
                return new RuntimeVariableExpression { Variable = variable };
            }

            if (node is CompareNovelValuesNode compare)
            {
                NovelVariableType valueType = GetOptionValue(compare.GetNodeOptionByName("Value Type"), NovelVariableType.Boolean);
                RuntimeComparisonOperation operation = GetOptionValue(compare.GetNodeOptionByName("Operator"), RuntimeComparisonOperation.Equal);
                if ((valueType == NovelVariableType.Boolean || valueType == NovelVariableType.String) &&
                    operation != RuntimeComparisonOperation.Equal && operation != RuntimeComparisonOperation.NotEqual)
                    _context?.LogImportError($"Compare supports only Equal and Not Equal for {valueType} values.");
                return new RuntimeComparisonExpression
                {
                    Operation = operation,
                    ValueKind = RuntimeKind(valueType),
                    A = BuildExpression(compare.GetInputPortByName("A"), activePath),
                    B = BuildExpression(compare.GetInputPortByName("B"), activePath)
                };
            }

            if (node is AndNovelValuesNode || node is OrNovelValuesNode || node is NotNovelValueNode)
            {
                RuntimeBooleanOperation operation = node is AndNovelValuesNode
                    ? RuntimeBooleanOperation.And
                    : node is OrNovelValuesNode ? RuntimeBooleanOperation.Or : RuntimeBooleanOperation.Not;
                string firstPort = node is NotNovelValueNode ? "Value" : "A";
                return new RuntimeBooleanExpression
                {
                    Operation = operation,
                    A = BuildExpression(node.GetInputPortByName(firstPort), activePath),
                    B = operation == RuntimeBooleanOperation.Not
                        ? null
                        : BuildExpression(node.GetInputPortByName("B"), activePath)
                };
            }

            if (node is SplitNovelCharacterNode split)
            {
                return new RuntimeCharacterComponentExpression
                {
                    Component = GetCharacterComponent(port.Name),
                    Character = BuildExpression(split.GetInputPortByName("Character"), activePath),
                    InstanceID = BuildExpression(split.GetInputPortByName("Instance ID"), activePath)
                };
            }

            if (node is MakeNovelCharacterReferenceNode makeReference)
            {
                return new RuntimeMakeCharacterReferenceExpression
                {
                    Character = BuildExpression(makeReference.GetInputPortByName("Character"), activePath),
                    InstanceID = BuildExpression(makeReference.GetInputPortByName("Instance ID"), activePath)
                };
            }

            if (node is SplitNovelCharacterReferenceNode splitReference)
            {
                return new RuntimeCharacterReferenceComponentExpression
                {
                    Component = port.Name == "Instance ID"
                        ? RuntimeCharacterReferenceComponent.InstanceID
                        : RuntimeCharacterReferenceComponent.Character,
                    Reference = BuildExpression(splitReference.GetInputPortByName("Character Reference"), activePath)
                };
            }

            if (node is ISubgraphNode subgraphNode &&
                node is not CallNovelPageNode &&
                subgraphNode.GetSubgraph() is NovelFunctionGraph functionGraph)
            {
                IVariable outputVariable = functionGraph.GetVariables().FirstOrDefault(variable =>
                    variable.VariableKind == VariableKind.Output &&
                    (variable.ID.ToString() == port.Name || variable.Name == port.DisplayName));
                return new RuntimeFunctionOutputExpression
                {
                    CallNodeID = _nodeIDMap.TryGetValue(node, out string id) ? id : string.Empty,
                    Name = outputVariable?.Name ?? port.DisplayName ?? port.Name
                };
            }

            if (node is DialogueNode && port.Name == "Current Speaker Reference")
                return BuildEffectiveCharacterReference(node, "Speaker", "Speaker Reference", activePath);
            if (node is CharacterActionNode && port.Name == "Character Reference")
                return BuildEffectiveCharacterReference(node, "Character", "Character Reference", activePath);

            if (node is DialogueNode && port.Name == "Current Speaker" &&
                node.GetInputPortByName("Speaker Reference")?.IsConnected == true)
                return CharacterFromReference(node.GetInputPortByName("Speaker Reference"), activePath);
            if (node is CharacterActionNode && port.Name == "Character" &&
                node.GetInputPortByName("Character Reference")?.IsConnected == true)
                return CharacterFromReference(node.GetInputPortByName("Character Reference"), activePath);

            string passThrough = node is DialogueNode && port.Name == "Current Speaker" ? "Speaker" :
                node is CharacterActionNode && port.Name == "Character" ? "Character" : null;
            return passThrough != null
                ? BuildExpression(node.GetInputPortByName(passThrough), activePath)
                : new RuntimeConstantExpression { Value = RuntimeValue.None() };
        }

        private RuntimeValueExpression CharacterFromReference(IPort referencePort, HashSet<IPort> activePath) =>
            new RuntimeCharacterReferenceComponentExpression
            {
                Component = RuntimeCharacterReferenceComponent.Character,
                Reference = BuildExpression(referencePort, activePath)
            };

        private RuntimeValueExpression BuildEffectiveCharacterReference(
            INode node,
            string characterPortName,
            string referencePortName,
            HashSet<IPort> activePath)
        {
            IPort referencePort = node.GetInputPortByName(referencePortName);
            if (referencePort?.IsConnected == true)
                return BuildExpression(referencePort, activePath);

            return new RuntimeMakeCharacterReferenceExpression
            {
                Character = BuildExpression(node.GetInputPortByName(characterPortName), activePath),
                InstanceID = Constant(GetOptionValue(node.GetNodeOptionByName("Instance ID"), string.Empty))
            };
        }

        private static IPort GetSubgraphPort(INode node, IVariable variable, PortDirection direction)
        {
            if (node == null || variable == null)
                return null;

            string portID = variable.ID.ToString();
            IPort exact = direction == PortDirection.Input
                ? node.GetInputPortByName(portID)
                : node.GetOutputPortByName(portID);
            if (exact != null)
                return exact;

            IEnumerable<IPort> ports = direction == PortDirection.Input
                ? node.GetInputPorts()
                : node.GetOutputPorts();
            return ports.FirstOrDefault(port =>
                string.Equals(port.DisplayName, variable.Name, StringComparison.Ordinal) ||
                string.Equals(port.Name, variable.Name, StringComparison.Ordinal));
        }

        private static RuntimeArithmeticOperation GetArithmeticOperation(INode node)
        {
            if (node is SubtractFloatNode || node is SubtractVector2Node) return RuntimeArithmeticOperation.Subtract;
            if (node is MultiplyFloatNode || node is MultiplyVector2Node) return RuntimeArithmeticOperation.Multiply;
            if (node is DivideFloatNode || node is DivideVector2Node) return RuntimeArithmeticOperation.Divide;
            return RuntimeArithmeticOperation.Add;
        }

        private static RuntimeValueKind RuntimeKind(NovelVariableType type) => type switch
        {
            NovelVariableType.Boolean => RuntimeValueKind.Boolean,
            NovelVariableType.Integer => RuntimeValueKind.Integer,
            NovelVariableType.Float => RuntimeValueKind.Float,
            NovelVariableType.String => RuntimeValueKind.String,
            _ => RuntimeValueKind.None
        };

        private static RuntimeCharacterComponent GetCharacterComponent(string portName)
        {
            switch (portName)
            {
                case "Speaker Name": return RuntimeCharacterComponent.SpeakerName;
                case "Body": return RuntimeCharacterComponent.Body;
                case "Eyes": return RuntimeCharacterComponent.Eyes;
                case "Eyes Closed": return RuntimeCharacterComponent.EyesClosed;
                case "Details": return RuntimeCharacterComponent.Details;
                case "Mouth": return RuntimeCharacterComponent.Mouth;
                case "Mouth Open": return RuntimeCharacterComponent.MouthOpen;
                case "Position (Normalized)": return RuntimeCharacterComponent.NormalizedPosition;
                case "Position (Canvas)": return RuntimeCharacterComponent.CanvasPosition;
                case "Rotation": return RuntimeCharacterComponent.Rotation;
                case "Scale": return RuntimeCharacterComponent.Scale;
                default: return RuntimeCharacterComponent.Character;
            }
        }

        private static RuntimeConstantExpression ConstantFromPort(IPort port)
        {
            if (port == null) return new RuntimeConstantExpression { Value = RuntimeValue.None() };
            if (port.DataType == typeof(float) && port.TryGetValue(out float number)) return Constant(number);
            if (port.DataType == typeof(int) && port.TryGetValue(out int integer)) return Constant(integer);
            if (port.DataType == typeof(bool) && port.TryGetValue(out bool boolean)) return Constant(boolean);
            if (port.DataType == typeof(string) && port.TryGetValue(out string text)) return Constant(text);
            if (port.DataType == typeof(Vector2) && port.TryGetValue(out Vector2 vector)) return Constant(vector);
            if (port.DataType == typeof(NovelCharacterReference) && port.TryGetValue(out NovelCharacterReference reference)) return Constant(reference);
            if (port.DataType == typeof(NovelCharacter) && port.TryGetValue(out NovelCharacter character)) return Constant(character);
            if (port.DataType == typeof(Sprite) && port.TryGetValue(out Sprite sprite)) return Constant(sprite);
            if (port.DataType == typeof(AudioClip) && port.TryGetValue(out AudioClip audio)) return Constant(audio);
            return new RuntimeConstantExpression { Value = RuntimeValue.None() };
        }

        private static RuntimeConstantExpression ConstantFromVariable(IVariable variable)
        {
            if (variable.DataType == typeof(float) && variable.TryGetDefaultValue(out float number)) return Constant(number);
            if (variable.DataType == typeof(int) && variable.TryGetDefaultValue(out int integer)) return Constant(integer);
            if (variable.DataType == typeof(bool) && variable.TryGetDefaultValue(out bool boolean)) return Constant(boolean);
            if (variable.DataType == typeof(string) && variable.TryGetDefaultValue(out string text)) return Constant(text);
            if (variable.DataType == typeof(Vector2) && variable.TryGetDefaultValue(out Vector2 vector)) return Constant(vector);
            if (variable.DataType == typeof(NovelCharacterReference) && variable.TryGetDefaultValue(out NovelCharacterReference reference)) return Constant(reference);
            if (variable.DataType == typeof(NovelCharacter) && variable.TryGetDefaultValue(out NovelCharacter character)) return Constant(character);
            if (variable.DataType == typeof(Sprite) && variable.TryGetDefaultValue(out Sprite sprite)) return Constant(sprite);
            if (variable.DataType == typeof(AudioClip) && variable.TryGetDefaultValue(out AudioClip audio)) return Constant(audio);
            return new RuntimeConstantExpression { Value = RuntimeValue.None() };
        }

        private static RuntimeConstantExpression Constant(float value) => new RuntimeConstantExpression { Value = RuntimeValue.From(value) };
        private static RuntimeConstantExpression Constant(int value) => new RuntimeConstantExpression { Value = RuntimeValue.From(value) };
        private static RuntimeConstantExpression Constant(bool value) => new RuntimeConstantExpression { Value = RuntimeValue.From(value) };
        private static RuntimeConstantExpression Constant(string value) => new RuntimeConstantExpression { Value = RuntimeValue.From(value) };
        private static RuntimeConstantExpression Constant(Vector2 value) => new RuntimeConstantExpression { Value = RuntimeValue.From(value) };
        private static RuntimeConstantExpression Constant(NovelCharacterReference value) => new RuntimeConstantExpression { Value = RuntimeValue.From(value) };
        private static RuntimeConstantExpression Constant(UnityEngine.Object value) => new RuntimeConstantExpression { Value = RuntimeValue.From(value) };

        private T GetFirstPortValue<T>(
            INode node,
            params string[] portNames)
        {
            foreach (string portName in portNames)
            {
                IPort port =
                    node.GetInputPortByName(portName);

                if (port == null)
                {
                    continue;
                }

                T value = GetPortValue<T>(port);

                if (value != null)
                {
                    return value;
                }
            }

            return default;
        }

        private T GetFirstOptionValue<T>(
            INode node,
            params string[] optionNames)
        {
            foreach (string optionName in optionNames)
            {
                INodeOption option =
                    node.GetNodeOptionByName(optionName);

                if (option == null)
                {
                    continue;
                }

                if (option.TryGetValue(out T value))
                {
                    return value;
                }
            }

            return default;
        }

        private void SetSpeaker(
            INode node,
            RuntimeDialogueNode runtimeNode)
        {
            NovelCharacter character =
                GetPortValue<NovelCharacter>(
                    node.GetInputPortByName("Speaker"));

            runtimeNode.NovelCharacter = character;
            runtimeNode.CharacterValue = BuildExpression(node.GetInputPortByName("Speaker"));
            runtimeNode.CharacterReferenceValue = BuildExpression(node.GetInputPortByName("Speaker Reference"));
            runtimeNode.InstanceID = GetOptionValue(node.GetNodeOptionByName("Instance ID"), string.Empty);

            runtimeNode.SpeakerName =
                character != null
                    ? character.SpeakerName
                    : string.Empty;

            runtimeNode.PortraitBody =
                character != null
                    ? character.PortraitBody
                    : null;

            runtimeNode.PortraitEyes =
                character != null
                    ? character.PortraitEyes
                    : null;

            runtimeNode.PortraitEyesClosed =
                character != null
                    ? character.PortraitEyesClosed
                    : null;

            runtimeNode.PortraitDetails =
                character != null
                    ? character.PortraitFaceDetails
                    : null;

            runtimeNode.PortraitMouth =
                character != null
                    ? character.PortraitMouth
                    : null;

            runtimeNode.PortraitMouthOpen =
                character != null
                    ? character.PortraitMouthOpen
                    : null;

            runtimeNode.MouthFrameInterval =
                character != null
                    ? character.MouthFrameInterval
                    : 0.12f;

            runtimeNode.MouthTimingVariation =
                character != null
                    ? character.MouthTimingVariation
                    : 0.35f;

            runtimeNode.MouthPauseChance =
                character != null
                    ? character.MouthPauseChance
                    : 0.12f;

            runtimeNode.MouthPauseMultiplier =
                character != null
                    ? character.MouthPauseMultiplier
                    : 1.8f;

            runtimeNode.BlinkIntervalMin =
                character != null
                    ? character.BlinkIntervalMin
                    : 2.5f;

            runtimeNode.BlinkIntervalMax =
                character != null
                    ? character.BlinkIntervalMax
                    : 5f;

            runtimeNode.BlinkDuration =
                character != null
                    ? character.BlinkDuration
                    : 0.12f;

            runtimeNode.TalkSound =
                character != null
                    ? character.TalkSound
                    : null;

            runtimeNode.PitchMinVariation =
                character != null
                    ? character.PitchMinVariation
                    : 0f;

            runtimeNode.PitchMaxVariation =
                character != null
                    ? character.PitchMaxVariation
                    : 0f;
        }

        private void SetPresentationOptions(
            INode node,
            RuntimeDialogueNode runtimeNode)
        {
            runtimeNode.PlaySound =
                GetPortValue<AudioClip>(
                    node.GetInputPortByName(
                        SimpleDialogueNode.SoundPortName));
            runtimeNode.PlaySoundValue = BuildExpression(node.GetInputPortByName(SimpleDialogueNode.SoundPortName));

            RichDialogueText dialogue =
                GetOptionValue(
                    node.GetNodeOptionByName("Dialogue"),
                    new RichDialogueText(string.Empty));

            runtimeNode.DialogueText =
                dialogue.Text ?? string.Empty;

            runtimeNode.Emotion =
                GetOptionValue(
                    node.GetNodeOptionByName("Emotion"),
                    CharacterEmotion.Neutral);

            runtimeNode.ShowTextImmediately =
                GetOptionValue(
                    node.GetNodeOptionByName(
                        "Show Text Immediately"),
                    false);

            runtimeNode.CharactersPerSecond =
                Mathf.Max(
                    1f,
                    GetOptionValue(
                        node.GetNodeOptionByName(
                            "Text Speed (Characters/Second)"),
                        30f));

            runtimeNode.AnimateMouth =
                GetOptionValue(
                    node.GetNodeOptionByName("Animate Mouth"),
                    true);

            runtimeNode.AnimateBlinking =
                GetOptionValue(
                    node.GetNodeOptionByName(
                        "Animate Blinking"),
                    true);
        }

        private T GetPortValue<T>(IPort port)
        {
            T value = NovelGraphValues.Resolve<T>(_editorGraph, port);
            if (value is UnityEngine.Object asset && asset != null)
            {
                string path = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(path)) _context?.DependsOnSourceAsset(path);
            }
            return value;
        }

        private T GetOptionValue<T>(
            INodeOption option,
            T fallbackValue = default)
        {
            if (option == null)
            {
                return fallbackValue;
            }

            return option.TryGetValue(out T value)
                ? value
                : fallbackValue;
        }
    }

    [ScriptedImporter(6, NovelFunctionGraph.AssetExtension)]
    public class NovelFunctionGraphImporter : NovelGraphImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            NovelFunctionGraph editorGraph =
                GraphDatabase.LoadGraphForImporter<NovelFunctionGraph>(ctx.assetPath);
            ImportGraph(ctx, editorGraph, ScriptableObject.CreateInstance<RuntimeNovelFunction>());
        }
    }
}
