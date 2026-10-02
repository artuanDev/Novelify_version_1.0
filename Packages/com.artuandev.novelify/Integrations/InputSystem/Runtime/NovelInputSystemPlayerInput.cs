using UnityEngine;
using UnityEngine.InputSystem;

namespace Novelify.InputSystem
{
    /// <summary>Keyboard, pointer, touch and gamepad shortcuts for NovelPlayerController.</summary>
    [AddComponentMenu("Novelify/Input System Player Input")]
    [UnityEngine.Scripting.Preserve]
    [RequireComponent(typeof(NovelPlayerController))]
    [DisallowMultipleComponent]
    public sealed class NovelInputSystemPlayerInput : MonoBehaviour
    {
        private NovelPlayerController _controller;

        private void Awake() => _controller = GetComponent<NovelPlayerController>();

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            Mouse mouse = Mouse.current;
            Touchscreen touch = Touchscreen.current;

            if (keyboard?.escapeKey.wasPressedThisFrame == true)
                _controller.CloseOverlays();
            if (keyboard?.backspaceKey.wasPressedThisFrame == true ||
                gamepad?.buttonEast.wasPressedThisFrame == true)
                _controller.ToggleBacklog();
            if (keyboard?.pageUpKey.wasPressedThisFrame == true ||
                gamepad?.dpad.up.wasPressedThisFrame == true)
                _controller.ScrollBacklog(0.25f);
            if (keyboard?.pageDownKey.wasPressedThisFrame == true ||
                gamepad?.dpad.down.wasPressedThisFrame == true)
                _controller.ScrollBacklog(-0.25f);
            if (keyboard?.pKey.wasPressedThisFrame == true ||
                gamepad?.startButton.wasPressedThisFrame == true)
                _controller.ToggleSettings();
            if (keyboard?.aKey.wasPressedThisFrame == true ||
                gamepad?.buttonWest.wasPressedThisFrame == true)
                _controller.ToggleAuto();
            if (keyboard?.sKey.wasPressedThisFrame == true ||
                gamepad?.buttonNorth.wasPressedThisFrame == true)
                _controller.ToggleSkip();

            bool pointer = mouse?.leftButton.wasPressedThisFrame == true &&
                           !_controller.IsInteractiveUIAt(mouse.position.ReadValue());
            if (touch?.primaryTouch.press.wasPressedThisFrame == true)
            {
                if (!_controller.IsInteractiveUIAt(touch.primaryTouch.position.ReadValue()))
                    pointer = true;
            }
            if (pointer || keyboard?.spaceKey.wasPressedThisFrame == true ||
                keyboard?.enterKey.wasPressedThisFrame == true ||
                keyboard?.numpadEnterKey.wasPressedThisFrame == true ||
                gamepad?.buttonSouth.wasPressedThisFrame == true)
                _controller.Advance();
        }
    }
}
