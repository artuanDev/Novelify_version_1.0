using System;
using System.Collections.Generic;
using UnityEngine;

namespace Novelify
{
    public partial class NovelGraphRunner
    {
        private sealed class HandlerEntry
        {
            public INovelNodeHandler Handler;
            public int Priority;
            public long Order;
        }

        private sealed class HandlerRegistration : IDisposable
        {
            private NovelGraphRunner _runner;
            private HandlerEntry _entry;

            public HandlerRegistration(NovelGraphRunner runner, HandlerEntry entry)
            {
                _runner = runner;
                _entry = entry;
            }

            public void Dispose()
            {
                if (_runner == null) return;
                _runner._nodeHandlers.Remove(_entry);
                _runner = null;
                _entry = null;
            }
        }

        private sealed class DelegateNodeHandler<TNode> : INovelNodeHandler where TNode : RuntimeNode
        {
            private readonly Func<NovelNodeExecutionContext, TNode, NovelNodeExecutionResult> _handler;

            public DelegateNodeHandler(Func<NovelNodeExecutionContext, TNode, NovelNodeExecutionResult> handler) =>
                _handler = handler;

            public bool CanHandle(RuntimeNode node) => node is TNode;

            public NovelNodeExecutionResult Execute(NovelNodeExecutionContext context, RuntimeNode node) =>
                _handler(context, (TNode)node);
        }

        private readonly List<HandlerEntry> _nodeHandlers = new List<HandlerEntry>();
        private long _nextHandlerOrder;
        private INovelPresentation _customPresentation;
        private bool _externallyPaused;
        private string _externalResumeNodeID;

        public INovelPresentation Presentation => _customPresentation;
        internal bool IsExternallyPausedInternal => _externallyPaused;

        /// <summary>
        /// Replaces the built-in TMP/portrait presentation. Configure this before starting a graph.
        /// Pass null to return to Novelify's built-in presentation.
        /// </summary>
        public void UsePresentation(INovelPresentation presentation)
        {
            if (_isGraphRunning)
                throw new InvalidOperationException("Stop the graph before replacing its presentation.");
            _customPresentation?.Stop();
            _customPresentation = presentation;
        }

        public IDisposable RegisterNodeHandler(INovelNodeHandler handler, int priority = 0)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var entry = new HandlerEntry
            {
                Handler = handler,
                Priority = priority,
                Order = _nextHandlerOrder++
            };
            _nodeHandlers.Add(entry);
            _nodeHandlers.Sort((a, b) =>
            {
                int priorityOrder = b.Priority.CompareTo(a.Priority);
                return priorityOrder != 0 ? priorityOrder : a.Order.CompareTo(b.Order);
            });
            return new HandlerRegistration(this, entry);
        }

        public IDisposable RegisterNodeHandler<TNode>(
            Func<NovelNodeExecutionContext, TNode, NovelNodeExecutionResult> handler,
            int priority = 0)
            where TNode : RuntimeNode
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            return RegisterNodeHandler(new DelegateNodeHandler<TNode>(handler), priority);
        }

        internal RuntimeValue EvaluateSessionExpression(RuntimeValueExpression expression) => Evaluate(expression);

        internal bool TryExecuteRegisteredNode(RuntimeNode node, out NovelNodeExecutionResult result)
        {
            HandlerEntry[] snapshot = _nodeHandlers.ToArray();
            foreach (HandlerEntry entry in snapshot)
            {
                if (entry?.Handler == null) continue;
                try
                {
                    if (!entry.Handler.CanHandle(node)) continue;
                    result = entry.Handler.Execute(new NovelNodeExecutionContext(this, node), node);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                    result = NovelNodeExecutionResult.Stop();
                }

                if (result.Kind != NovelNodeExecutionKind.NotHandled) return true;
            }

            result = NovelNodeExecutionResult.NotHandled();
            return false;
        }

        internal void PauseForExternalHandler(string resumeNodeID)
        {
            _externallyPaused = true;
            _externalResumeNodeID = resumeNodeID;
            _isWaiting = true;
        }

        internal bool ResumeExternalNodeInternal(string nextNodeID, out string error)
        {
            if (!_externallyPaused)
            {
                error = "The graph is not paused by a custom node handler.";
                return false;
            }

            string target = string.IsNullOrEmpty(nextNodeID) ? _externalResumeNodeID : nextNodeID;
            _externallyPaused = false;
            _externalResumeNodeID = null;
            _isWaiting = false;
            error = null;

            if (!string.IsNullOrEmpty(target)) ShowNode(target);
            else AdvanceCurrentNode();
            return true;
        }
    }
}
