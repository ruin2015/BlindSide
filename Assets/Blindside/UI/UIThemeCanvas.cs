// Puts this object's CanvasScaler on the UITheme reference resolution for its kind (menu or HUD) when it wakes, so scene
// and prefab canvases follow UITheme.hudReference / menuReference without their own copy of the numbers.
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [RequireComponent(typeof(CanvasScaler))]
    public class UIThemeCanvas : MonoBehaviour
    {
        public UITheme.CanvasKind kind = UITheme.CanvasKind.Menu;

        void Awake() { UITheme.Current.ApplyTo(GetComponent<CanvasScaler>(), kind); }
    }
}
