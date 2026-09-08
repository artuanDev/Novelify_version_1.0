using UnityEngine.InputSystem;

namespace Novelify
{
    /// <summary>
    /// Minimal example controller: auto-plays its assigned graph and advances it with the left mouse button.
    /// Use NovelGraphRunner directly when a game needs different input, UI, or orchestration.
    /// </summary>
    public partial class NovelManager : NovelGraphRunner
    {
        private void Start()
        {
            if (!HasStartedGraph) Session.Play(RuntimeGraph);
        }

        private void Update()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                Session.Advance();
        }
    }
}
