using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Novelify
{
    public partial class NovelGraphRunner
    {
        internal void UseStateStoreInternal(NovelStateStore stateStore)
        {
            UnsubscribeFromStateStore();
            _stateStore = stateStore ?? new NovelStateStore();
            if (isActiveAndEnabled)
            {
                SubscribeToStateStore();
                RefreshVisibleChoices();
            }
        }
        private void ShowChoices(RuntimeChoiceNode node, string restoreChoiceID = null)
        {
            if (_customPresentation == null && BackgroundChoicesPanel != null)
                BackgroundChoicesPanel.SetActive(true);
            var evaluated = new List<(ChoiceData Choice, ChoiceAvailability Availability)>();
            foreach (ChoiceData choice in node.Choices ?? new List<ChoiceData>())
                if (choice != null) evaluated.Add((choice, EvaluateChoiceAvailability(choice)));
            if (!evaluated.Any(item => item.Availability.Available))
            {
                RouteUnavailableChoices(node);
                return;
            }
            if (_customPresentation != null)
            {
                var options = new List<NovelChoicePresentation>();
                foreach ((ChoiceData choice, ChoiceAvailability availability) in evaluated)
                {
                    if (!availability.Available && choice.UnavailablePolicy == NovelChoiceUnavailablePolicy.Hide)
                        continue;
                    options.Add(new NovelChoicePresentation(
                        choice,
                        AsString(Evaluate(choice.ChoiceTextValue), choice.ChoiceText ?? string.Empty),
                        availability.Available,
                        availability.Reason));
                }
                _customPresentation.PresentChoices(options);
                return;
            }
            if (ChoiceButtonPrefab == null || ChoiceButtonContainer == null)
            {
                Debug.LogWarning("ChoiceButtonPrefab or ChoiceButtonContainer is missing.", this);
                return;
            }
            foreach ((ChoiceData choice, ChoiceAvailability availability) in evaluated)
            {
                if (!availability.Available && choice.UnavailablePolicy == NovelChoiceUnavailablePolicy.Hide)
                    continue;
                Button button = Instantiate(ChoiceButtonPrefab, ChoiceButtonContainer);
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                string text = AsString(Evaluate(choice.ChoiceTextValue), choice.ChoiceText ?? string.Empty);
                if (!availability.Available && !string.IsNullOrWhiteSpace(availability.Reason))
                    text += $"\n<color=#A0A0A0>{availability.Reason}</color>";
                if (label != null) label.SetText(text);
                button.interactable = availability.Available;
                if (availability.Available)
                {
                    button.onClick.AddListener(() => TrySelectChoice(node, choice));
                }
                if (!string.IsNullOrEmpty(choice.ChoiceID)) _choiceButtons[choice.ChoiceID] = button;
            }

            if (!string.IsNullOrEmpty(restoreChoiceID) &&
                _choiceButtons.TryGetValue(restoreChoiceID, out Button restored) && restored.interactable)
                restored.Select();
        }

        private sealed class ChoiceAvailability
        {
            public bool Available;
            public string Reason;
            public Dictionary<NovelVariableDefinition, RuntimeValue> PendingChanges;
        }

        private ChoiceAvailability EvaluateChoiceAvailability(ChoiceData choice)
        {
            var result = new ChoiceAvailability { Available = true };
            if (choice.Condition != null && !AsBool(Evaluate(choice.Condition), true))
            {
                result.Available = false;
                result.Reason = AsString(Evaluate(choice.DisabledReasonValue), choice.DisabledReason ?? string.Empty);
                return result;
            }
            if (choice.OnceOnly && StateStore.HasSelectedChoice(choice.ChoiceID))
            {
                result.Available = false;
                result.Reason = AsString(Evaluate(choice.DisabledReasonValue), choice.DisabledReason ?? string.Empty);
                if (string.IsNullOrWhiteSpace(result.Reason)) result.Reason = "Already chosen.";
                return result;
            }
            if (!TryPrepareChoiceTransaction(choice.StateChanges, out Dictionary<NovelVariableDefinition, RuntimeValue> pending,
                    out string transactionError))
            {
                result.Available = false;
                result.Reason = AsString(Evaluate(choice.DisabledReasonValue), choice.DisabledReason ?? string.Empty);
                if (string.IsNullOrWhiteSpace(result.Reason)) result.Reason = transactionError;
                return result;
            }
            result.PendingChanges = pending;
            return result;
        }

        private void TrySelectChoice(RuntimeChoiceNode node, ChoiceData choice)
        {
            TryCommitChoice(node, choice, out _);
        }

        internal bool TryChooseInternal(string choiceID, out string error)
        {
            if (_currentNode is not RuntimeChoiceNode node)
            {
                error = "The session is not currently presenting a Choice node.";
                return false;
            }

            ChoiceData choice = node.Choices?.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.ChoiceID, choiceID, StringComparison.Ordinal));
            if (choice == null)
            {
                error = $"The current Choice node has no option with ID '{choiceID}'.";
                return false;
            }

            return TryCommitChoice(node, choice, out error);
        }

        private bool TryCommitChoice(RuntimeChoiceNode node, ChoiceData choice, out string error)
        {
            error = null;
            if (_choiceSelectionCommitted || _currentNode != node)
            {
                error = "The choice is no longer active.";
                return false;
            }
            if (_isWaiting)
            {
                error = "The graph is waiting and cannot accept a choice yet.";
                return false;
            }
            if (_isTextRevealing)
            {
                CompleteTextImmediately();
                error = "The dialogue reveal was completed; choose again on the next frame.";
                return false;
            }
            if (_textCompletedFrame == Time.frameCount)
            {
                error = "A choice cannot be committed in the same frame that its text reveal completed.";
                return false;
            }

            ChoiceAvailability availability = EvaluateChoiceAvailability(choice);
            if (!availability.Available)
            {
                RefreshVisibleChoices();
                error = string.IsNullOrWhiteSpace(availability.Reason)
                    ? "That choice is currently unavailable."
                    : availability.Reason;
                return false;
            }

            _choiceSelectionCommitted = true;
            if (!CommitChoiceTransaction(availability.PendingChanges, out error))
            {
                _choiceSelectionCommitted = false;
                Debug.LogError($"Choice '{choice.ChoiceID}' could not commit atomically: {error}", this);
                RefreshVisibleChoices();
                return false;
            }
            StateStore.MarkChoiceSelected(choice.ChoiceID);
            Session.RaiseChoiceCommitted(RuntimeGraph, node, choice);
            if (!string.IsNullOrEmpty(choice.DestinationNodeID)) ShowNode(choice.DestinationNodeID);
            else if (TryReturnFromGraph(out string returnNodeID)) ShowNode(returnNodeID);
            else StopGraphInternal();
            return true;
        }

        private bool TryPrepareChoiceTransaction(
            IReadOnlyList<RuntimeChoiceStateChange> changes,
            out Dictionary<NovelVariableDefinition, RuntimeValue> pending,
            out string error)
        {
            pending = new Dictionary<NovelVariableDefinition, RuntimeValue>();
            error = null;
            if (changes == null) return true;
            foreach (RuntimeChoiceStateChange change in changes)
            {
                if (change?.Variable == null) { error = "Transaction has a missing variable."; return false; }
                RuntimeValue current = pending.TryGetValue(change.Variable, out RuntimeValue prior)
                    ? prior
                    : ReadVariable(change.Variable);
                RuntimeValue amount = Evaluate(change.Value);
                if (!NovelStateStore.Matches(change.Variable, amount))
                {
                    error = $"{change.Variable.Name} received the wrong value type.";
                    return false;
                }
                if (!TryCalculateChoiceChange(change, current, amount, out RuntimeValue next, out error)) return false;
                pending[change.Variable] = next;
            }
            return true;
        }

        private static bool TryCalculateChoiceChange(RuntimeChoiceStateChange change, RuntimeValue current,
            RuntimeValue amount, out RuntimeValue result, out string error)
        {
            result = null;
            error = null;
            if (change.Operation == NovelChoiceStateOperation.Set) { result = NovelStateStore.Clone(amount); return true; }
            if (current.Kind == RuntimeValueKind.Integer)
            {
                int left = current.IntegerValue;
                int right = amount.IntegerValue;
                if (change.Operation == NovelChoiceStateOperation.Spend && (right < 0 || left < right))
                { error = $"Not enough {change.Variable.Name}."; return false; }
                if (change.Operation == NovelChoiceStateOperation.Divide && right == 0)
                { error = $"{change.Variable.Name} cannot be divided by zero."; return false; }
                result = RuntimeValue.From(change.Operation switch
                {
                    NovelChoiceStateOperation.Subtract or NovelChoiceStateOperation.Spend => left - right,
                    NovelChoiceStateOperation.Multiply => left * right,
                    NovelChoiceStateOperation.Divide => left / right,
                    _ => left + right
                });
                return true;
            }
            if (current.Kind == RuntimeValueKind.Float)
            {
                float left = current.FloatValue;
                float right = amount.FloatValue;
                if (change.Operation == NovelChoiceStateOperation.Spend && (right < 0f || left < right))
                { error = $"Not enough {change.Variable.Name}."; return false; }
                if (change.Operation == NovelChoiceStateOperation.Divide && Mathf.Approximately(right, 0f))
                { error = $"{change.Variable.Name} cannot be divided by zero."; return false; }
                result = RuntimeValue.From(change.Operation switch
                {
                    NovelChoiceStateOperation.Subtract or NovelChoiceStateOperation.Spend => left - right,
                    NovelChoiceStateOperation.Multiply => left * right,
                    NovelChoiceStateOperation.Divide => left / right,
                    _ => left + right
                });
                return true;
            }
            error = $"{change.Operation} requires a numeric variable.";
            return false;
        }

        private bool CommitChoiceTransaction(
            IReadOnlyDictionary<NovelVariableDefinition, RuntimeValue> pending,
            out string error)
        {
            error = null;
            if (pending == null || pending.Count == 0) return true;
            var shared = new Dictionary<NovelVariableDefinition, RuntimeValue>();
            foreach (KeyValuePair<NovelVariableDefinition, RuntimeValue> change in pending)
                if (change.Key.Scope != NovelVariableScope.CallLocal) shared[change.Key] = change.Value;
            if (!StateStore.TryApplyBatch(shared, out error)) return false;
            foreach (KeyValuePair<NovelVariableDefinition, RuntimeValue> change in pending)
            {
                if (change.Key.Scope != NovelVariableScope.CallLocal) continue;
                _valueScope.Locals[change.Key.ID] = NovelStateStore.Clone(change.Value);
            }
            foreach (KeyValuePair<NovelVariableDefinition, RuntimeValue> change in pending)
                if (change.Key.Scope == NovelVariableScope.CallLocal)
                    LocalVariableChanged?.Invoke(change.Key, NovelStateStore.Clone(change.Value));
            return true;
        }

        private void RouteUnavailableChoices(RuntimeChoiceNode node)
        {
            if (_choiceSelectionCommitted || _currentNode != node) return;
            if (string.IsNullOrEmpty(node.UnavailableDestinationNodeID))
            {
                Debug.LogError("Choice has no actionable options and no Fallback connection.", this);
                return;
            }
            _choiceSelectionCommitted = true;
            ShowNode(node.UnavailableDestinationNodeID);
        }

        private void RefreshVisibleChoices()
        {
            if (_choiceSelectionCommitted || _refreshingChoices || _currentNode is not RuntimeChoiceNode choice ||
                (_customPresentation == null && ChoiceButtonContainer == null)) return;
            string selectedID = null;
            if (_customPresentation == null)
            {
                GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                foreach (KeyValuePair<string, Button> item in _choiceButtons)
                    if (item.Value != null && item.Value.gameObject == selected) { selectedID = item.Key; break; }
            }
            _refreshingChoices = true;
            try
            {
                ClearChoiceButtons();
                ShowChoices(choice, selectedID);
            }
            finally { _refreshingChoices = false; }
        }

        private void SubscribeToStateStore()
        {
            NovelStateStore store = StateStore;
            if (ReferenceEquals(_subscribedStateStore, store)) return;
            UnsubscribeFromStateStore();
            _subscribedStateStore = store;
            store.ValueChanged += OnChoiceRelevantStateChanged;
            store.ChoiceSelected += OnChoiceSelected;
            store.StateReset += RefreshVisibleChoices;
        }

        private void UnsubscribeFromStateStore()
        {
            if (_subscribedStateStore == null) return;
            _subscribedStateStore.ValueChanged -= OnChoiceRelevantStateChanged;
            _subscribedStateStore.ChoiceSelected -= OnChoiceSelected;
            _subscribedStateStore.StateReset -= RefreshVisibleChoices;
            _subscribedStateStore = null;
        }

        private void OnChoiceRelevantStateChanged(NovelVariableDefinition _, RuntimeValue __) => RefreshVisibleChoices();
        private void OnChoiceSelected(string _) => RefreshVisibleChoices();
    }
}
