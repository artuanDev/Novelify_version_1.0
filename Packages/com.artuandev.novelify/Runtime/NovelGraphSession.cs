using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Novelify
{
    /// <summary>
    /// Gameplay-facing API for one <see cref="NovelGraphRunner"/>.
    /// It keeps user scripts independent from Novelify's traversal, UI, and persistence internals.
    /// </summary>
    public sealed class NovelGraphSession
    {
        private readonly NovelGraphRunner _runner;
        private int _chapterRequestVersion;

        internal NovelGraphSession(NovelGraphRunner runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public NovelGraphRunner Runner => _runner;
        public RuntimeNovelGraph CurrentGraph => _runner.RuntimeGraph;
        public RuntimeNode CurrentNode => _runner.CurrentNode;
        public NovelStateStore State => _runner.StateStore;
        public IReadOnlyDictionary<string, CharacterInfo> Characters => _runner.AllCharacters;
        public bool IsRunning => _runner.IsGraphRunningInternal;
        public bool IsWaiting => _runner.IsWaiting;
        public bool IsPausedByNodeHandler => _runner.IsExternallyPausedInternal;
        public bool IsTextRevealing => _runner.IsTextRevealingInternal;
        public bool IsSavePending => _runner.IsSavePending;
        public int CallDepth => _runner.GraphCallDepthInternal;
        public NovelSaveData LatestCheckpoint => _runner.LatestCheckpoint;
        public IReadOnlyList<NovelHistoryEntryData> History => _runner.History;
        public string CurrentGraphID => CurrentGraph != null ? CurrentGraph.GraphID : string.Empty;
        public string CurrentNodeID => CurrentNode != null ? CurrentNode.NodeID : string.Empty;

        public IReadOnlyList<ChoiceData> CurrentChoices =>
            CurrentNode is RuntimeChoiceNode choice && choice.Choices != null
                ? choice.Choices
                : Array.Empty<ChoiceData>();

        public event Action<RuntimeNovelGraph> GraphStarted;
        public event Action<RuntimeNovelGraph> GraphStopped;
        public event Action<RuntimeNovelGraph, RuntimeNode> NodeEntered;
        public event Action<RuntimeNovelGraph, RuntimeDialogueNode, string> DialoguePresented;
        public event Action<RuntimeNovelGraph, RuntimeChoiceNode, ChoiceData> ChoiceCommitted;
        public event Action<string> EventRaised;

        public void Play(RuntimeNovelGraph graph)
        {
            _chapterRequestVersion++;
            _runner.StartGraphInternal(graph);
        }
        public void Advance() => _runner.AdvanceGraphInternal();
        public void Stop()
        {
            _chapterRequestVersion++;
            _runner.StopGraphInternal();
        }
        public void UseStateStore(NovelStateStore stateStore) => _runner.UseStateStoreInternal(stateStore);
        public void UsePresentation(INovelPresentation presentation) => _runner.UsePresentation(presentation);
        public void UseContentProvider(INovelContentProvider provider)
        {
            _chapterRequestVersion++;
            _runner.UseContentProvider(provider);
        }
        public void UseSaveProvider(INovelSaveProvider provider)
        {
            _chapterRequestVersion++;
            _runner.UseSaveProvider(provider);
        }

        public IDisposable RegisterNodeHandler(INovelNodeHandler handler, int priority = 0) =>
            _runner.RegisterNodeHandler(handler, priority);

        public IDisposable RegisterNodeHandler<TNode>(
            Func<NovelNodeExecutionContext, TNode, NovelNodeExecutionResult> handler,
            int priority = 0)
            where TNode : RuntimeNode =>
            _runner.RegisterNodeHandler(handler, priority);

        public IDisposable RegisterValueEvaluator(INovelValueEvaluator evaluator, int priority = 0) =>
            _runner.RegisterValueEvaluator(evaluator, priority);

        public IDisposable RegisterValueEvaluator<TExpression>(
            Func<NovelValueEvaluationContext, TExpression, RuntimeValue> evaluator,
            int priority = 0) where TExpression : RuntimeValueExpression =>
            _runner.RegisterValueEvaluator(evaluator, priority);

        public bool Resume(out string error) => _runner.ResumeExternalNodeInternal(null, out error);

        public bool Resume(string nextNodeID, out string error) =>
            _runner.ResumeExternalNodeInternal(nextNodeID, out error);

        public RuntimeValue Evaluate(RuntimeValueExpression expression) =>
            _runner.EvaluateSessionExpression(expression);

        /// <summary>Starts a graph by its stable catalogue ID.</summary>
        public bool Play(string graphID)
        {
            if (!TryGetGraph(graphID, out RuntimeNovelGraph graph))
            {
                Debug.LogError($"Novelify could not find graph '{graphID}' in the graph catalogue.", _runner);
                return false;
            }

            Play(graph);
            return true;
        }

        public bool TryGetGraph(string graphID, out RuntimeNovelGraph graph)
        {
            graph = null;
            if (_runner.ContentProvider != null &&
                _runner.ContentProvider.TryGetLoadedGraph(graphID, out graph)) return true;
            NovelGraphCatalog catalog = GetCatalog();
            return catalog != null && catalog.TryGetGraph(graphID, out graph);
        }

        /// <summary>Loads a chapter without starting it. Set a provider for streamed chapters.</summary>
        public async Task<RuntimeNovelGraph> LoadChapterAsync(
            string chapterID, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(chapterID))
                throw new ArgumentException("A chapter ID is required.", nameof(chapterID));
            cancellationToken.ThrowIfCancellationRequested();
            if (_runner.ContentProvider != null)
                return await _runner.ContentProvider.LoadGraphAsync(chapterID, cancellationToken);
            TryGetGraph(chapterID, out RuntimeNovelGraph graph);
            return graph;
        }

        /// <summary>Starts the most recent requested chapter once its graph has finished loading.</summary>
        public async Task<bool> PlayChapterAsync(
            string chapterID, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(chapterID))
                throw new ArgumentException("A chapter ID is required.", nameof(chapterID));
            int requestVersion = ++_chapterRequestVersion;
            RuntimeNovelGraph graph = await LoadChapterAsync(chapterID, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (requestVersion != _chapterRequestVersion) return false;
            if (graph == null)
            {
                Debug.LogError($"Novelify could not load chapter '{chapterID}'.", _runner);
                return false;
            }
            Play(graph);
            return true;
        }

        public bool TryGetVariable(string variableID, out NovelVariableDefinition variable)
        {
            variable = null;
            NovelGraphCatalog catalog = GetCatalog();
            return catalog != null && catalog.TryGetVariable(variableID, out variable);
        }

        public RuntimeValue GetVariable(NovelVariableDefinition variable) =>
            _runner.ReadSessionVariable(variable);

        public RuntimeValue GetVariable(string variableID) =>
            TryGetVariable(variableID, out NovelVariableDefinition variable)
                ? GetVariable(variable)
                : RuntimeValue.None();

        public bool GetBool(NovelVariableDefinition variable, bool fallback = false)
        {
            RuntimeValue value = GetVariable(variable);
            return value.Kind == RuntimeValueKind.Boolean ? value.BooleanValue : fallback;
        }

        public bool GetBool(string variableID, bool fallback = false) =>
            TryGetVariable(variableID, out NovelVariableDefinition variable) ? GetBool(variable, fallback) : fallback;

        public int GetInt(NovelVariableDefinition variable, int fallback = 0)
        {
            RuntimeValue value = GetVariable(variable);
            return value.Kind == RuntimeValueKind.Integer ? value.IntegerValue : fallback;
        }

        public int GetInt(string variableID, int fallback = 0) =>
            TryGetVariable(variableID, out NovelVariableDefinition variable) ? GetInt(variable, fallback) : fallback;

        public float GetFloat(NovelVariableDefinition variable, float fallback = 0f)
        {
            RuntimeValue value = GetVariable(variable);
            return value.Kind == RuntimeValueKind.Float ? value.FloatValue : fallback;
        }

        public float GetFloat(string variableID, float fallback = 0f) =>
            TryGetVariable(variableID, out NovelVariableDefinition variable) ? GetFloat(variable, fallback) : fallback;

        public string GetString(NovelVariableDefinition variable, string fallback = "")
        {
            RuntimeValue value = GetVariable(variable);
            return value.Kind == RuntimeValueKind.String ? value.StringValue ?? string.Empty : fallback;
        }

        public string GetString(string variableID, string fallback = "") =>
            TryGetVariable(variableID, out NovelVariableDefinition variable) ? GetString(variable, fallback) : fallback;

        public bool TrySetVariable(NovelVariableDefinition variable, RuntimeValue value, out string error) =>
            _runner.TryWriteSessionVariable(variable, value, out error);

        public bool TrySetVariable(string variableID, RuntimeValue value, out string error)
        {
            if (TryGetVariable(variableID, out NovelVariableDefinition variable))
                return TrySetVariable(variable, value, out error);
            error = $"Novelify could not find variable '{variableID}' in the graph catalogue.";
            return false;
        }

        public bool SetVariable(NovelVariableDefinition variable, RuntimeValue value) =>
            SetVariableAndReport(variable, value);

        public bool SetVariable(NovelVariableDefinition variable, bool value) =>
            SetVariableAndReport(variable, RuntimeValue.From(value));

        public bool SetVariable(NovelVariableDefinition variable, int value) =>
            SetVariableAndReport(variable, RuntimeValue.From(value));

        public bool SetVariable(NovelVariableDefinition variable, float value) =>
            SetVariableAndReport(variable, RuntimeValue.From(value));

        public bool SetVariable(NovelVariableDefinition variable, string value) =>
            SetVariableAndReport(variable, RuntimeValue.From(value));

        public bool SetVariable(string variableID, RuntimeValue value)
        {
            if (TryGetVariable(variableID, out NovelVariableDefinition variable))
                return SetVariableAndReport(variable, value);
            Debug.LogError($"Novelify could not find variable '{variableID}' in the graph catalogue.", _runner);
            return false;
        }

        public bool SetVariable(string variableID, bool value) =>
            SetVariable(variableID, RuntimeValue.From(value));

        public bool SetVariable(string variableID, int value) =>
            SetVariable(variableID, RuntimeValue.From(value));

        public bool SetVariable(string variableID, float value) =>
            SetVariable(variableID, RuntimeValue.From(value));

        public bool SetVariable(string variableID, string value) =>
            SetVariable(variableID, RuntimeValue.From(value));

        public bool TryChoose(string choiceID, out string error) =>
            _runner.TryChooseInternal(choiceID, out error);

        public bool HasChosen(string choiceID) => State.HasSelectedChoice(choiceID);

        public int GetVisitCount(string nodeID) =>
            State.GetVisitCount(CurrentGraphID, nodeID);

        public int GetVisitCount(string graphID, string nodeID) =>
            State.GetVisitCount(graphID, nodeID);

        public NovelPersistenceResult Capture(out NovelSaveData snapshot) =>
            _runner.CaptureSnapshot(out snapshot);

        public NovelPersistenceResult Restore(NovelSaveData snapshot)
        {
            _chapterRequestVersion++;
            return _runner.RestoreSnapshot(snapshot);
        }

        public NovelPersistenceResult Save(string slotID) => _runner.SaveSlot(slotID);
        public NovelPersistenceResult Load(string slotID)
        {
            _chapterRequestVersion++;
            return _runner.LoadSlot(slotID);
        }
        public IReadOnlyList<string> ListSlots() => _runner.SaveProvider.ListSlots();
        public NovelPersistenceResult DeleteSlot(string slotID) => _runner.SaveProvider.DeleteSlot(slotID);
        public async Task<NovelPersistenceResult> LoadAsync(
            string slotID, CancellationToken cancellationToken = default)
        {
            if (!NovelSaveStorage.IsValidSlotID(slotID))
                return new NovelPersistenceResult(NovelPersistenceStatus.InvalidSlot, "Invalid save slot ID.");
            NovelPersistenceResult read = _runner.SaveProvider.LoadSlot(slotID, out NovelSaveData snapshot);
            if (!read.Succeeded) return read;
            if (snapshot == null)
                return new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, "Snapshot is null.");
            int requestVersion = ++_chapterRequestVersion;
            if (_runner.ContentProvider != null)
            {
                var graphIDs = new HashSet<string>(StringComparer.Ordinal);
                if (!string.IsNullOrEmpty(snapshot.CurrentGraph?.GraphID))
                    graphIDs.Add(snapshot.CurrentGraph.GraphID);
                foreach (NovelExecutionFrameData frame in snapshot.Frames ?? new List<NovelExecutionFrameData>())
                    if (!string.IsNullOrEmpty(frame?.Graph?.GraphID)) graphIDs.Add(frame.Graph.GraphID);
                foreach (string graphID in graphIDs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (requestVersion != _chapterRequestVersion)
                        return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible,
                            "A newer chapter or save request replaced this load.");
                    if (TryGetGraph(graphID, out _)) continue;
                    if (await LoadChapterAsync(graphID, cancellationToken) == null)
                        return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible,
                            $"Saved graph '{graphID}' could not be loaded.");
                }
                bool needsCheckpoint = !TryGetGraph(snapshot.CurrentGraph?.GraphID, out RuntimeNovelGraph current) ||
                    current == null || current.AllNodes == null || !current.AllNodes.Exists(node =>
                        node is RuntimeDialogueNode &&
                        string.Equals(node.NodeID, snapshot.CurrentNodeID, StringComparison.Ordinal));
                if (needsCheckpoint && !string.IsNullOrEmpty(snapshot.CheckpointGraphID) &&
                    !graphIDs.Contains(snapshot.CheckpointGraphID) &&
                    !TryGetGraph(snapshot.CheckpointGraphID, out _))
                    await LoadChapterAsync(snapshot.CheckpointGraphID, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (requestVersion != _chapterRequestVersion)
                return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible,
                    "A newer chapter or save request replaced this load.");
            NovelPersistenceResult restored = _runner.RestoreSnapshot(snapshot);
            return read.Status == NovelPersistenceStatus.RestoredBackup && restored.Succeeded ? read : restored;
        }
        public NovelPersistenceResult SaveProfile() => _runner.SaveProfile();
        public NovelPersistenceResult LoadProfile() => _runner.LoadProfile();
        public void Checkpoint(string checkpointID, string autosaveSlotID = null) =>
            _runner.RequestCheckpoint(checkpointID, autosaveSlotID);

        private NovelGraphCatalog GetCatalog()
        {
            if (_runner.AssetCatalog == null)
                _runner.AssetCatalog = NovelGraphCatalog.LoadDefault();
            return _runner.AssetCatalog;
        }

        private bool SetVariableAndReport(NovelVariableDefinition variable, RuntimeValue value)
        {
            if (TrySetVariable(variable, value, out string error)) return true;
            Debug.LogError(error, _runner);
            return false;
        }

        internal void RaiseStarted(RuntimeNovelGraph graph) => GraphStarted?.Invoke(graph);
        internal void RaiseStopped(RuntimeNovelGraph graph) => GraphStopped?.Invoke(graph);
        internal void RaiseNodeEntered(RuntimeNovelGraph graph, RuntimeNode node) => NodeEntered?.Invoke(graph, node);
        internal void RaiseDialoguePresented(RuntimeNovelGraph graph, RuntimeDialogueNode node, string speakerName) =>
            DialoguePresented?.Invoke(graph, node, speakerName);
        internal void RaiseChoiceCommitted(RuntimeNovelGraph graph, RuntimeChoiceNode node, ChoiceData choice) =>
            ChoiceCommitted?.Invoke(graph, node, choice);
        internal void RaiseEvent(string eventName) => EventRaised?.Invoke(eventName);
    }
}
