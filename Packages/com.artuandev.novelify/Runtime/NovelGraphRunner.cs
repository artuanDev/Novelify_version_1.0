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
            if (contentProviderBehaviour != null)
            {
                if (contentProviderBehaviour is INovelContentProvider contentProvider)
                    Session.UseContentProvider(contentProvider);
                else
                    Debug.LogError(contentProviderBehaviour.GetType().Name +
                        " must implement INovelContentProvider.", this);
            }
            if (saveProviderBehaviour != null)
            {
                if (saveProviderBehaviour is INovelSaveProvider saveProvider)
                    Session.UseSaveProvider(saveProvider);
                else
                    Debug.LogError(saveProviderBehaviour.GetType().Name +
                        " must implement INovelSaveProvider.", this);
            }
            if (presentationBehaviour != null)
            {
                if (presentationBehaviour is INovelPresentation presentation)
                    UsePresentation(presentation);
                else
                    Debug.LogError(
                        presentationBehaviour.GetType().Name +
                        " must implement INovelPresentation.", this);
            }

            EnsureGeneratedPresentation(_customPresentation == null);
            InitializePresentation();
        }
        protected virtual void OnEnable() => SubscribeToStateStore();

        protected virtual void OnDisable()
        {
            UnsubscribeFromStateStore();
            Session.Stop();
            _stage?.StopMovement();
        }
    }
}
