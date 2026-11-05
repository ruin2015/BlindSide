// Marks a UI element whose overlap with its siblings is intentional (a layered decoration, a badge over a card …), so the UI lint
// (Blindside > UI > Lint) does not report it. `bounds` also excuses it — and everything under it — from the "runs out of its parent"
// check (a decoration meant to be clipped by its parent's mask, a shadow wider than its box).
using UnityEngine;

namespace Blindside.UI
{
    [DisallowMultipleComponent]
    public class LintIgnoreOverlap : MonoBehaviour
    {
        [Tooltip("Also skip the parent-bounds and off-screen checks for this element and its children.")]
        public bool bounds;
        [Tooltip("Why the overlap is intended (shown in the lint report's ignored list).")]
        public string reason;
    }
}
