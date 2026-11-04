using UnityEngine;
using UnityEngine.InputSystem;

namespace Subnet.Core
{
    /// <summary>
    /// The game's clock. Dialogue, cinematics, hacking and music run on unscaled time (so slow-mo never affects them),
    /// but they must still freeze in the pause menu: they read time from here instead of Time.unscaled*.
    /// Pausing also pauses all audio, so tracks resume exactly where they stopped.
    /// </summary>
    public static class GameTime
    {
        private static bool paused;
        private static float pauseStarted;
        private static float pausedTotal;
        private static int resumedFrame = -10;

        public static bool Paused => paused;

        /// <summary>Unscaled delta time that is 0 while the game is paused.</summary>
        public static float UnscaledDelta => paused ? 0f : Time.unscaledDeltaTime;

        /// <summary>Unscaled time that stops advancing while the game is paused.</summary>
        public static float UnscaledTime => (paused ? pauseStarted : Time.unscaledTime) - pausedTotal;

        /// <summary>
        /// True while paused, on the frame the pause key is pressed, and right after resuming,
        /// so the presses that open/close the menu never advance a dialogue or pick a choice.
        /// </summary>
        public static bool InputBlocked => paused || Time.frameCount - resumedFrame <= 1 || PauseKeyPressedThisFrame;

        /// <summary>The frame the pause menu closed and the next one.</summary>
        public static bool InputBlockedAfterResume => Time.frameCount - resumedFrame <= 1;

        public static bool PauseKeyPressedThisFrame
        {
            get
            {
                bool key = Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame);
                bool pad = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
                return key || pad;
            }
        }

        public static void SetPaused(bool value)
        {
            if (value == paused) return;
            paused = value;
            if (value)
            {
                pauseStarted = Time.unscaledTime;
            }
            else
            {
                pausedTotal += Time.unscaledTime - pauseStarted;
                resumedFrame = Time.frameCount;
            }
            Time.timeScale = value ? 0f : 1f;
            AudioListener.pause = value;
        }

        /// <summary>Like WaitForSecondsRealtime, but stops counting while paused.</summary>
        public static CustomYieldInstruction Wait(float seconds) => new PausableWait(seconds);

        private class PausableWait : CustomYieldInstruction
        {
            private readonly float end;
            public PausableWait(float seconds) { end = UnscaledTime + seconds; }
            public override bool keepWaiting => UnscaledTime < end;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            paused = false;
            pausedTotal = 0f;
            resumedFrame = -10;
            AudioListener.pause = false;
        }
    }
}
