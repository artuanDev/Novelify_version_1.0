namespace Novelify
{
    /// <summary>
    /// Minimal controller that automatically plays its assigned graph and advances on a pointer click.
    /// Use NovelGraphRunner directly when a game owns its input.
    /// </summary>
    public partial class NovelManager : NovelGraphRunner
    {
        private NovelPlayerController _playerController;
        private static readonly System.Type MouseType = System.Type.GetType(
            "UnityEngine.InputSystem.Mouse, Unity.InputSystem");
        private static readonly System.Reflection.PropertyInfo CurrentMouse =
            MouseType?.GetProperty("current");
        private static readonly System.Reflection.PropertyInfo LeftButton =
            MouseType?.GetProperty("leftButton");

        private void Start()
        {
            _playerController = GetComponent<NovelPlayerController>();
            if (!HasStartedGraph) Session.Play(RuntimeGraph);
        }

        private void Update()
        {
            _playerController ??= GetComponent<NovelPlayerController>();
            if (_playerController != null && _playerController.isActiveAndEnabled) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                Session.Advance();
                return;
            }
#endif
            object mouse = CurrentMouse?.GetValue(null);
            object button = mouse != null ? LeftButton?.GetValue(mouse) : null;
            if (button?.GetType().GetProperty("wasPressedThisFrame")?.GetValue(button) is true)
                Session.Advance();
        }

    }
}
