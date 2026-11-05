// ESC goes to one taker per frame. Whoever uses a press (a window closing itself, the battlefield cancelling a selection, a dialog
// answering) calls Consume(); the game menu (PauseMenu) opens only on a press nobody took. Read in the same frame, after the takers.
using UnityEngine;

namespace Blindside.UI
{
    public static class EscapeGate
    {
        static int consumedFrame = -1;

        public static void Consume() => consumedFrame = Time.frameCount;

        /// <summary>This frame's ESC press was already used.</summary>
        public static bool Consumed => consumedFrame == Time.frameCount;
    }
}
