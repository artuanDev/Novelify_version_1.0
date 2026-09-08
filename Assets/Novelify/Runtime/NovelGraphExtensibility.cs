using System;
using System.Collections.Generic;

namespace Novelify
{
    /// <summary>Presentation contract used by NovelGraphRunner. Implement this to supply a game's own UI.</summary>
    public interface INovelPresentation
    {
        bool IsRevealing { get; }
        void PresentDialogue(NovelDialoguePresentation dialogue);
        void PresentChoices(IReadOnlyList<NovelChoicePresentation> choices);
        void CompleteReveal();
        void HideDialogue();
        void ClearChoices();
        void Stop();
    }

    public sealed class NovelDialoguePresentation
    {
        private readonly Action _revealCompleted;
        private bool _completionSent;

        internal NovelDialoguePresentation(
            NovelGraphSession session,
            RuntimeNovelGraph graph,
            RuntimeDialogueNode node,
            NovelCharacterReference speaker,
            string speakerName,
            string text,
            Action revealCompleted)
        {
            Session = session;
            Graph = graph;
            Node = node;
            Speaker = speaker;
            SpeakerName = speakerName ?? string.Empty;
            Text = text ?? string.Empty;
            _revealCompleted = revealCompleted;
        }

        public NovelGraphSession Session { get; }
        public RuntimeNovelGraph Graph { get; }
        public RuntimeDialogueNode Node { get; }
        public NovelCharacterReference Speaker { get; }
        public string SpeakerName { get; }
        public string Text { get; }
        public bool IsChoice => Node is RuntimeChoiceNode;

        /// <summary>Call once when a custom typewriter/reveal animation finishes naturally.</summary>
        public void NotifyRevealCompleted()
        {
            if (_completionSent) return;
            _completionSent = true;
            _revealCompleted?.Invoke();
        }
    }

    public sealed class NovelChoicePresentation
    {
        internal NovelChoicePresentation(ChoiceData choice, string text, bool available, string disabledReason)
        {
            ChoiceID = choice?.ChoiceID ?? string.Empty;
            Text = text ?? string.Empty;
            Available = available;
            DisabledReason = disabledReason ?? string.Empty;
        }

        public string ChoiceID { get; }
        public string Text { get; }
        public bool Available { get; }
        public string DisabledReason { get; }
    }

    public enum NovelNodeExecutionKind
    {
        NotHandled,
        Continue,
        Jump,
        Pause,
        Stop
    }

    public readonly struct NovelNodeExecutionResult
    {
        private NovelNodeExecutionResult(NovelNodeExecutionKind kind, string nextNodeID)
        {
            Kind = kind;
            NextNodeID = nextNodeID;
        }

        public NovelNodeExecutionKind Kind { get; }
        public string NextNodeID { get; }

        public static NovelNodeExecutionResult NotHandled() =>
            new NovelNodeExecutionResult(NovelNodeExecutionKind.NotHandled, null);

        public static NovelNodeExecutionResult Continue(string nextNodeID = null) =>
            new NovelNodeExecutionResult(NovelNodeExecutionKind.Continue, nextNodeID);

        public static NovelNodeExecutionResult Jump(string nodeID) =>
            new NovelNodeExecutionResult(NovelNodeExecutionKind.Jump, nodeID);

        public static NovelNodeExecutionResult Pause(string resumeNodeID = null) =>
            new NovelNodeExecutionResult(NovelNodeExecutionKind.Pause, resumeNodeID);

        public static NovelNodeExecutionResult Stop() =>
            new NovelNodeExecutionResult(NovelNodeExecutionKind.Stop, null);
    }

    public sealed class NovelNodeExecutionContext
    {
        internal NovelNodeExecutionContext(NovelGraphRunner runner, RuntimeNode node)
        {
            Runner = runner;
            Node = node;
        }

        public NovelGraphRunner Runner { get; }
        public NovelGraphSession Session => Runner.Session;
        public RuntimeNovelGraph Graph => Runner.RuntimeGraph;
        public RuntimeNode Node { get; }

        public RuntimeValue Evaluate(RuntimeValueExpression expression) =>
            Runner.EvaluateSessionExpression(expression);

        public RuntimeValue GetVariable(NovelVariableDefinition variable) =>
            Session.GetVariable(variable);

        public bool TrySetVariable(NovelVariableDefinition variable, RuntimeValue value, out string error) =>
            Session.TrySetVariable(variable, value, out error);
    }

    /// <summary>
    /// Handles a runtime node before Novelify's built-in handlers. Return NotHandled to let lower-priority
    /// handlers or the built-in runtime process it.
    /// </summary>
    public interface INovelNodeHandler
    {
        bool CanHandle(RuntimeNode node);
        NovelNodeExecutionResult Execute(NovelNodeExecutionContext context, RuntimeNode node);
    }
}
