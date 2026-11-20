// The buttons' sounds, for every screen at once (no button needs wiring): the pointer coming onto something clickable — a Selectable
// that is interactable, or anything with a click handler (KitTextButton, the dialog's buttons, the roster cards …) — plays UiHover
// (once per thing, not while it moves over it); a left click on one plays UiConfirm. The thing under the pointer is found with the
// EventSystem's own raycast (only when the pointer moved or clicked), so it is exactly what would take the click.
// Lives on its own DontDestroyOnLoad object, made as the first scene loads. One reused list and event: nothing allocated per frame.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Blindside.UI
{
    public class UiSounds : MonoBehaviour
    {
        const float HoverVol = .3f, ConfirmVol = .8f, HoverGap = .04f;

        readonly List<RaycastResult> hits = new List<RaycastResult>(16);
        PointerEventData ped;
        EventSystem pedOwner;
        GameObject hovered;
        Vector2 lastPos = new Vector2(-1f, -1f);
        float lastHover = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<UiSounds>() != null) return;
            var go = new GameObject("UiSounds");
            DontDestroyOnLoad(go);
            go.AddComponent<UiSounds>();
        }

        void Update()
        {
            var es = EventSystem.current; var m = Mouse.current;
            if (es == null || !es.isActiveAndEnabled || m == null) { hovered = null; return; }
            Vector2 pos = m.position.ReadValue();
            bool click = m.leftButton.wasPressedThisFrame;
            if (pos != lastPos || click)
            {
                lastPos = pos;
                var now = Clickable(es, pos);
                if (now != hovered)
                {
                    hovered = now;
                    float t = Time.unscaledTime;
                    if (now != null && (lastHover < 0f || t - lastHover >= HoverGap)) { lastHover = t; SoundBank.Play(Snd.UiHover, HoverVol); }
                }
            }
            if (click && hovered != null) SoundBank.Play(Snd.UiConfirm, ConfirmVol);
        }

        /// <summary>The clickable thing under `pos` (the topmost UI hit's interactable Selectable or click handler), or null.</summary>
        GameObject Clickable(EventSystem es, Vector2 pos)
        {
            if (ped == null || pedOwner != es) { ped = new PointerEventData(es); pedOwner = es; }
            ped.position = pos;
            hits.Clear();
            es.RaycastAll(ped, hits);
            if (hits.Count == 0) return null;
            var go = hits[0].gameObject;
            if (go == null) return null;
            var sel = go.GetComponentInParent<Selectable>();
            if (sel != null) return sel.IsInteractable() && sel.isActiveAndEnabled ? sel.gameObject : null;
            return ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);
        }
    }
}
