// One .rcard (template inside HUD_Roster.prefab). CSS, 00_markup_and_styles.html:80-99; markup from buildRoster():
//   <div class="rcard"><div class="rc-por">{art}<span>{KEY.slice(0,2)}</span></div><div class="rc-body">
//     <div class="rc-top"><b>{KEY}</b><small>{kr} · {role}</small></div><div class="bar"><i></i></div>
//     <div class="rc-st"><span class="hpn"></span><span class="st"></span></div></div></div>
//   .rcard{display:flex;transition:transform .18s;box-shadow:0 4px 12px rgba(0,0,0,.4)}  (+ --hatch/--pp)
//   .rcard::after{right:0;top:0;bottom:0;width:4px;background:var(--ink)}
//   .rcard:hover{transform:translateX(4px)}  .rcard.sel{transform:translateX(12px)}
//   .rcard.sel::after{background:var(--blu);width:6px}
//   .rcard.sel::before{left:-12px;top:50%;margin-top:-6px;border:6px solid transparent;border-left-color:var(--blu)}
//   .rcard.ended{filter:grayscale(.9) brightness(.62)}  .rcard.dead{filter:grayscale(1) brightness(.4)}
//   .rcard.dead .rc-por::after{content:'K.I.A';inset:0;background:rgba(229,54,77,.85);font:700 11px var(--fd);color:#fff}
//   .bar i{background:var(--c);transition:width .5s cubic-bezier(.2,.9,.2,1)}
//   .rc-st .st{color:var(--blu)} .rc-st .st.end{color:var(--dimp)}
// Hierarchy: Card (this, placed by HudRoster) → Inner (UIStaggerIn's slideInL target) → Visual (hover/sel
// translateX target) → everything drawn. CSS `filter` is reproduced by recolouring every Graphic under
// Visual from its unfiltered colour (grayscale mix with Rec.709 luma, then a brightness multiply).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    public class HudRosterCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public RectTransform visual;
        public Image porBorder;              // .rc-por border-left:4px solid var(--c)
        public TMP_Text initials, initialsShadow;
        public GameObject kia;
        public TMP_Text nameText, subText;
        public RectTransform barFill;        // .bar i (anchored left, width = % of the bar)
        public Image barFillImage;
        public TMP_Text hpText, statusText;
        public Image edge;                   // ::after
        public GameObject selArrow;          // .sel::before

        public event System.Action<HudRosterCard> Clicked;
        public object Tag;                   // whatever the HUD binds this card to

        static readonly Color Dimp = UIPalette.Dim;

        readonly Dictionary<Graphic, Color> baseColors = new Dictionary<Graphic, Color>();
        Graphic[] graphics;
        bool hover, sel, ended, dead;
        float offsetX, fillFrac = -1f;
        Coroutine fillTween;

        void Awake()
        {
            // Idempotent: SetIdentity may run this before Unity does (clones start inactive), and a
            // second pass would re-capture already-filtered colours as the "unfiltered" base.
            if (graphics != null) return;
            graphics = visual.GetComponentsInChildren<Graphic>(true);
            foreach (var g in graphics) baseColors[g] = g.color;
        }

        void SetBase(Graphic g, Color c) { baseColors[g] = c; }

        /// <summary>The mockup's .rc box (UITheme.hud.rosterCard 250×62): .bar 8px colour edge, .por rosterFace×rosterFace,
        /// .info{padding:7px 18px 0 10px} with .nm (20px: 18px KEY, 12px kr · role), .seg (7px, 6px below) and .row (15px, 4px below).</summary>
        public void ApplyLayout()
        {
            var H = UITheme.Current.hud;
            float w = H.rosterCard.x, h = H.rosterCard.y, face = H.rosterFace;
            UILayout.Fixed((RectTransform)transform, w, h);

            var por = (RectTransform)porBorder.transform.parent;       // Por: the colour edge + the face box
            UILayout.Place(por, 0, 0, 8 + face, h);
            UILayout.Place(porBorder.rectTransform, 0, 0, 8, h);
            UILayout.Place((RectTransform)initials.transform.parent, 8, 0, face, h);

            float x = 8 + face + 10, iw = w - x - 18;
            UILayout.Place(nameText.rectTransform, x, 7, iw, 20);
            nameText.fontSize = 18;
            UILayout.Place(subText.rectTransform, x, 7, iw, 20);
            subText.fontSize = 12;
            UILayout.Place((RectTransform)barFill.parent, x, 33, iw, 7);
            UILayout.Place(hpText.rectTransform, x, 44, iw, 15);
            hpText.fontSize = 11.5f;
            UILayout.Place(statusText.rectTransform, x, 44, iw, 15);
            statusText.fontSize = 12;
        }

        /// <summary>Per-operator constants: --c, the monogram, KEY and `kr · role`.</summary>
        public void SetIdentity(string key, string sub, Color c)
        {
            if (graphics == null) Awake();
            string mono = key.Length >= 2 ? key.Substring(0, 2) : key;
            initials.text = mono; initialsShadow.text = mono;
            // The face (paper-gradient slot, no monogram) when this operator has art; otherwise the old monogram on the dark slot.
            var slot = initials.transform.parent.GetComponent<Image>();
            var face = OperatorArt.ApplyFace(slot, key);
            if (face != null)
            {
                initials.gameObject.SetActive(false); initialsShadow.gameObject.SetActive(false);
                var shade = slot.transform.Find("Shade"); if (shade != null) shade.gameObject.SetActive(false);
                SetBase(slot, Color.white); SetBase(face, Color.white);   // ApplyFilter (dead / ended greying) starts from these
            }
            nameText.text = key; subText.text = sub;
            LayoutTopRow();
            SetBase(porBorder, c);
            SetBase(barFillImage, c);
            ApplyFilter();
        }

        /// <summary>.rc-top{display:flex;justify-content:space-between;gap:6px} with `small{white-space:nowrap}`:
        /// the small label sits flush right when both fit, otherwise it starts 6px after the name and
        /// overflows to the right (flex items don't shrink below their nowrap content).</summary>
        void LayoutTopRow()
        {
            var nameRt = nameText.rectTransform; var subRt = subText.rectTransform;
            float boxW = nameRt.rect.width;
            float nameW = nameText.GetPreferredValues(nameText.text).x;
            float subW = subText.GetPreferredValues(subText.text).x;
            if (nameW + 6f + subW <= boxW)
            {
                subRt.anchoredPosition = nameRt.anchoredPosition;
                subRt.sizeDelta = nameRt.sizeDelta;
                subText.alignment = TextAlignmentOptions.BottomRight;
            }
            else
            {
                subRt.anchoredPosition = nameRt.anchoredPosition + new Vector2(Mathf.Ceil(nameW) + 6f, 0f);
                subRt.sizeDelta = new Vector2(Mathf.Ceil(subW) + 1f, nameRt.sizeDelta.y);
                subText.alignment = TextAlignmentOptions.BottomLeft;
            }
        }

        /// <summary>updateHUD()'s per-card pass.</summary>
        public void SetState(int hp, int maxHp, string status, bool statusEnded, bool selected, bool isEnded, bool isDead, bool animateBar)
        {
            if (graphics == null) Awake();
            hpText.text = $"{hp}/{maxHp}";
            statusText.text = status;
            SetBase(statusText, statusEnded ? Dimp : UIPalette.Blue);

            float frac = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
            if (!Mathf.Approximately(frac, fillFrac))
            {
                if (fillTween != null) StopCoroutine(fillTween);
                if (animateBar && fillFrac >= 0f && gameObject.activeInHierarchy && Application.isPlaying) fillTween = StartCoroutine(TweenFill(fillFrac, frac));
                else SetFill(frac);
                fillFrac = frac;
            }

            if (selected != sel) { sel = selected; UpdateOffsetTarget(); }
            selArrow.SetActive(sel);
            var er = edge.rectTransform; er.sizeDelta = new Vector2(sel ? 6f : 4f, er.sizeDelta.y);
            SetBase(edge, sel ? UIPalette.Blue : UIPalette.Ink);

            ended = isEnded; dead = isDead;
            kia.SetActive(dead);
            ApplyFilter();
        }

        /// <summary>The deploy-phase branch leaves .dead untouched and only drops .ended.</summary>
        public void SetDeploy(int hp, int maxHp, string status, bool selected)
            => SetState(hp, maxHp, status, false, selected, false, dead, true);

        void SetFill(float f) { barFill.anchorMax = new Vector2(f, 1f); barFill.sizeDelta = new Vector2(0f, barFill.sizeDelta.y); }

        IEnumerator TweenFill(float from, float to)
        {
            float t = 0f;
            while (t < .5f) { t += Time.unscaledDeltaTime; SetFill(Mathf.LerpUnclamped(from, to, UIEase.Tactical(t / .5f))); yield return null; }
            SetFill(to); fillTween = null;
        }

        void ApplyFilter()
        {
            float gray = dead ? 1f : ended ? .9f : 0f;
            float bright = dead ? .4f : ended ? .62f : 1f;
            foreach (var kv in baseColors)
            {
                if (kv.Key == null) continue;
                Color c = kv.Value;
                float l = .2126f * c.r + .7152f * c.g + .0722f * c.b;
                kv.Key.color = new Color(Mathf.Lerp(c.r, l, gray) * bright, Mathf.Lerp(c.g, l, gray) * bright, Mathf.Lerp(c.b, l, gray) * bright, c.a);
            }
        }

        // transition:transform .18s (CSS default `ease`), target 12px when selected, 4px on hover.
        // A coroutine rather than Update + `enabled=false`: a disabled component stops receiving
        // pointer events, which would kill hover/click.
        float TargetX => sel ? 12f : hover ? 4f : 0f;
        Coroutine moveTween;

        void UpdateOffsetTarget()
        {
            if (moveTween != null) StopCoroutine(moveTween);
            if (!gameObject.activeInHierarchy || !Application.isPlaying) { offsetX = TargetX; visual.anchoredPosition = new Vector2(offsetX, 0f); return; }
            moveTween = StartCoroutine(MoveTo(offsetX, TargetX));
        }

        IEnumerator MoveTo(float from, float to)
        {
            float t = 0f;
            while (t < .18f)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / .18f), e = p * p * (3f - 2f * p);
                offsetX = Mathf.Lerp(from, to, e);
                visual.anchoredPosition = new Vector2(offsetX, 0f);
                yield return null;
            }
            offsetX = to; visual.anchoredPosition = new Vector2(offsetX, 0f);
            moveTween = null;
        }

        public void OnPointerEnter(PointerEventData e) { hover = true; UpdateOffsetTarget(); }
        public void OnPointerExit(PointerEventData e) { hover = false; UpdateOffsetTarget(); }
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Clicked?.Invoke(this); }
    }
}
