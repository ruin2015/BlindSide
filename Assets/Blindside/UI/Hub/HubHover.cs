// A hover / press response for the HQ's buttons and rows: under the pointer the element eases (≈ .12 s, unscaled) toward its hover
// pose — shifted by `shift`, scaled by `scale`, its graphic tinted toward `hoverColor`, an optional marker (an arrow) faded in — and
// eases back when the pointer leaves (an accent bar can widen with it); a press dips the scale for a moment. Raises Entered (once per enter) so a screen can react (a
// line in the speech bubble). Nothing is allocated per frame; when settled it does nothing.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blindside.UI
{
    public class HubHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform target;
        public Graphic graphic;
        public Color baseColor = Color.white, hoverColor = Color.white;
        public Vector2 shift = new Vector2(-14f, 0f);
        public float scale = 1.03f;
        public CanvasGroup marker;
        public bool interactable = true;
        public System.Action Entered;
        /// <summary>Optional: a bar whose width eases from growFrom to growTo with the hover (a tile's accent stripe filling in).</summary>
        public RectTransform grow;
        public float growFrom, growTo;

        Vector2 basePos;
        bool hasBase, over, down;
        float h, press;

        public float Value => h;

        void Awake() { if (target == null) target = (RectTransform)transform; }

        /// <summary>The rest position (call after placing the element, and whenever an entrance moves it).</summary>
        public void SetBase(Vector2 p) { basePos = p; hasBase = true; Apply(); }

        public void OnPointerEnter(PointerEventData e) { if (!interactable) return; over = true; Entered?.Invoke(); }
        public void OnPointerExit(PointerEventData e) { over = false; down = false; }
        public void OnPointerDown(PointerEventData e) { if (interactable) { down = true; press = 1f; } }
        public void OnPointerUp(PointerEventData e) { down = false; }

        void OnDisable() { over = false; down = false; h = 0f; press = 0f; Apply(); }

        void Update()
        {
            float goal = over && interactable ? 1f : 0f;
            float dt = Time.unscaledDeltaTime;
            bool moving = Mathf.Abs(h - goal) > .001f || press > .001f;
            if (!moving) return;
            h = Mathf.MoveTowards(h, goal, dt / .12f);
            if (!down) press = Mathf.MoveTowards(press, 0f, dt / .15f);
            Apply();
        }

        void Apply()
        {
            if (target == null) return;
            float e = UIEase.Tactical(h);
            if (hasBase)
            {
                var p = basePos + shift * e;
                if (target.anchoredPosition != p) target.anchoredPosition = p;
            }
            float s = (1f + (scale - 1f) * e) * (1f - .03f * press);
            if (UIMotionSettings.reduceMotion) s = 1f;
            if (target.localScale.x != s) target.localScale = new Vector3(s, s, 1f);
            if (graphic != null)
            {
                var c = Color.Lerp(baseColor, hoverColor, e);
                if (graphic.color != c) graphic.color = c;
            }
            if (marker != null && marker.alpha != e) marker.alpha = e;
            if (grow != null)
            {
                float w = Mathf.Lerp(growFrom, growTo, e);
                if (grow.sizeDelta.x != w) grow.sizeDelta = new Vector2(w, grow.sizeDelta.y);
            }
        }
    }
}
