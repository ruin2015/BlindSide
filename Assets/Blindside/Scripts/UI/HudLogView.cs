// #log view (HUD_Log.prefab). CSS, 00_markup_and_styles.html:155-158:
//   #log{position:fixed;right:14px;bottom:96px;width:310px;display:flex;flex-direction:column;gap:2px;pointer-events:none}
//   #log div{font:500 11px var(--fk);background:rgba(14,16,20,.84);padding:3px 8px 3px 10px;border-left:3px solid var(--dim);
//            animation:logIn .3s both;color:#d7dce1;position:relative}
//   #log div::after{content:'';position:absolute;right:4px;top:50%;width:3px;height:3px;background:rgba(255,255,255,.3)}
//   #log div.a{border-color:var(--blu)} #log div.f{border-color:var(--foe)} #log div.w{border-color:var(--acc);color:#ffe9a8}
//   @keyframes logIn{from{transform:translateX(20px);opacity:0}to{transform:none;opacity:1}}
// Entries are stacked by hand (newest at the bottom) instead of through a LayoutGroup, so the logIn
// slide can move each entry's Inner child without a layout rebuild snapping it back.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Blindside.UI
{
    public class HudLogView : MonoBehaviour
    {
        [Tooltip("Inactive template: Entry root → Inner (bg Image + CanvasGroup) → Border / Dot / Text")]
        public RectTransform template;
        public float gap = 2f;

        static readonly Color BorderDim = UIPalette.Hex("#838A93");   // --dim
        static readonly Color TextNormal = UIPalette.Hex("#D7DCE1");
        static readonly Color TextWarn = UIPalette.Hex("#FFE9A8");

        class Entry { public RectTransform root, inner; public CanvasGroup cg; public float height; public string text; }
        readonly List<Entry> entries = new List<Entry>();

        // #log{left:calc(50% - 135px);bottom:76px;width:400px;gap:3px} div{height:24px;padding:4px 20px 0 30px;12px;nowrap;overflow:hidden}
        const float FromCentre = 135f, Bottom = 76f, PadL = 30f, PadR = 20f, PadT = 4f;

        void ApplyLayout()
        {
            var H = UITheme.Current.hud;
            gap = 3f;
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0f); rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(-FromCentre, Bottom);
            rt.sizeDelta = new Vector2(H.logWidth, 0f);
        }

        void OnEnable()
        {
            ApplyLayout();
            HudLog.OnAdd += Add;
            HudLog.OnCleared += ClearAll;
            HudLog.OnRemoved += RemoveLine;
            // A display:none → block toggle replays CSS animations, so re-showing #log slides every line in again.
            ClearAll();
            foreach (var (text, type) in HudLog.Recent) Add(text, type);
        }

        void OnDisable()
        {
            HudLog.OnAdd -= Add;
            HudLog.OnCleared -= ClearAll;
            HudLog.OnRemoved -= RemoveLine;
        }

        void RemoveLine(string text)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].text != text) continue;
                if (entries[i].root != null) Destroy(entries[i].root.gameObject);
                entries.RemoveAt(i);
                Restack();
                return;
            }
        }

        void ClearAll()
        {
            foreach (var e in entries) if (e.root != null) HudKit.DestroySafe(e.root.gameObject);
            entries.Clear();
        }

        void Add(string text, HudLogType type)
        {
            if (template == null) return;
            var root = Instantiate(template, template.parent);
            root.name = "Entry";
            root.gameObject.SetActive(true);
            var e = new Entry { root = root, inner = (RectTransform)root.Find("Inner"), text = text };
            e.cg = e.inner.GetComponent<CanvasGroup>();

            var border = e.inner.Find("Border").GetComponent<UnityEngine.UI.Image>();
            border.color = type switch
            {
                HudLogType.Ally => UIPalette.Blue,
                HudLogType.Foe => UIPalette.Foe,
                HudLogType.Warn => UIPalette.Accent,
                _ => BorderDim,
            };
            var tmp = e.inner.Find("Text").GetComponent<TMP_Text>();
            tmp.text = text;
            tmp.color = type == HudLogType.Warn ? TextWarn : TextNormal;

            // a fixed UITheme.hud.logLineHeight row; the text one line, cut at the right
            e.height = UITheme.Current.hud.logLineHeight;
            root.sizeDelta = new Vector2(root.sizeDelta.x, e.height);
            tmp.fontSize = 12f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap; tmp.overflowMode = TextOverflowModes.Ellipsis;
            var trt = tmp.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(PadL, 0f); trt.offsetMax = new Vector2(-PadR, -PadT);
            tmp.alignment = TextAlignmentOptions.TopLeft;
            var brt = border.rectTransform;                 // ::before{left:10px;width:4px}
            brt.anchoredPosition = new Vector2(10f, 0f); brt.sizeDelta = new Vector2(4f, brt.sizeDelta.y);

            entries.Add(e);
            while (entries.Count > HudLog.MaxLines)
            {
                var old = entries[0]; entries.RemoveAt(0);
                if (old.root != null) HudKit.DestroySafe(old.root.gameObject);
            }
            Restack();
            if (Application.isPlaying) StartCoroutine(LogIn(e));
        }

        /// <summary>Editor previews: lays the log out and adds a line directly (no HudLog subscription, no slide-in).</summary>
        public void PreviewLine(string text, HudLogType type)
        {
            ApplyLayout();
            Add(text, type);
        }

        // flex-direction:column anchored at the bottom: the last line sits at y=0, earlier ones above.
        void Restack()
        {
            float y = 0f;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var r = entries[i].root;
                if (r == null) continue;
                r.anchoredPosition = new Vector2(0f, y);
                y += entries[i].height + gap;
            }
        }

        static IEnumerator LogIn(Entry e)
        {
            const float dur = .3f;
            float t = 0f;
            while (t < dur && e.inner != null)
            {
                t += Time.unscaledDeltaTime;
                float p = CssEase(Mathf.Clamp01(t / dur));
                e.inner.anchoredPosition = new Vector2(20f * (1f - p), 0f);
                if (e.cg) e.cg.alpha = p;
                yield return null;
            }
            if (e.inner != null) { e.inner.anchoredPosition = Vector2.zero; if (e.cg) e.cg.alpha = 1f; }
        }

        /// <summary>CSS `ease` = cubic-bezier(.25,.1,.25,1) (logIn sets no timing function).</summary>
        static float CssEase(float x)
        {
            const float x1 = .25f, y1 = .1f, x2 = .25f, y2 = 1f;
            float t = x;
            for (int i = 0; i < 6; i++)
            {
                float bx = Bez(t, x1, x2) - x, d = BezD(t, x1, x2);
                if (Mathf.Abs(d) < 1e-5f) break;
                t = Mathf.Clamp01(t - bx / d);
            }
            return Bez(t, y1, y2);
        }
        static float Bez(float t, float p1, float p2) { float u = 1f - t; return 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t; }
        static float BezD(float t, float p1, float p2) { float u = 1f - t; return 3f * u * u * p1 + 6f * u * t * (p2 - p1) + 3f * t * t * (1f - p2); }
    }
}
