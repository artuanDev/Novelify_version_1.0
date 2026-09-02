using UnityEngine;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace Novelify.Editor
{
    [Serializable]
    [Graph(AssetExtension)]
    public class NovelGraph : Graph
    {
        public const string AssetExtension = "novelgraph";

        [NonSerialized] private bool _isEnabled;
        [NonSerialized] private bool _speakerPreviewSyncQueued;

        [MenuItem("Assets/Create/Novelify/Novel Graph", false)]
        private static void CreateAssetFile()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<NovelGraph>("NovelGraph");
        }

        public override void OnEnable()
        {
            base.OnEnable();
            _isEnabled = true;
            QueueSpeakerPreviewSynchronization();
        }

        public override void OnDisable()
        {
            _isEnabled = false;
            _speakerPreviewSyncQueued = false;
            EditorApplication.delayCall -= SynchronizeSpeakerPreviewsAfterGraphProcessing;
            base.OnDisable();
        }

        public override void OnGraphChanged(GraphLogger graphLogger)
        {
            base.OnGraphChanged(graphLogger);
            QueueSpeakerPreviewSynchronization();
        }

        private void QueueSpeakerPreviewSynchronization()
        {
            if (_speakerPreviewSyncQueued)
            {
                return;
            }

            _speakerPreviewSyncQueued = true;
            EditorApplication.delayCall += SynchronizeSpeakerPreviewsAfterGraphProcessing;
        }

        private void SynchronizeSpeakerPreviewsAfterGraphProcessing()
        {
            EditorApplication.delayCall -= SynchronizeSpeakerPreviewsAfterGraphProcessing;
            _speakerPreviewSyncQueued = false;

            if (!_isEnabled || !IsOpenInGraphWindow())
            {
                return;
            }

            SynchronizeSpeakerPreviews();
        }

        private bool IsOpenInGraphWindow()
        {
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window is IGraphWindow graphWindow &&
                    ReferenceEquals(graphWindow.Graph, this))
                {
                    return true;
                }
            }

            return false;
        }

        private void SynchronizeSpeakerPreviews()
        {
            bool isRecordingUndo = false;

            try
            {
                foreach (INode node in GetNodes())
                {
                    if (node is not DialogueNode && node is not ChoiceNode)
                    {
                        continue;
                    }

                    NovelCharacter character = GetSpeakerCharacter(node);
                    INodeOption previewOption = node.GetNodeOptionByName("Speaker Preview");

                    if (previewOption == null ||
                        !previewOption.TryGetValue(out SpeakerPortraitOption currentPreview) ||
                        currentPreview.Character == character)
                    {
                        continue;
                    }

                    if (!isRecordingUndo)
                    {
                        UndoBeginRecordGraph("Update Speaker Portrait Previews");
                        isRecordingUndo = true;
                    }

                    previewOption.TrySetValue(new SpeakerPortraitOption
                    {
                        Character = character
                    });
                }
            }
            finally
            {
                if (isRecordingUndo)
                {
                    UndoEndRecordGraph();
                }
            }
        }

        private NovelCharacter GetSpeakerCharacter(INode node)
        {
            IPort speakerPort = node.GetInputPortByName("Speaker");
            if (speakerPort == null)
            {
                return null;
            }

            IPort connectedPort = speakerPort.FirstConnectedPort;
            if (connectedPort?.GetNode() is IVariableNode variableNode &&
                variableNode.Variable.TryGetDefaultValue(out NovelCharacter variableCharacter))
            {
                return variableCharacter;
            }

            // GetWire also follows Graph Toolkit portals, so a Character variable can be
            // routed through a portal without losing the portrait preview.
            foreach (IVariable variable in GetVariables())
            {
                if (!variable.TryGetDefaultValue(out NovelCharacter portalCharacter))
                {
                    continue;
                }

                var variableNodes = new List<IVariableNode>();
                variable.GetNodes(variableNodes);

                foreach (IVariableNode variableReferenceNode in variableNodes)
                {
                    foreach (IPort outputPort in variableReferenceNode.GetOutputPorts())
                    {
                        if (GetWire(outputPort, speakerPort) != null)
                        {
                            return portalCharacter;
                        }
                    }
                }
            }

            speakerPort.TryGetValue(out NovelCharacter inlineCharacter);
            return inlineCharacter;
        }
    }
}
