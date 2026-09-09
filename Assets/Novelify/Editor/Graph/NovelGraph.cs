using UnityEngine;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Novelify.Editor
{
    [Serializable]
    [Graph(AssetExtension, GraphOptions.SupportsSubgraphs)]
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

            if (!_isEnabled || !SpeakerPreviewSynchronization.IsOpenInGraphWindow(this))
            {
                return;
            }

            SynchronizeSpeakerPreviews();
        }

        private void SynchronizeSpeakerPreviews()
        {
            SpeakerPreviewSynchronization.Synchronize(this);
        }
    }

    /// <summary>
    /// A reusable Novel Graph asset. Input and Output variables become ports on the
    /// function node that Graph Toolkit creates in a Novel Graph.
    /// </summary>
    [Serializable]
    [Graph(AssetExtension, GraphOptions.SupportsSubgraphs)]
    [Subgraph(typeof(NovelGraph))]
    public class NovelFunctionGraph : Graph
    {
        public const string AssetExtension = "novelfunction";
        public const string EnterVariableName = "Enter";
        public const string ContinueVariableName = "Continue";

        [NonSerialized] private bool _isEnabled;
        [NonSerialized] private bool _speakerPreviewSyncQueued;

        [MenuItem("Assets/Create/Novelify/Novel Function", false)]
        private static void CreateFunctionAssetFile()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Novel Function",
                "NovelFunction",
                AssetExtension,
                "Choose where to save the reusable Novel Function.");

            if (string.IsNullOrEmpty(path))
                return;

            NovelFunctionGraph graph = GraphDatabase.CreateGraph<NovelFunctionGraph>(path);
            if (graph == null)
                return;

            graph.UndoBeginRecordGraph("Initialize Novel Function");
            try
            {
                graph.EnsureFlowInterface();
            }
            finally
            {
                graph.UndoEndRecordGraph();
            }

            GraphDatabase.SaveGraph(graph);
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

            if (!_isEnabled || !SpeakerPreviewSynchronization.IsOpenInGraphWindow(this))
            {
                return;
            }

            SpeakerPreviewSynchronization.Synchronize(this);
        }

        public bool EnsureFlowInterface()
        {
            bool changed = false;
            IVariable enter = GetVariables().FirstOrDefault(variable => variable.Name == EnterVariableName);
            if (enter == null)
            {
                CreateInterfaceVariable(EnterVariableName, typeof(Untyped), null, VariableKind.Input);
                changed = true;
            }
            else if (enter.DataType != typeof(Untyped) ||
                     enter.VariableKind != VariableKind.Input ||
                     !HasExposedScope(enter))
            {
                enter.DataType = typeof(Untyped);
                enter.VariableKind = VariableKind.Input;
                SetExposedScope(enter);
                changed = true;
            }

            IVariable exit = GetVariables().FirstOrDefault(variable => variable.Name == ContinueVariableName);
            if (exit == null)
            {
                CreateInterfaceVariable(ContinueVariableName, typeof(Untyped), null, VariableKind.Output);
                changed = true;
            }
            else if (exit.DataType != typeof(Untyped) ||
                     exit.VariableKind != VariableKind.Output ||
                     !HasExposedScope(exit))
            {
                exit.DataType = typeof(Untyped);
                exit.VariableKind = VariableKind.Output;
                SetExposedScope(exit);
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Creates an input or output without going through Graph Toolkit 6000.6's
        /// broken exposed-variable creation path. That version logs a warning and
        /// writes Local scope even for valid Subgraph types.
        /// </summary>
        public IVariable CreateInterfaceVariable(
            string name,
            Type dataType,
            object defaultValue,
            VariableKind kind)
        {
            if (kind != VariableKind.Input && kind != VariableKind.Output)
                throw new ArgumentOutOfRangeException(nameof(kind), kind,
                    "A function interface variable must be an Input or Output.");

            // Creating Local avoids GraphModel.CreateGraphVariableDeclaration's
            // incorrect warning. VariableKind sets the modifier used to generate
            // subgraph ports; SetExposedScope keeps the serialized scope accurate.
            IVariable variable = CreateVariable(name, dataType, defaultValue, VariableKind.Local);
            variable.VariableKind = kind;
            SetExposedScope(variable);
            return variable;
        }

        private static bool HasExposedScope(IVariable variable)
        {
            PropertyInfo property = GetScopeProperty(variable);
            if (property == null)
                return true;

            object value = property.GetValue(variable);
            return value != null && Convert.ToInt32(value) == 1;
        }

        private static void SetExposedScope(IVariable variable)
        {
            PropertyInfo property = GetScopeProperty(variable);
            if (property?.CanWrite != true)
                return;

            property.SetValue(variable, Enum.ToObject(property.PropertyType, 1));
        }

        private static PropertyInfo GetScopeProperty(IVariable variable) =>
            variable?.GetType().GetProperty(
                "Scope",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    internal static class SpeakerPreviewSynchronization
    {
        public static bool IsOpenInGraphWindow(Graph graph)
        {
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window is IGraphWindow graphWindow &&
                    ReferenceEquals(graphWindow.Graph, graph))
                {
                    return true;
                }
            }

            return false;
        }

        public static void Synchronize(Graph graph)
        {
            bool isRecordingUndo = false;

            try
            {
                foreach (INode node in graph.GetNodes())
                {
                    if (node is not DialogueNode && node is not ChoiceNode)
                    {
                        continue;
                    }

                    if (node is ChoiceNode choice && choice.TryGetMigratedChoices(out ChoiceAuthoringList migrated))
                    {
                        if (!isRecordingUndo)
                        {
                            graph.UndoBeginRecordGraph("Migrate Choice Dropdowns");
                            isRecordingUndo = true;
                        }
                        choice.GetNodeOptionByName(ChoiceNode.ChoicesOptionID)?.TrySetValue(migrated);
                        choice.ClearMigratedLegacyChoiceValues();
                        choice.DefineNode();
                    }

                    if (node is ChoiceNode namedChoice && !namedChoice.ChoiceOutputNamesMatch())
                    {
                        if (!isRecordingUndo)
                        {
                            graph.UndoBeginRecordGraph("Update Novelify Authoring UI");
                            isRecordingUndo = true;
                        }
                        namedChoice.DefineNode();
                    }

                    NovelCharacter character = NovelGraphValues.Resolve<NovelCharacter>(
                        graph,
                        node.GetInputPortByName("Speaker"));
                    CharacterEmotion emotion = CharacterEmotion.Neutral;
                    node.GetNodeOptionByName("Emotion")?.TryGetValue(out emotion);
                    INodeOption speakerPreviewOption = node.GetNodeOptionByName("Speaker Preview");
                    bool updateSpeakerPreview = speakerPreviewOption != null &&
                        speakerPreviewOption.TryGetValue(out SpeakerPortraitOption currentSpeakerPreview) &&
                        (currentSpeakerPreview.Character != character || currentSpeakerPreview.Emotion != emotion);

                    RichDialogueText dialogue = new RichDialogueText(string.Empty);
                    node.GetNodeOptionByName("Dialogue")?.TryGetValue(out dialogue);
                    INodeOption dialoguePreviewOption = node.GetNodeOptionByName("Dialogue Preview");
                    bool updateDialoguePreview = dialoguePreviewOption != null &&
                        dialoguePreviewOption.TryGetValue(out DialoguePreviewOption currentDialoguePreview) &&
                        !string.Equals(
                            currentDialoguePreview.Text,
                            dialogue.Text,
                            StringComparison.Ordinal);

                    if (!updateSpeakerPreview && !updateDialoguePreview)
                    {
                        continue;
                    }

                    if (!isRecordingUndo)
                    {
                        graph.UndoBeginRecordGraph("Update Dialogue Node Previews");
                        isRecordingUndo = true;
                    }

                    if (updateSpeakerPreview)
                    {
                        speakerPreviewOption.TrySetValue(new SpeakerPortraitOption
                        {
                            Character = character,
                            Emotion = emotion
                        });
                    }

                    if (updateDialoguePreview)
                    {
                        dialoguePreviewOption.TrySetValue(
                            new DialoguePreviewOption(dialogue.Text ?? string.Empty));
                    }
                }
            }
            finally
            {
                if (isRecordingUndo)
                {
                    graph.UndoEndRecordGraph();
                }
            }
        }
    }

    /// <summary>
    /// Graph Toolkit locks mutations during graph lifecycle callbacks. Initialize
    /// the reserved function ports on the next editor update instead.
    /// </summary>
    internal sealed class NovelFunctionGraphInitializer : AssetPostprocessor
    {
        private static readonly HashSet<string> PendingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> PathsBeingSaved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _scheduled;

        [InitializeOnLoadMethod]
        private static void QueueExistingFunctions()
        {
            foreach (string path in AssetDatabase.GetAllAssetPaths())
                if (path.EndsWith("." + NovelFunctionGraph.AssetExtension, StringComparison.OrdinalIgnoreCase))
                    Queue(path);
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets)
                if (path.EndsWith("." + NovelFunctionGraph.AssetExtension, StringComparison.OrdinalIgnoreCase))
                    Queue(path);
        }

        private static void Queue(string path)
        {
            if (PathsBeingSaved.Contains(path))
                return;

            PendingPaths.Add(path);
            if (_scheduled) return;
            _scheduled = true;
            EditorApplication.delayCall += InitializePendingFunctions;
        }

        private static void InitializePendingFunctions()
        {
            _scheduled = false;
            string[] paths = PendingPaths.ToArray();
            PendingPaths.Clear();

            foreach (string path in paths)
            {
                NovelFunctionGraph graph = GraphDatabase.LoadGraph<NovelFunctionGraph>(path);
                if (graph == null) continue;

                graph.UndoBeginRecordGraph("Initialize Novel Function");
                bool changed;
                try
                {
                    changed = graph.EnsureFlowInterface();
                }
                finally
                {
                    graph.UndoEndRecordGraph();
                }

                if (!changed)
                    continue;

                PathsBeingSaved.Add(path);
                try
                {
                    GraphDatabase.SaveGraph(graph);
                }
                finally
                {
                    PathsBeingSaved.Remove(path);
                }
            }
        }
    }
}
