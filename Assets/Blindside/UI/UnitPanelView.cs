// The unit detail panel (#unitPanel / the mockup's #up), split out of PlayerSelectionController so it can be drawn from plain data
// (UnitPanelModel) — by the game, which fills the model from a SimUnit, and by the editor previews, which fill it with sample data.
// Construction and Show are the controller's former BuildUnitPanel / ShowUnitPanel bodies, unchanged.
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    /// <summary>Everything the unit panel shows, already formatted.</summary>
    public class UnitPanelModel
    {
        public string key;                 // CRUSHER (also the face key and the two-letter monogram)
        public string name;                // the codename shown (null: the key) — e.g. TERRORIST for a defender drawn as one
        public string ShownName => string.IsNullOrEmpty(name) ? key : name;
        public Color color;                // the operator colour (--c)
        public bool foe;                   // .foe: red header and HP bar, no charges
        public string code;                // OP-006 // ASSAULT // K-417
        public string krRich;              // kr + role · team (rich text)
        public int hp, maxHp;
        public readonly string[] grid = new string[5];   // MOVE / SPD / DMG / ACC / RNG values (rich text)
        public string weapon;              // the weapon line (kept for its text; not shown in .up)
        public string abilityName, charges, description;

        public struct Tag { public string text; public Color bg, fg; }
        public readonly List<Tag> tags = new List<Tag>();
        public void AddTag(string text, Color bg, Color fg) => tags.Add(new Tag { text = text, bg = bg, fg = fg });
    }

    public class UnitPanelView : MonoBehaviour
    {
        static readonly Color Paper = UIPalette.Paper, Ink = UIPalette.Ink, Ink2 = UIPalette.Ink2, Blu = UIPalette.Blue, Foe = UIPalette.Foe, Dimp = UIPalette.Dim;

        public RectTransform Root => (RectTransform)transform;
        /// <summary>The portrait + status tags above the panel (outside its reveal mask), shown and hidden with it.</summary>
        public RectTransform Over { get; private set; }
        public bool Visible => gameObject.activeSelf;
        /// <summary>Runs the reveal (the controller, which stays active — a hide mid-reveal must not freeze it half-way). Defaults to the view.</summary>
        public MonoBehaviour CoroutineHost;
        MonoBehaviour Host => CoroutineHost != null ? CoroutineHost : this;

        RectMask2D panelMask;
        CanvasGroup panelGroup;
        Image headBg, porBg, porStrip, hpFill;
        TMP_Text porText, codeText, nameText, krText, lvHp, lvMax, hpText, wpnText, abNameText, abChText, abDescText;
        readonly TMP_Text[] gridVal = new TMP_Text[5];
        RectTransform tagsRow;
        Coroutine panelAnim;

        // The mockup's #up at UITheme.hud.unitPanel (430×190, left 20 / bottom 20), every part at a fixed place:
        //   .hd{height:68px;padding:7px 20px 0 140px} .code{11px;max-width:190px} .n{26px/1.05} .r{12.5px}  .hp{right:0;width:92px;height:68px} b{28px} small{11px}
        //   .pt{left:14px;top:-30px;104×104;border-bottom:5px}  .bd{padding:6px 16px 0 140px} .grid{5 columns, gap 3px} div{padding:3px 6px 4px} label{11px} b{16px}
        //   .ab{left:14px;right:16px;top:118px;bottom:10px;padding:5px 10px;border-left:4px} .an{12.5px} span{11px} p{12px/1.4}
        // The portrait rises above the panel, outside the panel's reveal mask, so it lives in a sibling (Over) shown and hidden with it;
        // the status tags sit there too, beside the portrait. The HP row / weapon row have no place in .up and stay hidden.
        /// <summary>Instantiates HUD_UnitPanel under `parent` and builds the panel in it (hidden).</summary>
        public static UnitPanelView Build(Transform parent, GameObject prefab)
        {
            var go = Instantiate(prefab, parent, false);
            go.name = "UnitPanel";
            var v = go.AddComponent<UnitPanelView>();
            v.BuildContents();
            return v;
        }

        void BuildContents()
        {
            var go = gameObject;
            var panelRoot = Root;
            panelMask = go.GetComponent<RectMask2D>();
            panelGroup = go.GetComponent<CanvasGroup>();

            var H = UITheme.Current.hud;
            float W = H.unitPanel.x, PH = H.unitPanel.y, face = H.unitPanelFace, rise = H.unitPanelFaceRise;
            const float Left = 20f, Bottom = 20f, HeadH = 68f, TextX = 140f, HpW = 92f;
            UILayout.NoFitter(panelRoot);
            var oldGroup = go.GetComponent<LayoutGroup>(); if (oldGroup != null) DestroyImmediate(oldGroup);
            UILayout.Anchor(panelRoot, Vector2.zero, Left, Bottom, W, PH);
            var rootImg = go.GetComponent<Image>();
            if (rootImg != null) rootImg.raycastTarget = true; // the panel itself isn't the battlefield

            // ---- .hd ----
            var head = panelRoot.Find("Head") as RectTransform;
            if (head == null) head = HudKit.NewRect("Head", panelRoot);
            var oldTitle = head.Find("Title") as RectTransform;
            head.SetSiblingIndex(0);
            headBg = HudKit.AddImage(head, Ink);
            UILayout.Place(head, 0, 0, W, HeadH);

            float textW = W - TextX - HpW - 8f;
            codeText = HudKit.AddText(head, "", 11, new Color(1, 1, 1, .5f), false, TextAlignmentOptions.TopLeft, "Code", role: UITheme.TextRole.Display);
            codeText.characterSpacing = 10; codeText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(codeText.rectTransform, TextX, 7, Mathf.Min(190f, textW), 14);
            if (oldTitle != null) { oldTitle.SetParent(head, false); nameText = oldTitle.GetComponent<TMP_Text>(); }
            if (nameText == null) nameText = HudKit.AddText(head, "", 26, Color.white, true, TextAlignmentOptions.TopLeft, "Name", role: UITheme.TextRole.Display);
            nameText.fontSize = 26; nameText.color = Color.white; UIFonts.Apply(nameText, UITheme.TextRole.Display);
            nameText.alignment = TextAlignmentOptions.TopLeft; nameText.characterSpacing = 10; nameText.raycastTarget = false;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.LineHeight(nameText, 1.05f);
            UILayout.Place(nameText.rectTransform, TextX, 21, textW, 28);
            krText = HudKit.AddText(head, "", 12.5f, Color.white, true, TextAlignmentOptions.TopLeft, "Kr", role: UITheme.TextRole.Body);
            krText.overflowMode = TextOverflowModes.Ellipsis;          // never run under the .hp box
            UILayout.Place(krText.rectTransform, TextX, 48, textW, 16);

            // the HP bar: a 4px line along the header's bottom edge under the name block
            var bar = HudKit.NewRect("Bar", head);
            HudKit.AddImage(bar, new Color(1, 1, 1, .14f));
            UILayout.Place(bar, TextX, HeadH - 4f, textW, 4);
            var fill = HudKit.NewRect("Fill", bar);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.pivot = new Vector2(0, .5f);
            fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            hpFill = HudKit.AddImage(fill, Blu);
            hpText = HudKit.AddText(head, "", 11, Ink, true, TextAlignmentOptions.Left, "HpText", role: UITheme.TextRole.Display);
            hpText.gameObject.SetActive(false);                         // the .hp box already shows HP / max

            // .hp box
            var lv = HudKit.NewRect("Lv", head);
            HudKit.AddImage(lv, Paper);
            UILayout.Place(lv, W - HpW, 0, HpW, HeadH);
            lvHp = HudKit.AddText(lv, "", 28, Ink, true, TextAlignmentOptions.MidlineRight, "B", role: UITheme.TextRole.Display);
            UILayout.Place(lvHp.rectTransform, 0, 0, 56, HeadH);
            lvMax = HudKit.AddText(lv, "", 11, Dimp, true, TextAlignmentOptions.MidlineLeft, "Small", role: UITheme.TextRole.Display);
            UILayout.LineHeight(lvMax, 1.3f);
            UILayout.Place(lvMax.rectTransform, 59, 0, HpW - 59, HeadH);

            // ---- .pt + status tags (above the panel, outside its mask) ----
            var panelOver = HudKit.NewRect("UnitPanelOver", panelRoot.parent);
            Over = panelOver;
            panelOver.gameObject.AddComponent<LintIgnoreClip>().reason = "tags and portrait rise above the panel";
            panelOver.SetSiblingIndex(panelRoot.GetSiblingIndex() + 1);
            UILayout.Anchor(panelOver, Vector2.zero, Left, Bottom + PH, W, rise);
            var por = HudKit.NewRect("Por", panelOver);
            por.gameObject.AddComponent<LintIgnoreClip>().reason = "portrait rises above the panel";
            porBg = HudKit.AddImage(por, Blu);
            UILayout.Place(por, 14, 0, face, face);
            porText = HudKit.AddText(por, "", 19, Ink, true, TextAlignmentOptions.Center, role: UITheme.TextRole.Display);
            HudKit.Stretch(porText.rectTransform);
            // border-bottom:5px solid var(--c) — the operator colour under the face
            var strip = HudKit.NewRect("Strip", por);
            strip.anchorMin = Vector2.zero; strip.anchorMax = new Vector2(1, 0); strip.pivot = new Vector2(.5f, 0);
            strip.anchoredPosition = Vector2.zero; strip.sizeDelta = new Vector2(0, 5);
            porStrip = HudKit.AddImage(strip, Blu);
            tagsRow = HudKit.NewRect("Tags", panelOver);
            UILayout.Place(tagsRow, 14 + face + 8, rise - 22, W - (14 + face + 8), 18);
            UILayout.Row(tagsRow, 4);

            // ---- .bd .grid — 5 equal cells ----
            const float GridY = HeadH + 6f, GridH = 41f, GridGap = 3f;
            float gridW = W - TextX - 16f, cellW = (gridW - 4 * GridGap) / 5f;
            var grid = HudKit.NewRect("Grid", panelRoot);
            UILayout.Place(grid, TextX, GridY, gridW, GridH);
            string[] labels = { "MOVE", "SPD", "DMG", "ACC", "RNG" };
            for (int i = 0; i < 5; i++)
            {
                var cell = HudKit.NewRect(labels[i], grid);
                HudKit.AddImage(cell, Ink2);
                UILayout.Place(cell, i * (cellW + GridGap), 0, cellW, GridH);
                var dot = HudKit.NewRect("Dot", cell);                      // .grid div::before
                dot.anchorMin = dot.anchorMax = new Vector2(1, 1); dot.pivot = new Vector2(1, 1);
                dot.anchoredPosition = new Vector2(-3, -3); dot.sizeDelta = new Vector2(4, 4);
                HudKit.AddImage(dot, Blu);
                var lab = HudKit.AddText(cell, labels[i], 11, new Color(1, 1, 1, .6f), true, TextAlignmentOptions.TopLeft, "Label");
                lab.characterSpacing = 5;
                UILayout.Place(lab.rectTransform, 6, 3, cellW - 12, 14);
                gridVal[i] = HudKit.AddText(cell, "", 16, Color.white, true, TextAlignmentOptions.TopLeft, "Value", role: UITheme.TextRole.Display);
                UILayout.Place(gridVal[i].rectTransform, 6, 17, cellW - 8, 20);
            }

            // weapon row: no place in .up (the code line carries the weapon) — kept for its text, hidden
            var wpn = HudKit.NewRect("Wpn", panelRoot);
            wpnText = HudKit.AddText(wpn, "", 11, Dimp, false, TextAlignmentOptions.Left, "Name", role: UITheme.TextRole.Body);
            wpn.gameObject.SetActive(false);

            // ---- .ab ----
            const float AbX = 14f, AbY = 118f, AbR = 16f, AbB = 10f;
            float abW = W - AbX - AbR, abH = PH - AbY - AbB;
            var ab = HudKit.NewRect("Ability", panelRoot);
            HudKit.AddImage(ab, new Color(Blu.r, Blu.g, Blu.b, .09f));
            UILayout.Place(ab, AbX, AbY, abW, abH);
            var abBorder = HudKit.NewRect("Border", ab);
            abBorder.anchorMin = new Vector2(0, 0); abBorder.anchorMax = new Vector2(0, 1); abBorder.pivot = new Vector2(0, .5f);
            abBorder.anchoredPosition = Vector2.zero; abBorder.sizeDelta = new Vector2(4, 0);
            HudKit.AddImage(abBorder, Blu);
            float innerX = 4f + 10f, innerW = abW - innerX - 10f;
            var abIc = HudKit.AddText(ab, "✦", 12.5f, Ink, true, TextAlignmentOptions.TopLeft, "Ic");
            HudKit.UseSymbolFont(abIc);
            UILayout.Place(abIc.rectTransform, innerX, 5, 14, 16);
            abNameText = HudKit.AddText(ab, "", 12.5f, Ink, true, TextAlignmentOptions.TopLeft, "Name", role: UITheme.TextRole.Title);
            UILayout.Place(abNameText.rectTransform, innerX + 16, 5, innerW - 16 - 60, 16);
            abChText = HudKit.AddText(ab, "", 11, Blu, true, TextAlignmentOptions.TopRight, "Charges", role: UITheme.TextRole.Display);
            HudKit.UseSymbolFont(abChText); // ● ○ ∞
            UILayout.Place(abChText.rectTransform, innerX + innerW - 60, 5, 60, 16);
            abDescText = HudKit.AddText(ab, "", 12, Ink2, false, TextAlignmentOptions.TopLeft, "Desc", role: UITheme.TextRole.Body);
            abDescText.textWrappingMode = TextWrappingModes.Normal;
            UILayout.LineHeight(abDescText, 1.4f);
            UILayout.Place(abDescText.rectTransform, innerX, 22, innerW, abH - 22 - 4);

            go.SetActive(false);
            panelOver.gameObject.SetActive(false);
        }

        /// <summary>Port of showUnitPanel(u,noAnim), from a filled model.</summary>
        public void Show(UnitPanelModel m, bool noAnim)
        {
            var panelRoot = Root;
            Color c = m.color;
            panelRoot.gameObject.SetActive(true);
            if (Over != null) Over.gameObject.SetActive(true);

            headBg.color = m.foe ? HudKit.FoeHead : Ink;
            string key = m.key;
            porText.text = key.Length >= 2 ? key.Substring(0, 2) : key; // no face for this key → the monogram on the operator colour
            var face = OperatorArt.ApplyFace(porBg, key);              // the face on the paper-gradient slot, no monogram
            porText.gameObject.SetActive(face == null);
            porStrip.gameObject.SetActive(face != null); porStrip.color = c;
            if (face == null)
            {
                porBg.sprite = null; porBg.color = c;
                var stale = porBg.transform.Find("Face"); if (stale != null) stale.gameObject.SetActive(false);
            }
            codeText.text = m.code;
            nameText.text = key;
            krText.text = m.krRich;
            lvHp.text = m.hp.ToString();
            lvMax.text = $"HP\n/{m.maxHp}";

            float frac = m.maxHp > 0 ? Mathf.Clamp01((float)m.hp / m.maxHp) : 0f;
            hpFill.rectTransform.anchorMax = new Vector2(frac, 1f);
            hpFill.color = m.foe ? Foe : Blu;
            hpText.text = $"{m.hp} / {m.maxHp}";

            for (int i = 0; i < 5; i++) gridVal[i].text = m.grid[i];

            wpnText.text = m.weapon;

            abNameText.text = m.abilityName;
            abChText.text = m.charges;
            abDescText.text = m.description;

            // tags
            for (int i = tagsRow.childCount - 1; i >= 0; i--) HudKit.DestroySafe(tagsRow.GetChild(i).gameObject);
            int n = 0;
            foreach (var tag in m.tags)
            {
                var t = HudKit.NewRect("Tag", tagsRow);
                HudKit.AddImage(t, tag.bg);
                var label = HudKit.AddText(t, tag.text, 11, tag.fg, true, TextAlignmentOptions.Center);
                UILayout.Fixed(t, Mathf.Ceil(label.GetPreferredValues(tag.text).x) + 14f, 18f);
                HudKit.Stretch(label.rectTransform);
                n++;
            }
            tagsRow.gameObject.SetActive(n > 0);

            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot); // TMP wrap height needs the settled width

            // #unitPanel.swap{animation:panelIn .38s} — clip reveal left→right, opacity .4→1.
            if (!noAnim && panelMask != null && HudKit.Animate)
            {
                if (panelAnim != null) Host.StopCoroutine(panelAnim);
                panelAnim = Host.StartCoroutine(UIAnim.ClipReveal(panelMask, panelGroup, .38f));
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (Over != null) Over.gameObject.SetActive(false);
        }
    }
}
