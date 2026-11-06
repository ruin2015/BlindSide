// The HQ's goods screen (装備購入 and 医務室's 医薬品 / ガジェット): a full screen in the station screens' style —
//   DecoBackdrop   paper; DecoBlockA / DecoBlockB ink blocks off the top-left corner; the back bar top left (HubTiles.Back)
//   Card           left (69, 76, 554 × 600): ink2, cut corners, DecoShadow, a blue band on top and short hazard stripes
//     Window       the selected product's picture (501 × 335, light grey with a faint grid and blue corner ticks): UIGlitchImage — the part's illustration, the gun's own
//                  model on the workbench (a HubWorkbench of its own, live while a gun is shown), an item's own (the 医務室's six), or a big BSTactical glyph
//                  (UIGlitchText); a new pick replays the glitch (Select preset / a burst)
//     Name 30 title (white) · Kind 12 (blue) · Desc 18 body (paper, wraps) · Info 14 body (dim: stats, how many held)
//     Toast        blue plate at the card's foot: what was just bought / why not (fades after 2.2 s)
//   DecoPanel      ink parallelogram on the right (top [834, 1507], bottom [612, 1285]: the rows lean with it)
//     Tabs         a segmented bar at the top (the open one a light tile with a blue top line) — Q / E or a click
//     Rows         the products, one under another along the panel's left edge (masked 76–694, scrolled to keep the pick in view):
//                  name · price (円) · a pill ×held. The pick: a light gradient tile (ink words), full size, 18 out of the line, ➜;
//                  the rest: ink3 (white words), 84 %, dim
//     Money        blue bar (721, 709, 498 × 55): 所持金 + the amount (円)
//   Help           under the card: HubTiles.Help in two columns (key caps, the mouse)
//   Ask            the purchase dialog over everything: 「何個購入しますか？」 (a gun: 「購入しますか？」, one only), − count +, the total
//                  and what is left, 「購入する」 / 「やめる」. ← → / A D / the wheel change the count, Enter buys, ESC closes it
// Motion (unscaled): Evaluate(t) — the screen fades in (.2), the panel slides from the right (.45), the card from the left with a turn
// (.4), the tabs drop, the money bar slides; the rows come in one after another from the right (.04 apart). Close plays it
// backwards (.28) before hiding and raises Closed. The pick eases (.14) in
// colour, size and offset; the list scrolls smoothly; the dialog pops (.22). Keys: ↑ ↓ / W S / the wheel pick, Enter / a click buys.
// Rows are rebuilt on a tab change or a purchase only; nothing is allocated per frame.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.Hub;
using Blindside.Save;

namespace Blindside.UI
{
    public class HubShopScreen : MonoBehaviour, ITimedEffect
    {
        public enum Cat { Weapon, Attachment, Gadget, Medical }

        // the card
        public const float CardX = 69f, CardY = 76f, CardW = 554f, CardH = 600f, WinX = 26f, WinY = 29f, WinW = 501f, WinH = 335f;
        // the panel: its left edge runs from (PanelTopX, 0) to (PanelBottomX, 810)
        public const float ScreenH = 810f, PanelTopX = 834f, PanelBottomX = 612f, PanelW = 895f;
        public const float RowsTop = 76f, RowsBottom = 694f, RowW = 600f, RowH = 64f, RowGap = 8f, RowSmall = .84f, RowInset = 34f;
        public const float PickTime = .14f;

        sealed class Product
        {
            public string name = "", kind = "", desc = "", info = "", glyph = "";
            public int price, have, cap;
            public bool owned;
            public Sprite art;
            public WeaponData gun;
            public System.Func<int, string> buy;
            public int Room => owned ? 0 : Mathf.Max(0, cap - have);
        }

        sealed class Row
        {
            public RectTransform rt;
            public Image plate;
            public SlantedGraphic slant;
            public CanvasGroup group, marker;
            public TMP_Text name, price, pill;
            public Image pillPlate;
            public float pick;   // 0 → 1 while it is the pick
        }

        HubScreen screen;
        RectTransform root, panel, card, rowsRoot, tabsRoot, money, ask, askBox, toast;
        CanvasGroup group, cardGroup, tabsGroup, moneyGroup, askGroup, toastGroup;
        Vector2 panelPos, cardPos, tabsPos, moneyPos;
        UIGlitchImage photo;
        RectTransform photoRt;
        TMP_Text glyph, nameText, kindText, descText, infoText, moneyText, toastText;
        UIGlitchText glyphGlitch;
        TMP_Text askTitle, askName, askCount, askMax, askTotal, askLeft;
        RectTransform askMinus, askPlus;
        HubWorkbench bench;

        Cat[] cats = { Cat.Weapon };
        int cat, pick, askN;
        float clock, rowClock, scroll, scrollGoal, toastClock = 99f, askClock = 99f;
        bool running, asking;
        readonly List<Product> products = new List<Product>(24);
        readonly List<Row> rows = new List<Row>(24);
        readonly List<GameObject> tabObjects = new List<GameObject>(4);

        public bool IsOpen => gameObject.activeSelf;
        SaveData D => screen.Data;
        bool Editable => screen.Editable;

        // ---- build -------------------------------------------------------------------------------------------------------------
        public static HubShopScreen Build(Transform parent, HubScreen screen)
        {
            var theme = UITheme.Current;
            var r = HudKit.NewRect("HubShop", parent);
            HudKit.Stretch(r);
            var s = r.gameObject.AddComponent<HubShopScreen>();
            s.screen = screen; s.root = r;
            s.group = r.gameObject.AddComponent<CanvasGroup>();
            var bg = HudKit.NewRect("DecoBackdrop", r);
            HudKit.Stretch(bg);
            HudKit.AddImage(bg, theme.paper, raycast: true);
            var ba = HudKit.NewRect("DecoBlockA", r); UILayout.Place(ba, 222f, 0f, 237f, 36f); HudKit.AddImage(ba, theme.ink);
            var bb = HudKit.NewRect("DecoBlockB", r); UILayout.Place(bb, 0f, 70f, 70f, 240f); HudKit.AddImage(bb, theme.ink);

            s.BuildPanel(theme);
            s.BuildCard(theme);
            s.BuildHelp(theme);
            s.BuildAsk(theme);
            r.gameObject.SetActive(false);
            return s;
        }

        static void Bounds(RectTransform rt, string why)
        {
            var ig = rt.gameObject.AddComponent<LintIgnoreOverlap>();
            ig.bounds = true; ig.reason = why;
        }

        void BuildPanel(UITheme theme)
        {
            panel = HudKit.NewRect("DecoPanel", root);
            UILayout.Place(panel, PanelBottomX, 0f, PanelW, ScreenH);
            HudKit.AddImage(panel, theme.ink);
            panel.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, PanelTopX - PanelBottomX);
            Bounds(panel, "the goods panel bleeds off the right edge by design");
            panelPos = panel.anchoredPosition;

            tabsRoot = HudKit.NewRect("Tabs", root);
            UILayout.Place(tabsRoot, 0f, 0f, 1440f, 70f);
            tabsGroup = tabsRoot.gameObject.AddComponent<CanvasGroup>();
            tabsPos = tabsRoot.anchoredPosition;

            var mask = HudKit.NewRect("RowsArea", root);
            UILayout.Place(mask, PanelBottomX - 40f, RowsTop, 1440f - PanelBottomX + 40f, RowsBottom - RowsTop);
            mask.gameObject.AddComponent<RectMask2D>();
            rowsRoot = HudKit.NewRect("Rows", mask);
            HudKit.Stretch(rowsRoot);
            Bounds(rowsRoot, "scroll content: rows past the area are masked until the pick scrolls to them");

            money = HudKit.NewRect("Money", root);
            UILayout.Place(money, 721f, 709f, 498f, 55f);
            HudKit.AddImage(money, theme.blue);
            money.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 14f);
            moneyGroup = money.gameObject.AddComponent<CanvasGroup>();
            var ml = HudKit.AddText(money, "所持金", 14f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Label", UITheme.TextRole.Body);
            UILayout.Place(ml.rectTransform, 26f, 0f, 120f, 55f);
            moneyText = HudKit.AddText(money, "", 26f, Color.white, true, TextAlignmentOptions.MidlineRight, "Amount", UITheme.TextRole.Body);
            moneyText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(moneyText.rectTransform, 140f, 0f, 498f - 172f, 55f);
            moneyPos = money.anchoredPosition;
        }

        void BuildCard(UITheme theme)
        {
            card = HudKit.NewRect("Card", root);
            UILayout.Place(card, CardX, CardY, CardW, CardH);
            cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            var face = HudKit.NewRect("Face", card);
            HudKit.Stretch(face);
            var fi = HudKit.AddImage(face, theme.ink2);
            face.gameObject.AddComponent<CutCornerGraphic>().Set(22f, CutCornerGraphic.Corners.Mockup);
            HudKit.Stretch(KitShadow.Behind(fi.rectTransform, new Color(.03f, .04f, .06f), .35f, 10f, 24f).rectTransform);
            cardPos = card.anchoredPosition;

            // a blue band along the top, short hazard stripes at the top right, the window's frame, grid and corner ticks
            var band = HudKit.NewRect("DecoBand", card);
            UILayout.Place(band, 0f, 0f, CardW - 22f, 5f);
            HudKit.AddImage(band, theme.blue);
            for (int i = 0; i < 5; i++)
            {
                var st = HudKit.NewRect("DecoStripe" + i, card);
                UILayout.Place(st, CardW - 150f + i * 18f, 12f, 9f, 10f);
                HudKit.AddImage(st, new Color(theme.acc.r, theme.acc.g, theme.acc.b, .9f));
                st.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 5f);
            }
            var frame = HudKit.NewRect("DecoFrame", card);
            UILayout.Place(frame, WinX - 2f, WinY - 2f, WinW + 4f, WinH + 4f);
            HudKit.AddImage(frame, theme.ink3);
            var win = HudKit.NewRect("Window", card);
            UILayout.Place(win, WinX, WinY, WinW, WinH);
            HudKit.AddImage(win, Color.Lerp(theme.paper, theme.paper2, .55f));
            win.gameObject.AddComponent<RectMask2D>();
            var gridRt = HudKit.NewRect("DecoGrid", win);
            HudKit.Stretch(gridRt);
            var gi = gridRt.gameObject.AddComponent<Image>();
            gi.sprite = HubTiles.Grid(); gi.type = Image.Type.Tiled; gi.raycastTarget = false;
            gi.color = new Color(theme.ink.r, theme.ink.g, theme.ink.b, .07f);
            for (int i = 0; i < 4; i++)
            {
                bool right = i % 2 == 1, bottom = i >= 2;
                var tick = HudKit.NewRect("DecoCorner" + i, win);
                tick.anchorMin = tick.anchorMax = tick.pivot = new Vector2(right ? 1f : 0f, bottom ? 0f : 1f);
                tick.anchoredPosition = new Vector2(right ? -8f : 8f, bottom ? 8f : -8f); tick.sizeDelta = new Vector2(22f, 4f);
                HudKit.AddImage(tick, theme.blue);
                var tick2 = HudKit.NewRect("DecoCornerV" + i, win);
                tick2.anchorMin = tick2.anchorMax = tick2.pivot = tick.pivot;
                tick2.anchoredPosition = tick.anchoredPosition; tick2.sizeDelta = new Vector2(4f, 22f);
                HudKit.AddImage(tick2, theme.blue);
            }
            photoRt = HudKit.NewRect("Photo", win);
            photoRt.anchorMin = photoRt.anchorMax = photoRt.pivot = new Vector2(.5f, .5f);
            photo = photoRt.gameObject.AddComponent<UIGlitchImage>();
            photo.raycastTarget = false;
            photo.preset = UIGlitchImage.Preset.Select;
            glyph = HudKit.AddText(win, "", 230f, theme.ink, true, TextAlignmentOptions.Center, "Glyph", UITheme.TextRole.Display);
            HudKit.UseSymbolFont(glyph);
            HudKit.Stretch(glyph.rectTransform);
            glyphGlitch = glyph.gameObject.AddComponent<UIGlitchText>();
            glyphGlitch.source = glyph; glyphGlitch.loop = false; glyphGlitch.maxOffset = 9f;

            float y = WinY + WinH + 22f;
            kindText = HudKit.AddText(card, "", 12f, theme.blue, true, TextAlignmentOptions.MidlineLeft, "Kind", UITheme.TextRole.Body);
            kindText.textWrappingMode = TextWrappingModes.NoWrap; kindText.characterSpacing = 4f;
            UILayout.Place(kindText.rectTransform, WinX, y, WinW, 18f);
            nameText = HudKit.AddText(card, "", 30f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Name", UITheme.TextRole.Title);
            nameText.textWrappingMode = TextWrappingModes.NoWrap; nameText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(nameText.rectTransform, WinX, y + 20f, WinW, 42f);
            var rule = HudKit.NewRect("DecoRule", card);
            UILayout.Place(rule, WinX, y + 68f, 64f, 4f);
            HudKit.AddImage(rule, theme.blue);
            descText = HudKit.AddText(card, "", 18f, theme.paper, false, TextAlignmentOptions.TopLeft, "Desc", UITheme.TextRole.Body);
            descText.textWrappingMode = TextWrappingModes.Normal; descText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(descText.rectTransform, WinX, y + 84f, WinW, 56f);
            infoText = HudKit.AddText(card, "", 14f, Color.Lerp(theme.paper2, theme.dim, .35f), false, TextAlignmentOptions.TopLeft, "Info", UITheme.TextRole.Body);
            infoText.textWrappingMode = TextWrappingModes.Normal; infoText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(infoText.rectTransform, WinX, y + 142f, WinW, 20f);

            toast = HudKit.NewRect("Toast", card);
            UILayout.Place(toast, WinX, CardH - 44f, WinW, 34f);
            HudKit.AddImage(toast, theme.blue);
            toast.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastText = HudKit.AddText(toast, "", 14f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Body);
            toastText.textWrappingMode = TextWrappingModes.NoWrap; toastText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(toastText.rectTransform, 22f, 0f, WinW - 44f, 34f);
        }

        void BuildHelp(UITheme theme)
        {
            HubTiles.Help(root, CardX, CardY + CardH + 14f, true, 2, "MOUSE Enter", "購入", "↑ ↓", "選ぶ", "Q E", "分類", "ESC", "戻る");
            var back = HubTiles.Back(root, Close);
            UILayout.Place(back, 24f, 14f, 176f, 46f);
        }

        void BuildAsk(UITheme theme)
        {
            ask = HudKit.NewRect("Ask", root);
            HudKit.Stretch(ask);
            askGroup = ask.gameObject.AddComponent<CanvasGroup>();
            var dim = HudKit.NewRect("DecoDim", ask);
            HudKit.Stretch(dim);
            HudKit.AddImage(dim, new Color32(3, 5, 8, 150), raycast: true);
            const float W = 560f, H = 310f;
            askBox = HudKit.NewRect("Box", ask);
            askBox.anchorMin = askBox.anchorMax = askBox.pivot = new Vector2(.5f, .5f);
            askBox.sizeDelta = new Vector2(W, H);
            var bi = HudKit.AddImage(askBox, theme.ink, raycast: true);
            askBox.gameObject.AddComponent<CutCornerGraphic>().Set(22f, CutCornerGraphic.Corners.Mockup);
            HudKit.Stretch(KitShadow.Behind(bi.rectTransform, new Color(.03f, .04f, .06f), .6f, 14f, 30f).rectTransform);
            var band = HudKit.NewRect("DecoBand", askBox);
            UILayout.Place(band, 0f, 0f, 12f, H);
            HudKit.AddImage(band, theme.blue);

            askTitle = HudKit.AddText(askBox, "", 24f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Title", UITheme.TextRole.Title);
            askTitle.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(askTitle.rectTransform, 40f, 22f, W - 80f, 34f);
            askName = HudKit.AddText(askBox, "", 15f, theme.paper2, false, TextAlignmentOptions.MidlineLeft, "Item", UITheme.TextRole.Body);
            askName.textWrappingMode = TextWrappingModes.NoWrap; askName.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(askName.rectTransform, 40f, 58f, W - 80f, 22f);

            askMinus = AskButton(askBox, "Minus", "−", 150f, 96f, 64f, 56f, 1, () => Step(-1));
            askPlus = AskButton(askBox, "Plus", "+", W - 150f - 64f, 96f, 64f, 56f, 1, () => Step(1));
            askCount = HudKit.AddText(askBox, "", 46f, Color.white, true, TextAlignmentOptions.Center, "Count", UITheme.TextRole.Body);
            askCount.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(askCount.rectTransform, 214f, 92f, W - 428f, 64f);
            askMax = HudKit.AddText(askBox, "", 12f, theme.paper2, false, TextAlignmentOptions.Center, "Max", UITheme.TextRole.Body);
            askMax.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(askMax.rectTransform, 150f, 156f, W - 300f, 18f);

            askTotal = HudKit.AddText(askBox, "", 20f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Total", UITheme.TextRole.Body);
            askTotal.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(askTotal.rectTransform, 40f, 184f, 260f, 28f);
            askLeft = HudKit.AddText(askBox, "", 13f, theme.paper2, false, TextAlignmentOptions.MidlineRight, "Left", UITheme.TextRole.Body);
            askLeft.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(askLeft.rectTransform, 300f, 184f, W - 340f, 28f);

            AskButton(askBox, "Cancel", "やめる", 40f, H - 76f, 170f, 48f, 0, CancelAsk);
            AskButton(askBox, "Confirm", "購入する", W - 40f - 240f, H - 76f, 240f, 48f, 2, Confirm);
            ask.gameObject.SetActive(false);
        }

        /// <summary>A dialog button: style 0 dark (ink3), 1 a light tile (ink words), 2 the blue tile.</summary>
        static RectTransform AskButton(RectTransform parent, string name, string label, float x, float y, float w, float h, int style, UnityEngine.Events.UnityAction act)
        {
            var theme = UITheme.Current;
            var rt = HudKit.NewRect(name, parent);
            UILayout.Place(rt, x, y, w, h);
            var hv = rt.gameObject.AddComponent<HubHover>();
            hv.target = rt; hv.shift = Vector2.zero; hv.scale = 1.04f;
            Graphic g;
            if (style == 0)
            {
                var img = HudKit.AddImage(rt, theme.ink3, raycast: true);
                hv.graphic = img; hv.baseColor = theme.ink3; hv.hoverColor = Color.Lerp(theme.ink3, Color.white, .12f);
                g = img;
            }
            else
            {
                var tile = HubTiles.Make(rt, style == 2, true);
                HubTiles.Hover(hv, tile, 14f);
                g = tile.face;
            }
            var b = rt.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = g;
            b.onClick.AddListener(() => { Sfx.Ui(); act(); });
            var t = HudKit.AddText(rt, label, label.Length == 1 ? 30f : 18f, style == 1 ? theme.ink : Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Title);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            HudKit.Stretch(t.rectTransform);
            return rt;
        }

        // ---- open / close ------------------------------------------------------------------------------------------------------
        static readonly Cat[] ShopCats = { Cat.Weapon, Cat.Attachment, Cat.Gadget };
        static readonly Cat[] MedicalCats = { Cat.Medical };

        /// <summary>The shop (武器 / アタッチメント / ガジェット) on tab `tab`.</summary>
        public void OpenShop(int tab) => Open(ShopCats, tab);
        /// <summary>The 医務室's medicines.</summary>
        public void OpenMedical() => Open(MedicalCats, 0);

        void Open(Cat[] list, int start)
        {
            cats = list;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            asking = false; ask.gameObject.SetActive(false);
            toastClock = 99f; toastGroup.alpha = 0f;
            BuildTabs();
            ShowCat(Mathf.Clamp(start, 0, cats.Length - 1), false);
            RefreshMoney();
            closeClock = -1f;
            rowsRoot.anchoredPosition = Vector2.zero;
            clock = 0f;
            running = Application.isPlaying;
            Evaluate(running ? 0f : Duration);
        }

        /// <summary>After the exit animation (the station screen under it slides its options in again).</summary>
        public System.Action Closed;
        float closeClock = -1f;
        public const float CloseTime = .28f;

        /// <summary>Back to the screen under it: everything slides off (the panel right, the card left) and fades, then it hides.</summary>
        public void Close()
        {
            if (closeClock >= 0f) return;
            running = false;
            asking = false; ask.gameObject.SetActive(false);
            if (!Application.isPlaying || UIMotionSettings.reduceMotion) { Finish(); return; }
            closeClock = 0f;
        }

        void Finish()
        {
            closeClock = -1f;
            if (bench != null) bench.SetLive(false);
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        /// <summary>The exit, `p` 0 → 1.</summary>
        void EvaluateClose(float p)
        {
            float e = UIEase.Tactical(p);
            group.alpha = 1f - e;
            panel.anchoredPosition = panelPos + new Vector2(420f * e, 0f);
            card.anchoredPosition = cardPos + new Vector2(-260f * e, 0f);
            card.localRotation = Quaternion.Euler(0f, 0f, 6f * e);
            tabsRoot.anchoredPosition = tabsPos + new Vector2(0f, 50f * e);
            money.anchoredPosition = moneyPos + new Vector2(200f * e, 0f);
            rowsRoot.anchoredPosition = new Vector2(260f * e, 0f);
        }

        void OnDestroy() { if (bench != null) HudKit.DestroySafe(bench.gameObject); }

        public void RefreshMoney() => moneyText.SetText(Editable ? D.money.ToString("N0") + " 円" : "— 円");

        // ---- tabs --------------------------------------------------------------------------------------------------------------
        static string CatName(Cat c) => c == Cat.Weapon ? "武器" : c == Cat.Attachment ? "アタッチメント" : c == Cat.Gadget ? "ガジェット" : "医薬品";

        void BuildTabs()
        {
            foreach (var g in tabObjects) if (g != null) HudKit.DestroySafe(g);
            tabObjects.Clear();
            for (int i = 0; i < cats.Length; i++)
            {
                int idx = i;
                var t = HudKit.NewRect("Tab" + i, tabsRoot);
                UILayout.Place(t, 868f + i * 172f, 18f, 170f, 42f);
                var img = HudKit.AddImage(t, UITheme.Current.ink2, raycast: true);
                var top = HudKit.NewRect("DecoTop", t);
                top.anchorMin = new Vector2(0f, 1f); top.anchorMax = new Vector2(1f, 1f); top.pivot = new Vector2(.5f, 1f);
                top.anchoredPosition = Vector2.zero; top.sizeDelta = new Vector2(0f, 4f);
                HudKit.AddImage(top, UITheme.Current.blue);
                var b = t.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = img;
                b.onClick.AddListener(() => { if (asking) return; Sfx.Ui(); ShowCat(idx, true); });
                var tx = HudKit.AddText(t, CatName(cats[i]), 15f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Title);
                tx.textWrappingMode = TextWrappingModes.NoWrap;
                HudKit.Stretch(tx.rectTransform);
                tabObjects.Add(t.gameObject);
            }
        }

        void PaintTabs()
        {
            var theme = UITheme.Current;
            for (int i = 0; i < tabObjects.Count; i++)
            {
                bool on = i == cat;
                var img = tabObjects[i].GetComponent<Image>();
                img.color = on ? Color.white : theme.ink2;
                img.sprite = on ? HubTiles.Gradient() : null;
                tabObjects[i].transform.Find("DecoTop").gameObject.SetActive(on);
                var tx = tabObjects[i].GetComponentInChildren<TMP_Text>();
                tx.color = on ? theme.ink : new Color(1f, 1f, 1f, .6f);
            }
        }

        void ShowCat(int i, bool animate)
        {
            cat = i;
            PaintTabs();
            pick = 0;
            Fill();
            BuildRows();
            scroll = scrollGoal = 0f;
            rowClock = Application.isPlaying ? (animate ? .25f : 0f) : 99f;
            for (int r = 0; r < rows.Count; r++) rows[r].pick = r == pick ? 1f : 0f;
            ShowPick(true);
            LayoutRows();
        }

        // ---- the goods -----------------------------------------------------------------------------------------------------------
        static string Glyph(string key)
        {
            switch (key)
            {
                case "frag": return "✹";
                case "smoke": return "◎";
                case "flash": return "✦";
                case "drone": return "⌖";
                default: return "✚";
            }
        }

        static string Users(WeaponData w)
        {
            string s = "";
            foreach (var o in HubCatalog.Squad)
                foreach (var g in HubCatalog.GunsOf(o))
                    if (g == w) { s += (s.Length > 0 ? " / " : "") + o.DisplayName; break; }
            return s;
        }

        void Fill()
        {
            products.Clear();
            var c = cats[cat];
            if (c == Cat.Weapon)
            {
                foreach (var w in HubCatalog.WeaponsForSale)
                {
                    var gun = w;
                    bool owned = HubCatalog.Owns(D, w);
                    string users = Users(w);
                    products.Add(new Product
                    {
                        name = w.weaponName, kind = "WEAPON  ·  武器", gun = w, price = HubCatalog.WeaponPrice(w), have = owned ? 1 : 0, cap = 1, owned = owned,
                        desc = string.IsNullOrEmpty(users) ? "部隊の新しい武器。" : users + " が使える武器。隊員・装備管理で装備する。",
                        info = ShopContent.GunNote(w) + (owned ? "\n所持済み" : ""),
                        buy = n => { if (HubCatalog.Owns(D, gun)) return null; D.ownedWeapons.Add(gun.key); return gun.weaponName + " を購入しました"; },
                    });
                }
                return;
            }
            if (c == Cat.Attachment)
            {
                foreach (var a in HubCatalog.Attachments)
                {
                    var part = a;
                    int have = D.AttachmentStockOf(a.key).count, fitted = D.AttachmentsFitted(a.key);
                    products.Add(new Product
                    {
                        name = a.name, kind = "ATTACHMENT  ·  " + HubCatalog.AttachmentSlots[a.slot], art = HubCatalog.ArtOf(a), glyph = "⬢",
                        price = a.price, have = have, cap = SaveData.MaxAttachmentStock,
                        desc = a.Summary, info = "所持 " + have + (fitted > 0 ? " (装着中 " + fitted + ")" : "") + " / " + SaveData.MaxAttachmentStock + "  ·  1つにつき1丁に装着",
                        buy = n => { D.AttachmentStockOf(part.key).count += n; return part.name + " ×" + n + " を購入しました"; },
                    });
                }
                return;
            }
            bool medical = c == Cat.Medical;
            foreach (var g in HubCatalog.Gadgets)
            {
                if (g.medical != medical) continue;
                var item = g;
                int have = D.GadgetOf(g.key).count;
                products.Add(new Product
                {
                    name = g.name, kind = medical ? "MEDICAL  ·  医薬品" : "GADGET  ·  ガジェット", glyph = Glyph(g.key), art = HubCatalog.ArtOf(g),
                    price = g.price, have = have, cap = HubCatalog.MaxGadgetStock,
                    desc = g.note + "。", info = "所持 " + have + " / " + HubCatalog.MaxGadgetStock + "  ·  出撃時に1人1つ持てる",
                    buy = n => { D.GadgetOf(item.key).count += n; return item.name + " ×" + n + " を購入しました"; },
                });
            }
        }

        void BuildRows()
        {
            foreach (var r in rows) if (r.rt != null) HudKit.DestroySafe(r.rt.gameObject);
            rows.Clear();
            var theme = UITheme.Current;
            for (int i = 0; i < products.Count; i++)
            {
                var p = products[i];
                var r = new Row();
                r.rt = HudKit.NewRect("Row" + i, rowsRoot);
                r.rt.anchorMin = r.rt.anchorMax = new Vector2(0f, 1f); r.rt.pivot = new Vector2(0f, .5f);
                r.rt.sizeDelta = new Vector2(RowW, RowH);
                r.plate = HudKit.AddImage(r.rt, theme.ink3, raycast: true);
                r.plate.sprite = HubTiles.Gradient();
                r.slant = r.rt.gameObject.AddComponent<SlantedGraphic>();
                r.slant.Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, RowH * (PanelTopX - PanelBottomX) / ScreenH);
                r.group = r.rt.gameObject.AddComponent<CanvasGroup>();
                var ptr = r.rt.gameObject.AddComponent<HubPointer>();
                ptr.index = i; ptr.Entered = Hover; ptr.Clicked = ClickRow;

                var mk = HudKit.NewRect("Marker", r.rt);
                mk.anchorMin = mk.anchorMax = new Vector2(0f, .5f); mk.pivot = new Vector2(1f, .5f);
                mk.anchoredPosition = new Vector2(-4f, 0f); mk.sizeDelta = new Vector2(30f, 30f);
                r.marker = mk.gameObject.AddComponent<CanvasGroup>();
                var arrow = HudKit.AddText(mk, "➜", 22f, theme.blue, true, TextAlignmentOptions.Center, "Arrow", UITheme.TextRole.Display);
                HudKit.UseSymbolFont(arrow);
                HudKit.Stretch(arrow.rectTransform);

                bool can = p.Room > 0;
                r.name = HudKit.AddText(r.rt, p.name, 26f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Name", UITheme.TextRole.Title);
                r.name.textWrappingMode = TextWrappingModes.NoWrap; r.name.overflowMode = TextOverflowModes.Ellipsis;
                UILayout.Place(r.name.rectTransform, 36f, 0f, RowW - 300f, RowH);
                r.price = HudKit.AddText(r.rt, p.owned ? "購入済み" : !can ? "上限" : p.price.ToString("N0") + " 円", 17f,
                    Color.white, true, TextAlignmentOptions.MidlineRight, "Price", UITheme.TextRole.Body);
                r.price.textWrappingMode = TextWrappingModes.NoWrap;
                UILayout.Place(r.price.rectTransform, RowW - 262f, 0f, 150f, RowH);
                var pill = HudKit.NewRect("Pill", r.rt);
                UILayout.Place(pill, RowW - 104f, (RowH - 34f) * .5f, 70f, 34f);
                r.pillPlate = HudKit.AddImage(pill, theme.paper);
                pill.gameObject.AddComponent<CutCornerGraphic>().Set(8f, CutCornerGraphic.Corners.All);
                r.pill = HudKit.AddText(pill, "×" + p.have.ToString("00"), 17f, theme.ink, true, TextAlignmentOptions.Center, "Count", UITheme.TextRole.Body);
                r.pill.textWrappingMode = TextWrappingModes.NoWrap;
                HudKit.Stretch(r.pill.rectTransform);
                rows.Add(r);
            }
        }

        /// <summary>The panel's left edge at height y (canvas units from the top).</summary>
        static float EdgeX(float y) => Mathf.Lerp(PanelTopX, PanelBottomX, y / ScreenH);

        /// <summary>Places the rows (heights from their pick ease, along the panel's edge, scrolled) — every frame while anything moves.</summary>
        void LayoutRows()
        {
            var theme = UITheme.Current;
            bool still = UIMotionSettings.reduceMotion;
            float y = 10f - scroll;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                float e = UIEase.Tactical(r.pick);
                float s = Mathf.Lerp(RowSmall, 1f, e);
                float h = RowH * s;
                float cy = y + h * .5f;
                float screenY = RowsTop + cy;
                float x = EdgeX(screenY) - (PanelBottomX - 40f) + RowInset - 18f * e;
                float enter = Mathf.Clamp01((rowClock - .04f * Mathf.Min(i, 10)) / .3f);
                float ep = UIEase.Tactical(enter);
                if (!still) x += 160f * (1f - ep);
                var pos = new Vector2(x, -cy);
                if (r.rt.anchoredPosition != pos) r.rt.anchoredPosition = pos;
                var sc = new Vector3(s, s, 1f);
                if (r.rt.localScale != sc) r.rt.localScale = sc;
                bool can = products[i].Room > 0;
                // the pick: a light tile with ink words (a grey one when it can't be bought); the rest dark with white words
                var col = Color.Lerp(theme.ink3, can ? Color.white : theme.paper2, e);
                if (r.plate.color != col) r.plate.color = col;
                var word = Color.Lerp(Color.white, theme.ink, e);
                if (r.name.color != word) r.name.color = word;
                var pw = Color.Lerp(Color.white, can ? theme.blue : theme.dim, e);
                if (r.price.color != pw) r.price.color = pw;
                var pp = Color.Lerp(theme.paper, theme.ink, e);
                if (r.pillPlate.color != pp) r.pillPlate.color = pp;
                var pt = Color.Lerp(theme.ink, Color.white, e);
                if (r.pill.color != pt) r.pill.color = pt;
                float a = ep * Mathf.Lerp(.55f, 1f, e);
                if (r.group.alpha != a) r.group.alpha = a;
                if (r.marker.alpha != e) r.marker.alpha = e;
                y += h + RowGap;
            }
        }

        float ContentHeight()
        {
            float h = 10f;
            for (int i = 0; i < rows.Count; i++) h += RowH * (i == pick ? 1f : RowSmall) + RowGap;
            return h;
        }

        void ScrollToPick()
        {
            float area = RowsBottom - RowsTop;
            float y = 10f + pick * (RowH * RowSmall + RowGap);
            scrollGoal = Mathf.Clamp(y - area * .4f, 0f, Mathf.Max(0f, ContentHeight() - area + 10f));
        }

        // ---- the pick -------------------------------------------------------------------------------------------------------------
        void Hover(int i) { if (!asking && i != pick) SetPick(i); }

        void ClickRow(int i)
        {
            if (asking) return;
            if (i != pick) SetPick(i);
            Sfx.Ui();
            Ask();
        }

        void SetPick(int i)
        {
            if (products.Count == 0) return;
            i = Mathf.Clamp(i, 0, products.Count - 1);
            if (i == pick) return;
            pick = i;
            ShowPick(false);
        }

        /// <summary>The card shows the pick (its picture glitches in again).</summary>
        void ShowPick(bool first)
        {
            ScrollToPick();
            if (first) scroll = scrollGoal;
            if (products.Count == 0)
            {
                nameText.SetText(""); kindText.SetText(""); descText.SetText("販売中の商品はありません"); infoText.SetText("");
                photo.enabled = false; glyph.SetText("");
                if (bench != null) bench.SetLive(false);
                return;
            }
            var p = products[pick];
            nameText.SetText(p.name);
            kindText.SetText(p.kind);
            descText.SetText(p.desc);
            infoText.SetText(p.info);
            bool live = false;
            if (p.gun != null)
            {
                if (bench == null)
                {
                    bench = HubWorkbench.Build();
                    bench.transform.position = HubWorkbench.Origin + new Vector3(0f, -40f, 0f);   // (not the loadout screen's bench)
                }
                bench.Show(p.gun);
                live = Application.isPlaying;
                if (!live) bench.RenderNow();
                // the 16:9 render cropped to the window (3:2)
                float u = (WinW / WinH) / ((float)HubWorkbench.TexW / HubWorkbench.TexH);
                photo.SetTexture(bench.Texture, new Rect((1f - u) * .5f, 0f, u, 1f));
                photoRt.sizeDelta = new Vector2(WinW, WinH);
                photo.enabled = true; glyph.SetText("");
            }
            else if (p.art != null)
            {
                // the glitch image draws the sprite's packed (trimmed) rect: sized and placed where it sits in the full frame,
                // the full frame fitted to the window's height
                photo.SetSprite(p.art);
                var full = p.art.rect; var packed = p.art.textureRect;
                float k = (WinH - 10f) / Mathf.Max(1f, Mathf.Max(full.width, full.height));
                photoRt.sizeDelta = packed.size * k;
                photoRt.anchoredPosition = (packed.center - new Vector2(full.x, full.y) - full.size * .5f) * k;
                photo.enabled = true; glyph.SetText("");
            }
            else
            {
                photo.enabled = false;
                glyph.SetText(p.glyph);
            }
            if (bench != null) bench.SetLive(live);
            if (p.art == null || p.gun != null) photoRt.anchoredPosition = Vector2.zero;
            if (Application.isPlaying)
            {
                if (photo.enabled) photo.Play();
                else glyphGlitch.Burst(.45f);
                cardKick = 0f;
            }
            else photo.Evaluate(99f);
        }

        float cardKick = 99f;

        // ---- buying ---------------------------------------------------------------------------------------------------------------
        void Ask()
        {
            if (products.Count == 0) return;
            var p = products[pick];
            if (!Editable) { Toast("セーブデータがありません (購入できません)"); return; }
            if (p.owned) { Toast("購入済みです"); return; }
            if (p.Room <= 0) { Toast("これ以上持てません (上限 " + p.cap + ")"); return; }
            if (D.money < p.price) { Toast("所持金が足りません"); return; }
            asking = true;
            askN = 1;
            bool one = p.gun != null;
            askTitle.SetText(one ? "購入しますか？" : "何個購入しますか？");
            askName.SetText(p.name + "  ·  " + p.price.ToString("N0") + " 円");
            askMinus.gameObject.SetActive(!one); askPlus.gameObject.SetActive(!one);
            ask.gameObject.SetActive(true);
            ask.SetAsLastSibling();
            askClock = Application.isPlaying ? 0f : 99f;
            PaintAsk();
            EvaluateAsk(askClock);
        }

        int AskMax()
        {
            var p = products[pick];
            return Mathf.Max(1, Mathf.Min(p.Room, p.price > 0 ? D.money / p.price : p.Room));
        }

        void Step(int d)
        {
            if (!asking) return;
            int n = Mathf.Clamp(askN + d, 1, AskMax());
            if (n == askN) return;
            askN = n;
            PaintAsk();
            askPop = 0f;
        }

        float askPop = 99f;

        void PaintAsk()
        {
            var p = products[pick];
            int max = AskMax();
            askCount.SetText(p.gun != null ? "×1" : askN.ToString());
            askMax.SetText(p.gun != null ? "" : "最大 " + max + "  (所持 " + p.have + " / " + p.cap + ")");
            int total = p.price * askN;
            askTotal.SetText("合計  " + total.ToString("N0") + " 円");
            askLeft.SetText("残り  " + (D.money - total).ToString("N0") + " 円");
        }

        void CancelAsk()
        {
            asking = false;
            ask.gameObject.SetActive(false);
        }

        void Confirm()
        {
            if (!asking) return;
            var p = products[pick];
            int n = Mathf.Clamp(askN, 1, AskMax());
            int total = p.price * n;
            CancelAsk();
            if (!Editable || p.Room < n || D.money < total) { Toast("購入できませんでした"); return; }
            D.money -= total;
            string msg = p.buy(n);
            SaveSystem.Write(false);
            RefreshMoney();
            screen.RefreshTopbar();
            int keep = pick;
            Fill();
            BuildRows();
            pick = Mathf.Clamp(keep, 0, products.Count - 1);
            for (int r = 0; r < rows.Count; r++) rows[r].pick = r == pick ? 1f : 0f;
            rowClock = 99f;
            ShowPick(false);
            scroll = scrollGoal;
            LayoutRows();
            Toast(msg ?? "購入しました");
            moneyKick = 0f;
        }

        float moneyKick = 99f;

        void Toast(string text)
        {
            toastText.SetText(text);
            toastClock = Application.isPlaying ? 0f : .5f;
            EvaluateToast();
        }

        // ---- motion ---------------------------------------------------------------------------------------------------------------
        public float Duration => .7f;

        static float Ease(float t, float from, float len) => UIEase.Tactical(Mathf.Clamp01((t - from) / len));

        public void Evaluate(float t)
        {
            bool still = UIMotionSettings.reduceMotion;
            group.alpha = Mathf.Clamp01(t / .2f);
            float p = Ease(t, 0f, .45f);
            panel.anchoredPosition = panelPos + new Vector2(still ? 0f : 420f * (1f - p), 0f);
            p = Ease(t, .06f, .4f);
            card.anchoredPosition = cardPos + new Vector2(still ? 0f : -260f * (1f - p), 0f);
            card.localRotation = Quaternion.Euler(0f, 0f, still ? 0f : 6f * (1f - p));
            cardGroup.alpha = p;
            p = Ease(t, .18f, .3f);
            tabsRoot.anchoredPosition = tabsPos + new Vector2(0f, still ? 0f : 50f * (1f - p));
            tabsGroup.alpha = p;
            p = Ease(t, .3f, .3f);
            moneyGroup.alpha = p;
            float kick = moneyKick < .3f && !still ? Mathf.Sin(moneyKick / .3f * Mathf.PI) * -10f : 0f;
            money.anchoredPosition = moneyPos + new Vector2(still ? 0f : 200f * (1f - p), kick);
        }

        void EvaluateAsk(float t)
        {
            float p = Ease(t, 0f, .22f);
            askGroup.alpha = p;
            float s = UIMotionSettings.reduceMotion ? 1f : Mathf.Lerp(.86f, 1f, p);
            askBox.localScale = new Vector3(s, s, 1f);
            float k = askPop < .16f ? 1f + .18f * Mathf.Sin(askPop / .16f * Mathf.PI) : 1f;
            askCount.rectTransform.localScale = new Vector3(k, k, 1f);
        }

        void EvaluateToast()
        {
            float t = toastClock;
            float a = t < .15f ? t / .15f : t < 2.2f ? 1f : Mathf.Clamp01(1f - (t - 2.2f) / .3f);
            toastGroup.alpha = a;
            float dy = UIMotionSettings.reduceMotion ? 0f : 12f * (1f - UIEase.Tactical(Mathf.Clamp01(t / .2f)));
            toast.anchoredPosition = new Vector2(WinX, -(CardH - 44f) + -dy);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (closeClock >= 0f)
            {
                closeClock += dt;
                EvaluateClose(Mathf.Clamp01(closeClock / CloseTime));
                if (closeClock >= CloseTime) Finish();
                return;
            }
            clock += dt;
            if (running) { Evaluate(clock); if (clock >= Duration && moneyKick >= .3f) running = false; }
            if (moneyKick < .3f) { moneyKick += dt; Evaluate(Mathf.Max(clock, Duration)); }
            if (toastClock < 2.6f) { toastClock += dt; EvaluateToast(); }

            // the rows: entrance, the pick's ease, the scroll
            bool moving = rowClock < 1.2f || Mathf.Abs(scroll - scrollGoal) > .1f;
            if (rowClock < 1.2f) rowClock += dt;
            for (int i = 0; i < rows.Count; i++)
            {
                float goal = i == pick ? 1f : 0f;
                if (rows[i].pick != goal) { rows[i].pick = Mathf.MoveTowards(rows[i].pick, goal, dt / PickTime); moving = true; }
            }
            if (Mathf.Abs(scroll - scrollGoal) > .1f) scroll = Mathf.Lerp(scroll, scrollGoal, 1f - Mathf.Exp(-14f * dt));
            else scroll = scrollGoal;
            if (moving) LayoutRows();

            // the card's small kick on a new pick
            if (cardKick < .2f)
            {
                cardKick += dt;
                float k = UIMotionSettings.reduceMotion ? 0f : Mathf.Sin(Mathf.Clamp01(cardKick / .2f) * Mathf.PI) * 6f;
                if (!running) card.anchoredPosition = cardPos + new Vector2(0f, k);
            }

            if (asking)
            {
                if (askClock < .3f || askPop < .16f) { askClock += dt; askPop += dt; EvaluateAsk(askClock); }
                Keys(true);
            }
            else Keys(false);
        }

        void Keys(bool dialog)
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            if (dialog)
            {
                if (kb != null)
                {
                    if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) Step(-1);
                    if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) Step(1);
                    if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) { Sfx.Ui(); Confirm(); return; }
                    if (kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed) { EscapeGate.Consume(); CancelAsk(); return; }
                }
                if (wheel > 0f) Step(1); else if (wheel < 0f) Step(-1);
                return;
            }
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed) { EscapeGate.Consume(); Close(); return; }
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) SetPick(pick - 1);
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) SetPick(pick + 1);
                if (cats.Length > 1)
                {
                    if (kb.qKey.wasPressedThisFrame) { Sfx.Ui(); ShowCat((cat + cats.Length - 1) % cats.Length, true); }
                    if (kb.eKey.wasPressedThisFrame) { Sfx.Ui(); ShowCat((cat + 1) % cats.Length, true); }
                }
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) { Sfx.Ui(); Ask(); }
            }
            if (wheel < 0f) SetPick(pick + 1); else if (wheel > 0f) SetPick(pick - 1);
        }

        /// <summary>Previews: posed settled — tab `tab` of the shop (or the medicines), `select` picked, the dialog open with `count`.</summary>
        public void Pose(bool medical, int tab, int select, int count = 0)
        {
            if (medical) OpenMedical(); else OpenShop(tab);
            Evaluate(Duration);
            pick = -1;
            SetPick(select);
            for (int r = 0; r < rows.Count; r++) rows[r].pick = r == pick ? 1f : 0f;
            rowClock = 99f;
            scroll = scrollGoal;
            LayoutRows();
            if (count > 0)
            {
                Ask();
                askN = Mathf.Clamp(count, 1, asking ? AskMax() : 1);
                if (asking) PaintAsk();
                EvaluateAsk(99f);
            }
        }
    }
}
