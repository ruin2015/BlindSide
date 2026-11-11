// The armoury's angled panels (HubArmoryScreen: a world-space canvas in front of the room's camera) take their clicks ahead of every
// screen-space overlay: a world-space GraphicRaycaster normally comes after the overlays, so the HQ's own UI under the screen (or the
// screen's full-screen picture of the room) would catch the click first. Where no panel is hit, the overlays get it as usual.
using UnityEngine.UI;

namespace Blindside.UI
{
    public class ArmoryPanelRaycaster : GraphicRaycaster
    {
        public override int sortOrderPriority => int.MaxValue;
        public override int renderOrderPriority => int.MaxValue;
    }
}
