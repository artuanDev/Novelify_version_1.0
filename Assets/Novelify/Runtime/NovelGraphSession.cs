using System;
using System.Collections.Generic;
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

        internal NovelGraphSession(NovelGraphRunner runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public NovelGraphRunner Runner => _runner;
        [Obsolete("Use Runner. NovelManager is now only the built-in example controller.")]
        public NovelGraphRunner Manager => _runner;
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

        public void Play(RuntimeNovelGraph graph) => _runner.StartGraphInternal(graph);
        public void Advance() => _runner.AdvanceGraphInternal();
        public void Stop() => _runner.StopGraphInternal();
        public void UseStateStore(NovelStateStore stateStore) => _runner.UseStateStoreInternal(stateStore);
        public void UsePresentation(INovelPresentation presentation) => _runner.UsePresentation(presentation);

        public IDisposable RegisterNodeHandler(INovelNodeHandler handler, int priority = 0) =>
            _runner.RegisterNodeHandler(handler, priority);

        public IDisposable RegisterNodeHandler<TNode>(
            Func<NovelNodeExecutionContext, TNode, NovelNodeExecutionResult> handler,
            int priority = 0)
            where TNode : RuntimeNode =>
            _runner.RegisterNodeHandler(handler, priority);

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
            NovelGraphCatalog catalog = GetCatalog();
            return catalog != null && catalog.TryGetGraph(graphID, out graph);
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

        public NovelPersistenceResult Restore(NovelSaveData snapshot) =>
            _runner.RestoreSnapshot(snapshot);

        public NovelPersistenceResult Save(string slotID) => _runner.SaveSlot(slotID);
        public NovelPersistenceResult Load(string slotID) => _runner.LoadSlot(slotID);
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
