using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Novelify
{
    public partial class NovelGraphRunner
    {
        [Header("Persistence")]
        [Tooltip("Player-safe graph, character and variable lookup. Defaults to Resources/NovelGraphCatalog.")]
        public NovelGraphCatalog AssetCatalog;
        [Min(1)] public int BacklogLimit = 100;

        public bool IsSavePending => _pendingSlotIDs.Count > 0 || _pendingCapture ||
                                     !string.IsNullOrEmpty(_pendingCheckpointID);
        public NovelSaveData LatestCheckpoint { get; private set; }
        public IReadOnlyList<NovelHistoryEntryData> History => _history;
        public NovelSaveStorage SaveStorage => _saveStorage ??= new NovelSaveStorage();

        public event Action<NovelSaveData> CheckpointCaptured;
        public event Action<string, NovelPersistenceResult> SlotSaveCompleted;

        private readonly List<NovelHistoryEntryData> _history = new List<NovelHistoryEntryData>();
        private NovelSaveStorage _saveStorage;
        private readonly HashSet<string> _pendingSlotIDs = new HashSet<string>(StringComparer.Ordinal);
        private string _pendingCheckpointID;
        private string _pendingCheckpointGraphID;
        private bool _pendingCapture;
        private bool _restoringSnapshot;

        private sealed class RestorePlan
        {
            public RuntimeNovelGraph CurrentGraph;
            public RuntimeNode CurrentNode;
            public RuntimeValueScope CurrentScope;
            public readonly List<GraphCallFrame> Frames = new List<GraphCallFrame>();
            public readonly List<(NovelCharacterStateData Data, NovelCharacter Character)> Characters =
                new List<(NovelCharacterStateData, NovelCharacter)>();
            public bool UsedCheckpointFallback;
            public bool ContentChanged;
        }

        public void UseSaveStorage(NovelSaveStorage storage) => _saveStorage = storage ?? new NovelSaveStorage();

        public void RequestCheckpoint(string checkpointID, string autosaveSlotID = null)
        {
            _pendingCheckpointID = checkpointID ?? string.Empty;
            _pendingCheckpointGraphID = RuntimeGraph != null ? RuntimeGraph.GraphID : string.Empty;
            _pendingCapture = true;
            if (!string.IsNullOrEmpty(autosaveSlotID))
            {
                if (NovelSaveStorage.IsValidSlotID(autosaveSlotID)) _pendingSlotIDs.Add(autosaveSlotID);
                else Debug.LogError($"Checkpoint autosave slot '{autosaveSlotID}' is invalid.", this);
            }
            OnSupportedSaveBoundary();
        }

        public NovelPersistenceResult CaptureSnapshot(out NovelSaveData snapshot)
        {
            snapshot = null;
            if (!CanCaptureNow())
            {
                _pendingCapture = true;
                return new NovelPersistenceResult(NovelPersistenceStatus.Pending,
                    "Snapshot deferred until dialogue reveal and all automatic or awaited work have finished.");
            }
            return BuildSnapshot(out snapshot);
        }

        public NovelSaveData CaptureSnapshot()
        {
            CaptureSnapshot(out NovelSaveData snapshot);
            return snapshot;
        }

        public NovelPersistenceResult RestoreSnapshot(NovelSaveData snapshot)
        {
            NovelPersistenceResult validation = BuildRestorePlan(snapshot, out RestorePlan plan);
            if (!validation.Succeeded) return validation;

            // From this point validation has completed and it is safe to replace the live session.
            ++_flowVersion;
            CancelWait();
            StopNodePresentation();
            ClearChoiceButtons();
            StopAudio(PlaySoundSource);
            _stage?.StopMovement();
            _graphCalls.Clear();

            if (!StateStore.RestoreStory(snapshot.StoryVariables, snapshot.SelectedChoiceIDs,
                    snapshot.Visits, snapshot.ReadLineIDs, out string stateError))
                return new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, stateError);

            _history.Clear();
            if (snapshot.History != null)
                _history.AddRange(snapshot.History.Where(entry => entry != null).TakeLast(Mathf.Max(1, BacklogLimit)));

            Stage.HideAll();
            foreach ((NovelCharacterStateData data, NovelCharacter character) in plan.Characters)
            {
                CharacterInfo info = Stage.Show(character, data.InstanceID);
                if (info == null) continue;
                float facingSign = data.Facing == CharacterFacing.Left ? -1f : 1f;
                Vector2 scale = data.Scale;
                scale.x = Mathf.Abs(scale.x) * facingSign;
                info.TransformTo(data.Position, data.Rotation, scale, false, 0f);
                info.Opacity = data.HasOpacity ? Mathf.Clamp01(data.Opacity) : 1f;
                info.SetEmotion(data.Emotion);
                info.gameObject.SetActive(data.Visible);
            }

            foreach (GraphCallFrame frame in plan.Frames) _graphCalls.Push(frame);
            _valueScope = plan.CurrentScope;
            LoadGraph(plan.CurrentGraph);
            _hasStartedGraph = true;
            _isGraphRunning = true;
            _pendingCapture = false;
            _pendingSlotIDs.Clear();
            _pendingCheckpointID = null;
            LatestCheckpoint = snapshot;

            _restoringSnapshot = true;
            try
            {
                if (plan.UsedCheckpointFallback)
                {
                    ShowNode(plan.CurrentNode.NodeID);
                }
                else
                {
                    _currentNode = plan.CurrentNode;
                    ShowDialogueNode((RuntimeDialogueNode)plan.CurrentNode);
                    CompleteTextImmediately();
                }
            }
            finally { _restoringSnapshot = false; }

            if (plan.UsedCheckpointFallback || plan.ContentChanged)
                return new NovelPersistenceResult(NovelPersistenceStatus.Migrated,
                    plan.UsedCheckpointFallback
                        ? "The saved node was removed; resumed from its known checkpoint."
                        : "Content changed, but all saved references remain compatible.");
            return new NovelPersistenceResult(NovelPersistenceStatus.Success);
        }

        public NovelPersistenceResult SaveSlot(string slotID)
        {
            if (!NovelSaveStorage.IsValidSlotID(slotID))
                return new NovelPersistenceResult(NovelPersistenceStatus.InvalidSlot,
                    "Slot IDs may contain only letters, numbers, '-' and '_', up to 64 characters.");
            if (!CanCaptureNow())
            {
                _pendingSlotIDs.Add(slotID);
                return new NovelPersistenceResult(NovelPersistenceStatus.Pending,
                    "Save deferred until the next stable dialogue or choice boundary.");
            }
            NovelPersistenceResult capture = BuildSnapshot(out NovelSaveData snapshot);
            if (!capture.Succeeded) return capture;
            return SaveStorage.SaveSlot(slotID, snapshot);
        }

        public NovelPersistenceResult LoadSlot(string slotID)
        {
            NovelPersistenceResult read = SaveStorage.LoadSlot(slotID, out NovelSaveData snapshot);
            if (!read.Succeeded) return read;
            NovelPersistenceResult restored = RestoreSnapshot(snapshot);
            if (!restored.Succeeded) return restored;
            return read.Status == NovelPersistenceStatus.RestoredBackup
                ? read
                : restored;
        }

        public NovelPersistenceResult SaveProfile()
        {
            var profile = new NovelProfileSaveData
            {
                TimestampUtc = DateTime.UtcNow.ToString("O"),
                ProfileVariables = StateStore.CaptureProfileVariables()
            };
            return SaveStorage.SaveProfile(profile);
        }

        public NovelPersistenceResult LoadProfile()
        {
            NovelPersistenceResult read = SaveStorage.LoadProfile(out NovelProfileSaveData profile);
            if (!read.Succeeded) return read;
            if (profile.SchemaVersion != NovelProfileSaveData.CurrentSchemaVersion)
                return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible,
                    $"Unsupported profile schema {profile.SchemaVersion}.");
            NovelPersistenceResult validation = ValidateVariables(profile.ProfileVariables, NovelVariableScope.Profile);
            if (!validation.Succeeded) return validation;
            return StateStore.RestoreProfile(profile.ProfileVariables, out string error)
                ? read
                : new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, error);
        }

        private bool CanCaptureNow() => _currentNode is RuntimeDialogueNode && !_isWaiting && !_isTextRevealing;

        private void OnDialogueBoundaryPresented(RuntimeDialogueNode node, string speaker, string resolvedText)
        {
            if (!_restoringSnapshot)
            {
                string graphID = RuntimeGraph != null ? RuntimeGraph.GraphID : string.Empty;
                string lineID = graphID + ":" + (node?.NodeID ?? string.Empty);
                StateStore.MarkLineRead(lineID);
                _history.Add(new NovelHistoryEntryData
                {
                    GraphID = graphID,
                    LineID = lineID,
                    Speaker = speaker ?? string.Empty,
                    ResolvedText = resolvedText ?? string.Empty
                });
                int overflow = _history.Count - Mathf.Max(1, BacklogLimit);
                if (overflow > 0) _history.RemoveRange(0, overflow);
            }
            OnSupportedSaveBoundary();
        }

        private void OnSupportedSaveBoundary()
        {
            if (_restoringSnapshot || !CanCaptureNow() || !IsSavePending) return;
            NovelPersistenceResult capture = BuildSnapshot(out NovelSaveData snapshot);
            if (!capture.Succeeded)
            {
                foreach (string slot in _pendingSlotIDs)
                    SlotSaveCompleted?.Invoke(slot, capture);
                return;
            }

            LatestCheckpoint = snapshot;
            _pendingCapture = false;
            _pendingCheckpointID = null;
            _pendingCheckpointGraphID = null;
            CheckpointCaptured?.Invoke(snapshot);

            if (_pendingSlotIDs.Count > 0)
            {
                string[] slots = _pendingSlotIDs.OrderBy(value => value, StringComparer.Ordinal).ToArray();
                _pendingSlotIDs.Clear();
                foreach (string slot in slots)
                {
                    NovelPersistenceResult saved = SaveStorage.SaveSlot(slot, snapshot);
                    SlotSaveCompleted?.Invoke(slot, saved);
                }
            }
        }

        private NovelPersistenceResult BuildSnapshot(out NovelSaveData snapshot)
        {
            snapshot = null;
            if (!CanCaptureNow())
                return new NovelPersistenceResult(NovelPersistenceStatus.Pending, "The session is not at a supported save boundary.");
            if (RuntimeGraph == null || string.IsNullOrEmpty(RuntimeGraph.GraphID) || _currentNode == null)
                return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible, "Current graph or node has no persistent ID.");

            var result = new NovelSaveData
            {
                TimestampUtc = DateTime.UtcNow.ToString("O"),
                CheckpointID = _pendingCheckpointID ?? LatestCheckpoint?.CheckpointID ?? string.Empty,
                CheckpointGraphID = _pendingCheckpointGraphID ?? LatestCheckpoint?.CheckpointGraphID ?? string.Empty,
                CurrentGraph = CaptureGraph(RuntimeGraph),
                CurrentNodeID = _currentNode.NodeID,
                StoryVariables = StateStore.CaptureStoryVariables(),
                SelectedChoiceIDs = StateStore.CaptureSelectedChoices(),
                Visits = StateStore.CaptureVisits(),
                ReadLineIDs = StateStore.CaptureReadLineIDs(),
                History = _history.TakeLast(Mathf.Max(1, BacklogLimit)).ToList()
            };

            NovelPersistenceResult scopeResult = CaptureScope(_valueScope, out result.CurrentScope);
            if (!scopeResult.Succeeded) return scopeResult;

            foreach (GraphCallFrame frame in _graphCalls.Reverse())
            {
                scopeResult = CaptureScope(frame.Scope, out NovelValueScopeData frameScope);
                if (!scopeResult.Succeeded) return scopeResult;
                result.Frames.Add(new NovelExecutionFrameData
                {
                    Graph = CaptureGraph(frame.Graph),
                    ReturnNodeID = frame.ReturnNodeID,
                    CallSiteID = frame.FunctionCallNodeID,
                    Scope = frameScope
                });
            }

            NovelGraphCatalog catalog = GetAssetCatalog();
            foreach (KeyValuePair<string, CharacterInfo> item in AllCharacters.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                CharacterInfo info = item.Value;
                if (info == null || info.character == null) continue;
                string characterID = catalog != null ? catalog.GetCharacterID(info.character) : null;
                if (string.IsNullOrEmpty(characterID))
                    return new NovelPersistenceResult(NovelPersistenceStatus.UnsupportedValue,
                        $"Character '{info.character.name}' is missing from the runtime asset catalog.");
                Vector2 scale = info.Scale;
                result.Characters.Add(new NovelCharacterStateData
                {
                    CharacterID = characterID,
                    InstanceID = info.InstanceID ?? string.Empty,
                    Visible = info.gameObject.activeSelf,
                    Emotion = info.Emotion,
                    Position = info.Position,
                    Rotation = info.Rotation,
                    Scale = new Vector2(Mathf.Abs(scale.x), scale.y),
                    Facing = scale.x < 0f ? CharacterFacing.Left : CharacterFacing.Right,
                    HasOpacity = true,
                    Opacity = info.Opacity
                });
            }

            snapshot = result;
            return new NovelPersistenceResult(NovelPersistenceStatus.Success);
        }

        private NovelPersistenceResult CaptureScope(RuntimeValueScope scope, out NovelValueScopeData data)
        {
            data = new NovelValueScopeData();
            foreach (KeyValuePair<string, RuntimeValue> item in scope.Inputs.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                NovelPersistenceResult serialized = SerializeValue(item.Value, out NovelSerializedValue value);
                if (!serialized.Succeeded) return serialized;
                data.Inputs.Add(new NovelNamedValueData { Name = item.Key, Value = value });
            }
            foreach (KeyValuePair<string, RuntimeValue> item in scope.Locals.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                NovelPersistenceResult serialized = SerializeValue(item.Value, out NovelSerializedValue value);
                if (!serialized.Succeeded) return serialized;
                data.Locals.Add(new NovelNamedValueData { Name = item.Key, Value = value });
            }
            foreach (KeyValuePair<string, RuntimeValue> item in scope._outputs.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                NovelPersistenceResult serialized = SerializeValue(item.Value, out NovelSerializedValue value);
                if (!serialized.Succeeded) return serialized;
                SplitOutputKey(item.Key, out string nodeID, out string name);
                data.CachedOutputs.Add(new NovelCachedOutputData { NodeID = nodeID, Name = name, Value = value });
            }
            return new NovelPersistenceResult(NovelPersistenceStatus.Success);
        }

        private NovelPersistenceResult SerializeValue(RuntimeValue value, out NovelSerializedValue serialized)
        {
            serialized = NovelStateStore.Serialize(value ?? RuntimeValue.None());
            switch (value?.Kind ?? RuntimeValueKind.None)
            {
                case RuntimeValueKind.None:
                case RuntimeValueKind.Float:
                case RuntimeValueKind.Integer:
                case RuntimeValueKind.Boolean:
                case RuntimeValueKind.String:
                case RuntimeValueKind.Vector2:
                    return new NovelPersistenceResult(NovelPersistenceStatus.Success);
                case RuntimeValueKind.CharacterReference:
                    serialized.InstanceID = value.CharacterReferenceValue.InstanceID ?? string.Empty;
                    serialized.AssetID = GetAssetCatalog()?.GetCharacterID(value.CharacterReferenceValue.Character);
                    break;
                case RuntimeValueKind.Object when value.ObjectValue is NovelCharacter character:
                    serialized.AssetID = GetAssetCatalog()?.GetCharacterID(character);
                    break;
                default:
                    return new NovelPersistenceResult(NovelPersistenceStatus.UnsupportedValue,
                        $"Runtime value kind '{value.Kind}' has no persistent asset-ID serializer.");
            }
            return string.IsNullOrEmpty(serialized.AssetID)
                ? new NovelPersistenceResult(NovelPersistenceStatus.UnsupportedValue,
                    "A character value is missing from the runtime asset catalog.")
                : new NovelPersistenceResult(NovelPersistenceStatus.Success);
        }

        private NovelPersistenceResult BuildRestorePlan(NovelSaveData snapshot, out RestorePlan plan)
        {
            plan = null;
            if (snapshot == null) return new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, "Snapshot is null.");
            if (snapshot.SchemaVersion != NovelSaveData.CurrentSchemaVersion)
                return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible,
                    $"Unsupported save schema {snapshot.SchemaVersion}; expected {NovelSaveData.CurrentSchemaVersion}.");

            var candidate = new RestorePlan();
            NovelPersistenceResult graphResult = ResolveSavedGraph(snapshot.CurrentGraph, out candidate.CurrentGraph, out bool changed);
            if (!graphResult.Succeeded) return graphResult;
            candidate.ContentChanged |= changed;
            candidate.CurrentNode = FindNode(candidate.CurrentGraph, snapshot.CurrentNodeID);

            if (candidate.CurrentNode is not RuntimeDialogueNode)
            {
                if (!TryResolveCheckpointFallback(snapshot, out candidate.CurrentGraph, out candidate.CurrentNode))
                    return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible,
                        $"Saved boundary node '{snapshot.CurrentNodeID}' no longer exists and no known checkpoint can recover it.");
                candidate.UsedCheckpointFallback = true;
            }

            NovelPersistenceResult scope = RestoreScope(snapshot.CurrentScope, out candidate.CurrentScope);
            if (!scope.Succeeded) return scope;
            foreach (NovelExecutionFrameData savedFrame in snapshot.Frames ?? new List<NovelExecutionFrameData>())
            {
                if (savedFrame == null) return new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, "A call frame is null.");
                graphResult = ResolveSavedGraph(savedFrame.Graph, out RuntimeNovelGraph graph, out changed);
                if (!graphResult.Succeeded) return graphResult;
                candidate.ContentChanged |= changed;
                    if (!string.IsNullOrEmpty(savedFrame.ReturnNodeID) && FindNode(graph, savedFrame.ReturnNodeID) == null)
                    return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible,
                            $"Return node '{savedFrame.ReturnNodeID}' no longer exists in graph '{graph.GraphID}'.");
                    if (!string.IsNullOrEmpty(savedFrame.CallSiteID) && FindNode(graph, savedFrame.CallSiteID) == null)
                        return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible,
                            $"Call-site node '{savedFrame.CallSiteID}' no longer exists in graph '{graph.GraphID}'.");
                scope = RestoreScope(savedFrame.Scope, out RuntimeValueScope frameScope);
                if (!scope.Succeeded) return scope;
                candidate.Frames.Add(new GraphCallFrame(graph, savedFrame.ReturnNodeID, frameScope, savedFrame.CallSiteID));
            }

            var validationStore = new NovelStateStore();
            if (!validationStore.RestoreStory(snapshot.StoryVariables, snapshot.SelectedChoiceIDs,
                    snapshot.Visits, snapshot.ReadLineIDs, out string stateError))
                return new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, stateError);

            NovelPersistenceResult variableValidation = ValidateVariables(snapshot.StoryVariables, NovelVariableScope.Story);
            if (!variableValidation.Succeeded) return variableValidation;

            NovelGraphCatalog catalog = GetAssetCatalog();
            foreach (NovelCharacterStateData data in snapshot.Characters ?? new List<NovelCharacterStateData>())
            {
                if (data == null || catalog == null || !catalog.TryGetCharacter(data.CharacterID, out NovelCharacter character))
                    return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible,
                        $"Saved character asset '{data?.CharacterID}' is not in the runtime catalog.");
                candidate.Characters.Add((data, character));
            }
            plan = candidate;
            return new NovelPersistenceResult(NovelPersistenceStatus.Success);
        }

        private NovelPersistenceResult ValidateVariables(IReadOnlyList<NovelSavedVariable> values,
            NovelVariableScope expectedScope)
        {
            if (values == null || values.Count == 0) return new NovelPersistenceResult(NovelPersistenceStatus.Success);
            NovelGraphCatalog catalog = GetAssetCatalog();
            if (catalog == null)
                return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible,
                    "Saved variables require a runtime asset catalog.");
            foreach (NovelSavedVariable saved in values)
            {
                if (saved == null || !catalog.TryGetVariable(saved.VariableID, out NovelVariableDefinition variable))
                    return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible,
                        $"Saved variable '{saved?.VariableID}' is not in the runtime catalog.");
                if (variable.Scope != expectedScope || saved.Value == null || saved.Value.Kind != variable.ValueKind)
                    return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible,
                        $"Saved variable '{variable.Name}' no longer matches its {expectedScope} type definition.");
            }
            return new NovelPersistenceResult(NovelPersistenceStatus.Success);
        }

        private NovelPersistenceResult ResolveSavedGraph(NovelGraphState saved, out RuntimeNovelGraph graph, out bool changed)
        {
            graph = null;
            changed = false;
            if (saved == null || string.IsNullOrEmpty(saved.GraphID))
                return new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, "A saved graph reference has no graph ID.");
            graph = ResolveGraph(saved.GraphID);
            if (graph == null)
                return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible,
                    $"Graph '{saved.GraphID}' is not available in the runtime catalog.");
            if (saved.GraphSchemaVersion > graph.SchemaVersion)
                return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible,
                    $"Graph '{saved.GraphID}' uses newer schema {saved.GraphSchemaVersion}.");
            changed = !string.Equals(saved.ContentVersion ?? string.Empty, graph.ContentVersion ?? string.Empty, StringComparison.Ordinal);
            return new NovelPersistenceResult(NovelPersistenceStatus.Success);
        }

        private bool TryResolveCheckpointFallback(NovelSaveData snapshot,
            out RuntimeNovelGraph graph, out RuntimeNode node)
        {
            graph = ResolveGraph(snapshot.CheckpointGraphID);
            node = null;
            if (graph == null || string.IsNullOrEmpty(snapshot.CheckpointID)) return false;
            RuntimeCheckpointNode checkpoint = graph.AllNodes?.OfType<RuntimeCheckpointNode>()
                .FirstOrDefault(item => string.Equals(item.CheckpointID, snapshot.CheckpointID, StringComparison.Ordinal));
            node = FindNode(graph, checkpoint?.NextNodeID);
            return node != null;
        }

        private NovelPersistenceResult RestoreScope(NovelValueScopeData saved, out RuntimeValueScope scope)
        {
            scope = new RuntimeValueScope();
            if (saved == null) return new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, "A saved value scope is null.");
            foreach (NovelNamedValueData item in saved.Inputs ?? new List<NovelNamedValueData>())
            {
                NovelPersistenceResult result = DeserializeValue(item?.Value, out RuntimeValue value);
                if (!result.Succeeded || item == null || string.IsNullOrEmpty(item.Name))
                    return result.Succeeded ? new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, "Saved input has no name.") : result;
                if (!scope.Inputs.TryAdd(item.Name, value)) return DuplicateScopeValue(item.Name);
            }
            foreach (NovelNamedValueData item in saved.Locals ?? new List<NovelNamedValueData>())
            {
                NovelPersistenceResult result = DeserializeValue(item?.Value, out RuntimeValue value);
                if (!result.Succeeded || item == null || string.IsNullOrEmpty(item.Name))
                    return result.Succeeded ? new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, "Saved local has no name.") : result;
                if (!scope.Locals.TryAdd(item.Name, value)) return DuplicateScopeValue(item.Name);
            }
            foreach (NovelCachedOutputData item in saved.CachedOutputs ?? new List<NovelCachedOutputData>())
            {
                NovelPersistenceResult result = DeserializeValue(item?.Value, out RuntimeValue value);
                if (!result.Succeeded || item == null || string.IsNullOrEmpty(item.NodeID) || string.IsNullOrEmpty(item.Name))
                    return result.Succeeded ? new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, "Saved cached output has no identity.") : result;
                string key = RuntimeValueScope.OutputKey(item.NodeID, item.Name);
                if (!scope._outputs.TryAdd(key, value)) return DuplicateScopeValue(key);
            }
            return new NovelPersistenceResult(NovelPersistenceStatus.Success);
        }

        private NovelPersistenceResult DeserializeValue(NovelSerializedValue serialized, out RuntimeValue value)
        {
            value = null;
            if (serialized == null) return new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, "A saved value is null.");
            value = NovelStateStore.Deserialize(serialized);
            if (serialized.Kind == RuntimeValueKind.CharacterReference || serialized.Kind == RuntimeValueKind.Object)
            {
                NovelGraphCatalog catalog = GetAssetCatalog();
                if (catalog == null || !catalog.TryGetCharacter(serialized.AssetID, out NovelCharacter character))
                    return new NovelPersistenceResult(NovelPersistenceStatus.Incompatible,
                        $"Saved character asset '{serialized.AssetID}' is not in the runtime catalog.");
                if (serialized.Kind == RuntimeValueKind.CharacterReference)
                    value.CharacterReferenceValue = new NovelCharacterReference(character, serialized.InstanceID);
                else value.ObjectValue = character;
                return new NovelPersistenceResult(NovelPersistenceStatus.Success);
            }
            if (serialized.Kind is RuntimeValueKind.None or RuntimeValueKind.Float or RuntimeValueKind.Integer or
                RuntimeValueKind.Boolean or RuntimeValueKind.String or RuntimeValueKind.Vector2)
                return new NovelPersistenceResult(NovelPersistenceStatus.Success);
            return new NovelPersistenceResult(NovelPersistenceStatus.Corrupt,
                $"Saved runtime value kind '{serialized.Kind}' is unsupported.");
        }

        private RuntimeNovelGraph ResolveGraph(string graphID)
        {
            if (string.IsNullOrEmpty(graphID)) return null;
            RuntimeNovelGraph graph = GetAssetCatalog()?.GetGraph(graphID);
            if (graph != null) return graph;
            var visited = new HashSet<RuntimeNovelGraph>();
            return FindReachableGraph(RuntimeGraph, graphID, visited);
        }

        private static RuntimeNovelGraph FindReachableGraph(RuntimeNovelGraph root, string graphID,
            HashSet<RuntimeNovelGraph> visited)
        {
            if (root == null || !visited.Add(root)) return null;
            if (string.Equals(root.GraphID, graphID, StringComparison.Ordinal)) return root;
            foreach (RuntimeNode node in root.AllNodes ?? new List<RuntimeNode>())
            {
                RuntimeNovelGraph child = node switch
                {
                    RuntimeCallNovelPageNode page => page.Graph,
                    RuntimeCallNovelFunctionNode function => function.Function,
                    _ => null
                };
                RuntimeNovelGraph found = FindReachableGraph(child, graphID, visited);
                if (found != null) return found;
            }
            return null;
        }

        private NovelGraphCatalog GetAssetCatalog()
        {
            if (AssetCatalog == null) AssetCatalog = NovelGraphCatalog.LoadDefault();
            return AssetCatalog;
        }

        private static RuntimeNode FindNode(RuntimeNovelGraph graph, string nodeID) =>
            graph?.AllNodes?.FirstOrDefault(item => item != null && string.Equals(item.NodeID, nodeID, StringComparison.Ordinal));

        private static NovelGraphState CaptureGraph(RuntimeNovelGraph graph) => new NovelGraphState
        {
            GraphID = graph?.GraphID ?? string.Empty,
            ContentVersion = graph?.ContentVersion ?? string.Empty,
            GraphSchemaVersion = graph?.SchemaVersion ?? 0
        };

        private static NovelPersistenceResult DuplicateScopeValue(string name) =>
            new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, $"Saved scope value '{name}' is duplicated.");

        private static void SplitOutputKey(string key, out string nodeID, out string name)
        {
            int separator = key?.IndexOf('\n') ?? -1;
            nodeID = separator >= 0 ? key.Substring(0, separator) : key ?? string.Empty;
            name = separator >= 0 ? key.Substring(separator + 1) : string.Empty;
        }
    }
}
