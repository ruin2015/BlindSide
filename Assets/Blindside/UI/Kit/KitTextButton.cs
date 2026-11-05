// A menu entry that is only its text (the title's main menu): no panel, the label in Title (NotoJP-Black). Pointer over it: the text grows
// to `hoverScale` and turns from #c7ced6 to white over .16s (tactical ease); pressed: back to 1.06 while held. The rect is the text's own
// size (plus 8 each side) and is what takes the click (the TMP text is the raycast target). ITimedEffect.Evaluate(t) poses the tween from
// the scale / colour it had to the new state's; Update only advances an unscaled clock while it runs. No allocation per frame.
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Text Button")]
    public class KitTextButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ITimedEffect
    {
        public const float StateTime = .16f, PressScale = 1.06f, PadX = 8f;
        public static readonly Color IdleC = new Color32(0xc7, 0xce, 0xd6, 0xff);

        public TMP_Text label;
        public float hoverScale = 1.18f;
        public UnityEngine.Events.UnityEvent onClick = new UnityEngine.Events.UnityEvent();

        public static readonly Color DisabledC = new Color32(0x5d, 0x64, 0x6c, 0xff);   // dim

        /// <summary>False: drawn dim, no hover, no click.</summary>
        public bool Interactable { get; private set; } = true;

        public void SetInteractable(bool on)
        {
            Interactable = on;
            over = down = false;
            running = false;
            fromScale = toScale = 1f;
            fromC = toC = on ? IdleC : DisabledC;
            Pose(1f, toC);
        }

        bool over, down;
        float fromScale = 1f, toScale = 1f, curScale = 1f;
        Color fromC = IdleC, toC = IdleC, curC = IdleC;
        float clock; bool running;

        public float Duration => StateTime;

        public void OnPointerEnter(PointerEventData e) { if (!Interactable) return; over = true; Go(); }
        public void OnPointerExit(PointerEventData e) { if (!Interactable) return; over = false; down = false; Go(); }
        public void OnPointerDown(PointerEventData e) { if (!Interactable || e.button != PointerEventData.InputButton.Left) return; down = true; Go(); }
        public void OnPointerUp(PointerEventData e) { if (!Interactable) return; down = false; Go(); }
        public void OnPointerClick(PointerEventData e) { if (Interactable && e.button == PointerEventData.InputButton.Left) onClick.Invoke(); }

        void OnDisable()
        {
            over = down = false;
            running = false;
            var c = Interactable ? IdleC : DisabledC;
            Pose(1f, c);
            fromScale = toScale = 1f; fromC = toC = c;
        }

        void Go()
        {
            fromScale = curScale; fromC = curC;
            toScale = down ? PressScale : over ? hoverScale : 1f;
            toC = over ? Color.white : IdleC;
            clock = 0f; running = Application.isPlaying;
            Evaluate(running ? 0f : Duration);
        }

        /// <summary>Previews: the hover look (or the idle one) at once.</summary>
        public void PoseHover(bool on)
        {
            over = on; down = false;
            fromScale = toScale = on ? hoverScale : 1f;
            fromC = toC = on ? Color.white : IdleC;
            Pose(toScale, toC);
        }

        public void Evaluate(float t)
        {
            float k = UIMotionSettings.reduceMotion ? 1f : UIEase.Tactical(Mathf.Clamp01(t / StateTime));
            Pose(Mathf.LerpUnclamped(fromScale, toScale, k), Color.Lerp(fromC, toC, k));
        }

        void Pose(float s, Color c)
        {
            curScale = s; curC = c;
            var ls = new Vector3(s, s, 1f);
            if (transform.localScale != ls) transform.localScale = ls;
            if (label != null && label.color != c) label.color = c;
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }

        /// <summary>An entry `text` under `parent` (size `fontSize`), its pivot at `pivotX` (0 left, .5 centre, 1 right) so it grows from
        /// there. Place it with UILayout / anchoredPosition; its rect is the text's width + 2 × 8 by `height`.</summary>
        public static KitTextButton Create(Transform parent, string name, string text, float fontSize, float height, float pivotX, UnityEngine.Events.UnityAction onClick)
        {
            var rt = HudKit.NewRect(name, parent);
            var b = rt.gameObject.AddComponent<KitTextButton>();
            b.label = HudKit.AddText(rt, text, fontSize, IdleC, true, TextAlignmentOptions.Center, "Label", UITheme.TextRole.Title);
            b.label.textWrappingMode = TextWrappingModes.NoWrap;
            b.label.raycastTarget = true;
            HudKit.Stretch(b.label.rectTransform);
            float w = Mathf.Ceil(b.label.GetPreferredValues(text).x) + PadX * 2f;
            rt.sizeDelta = new Vector2(w, height);
            rt.pivot = new Vector2(pivotX, .5f);
            if (onClick != null) b.onClick.AddListener(onClick);
            b.Pose(1f, IdleC);
            return b;
        }
    }
}
