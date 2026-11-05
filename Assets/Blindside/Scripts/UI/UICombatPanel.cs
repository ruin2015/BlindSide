// Port of the #combat cut-in (blindside_source/05_ui_input_intro.js:132-142 combatShow/combatUpdate/
// combatResult/combatCrit/combatHide, styles 00_markup_and_styles.html:212-225, 347, 430-434):
//   <div class="cb-side l" style="--c:ca"> name · kr·weapon · bar · hp </div>
//   <div class="cb-mid"> cb-tag (PREEMPTIVE · 先制攻撃 ×1.4 | ENGAGE · 交戦) · cb-stat · cb-hs · cb-res </div>
//   <div class="cb-side r" style="--c:ct"> name · kr · bar · hp </div>
// Built at runtime (Build) like the other GameplayUIBridge widgets. Everything animates on unscaled
// time so it keeps moving through a headshot's hit stop, like the CSS animations do.
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    public class UICombatPanel : MonoBehaviour
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image leftBorder, rightBorder, leftFill, rightFill, mid, tagBg;
        public TMP_Text leftName, leftKr, leftHp, rightName, rightKr, rightHp, tagText, statText, hsText, resText;

        static readonly Color PaperPp = UIPalette.Hex("#E3E6E9");        // --pp (flattened gradient)
        static readonly Color HsPink = UIPalette.Hex("#FF8A98");         // .cb-hs
        const float BaseY = 96f;                                         // bottom: 96px

        Coroutine inAnim, critAnim, barAnim;

        /// <summary>combatShow(a,t,ch,dmg,shots,pre,walls,blocked,hsC).</summary>
        public void Show(string aKey, string aKr, Color ca, int aHp, int aMax,
                         string tKey, string tKr, Color ct, int tHp, int tMax,
                         int chance, int dmg, int shots, bool pre, int walls, bool blocked, int hsC)
        {
            gameObject.SetActive(true);
            leftBorder.color = ca; leftFill.color = ca;
            rightBorder.color = ct; rightFill.color = ct;
            leftName.text = aKey; leftKr.text = aKr;
            rightName.text = tKey; rightKr.text = tKr;
            SetFill(leftFill, aMax > 0 ? (float)aHp / aMax : 0f);
            SetFill(rightFill, tMax > 0 ? (float)tHp / tMax : 0f);
            leftHp.text = $"{aHp}/{aMax}"; rightHp.text = $"{tHp}/{tMax}";

            tagBg.color = pre ? UIPalette.Foe : UIPalette.Blue;
            tagText.text = pre ? "PREEMPTIVE · 先制攻撃 ×1.4" : "ENGAGE · 交戦";
            statText.text = $"命中 <size=24><color=#FFC81E>{chance}%</color></size> · <size=24><color=#FFC81E>{dmg}</color></size>×{shots}";
            hsText.gameObject.SetActive(hsC > 0);
            hsText.text = $"HEADSHOT <size=14><color=#E5364D>{hsC}%</color></size> · ダメージ×2";
            resText.text = (walls > 0 ? $"壁貫通 {walls} (-{walls * 22}%)" : "") + (blocked ? " · 正面シールド" : "");

            if (critAnim != null) { StopCoroutine(critAnim); critAnim = null; }
            if (barAnim != null) { StopCoroutine(barAnim); barAnim = null; }
            mid.color = UIPalette.Ink; SetMidText(Color.white);
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);

            // el.classList.remove('show'); void el.offsetWidth; el.classList.add('show') — replays combatIn
            if (inAnim != null) StopCoroutine(inAnim);
            inAnim = StartCoroutine(CombatIn());
        }

        /// <summary>combatUpdate(t): the target's bar (transition:width .5s) and hp text.</summary>
        public void UpdateTarget(int hp, int max)
        {
            rightHp.text = $"{hp}/{max}";
            float to = max > 0 ? Mathf.Clamp01((float)hp / max) : 0f;
            if (barAnim != null) StopCoroutine(barAnim);
            if (isActiveAndEnabled) barAnim = StartCoroutine(BarTo(rightFill, to, .5f));
            else SetFill(rightFill, to);
        }

        /// <summary>combatResult(h,n,tot,bl,hs).</summary>
        public void Result(int hits, int shots, int total, bool blocked, int headshots)
        {
            resText.text = blocked ? "BLOCKED — シールドが防いだ"
                : $"{hits}/{shots} HIT · -{total}" + (headshots > 0 ? $" <mark=#E5364DFF><color=#FFFFFF><i> HEADSHOT ×{headshots} </i></color></mark>" : "");
        }

        /// <summary>combatCrit(n): #combat.crit .cb-mid{animation:critPulse .5s}.</summary>
        public void Crit()
        {
            if (!isActiveAndEnabled) return;
            if (critAnim != null) StopCoroutine(critAnim);
            critAnim = StartCoroutine(CritPulse());
        }

        /// <summary>Only the two sides (names + HP bars), without the middle block (tag, odds, headshot, result) — the
        /// battle stage's view.</summary>
        public void SetHpOnly(bool on)
        {
            if (mid == null || mid.gameObject.activeSelf == !on) return;
            mid.gameObject.SetActive(!on);
            if (root != null) LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        /// <summary>combatHide(): display:none.</summary>
        public void Hide()
        {
            if (inAnim != null) { StopCoroutine(inAnim); inAnim = null; }
            gameObject.SetActive(false);
        }

        // @keyframes combatIn{from{transform:translate(-50%,30px);opacity:0}} .35s cubic-bezier(.2,.9,.2,1) both
        IEnumerator CombatIn()
        {
            float t = 0f, d = .35f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float e = UIEase.Tactical(t / d);
                group.alpha = e;
                root.anchoredPosition = new Vector2(0f, BaseY - 30f * (1f - e));
                yield return null;
            }
            group.alpha = 1f; root.anchoredPosition = new Vector2(0f, BaseY);
            inAnim = null;
        }

        // @keyframes critPulse{0%{background:#fff;color:var(--ink)}30%{background:var(--foe)}100%{background:var(--ink)}}
        IEnumerator CritPulse()
        {
            float t = 0f, d = .5f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / d);
                mid.color = p < .3f ? Color.Lerp(Color.white, UIPalette.Foe, p / .3f) : Color.Lerp(UIPalette.Foe, UIPalette.Ink, (p - .3f) / .7f);
                SetMidText(Color.Lerp(UIPalette.Ink, Color.white, p));
                yield return null;
            }
            mid.color = UIPalette.Ink; SetMidText(Color.white);
            critAnim = null;
        }

        void SetMidText(Color c) { statText.color = c; resText.color = c; }

        IEnumerator BarTo(Image fill, float to, float d)
        {
            float from = fill.rectTransform.anchorMax.x, t = 0f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                SetFill(fill, Mathf.Lerp(from, to, UIEase.Tactical(t / d)));
                yield return null;
            }
            SetFill(fill, to);
            barAnim = null;
        }

        static void SetFill(Image fill, float frac) => fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);

        // ================================================================
        //  Construction
        // ================================================================
        public static UICombatPanel Build(Transform canvas)
        {
            // #combat{position:fixed;left:50%;bottom:96px;transform:translateX(-50%);display:flex;box-shadow:var(--shadow)}
            var root = NewRect("CombatPanel", canvas);
            root.anchorMin = root.anchorMax = new Vector2(.5f, 0f);
            root.pivot = new Vector2(.5f, 0f);
            root.anchoredPosition = new Vector2(0f, BaseY);
            root.gameObject.SetActive(false);
            var hl = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            var fit = root.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var shadowBg = root.gameObject.AddComponent<Image>();
            shadowBg.color = new Color(0, 0, 0, 0); shadowBg.raycastTarget = false;
            var sh = root.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, .5f); sh.effectDistance = new Vector2(0, -10);
            sh.useGraphicAlpha = false;   // the host Image is transparent; only its shadow draws
            var panel = root.gameObject.AddComponent<UICombatPanel>();
            panel.root = root;
            panel.group = root.gameObject.AddComponent<CanvasGroup>();
            panel.group.blocksRaycasts = false; panel.group.interactable = false;   // pointer-events:none

            BuildSide(root, true, out panel.leftBorder, out panel.leftName, out panel.leftKr, out panel.leftFill, out panel.leftHp);

            // .cb-mid{width:210px;background:var(--ink);color:#fff;padding:8px 10px;text-align:center;flex column centred}
            var mid = NewRect("Mid", root);
            panel.mid = Img(mid, UIPalette.Ink);
            Le(mid, 210f, -1f);
            var vl = mid.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(10, 10, 8, 8); vl.spacing = 3f; vl.childAlignment = TextAnchor.MiddleCenter;
            vl.childControlWidth = vl.childControlHeight = true; vl.childForceExpandWidth = false; vl.childForceExpandHeight = false;
            // .cb-tag{font:700 10px var(--fm);background:var(--blu);color:#fff;padding:2px 6px;align-self:center;margin-bottom:4px}
            var tag = NewRect("Tag", mid);
            panel.tagBg = Img(tag, UIPalette.Blue);
            var th = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
            th.padding = new RectOffset(6, 6, 2, 2); th.childControlWidth = th.childControlHeight = true;
            th.childForceExpandWidth = th.childForceExpandHeight = false;
            panel.tagText = Txt(tag, "", 10, Color.white, TextAlignmentOptions.Center, UITheme.TextRole.Display);
            // .cb-stat{font-size:12px;font-weight:700} .cb-stat b{font:700 24px var(--fd);color:var(--acc)}
            panel.statText = Txt(mid, "", 12, Color.white, TextAlignmentOptions.Center, UITheme.TextRole.Body);
            // .cb-hs{font:700 10px var(--fm);color:#ff8a98;letter-spacing:.06em} .cb-hs b{font:700 14px;color:var(--foe)}
            panel.hsText = Txt(mid, "", 10, HsPink, TextAlignmentOptions.Center, UITheme.TextRole.Display);
            panel.hsText.characterSpacing = 6;
            // .cb-res{font:700 13px var(--fd);min-height:18px;margin-top:3px}
            panel.resText = Txt(mid, "", 13, Color.white, TextAlignmentOptions.Center, UITheme.TextRole.Display);
            Le(panel.resText.rectTransform, -1f, -1f, 18f);

            BuildSide(root, false, out panel.rightBorder, out panel.rightName, out panel.rightKr, out panel.rightFill, out panel.rightHp);
            return panel;
        }

        // .cb-side{width:220px;padding:10px 14px} .l{border-left:6px solid var(--c)} .r{border-right:6px;text-align:right}
        static void BuildSide(RectTransform parent, bool left, out Image border, out TMP_Text name, out TMP_Text kr, out Image fill, out TMP_Text hp)
        {
            var side = NewRect(left ? "SideL" : "SideR", parent);
            Img(side, PaperPp);
            Le(side, 220f, -1f);
            var vl = side.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = left ? new RectOffset(14 + 6, 14, 10, 10) : new RectOffset(14, 14 + 6, 10, 10);
            vl.childControlWidth = vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            vl.childAlignment = left ? TextAnchor.UpperLeft : TextAnchor.UpperRight;

            var b = NewRect("Border", side);
            b.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            b.anchorMin = new Vector2(left ? 0 : 1, 0); b.anchorMax = new Vector2(left ? 0 : 1, 1);
            b.pivot = new Vector2(left ? 0 : 1, .5f); b.anchoredPosition = Vector2.zero; b.sizeDelta = new Vector2(6, 0);
            border = Img(b, UIPalette.Blue);

            var align = left ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
            // .cb-name{font:700 21px/1 var(--fd);letter-spacing:.1em}
            name = Txt(side, "", 21, UIPalette.Ink, align, UITheme.TextRole.Display);
            name.characterSpacing = 10;
            // .cb-kr{font-size:11px;color:var(--dimp)}
            kr = Txt(side, "", 11, UIPalette.Dim, align, UITheme.TextRole.Body);
            // .cb-side .bar{height:8px;margin-top:8px} .bar{background:rgba(0,0,0,.14)} .bar i{background:var(--c)}
            var wrap = NewRect("BarWrap", side);
            Le(wrap, -1f, 16f);
            var bar = NewRect("Bar", wrap);
            bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0); bar.pivot = new Vector2(.5f, 0);
            bar.anchoredPosition = Vector2.zero; bar.sizeDelta = new Vector2(0, 8);
            Img(bar, new Color(0, 0, 0, .14f));
            var f = NewRect("Fill", bar);
            f.anchorMin = Vector2.zero; f.anchorMax = Vector2.one; f.pivot = new Vector2(0, .5f);
            f.offsetMin = Vector2.zero; f.offsetMax = Vector2.zero;
            fill = Img(f, UIPalette.Blue);
            // .bar::after — 1px tick every 10px
            var ticks = NewRect("Ticks", bar);
            ticks.anchorMin = Vector2.zero; ticks.anchorMax = Vector2.one; ticks.offsetMin = ticks.offsetMax = Vector2.zero;
            var raw = ticks.gameObject.AddComponent<RawImage>();
            raw.texture = TickTexture(); raw.raycastTarget = false;
            raw.uvRect = new Rect(0, 0, (220f - 28f - 6f) / 10f, 1f);
            // .cb-hpn{font:700 12px var(--fm)}
            hp = Txt(side, "", 12, UIPalette.Ink, align, UITheme.TextRole.Display);
        }

        static Texture2D tickTex;
        static Texture2D TickTexture()
        {
            if (tickTex != null) return tickTex;
            tickTex = new Texture2D(10, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "BarTicks" };
            var px = new Color[10];
            for (int i = 0; i < 10; i++) px[i] = i == 9 ? new Color(0, 0, 0, .25f) : Color.clear;
            tickTex.SetPixels(px); tickTex.Apply();
            return tickTex;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static Image Img(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return img;
        }

        static TMP_Text Txt(Transform parent, string text, float size, Color color, TextAlignmentOptions align, UITheme.TextRole role)
        {
            var rt = NewRect("Text", parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            UIFonts.Apply(t, role); t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        static void Le(RectTransform rt, float prefW, float prefH, float minH = -1f)
        {
            var le = UIUtil.GetOrAdd<LayoutElement>(rt.gameObject);
            le.preferredWidth = prefW; le.preferredHeight = prefH; le.minHeight = minH;
            if (prefW > 0) le.minWidth = prefW;
        }
    }
}
