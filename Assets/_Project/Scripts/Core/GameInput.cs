using UnityEngine;
using UnityEngine.InputSystem;

namespace Subnet.Core
{
    /// <summary>
    /// Central, code-defined input actions shared by every gameplay system.
    /// The project runs with "Input System Package (New)" only, so all polling goes through here.
    /// Actions are created lazily on first access and stay enabled for the whole session.
    /// Bindings mirror Undertale/Omori conventions: Z / Enter / Space confirm, X / Esc cancel.
    /// </summary>
    public static class GameInput
    {
        private static InputAction move;
        private static InputAction submit;
        private static InputAction cancel;
        private static bool initialized;

        /// <summary>Vector2 movement (WASD, arrows, left stick, d-pad).</summary>
        public static InputAction Move { get { EnsureInitialized(); return move; } }

        /// <summary>Confirm / interact / advance dialogue.</summary>
        public static InputAction Submit { get { EnsureInitialized(); return submit; } }

        /// <summary>Back / cancel / hold-to-skip cinematics.</summary>
        public static InputAction Cancel { get { EnsureInitialized(); return cancel; } }

        /// <summary>Convenience read of the movement vector, clamped to unit length.</summary>
        public static Vector2 MoveVector => Vector2.ClampMagnitude(Move.ReadValue<Vector2>(), 1f);

        /// <summary>True on the frame the confirm button went down.</summary>
        /// (Ignored while the pause menu is open and on the frame it closes.)
        public static bool SubmitPressed => !GameplayInputBlocked && Submit.WasPressedThisFrame();

        /// <summary>True on the frame the cancel button went down.</summary>
        public static bool CancelPressed => !GameplayInputBlocked && Cancel.WasPressedThisFrame();

        private static bool GameplayInputBlocked => GameTime.Paused || GameTime.InputBlockedAfterResume;

        private static void EnsureInitialized()
        {
            if (initialized) return;

            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            move.AddBinding("<Gamepad>/dpad");

            submit = new InputAction("Submit", InputActionType.Button);
            submit.AddBinding("<Keyboard>/z");
            submit.AddBinding("<Keyboard>/e");
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/numpadEnter");
            submit.AddBinding("<Keyboard>/space");
            submit.AddBinding("<Gamepad>/buttonSouth");

            cancel = new InputAction("Cancel", InputActionType.Button);
            cancel.AddBinding("<Keyboard>/x");
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Keyboard>/backspace");
            cancel.AddBinding("<Gamepad>/buttonEast");
            cancel.AddBinding("<Gamepad>/start");

            move.Enable();
            submit.Enable();
            cancel.Enable();
            initialized = true;
        }

        /// <summary>
        /// Disposes actions between play sessions so the static state stays clean
        /// even when "Enter Play Mode Options" disables domain reload.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            move?.Dispose();
            submit?.Dispose();
            cancel?.Dispose();
            move = submit = cancel = null;
            initialized = false;
        }
    }
}
