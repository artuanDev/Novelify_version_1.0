using UnityEngine;
using UnityEngine.InputSystem;

namespace Novelify.InputSystem
{
    /// <summary>
    /// Optional Input System adapter for advancing a Novelify conversation.
    /// Choices continue to use the scene's EventSystem navigation and submit actions.
    /// </summary>
    [AddComponentMenu("Novelify/Input System Advance")]
    [DisallowMultipleComponent]
    public sealed class NovelInputSystemAdvance : MonoBehaviour
    {
        [SerializeField] private NovelGraphRunner runner;
        [SerializeField] private bool advanceWithPrimaryPointer = true;
        [SerializeField] private bool advanceWithKeyboard = true;

        private void Awake()
        {
            if (runner == null)
                runner = GetComponent<NovelGraphRunner>();
        }

        private void Update()
        {
            if (runner == null)
                return;

            bool pointerPressed = advanceWithPrimaryPointer &&
                                  Mouse.current != null &&
                                  Mouse.current.leftButton.wasPressedThisFrame;
            bool keyboardPressed = advanceWithKeyboard &&
                                   Keyboard.current != null &&
                                   (Keyboard.current.spaceKey.wasPressedThisFrame ||
                                    Keyboard.current.enterKey.wasPressedThisFrame ||
                                    Keyboard.current.numpadEnterKey.wasPressedThisFrame);

            if (pointerPressed || keyboardPressed)
                runner.Session.Advance();
        }
    }
}
