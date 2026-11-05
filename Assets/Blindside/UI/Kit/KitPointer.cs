// Pointer hover / click of a kit block that isn't a Button (the title's side cards, map card, editor button, DEPLOY): forwards the events
// to delegates set once when the block is built. The raycast comes from a Graphic on the same object (its SlantedGraphic filters it).
using UnityEngine;
using UnityEngine.EventSystems;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Pointer")]
    public class KitPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public System.Action<bool> onHover;
        public System.Action onClick;
        public bool interactable = true;

        public void OnPointerEnter(PointerEventData e) { if (interactable && onHover != null) onHover(true); }
        public void OnPointerExit(PointerEventData e) { if (onHover != null) onHover(false); }
        public void OnPointerClick(PointerEventData e)
        {
            if (interactable && e.button == PointerEventData.InputButton.Left && onClick != null) onClick();
        }
    }
}
