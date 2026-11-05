// Marks a UI element whose protrusion out of its parent's rect (or text larger than its own rect) is intentional — a portrait that rises
// above its panel, a selection arrow hanging off a card — so the UI lint (Blindside > UI > Lint) skips the "clipped" check for it.
// Only this element is excused, not its children.
using UnityEngine;

namespace Blindside.UI
{
    [DisallowMultipleComponent]
    public class LintIgnoreClip : MonoBehaviour
    {
        [Tooltip("Why the protrusion is intended.")]
        public string reason;
    }
}
