// Pointer events of one element as callbacks with its index (the shop's product rows: hover selects, a click buys). No state, no
// per-frame work.
using UnityEngine;
using UnityEngine.EventSystems;

namespace Blindside.UI
{
    public class HubPointer : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public int index;
        public System.Action<int> Entered, Clicked;

        public void OnPointerEnter(PointerEventData e) => Entered?.Invoke(index);
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Clicked?.Invoke(index); }
    }
}
