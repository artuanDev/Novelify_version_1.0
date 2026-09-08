using UnityEngine;

namespace Novelify
{
    /// <summary>
    /// Reusable Novelify runtime. It can be driven directly, by NovelManager, or by any game-specific component.
    /// </summary>
    public partial class NovelGraphRunner : MonoBehaviour
    {
        private NovelGraphSession _session;

        public NovelGraphSession Session => _session ??= new NovelGraphSession(this);

        protected virtual void Awake()
        {
            if (presentationBehaviour != null)
            {
                if (presentationBehaviour is INovelPresentation presentation)
                    UsePresentation(presentation);
                else
                    Debug.LogError($"{presentationBehaviour.GetType().Name} must implement INovelPresentation.", this);
            }
            InitializePresentation();
        }
        protected virtual void OnEnable() => SubscribeToStateStore();

        // Compatibility conveniences. New integrations can use Session directly.
        public void PlayGraph(RuntimeNovelGraph graph) => Session.Play(graph);
        public void Advance() => Session.Advance();
        public void EndDialogue() => Session.Stop();
        public void UseStateStore(NovelStateStore stateStore) => Session.UseStateStore(stateStore);

        protected virtual void OnDisable()
        {
            UnsubscribeFromStateStore();
            Session.Stop();
            _stage?.StopMovement();
        }
    }
}
