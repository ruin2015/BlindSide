// finish(): `G.over=true; G.busy=true` then (1.2s later) `clearUI()` — the selection side of a match ending.
// PlayerCanAct() already reads turns.Over, so pointer/keyboard commands stop the instant the match is decided;
// this drops the open menu / hint / tip / path / dialog so nothing stays half-finished behind the result screen.
using UnityEngine;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        /// <summary>Locks every command (G.busy) and clears the selection UI. Idempotent.</summary>
        public void LockForMatchEnd()
        {
            busy = true;
            if (built) { Deselect(); HideTip(); }
            downActive = false; downPath = false; downMoved = false;
        }
    }
}
