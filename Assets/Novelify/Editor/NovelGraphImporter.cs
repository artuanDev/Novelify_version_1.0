using UnityEngine;
using UnityEditor.AssetImporters;
using Unity.GraphToolkit.Editor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Novelify.Editor
{
    [ScriptedImporter(1, NovelGraph.AssetExtension)]
    public class NovelGraphImporter : ScriptedImporter
    {
        private NovelGraph _editorGraph;

        /*This method runs whenever an asset of th specified extension is imported,
        meaning it runs whenever we save the novelgraph editor or create it.
        */
        public override void OnImportAsset(AssetImportContext ctx)
        {
            //Because this is an editor script we can actually access the editor graph
            NovelGraph editorGraph = GraphDatabase.LoadGraphForImporter<NovelGraph>(ctx.assetPath);
            _editorGraph = editorGraph;

            //We can then create an instance of the runtime novelgraph here since its a scriptable object
            RuntimeNovelGraph runtimeGraph = ScriptableObject.CreateInstance<RuntimeNovelGraph>();

            var nodeIDMap = new Dictionary<INode, string>();

            //We loop through all the nodes and give each one an ID
            foreach (var node in editorGraph.GetNodes())
            {
                nodeIDMap[node] = Guid.NewGuid().ToString();
            }

            //Then we access to our start node to start reading the graph
            var startNode = editorGraph.GetNodes().OfType<StartNode>().FirstOrDefault();

            if(startNode != null)
            {
                var entryPort = startNode.GetOutputPorts().FirstOrDefault()?.FirstConnectedPort;

                if(entryPort != null)
                {
                    runtimeGraph.EntryNodeID = nodeIDMap[entryPort.GetNode()];
                }
            }

            foreach (var iNode in editorGraph.GetNodes())
            {
                /*If the node is a start node or endnode there is no need to proccess it,
                so skip them*/
                if (iNode is StartNode || iNode is EndNode) continue;

                var runtimeNode = new RuntimeDialogueNode { NodeID = nodeIDMap[iNode] };

                //If the current node being used is dialogue node, then process the dialogue node
                if(iNode is DialogueNode dialogueNode)
                {
                    ProcessDialogueNode(dialogueNode, runtimeNode, nodeIDMap);
                }
                else if(iNode is ChoiceNode choiceNode)
                {
                    ProcessChoiceNode(choiceNode, runtimeNode, nodeIDMap);
                }
                //you need to add the current runtime node to the list, if not they won´t be detected
                runtimeGraph.AllNodes.Add(runtimeNode);
            }

            //This makes it possible to drag and drop our graph into an inspector field that asks for it.
            ctx.AddObjectToAsset("RuntimeData", runtimeGraph);
            ctx.SetMainObject(runtimeGraph);
        }

        //Helper function that will process the node dialogue
        private void ProcessDialogueNode(DialogueNode node, RuntimeDialogueNode runtimeNode, Dictionary<INode, string> nodeIDMap)
        {
            SetSpeaker(node, runtimeNode);
            SetPresentationOptions(node, runtimeNode);

            //We get the next node until the chain is over
            var nextNodePort = node.GetOutputPortByName("out").FirstConnectedPort;

            if(nextNodePort != null)
            {
                runtimeNode.NextNodeID = nodeIDMap[nextNodePort.GetNode()];
            }
        }

        private void ProcessChoiceNode(ChoiceNode node, RuntimeDialogueNode runtimeNode, Dictionary<INode, string> nodeIDMap)
        {
            SetSpeaker(node, runtimeNode);
            SetPresentationOptions(node, runtimeNode);
            
            //we get the choices option via checking if they start with "Choice"
            var choiceOutputPorts = node.GetOutputPorts().Where(p => p.Name.StartsWith("Choice "));

            foreach (var outputPort in choiceOutputPorts)
            {
                var index = outputPort.Name.Substring("Choice ".Length);
                var textPort = node.GetInputPortByName($"Choice Text {index}");

                var choiceData = new ChoiceData
                {
                    ChoiceText = GetPortValue<string>(textPort),
                    DestinationNodeID = outputPort.FirstConnectedPort != null ? nodeIDMap[outputPort.FirstConnectedPort.GetNode()] : null
                };

                runtimeNode.Choices.Add(choiceData);
            }
        }

        private void SetSpeaker(INode node, RuntimeDialogueNode runtimeNode)
        {
            NovelCharacter character = GetPortValue<NovelCharacter>(
                node.GetInputPortByName("Speaker"));

            runtimeNode.SpeakerName = character != null ? character.SpeakerName : string.Empty;
            runtimeNode.PortraitBody = character != null ? character.PortraitBody : null;
            runtimeNode.PortraitEyes = character != null ? character.PortraitEyes : null;
            runtimeNode.PortraitEyesClosed = character != null ? character.PortraitEyesClosed : null;
            runtimeNode.PortraitDetails = character != null ? character.PortraitFaceDetails : null;
            runtimeNode.PortraitMouth = character != null ? character.PortraitMouth : null;
            runtimeNode.PortraitMouthOpen = character != null ? character.PortraitMouthOpen : null;
            runtimeNode.MouthFrameInterval = character != null ? character.MouthFrameInterval : 0.12f;
            runtimeNode.MouthTimingVariation = character != null ? character.MouthTimingVariation : 0.35f;
            runtimeNode.MouthPauseChance = character != null ? character.MouthPauseChance : 0.12f;
            runtimeNode.MouthPauseMultiplier = character != null ? character.MouthPauseMultiplier : 1.8f;
            runtimeNode.BlinkIntervalMin = character != null ? character.BlinkIntervalMin : 2.5f;
            runtimeNode.BlinkIntervalMax = character != null ? character.BlinkIntervalMax : 5f;
            runtimeNode.BlinkDuration = character != null ? character.BlinkDuration : 0.12f;
            runtimeNode.TalkSound = character != null ? character.TalkSound : null;
            runtimeNode.PitchMinVariation = character != null ? character.PitchMinVariation : 0;
            runtimeNode.PitchMaxVariation = character != null ? character.PitchMaxVariation : 0;
        }

        private void SetPresentationOptions(INode node, RuntimeDialogueNode runtimeNode)
        {
            runtimeNode.DialogueText = GetOptionValue(
                node.GetNodeOptionByName("Dialogue"), string.Empty);
            runtimeNode.Emotion = GetOptionValue(
                node.GetNodeOptionByName("Emotion"), CharacterEmotion.Neutral);
            runtimeNode.ShowTextImmediately = GetOptionValue(
                node.GetNodeOptionByName("Show Text Immediately"), false);
            runtimeNode.CharactersPerSecond = Mathf.Max(1f, GetOptionValue(
                node.GetNodeOptionByName("Text Speed (Characters/Second)"), 30f));
            runtimeNode.AnimateMouth = GetOptionValue(
                node.GetNodeOptionByName("Animate Mouth"), true);
            runtimeNode.AnimateBlinking = GetOptionValue(
                node.GetNodeOptionByName("Animate Blinking"), true);
        }

        //Get the port value no matter which type is it
        private T GetPortValue<T>(IPort port)
        {
            if (port == null) return default;

            //if a variable is connected to the port, take that node information
            if(port.FirstConnectedPort?.GetNode() is IVariableNode variableNode)
            {
                variableNode.Variable.TryGetDefaultValue(out T value);
                return value;
            }

            // Resolve variables routed through Graph Toolkit portals as well as direct wires.
            if (_editorGraph != null)
            {
                foreach (IVariable variable in _editorGraph.GetVariables())
                {
                    if (!variable.TryGetDefaultValue(out T portalValue))
                    {
                        continue;
                    }

                    var variableNodes = new List<IVariableNode>();
                    variable.GetNodes(variableNodes);

                    foreach (IVariableNode variableReferenceNode in variableNodes)
                    {
                        foreach (IPort outputPort in variableReferenceNode.GetOutputPorts())
                        {
                            if (_editorGraph.GetWire(outputPort, port) != null)
                            {
                                return portalValue;
                            }
                        }
                    }
                }
            }

            //If no variable is connected, then get the typed value
            port.TryGetValue(out T fallbackValue);
            return fallbackValue;
        }

        //Get the option value no matter which type is it
        private T GetOptionValue<T>(INodeOption option, T fallbackValue = default)
        {
            if (option == null) return fallbackValue;

            /*Options don´t have connections and must be manually edited, so no need to access
            any node variable here*/

            return option.TryGetValue(out T value) ? value : fallbackValue;
        }
    }
}
