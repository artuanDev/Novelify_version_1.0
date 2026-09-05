using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Novelify.Editor
{
    [ScriptedImporter(4, NovelGraph.AssetExtension)]
    public class NovelGraphImporter : ScriptedImporter
    {
        private NovelGraph _editorGraph;
        private AssetImportContext _context;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            _context = ctx;
            NovelGraph editorGraph =
                GraphDatabase.LoadGraphForImporter<NovelGraph>(
                    ctx.assetPath);

            _editorGraph = editorGraph;

            RuntimeNovelGraph runtimeGraph =
                ScriptableObject.CreateInstance<RuntimeNovelGraph>();

            var nodeIDMap = new Dictionary<INode, string>();

            foreach (INode node in editorGraph.GetNodes())
            {
                nodeIDMap[node] = Guid.NewGuid().ToString();
            }

            StartNode startNode =
                editorGraph.GetNodes()
                    .OfType<StartNode>()
                    .FirstOrDefault();

            if (startNode != null)
            {
                INode entryNode = NovelGraphValues.FlowDestination(editorGraph, startNode.GetOutputPortByName("out"));

                if (entryNode != null && nodeIDMap.ContainsKey(entryNode))
                {
                    runtimeGraph.EntryNodeID =
                        nodeIDMap[entryNode];
                }
            }

            foreach (INode editorNode in editorGraph.GetNodes())
            {
                if (editorNode is StartNode || editorNode is IVariableNode)
                {
                    continue;
                }

                RuntimeNode runtimeNode;

                if (editorNode is SimpleDialogueNode dialogueNode)
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
                else if (editorNode is TranslateSpeakerPortraitNode translateSpeakerPortraitNode)
                {
                    var runtimeTranslateSpeakerPortrait =
                        new RuntimeTranslateSpeakerPortraitNode
                        {
                            NodeID = nodeIDMap[editorNode]
                        };

                    ProcessTranslateSpeakerNode(
                        translateSpeakerPortraitNode,
                        runtimeTranslateSpeakerPortrait,
                        nodeIDMap);

                    runtimeNode = runtimeTranslateSpeakerPortrait;
                }
                else
                {
                    runtimeNode = CreateUtilityNode(editorNode);
                    runtimeNode.NodeID = nodeIDMap[editorNode];
                    runtimeNode.NextNodeID = GetNextNodeID(editorNode, nodeIDMap);
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

                var choiceData = new ChoiceData
                {
                    ChoiceText =
                        GetPortValue<string>(textPort),

                    DestinationNodeID = GetDestinationID(outputPort, nodeIDMap)
                };

                runtimeNode.Choices.Add(choiceData);
            }
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

            if (runtimeNode.ClipSound == null)
            {
                Debug.LogWarning(
                    "PlaySoundNode could not resolve an AudioClip " +
                    "from its AudioToPlay input.",
                    this);
            }

            runtimeNode.NextNodeID =
                GetNextNodeID(node, nodeIDMap);
        }

        private void ProcessTranslateSpeakerNode(
            TranslateSpeakerPortraitNode node,
            RuntimeTranslateSpeakerPortraitNode runtimeNode,
            Dictionary<INode, string> nodeIDMap)
        {
            runtimeNode.OffsetX = GetOptionValue(node.GetNodeOptionByName("OffsetX"), 0.0f);
            runtimeNode.OffsetY = GetOptionValue(node.GetNodeOptionByName("OffsetY"), 0.0f);
            runtimeNode.Character = GetPortValue<NovelCharacter>(node.GetInputPortByName("Character"));
            runtimeNode.InstanceID = GetOptionValue(node.GetNodeOptionByName("Instance ID"), string.Empty);
            runtimeNode.SmoothMovement = GetOptionValue(node.GetNodeOptionByName("Smooth Movement"), false);
            runtimeNode.Duration = Mathf.Max(0f, GetOptionValue(node.GetNodeOptionByName("Duration"), 0.5f));
            runtimeNode.WaitForCompletion = GetOptionValue(node.GetNodeOptionByName("Wait For Completion"), true);
            runtimeNode.EaseInOut = GetOptionValue(node.GetNodeOptionByName("Ease In Out"), true);
            runtimeNode.Relative = GetOptionValue(node.GetNodeOptionByName("Relative"), false);

            if (runtimeNode.Character == null)
                _context?.LogImportWarning("Translate Speaker Portrait needs a Character input. Assign a character asset or connect a Current Speaker output.");

            runtimeNode.NextNodeID =
                GetNextNodeID(node, nodeIDMap);
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
                        Position = GetOptionValue(node.GetNodeOptionByName("Position"), Vector2.zero) };
                case HideCharacterNode _:
                    return new RuntimeHideCharacterNode { Character = character, InstanceID = instanceID };
                case HideAllCharactersNode _: return new RuntimeHideAllCharactersNode();
                case SetCharacterEmotionNode _:
                    return new RuntimeSetCharacterEmotionNode { Character = character, InstanceID = instanceID, Emotion = emotion };
                case WaitNode _:
                    return new RuntimeWaitNode { Duration = Mathf.Max(0f, GetOptionValue(node.GetNodeOptionByName("Duration"), 1f)) };
                case DialogueEventNode _:
                    return new RuntimeDialogueEventNode { EventName = GetOptionValue(node.GetNodeOptionByName("Event Name"), string.Empty) };
                case StopSoundNode _: return new RuntimeStopSoundNode();
                default: return new RuntimeNode();
            }
        }

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
}
