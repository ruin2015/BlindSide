// The HQ's operator growth panel (the select screen: click a squad row). Built at runtime under a full-screen parent, like the settings panel:
//   Dim (rgba(3,5,8,.5), takes every click) + Pop 680 × 430, centred: Box paper, 26 cut at the top right / bottom left, DecoShadow
//     Head     ink 46: blue band 12, 「隊員強化」 18 body, "OPERATOR GROWTH" 11 display
//     Left     (28 in, 190 wide) the operator's face (OperatorArt.Face, 150×150, operator-colour band under it), name 22 display,
//              kana · role 12, "Lv N" 20 display, the level's EXP bar and "exp / need EXP", 残りポイント (gold) — the points left to give
//     Stats    five rows 46 high (体力 / 防御力 / 攻撃力 / 機動力 / 精密力): label 14 + what one point does (11, dim); on the right
//              [−] points [+] and the total it gives now (13 display, blue). [+] spends a point, [−] takes back one given since the
//              panel opened; nothing is kept until 確定
//     Mastery  the operator's weapon: name, 熟練度 Lv N, its bar, and what it gives (damage % / hit chance)
//     Footer   52: 「閉じる」 (paper2, discards) + 「確定」 (blue: writes the points into the save and saves — the SAVE notice shows)
// Without a save session everything shows level 1 and the buttons are off. ESC closes (EscapeGate). Entrance as the settings panel
// (.2s dim, .32s pop); ITimedEffect.Evaluate(t); nothing allocated per frame (texts change only on a click).
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.Progress;
using Blindside.Save;

namespace Blindside.UI
{
    public class KitGrowthPanel : MonoBehaviour, ITimedEffect
    {
        public const float Width = 680f, HeadH = 46f, RowH = 46f, FootH = 52f, PadX = 28f, LeftW = 190f, Cut = 26f, BandW = 12f;
        public const float BodyH = 332f, Height = HeadH + BodyH + FootH;
        public const float DimTime = .2f, PopTime = .32f;
        static readonly Color DimColor = new Color32(3, 5, 8, 128);
        static readonly Color Gold = UIPalette.Hex("#FFC81E");
        public static readonly string[] StatNames = { "体力", "防御力", "攻撃力", "機動力", "精密力" };
        static readonly string[] StatNotes =
        {
            "1ポイントごとに最大HP +4",
            "1ポイントで被ダメージ −1.5% (最大30%)",
            "1ポイントごとに与えるダメージ +2%",
            "1ポイントごとに移動力 +0.2m",
            "1ポイントごとに命中率 +0.5",
        };

        RectTransform pop, box;
        CanvasGroup popGroup;
        Image dim, face;
        TMP_Text nameText, kanaText, levelText, expText, pointsText, weaponText, masteryText, masteryEffect;
        RectTransform expFill, masteryFill, band;
        readonly TMP_Text[] values = new TMP_Text[5], effects = new TMP_Text[5];
        readonly Button[] plus = new Button[5], minus = new Button[5];
        readonly Image[] plusImg = new Image[5], minusImg = new Image[5];
        Button confirm; Image confirmImg;
        float clock; bool running;

        OperatorData op;
        OperatorProgress prog;
        WeaponProgress wprog;
        readonly int[] add = new int[5];

        public bool IsOpen => gameObject.activeSelf;

        // ---- build -------------------------------------------------------------------------------------------------------------
        public static KitGrowthPanel Build(Transform parent)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect("GrowthPanel", parent);
            HudKit.Stretch(root);
            var p = root.gameObject.AddComponent<KitGrowthPanel>();

            var dimRt = HudKit.NewRect("DecoDim", root);
            HudKit.Stretch(dimRt);
            p.dim = HudKit.AddImage(dimRt, DimColor, raycast: true);

            p.pop = HudKit.NewRect("Pop", root);
            p.pop.anchorMin = p.pop.anchorMax = p.pop.pivot = new Vector2(.5f, .5f);
            p.pop.sizeDelta = new Vector2(Width, Height);
            p.popGroup = p.pop.gameObject.AddComponent<CanvasGroup>();
            var boxPanel = TacticalPanel.Create(p.pop, "Box", KitColor.Paper, SlantedGraphic.Size.Custom, 0f);
            HudKit.Stretch(boxPanel.rectTransform);
            boxPanel.raycastTarget = true;
            boxPanel.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.Mockup);
            HudKit.Stretch(KitShadow.Behind(boxPanel.rectTransform, new Color(.03f, .04f, .06f), .6f, 14f, 30f).rectTransform);
            p.box = boxPanel.rectTransform;

            // head
            var head = HudKit.NewRect("Head", p.box);
            UILayout.Place(head, 0f, 0f, Width, HeadH);
            HudKit.AddImage(head, theme.ink);
            head.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.TopRight);
            var hb = HudKit.NewRect("DecoBand", head);
            hb.anchorMin = new Vector2(0f, 0f); hb.anchorMax = new Vector2(0f, 1f); hb.pivot = new Vector2(0f, .5f);
            hb.anchoredPosition = Vector2.zero; hb.sizeDelta = new Vector2(BandW, 0f);
            HudKit.AddImage(hb, theme.blue);
            var title = HudKit.AddText(head, "隊員強化", 18f, Color.white, false, TextAlignmentOptions.MidlineLeft, "Title", UITheme.TextRole.Body);
            UILayout.Place(title.rectTransform, BandW + PadX, 0f, 200f, HeadH);
            var en = HudKit.AddText(head, "OPERATOR GROWTH", 11f, Color.Lerp(theme.ink, Color.white, .65f), true, TextAlignmentOptions.MidlineRight, "En", UITheme.TextRole.Display);
            en.characterSpacing = 10f;
            UILayout.Place(en.rectTransform, Width - 52f - 200f, 0f, 200f, HeadH);

            p.BuildLeft(theme);
            p.BuildStats(theme);
            p.BuildMastery(theme);
            p.BuildFooter(theme);

            root.gameObject.SetActive(false);
            return p;
        }

        void BuildLeft(UITheme theme)
        {
            float x = PadX, y = HeadH + 18f;
            var frame = HudKit.NewRect("Face", box);
            UILayout.Place(frame, x, y, 150f, 150f);
            HudKit.AddImage(frame, Color.Lerp(theme.paper2, theme.ink, .1f));
            frame.gameObject.AddComponent<RectMask2D>();
            var f = HudKit.NewRect("Photo", frame);
            HudKit.Stretch(f);
            face = f.gameObject.AddComponent<Image>();
            face.preserveAspect = true; face.raycastTarget = false;
            band = HudKit.NewRect("DecoBand", box);
            UILayout.Place(band, x, y + 150f, 150f, 4f);
            HudKit.AddImage(band, theme.blue);

            nameText = HudKit.AddText(box, "", 22f, theme.ink, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Display);
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(nameText.rectTransform, x, y + 162f, LeftW, 26f);
            kanaText = HudKit.AddText(box, "", 12f, theme.dim, false, TextAlignmentOptions.TopLeft, "Kana", UITheme.TextRole.Body);
            kanaText.textWrappingMode = TextWrappingModes.NoWrap; kanaText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(kanaText.rectTransform, x, y + 188f, LeftW, 16f);
            levelText = HudKit.AddText(box, "", 20f, theme.ink, true, TextAlignmentOptions.TopLeft, "Level", UITheme.TextRole.Display);
            UILayout.Place(levelText.rectTransform, x, y + 210f, 90f, 24f);
            expText = HudKit.AddText(box, "", 11f, theme.dim, true, TextAlignmentOptions.BottomRight, "Exp", UITheme.TextRole.Display);
            UILayout.Place(expText.rectTransform, x + 70f, y + 212f, LeftW - 70f, 20f);
            expFill = Bar(box, x, y + 238f, LeftW, 6f, theme.blue);
            pointsText = HudKit.AddText(box, "", 14f, theme.ink, true, TextAlignmentOptions.TopLeft, "Points", UITheme.TextRole.Title);
            UILayout.Place(pointsText.rectTransform, x, y + 252f, LeftW, 22f);
        }

        void BuildStats(UITheme theme)
        {
            float x = PadX + LeftW + 24f, w = Width - x - PadX, y0 = HeadH + 14f;
            for (int i = 0; i < 5; i++)
            {
                float y = y0 + i * RowH;
                var l = HudKit.AddText(box, StatNames[i], 14f, theme.ink, true, TextAlignmentOptions.TopLeft, "StatLabel" + i, UITheme.TextRole.Body);
                UILayout.Place(l.rectTransform, x, y + 4f, 120f, 18f);
                var n = HudKit.AddText(box, StatNotes[i], 11f, theme.dim, false, TextAlignmentOptions.TopLeft, "StatNote" + i, UITheme.TextRole.Body);
                n.textWrappingMode = TextWrappingModes.NoWrap; n.overflowMode = TextOverflowModes.Ellipsis;
                UILayout.Place(n.rectTransform, x, y + 24f, w - 190f, 16f);
                int idx = i;
                minus[i] = Step(box, "Minus" + i, "-", x + w - 186f, y + 6f, () => Change(idx, -1), out minusImg[i]);
                values[i] = HudKit.AddText(box, "", 18f, theme.ink, true, TextAlignmentOptions.Center, "StatValue" + i, UITheme.TextRole.Display);
                UILayout.Place(values[i].rectTransform, x + w - 154f, y + 6f, 40f, 28f);
                plus[i] = Step(box, "Plus" + i, "+", x + w - 114f, y + 6f, () => Change(idx, 1), out plusImg[i]);
                effects[i] = HudKit.AddText(box, "", 13f, theme.blue, true, TextAlignmentOptions.MidlineRight, "StatEffect" + i, UITheme.TextRole.Display);
                effects[i].textWrappingMode = TextWrappingModes.NoWrap;
                UILayout.Place(effects[i].rectTransform, x + w - 76f, y + 6f, 76f, 28f);
                if (i < 4)
                {
                    var rule = HudKit.NewRect("DecoRule", box);
                    UILayout.Place(rule, x, y + RowH - 1f, w, 1f);
                    HudKit.AddImage(rule, Color.Lerp(theme.paper, theme.ink, .12f));
                }
            }
        }

        void BuildMastery(UITheme theme)
        {
            float x = PadX + LeftW + 24f, w = Width - x - PadX, y = HeadH + 14f + 5f * RowH + 10f;
            var bg = HudKit.NewRect("MasteryBox", box);
            UILayout.Place(bg, x, y, w, 62f);
            HudKit.AddImage(bg, Color.Lerp(theme.paper, theme.paper2, .6f));
            bg.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
            var tag = HudKit.AddText(bg, "WEAPON MASTERY", 11f, theme.dim, true, TextAlignmentOptions.TopLeft, "Tag", UITheme.TextRole.Display);
            tag.characterSpacing = 8f;
            UILayout.Place(tag.rectTransform, 18f, 6f, 160f, 14f);
            weaponText = HudKit.AddText(bg, "", 14f, theme.ink, true, TextAlignmentOptions.TopLeft, "Weapon", UITheme.TextRole.Body);
            weaponText.textWrappingMode = TextWrappingModes.NoWrap; weaponText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(weaponText.rectTransform, 18f, 22f, w - 220f, 20f);
            masteryText = HudKit.AddText(bg, "", 14f, theme.ink, true, TextAlignmentOptions.TopRight, "Mastery", UITheme.TextRole.Body);
            UILayout.Place(masteryText.rectTransform, w - 210f, 6f, 100f, 18f);
            masteryEffect = HudKit.AddText(bg, "", 12f, theme.blue, true, TextAlignmentOptions.TopRight, "MasteryEffect", UITheme.TextRole.Body);
            masteryEffect.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(masteryEffect.rectTransform, w - 210f, 24f, 196f, 18f);
            masteryFill = Bar(bg, 18f, 48f, w - 46f, 4f, Gold);
        }

        void BuildFooter(UITheme theme)
        {
            float y = HeadH + BodyH, half = Width * .5f;
            var close = HudKit.NewRect("Close", box);
            UILayout.Place(close, 0f, y, half + 6f, FootH);
            var ci = HudKit.AddImage(close, theme.paper2, raycast: true);
            close.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.BottomLeft);
            var cb = close.gameObject.AddComponent<Button>();
            cb.transition = Selectable.Transition.None; cb.targetGraphic = ci;
            cb.onClick.AddListener(Close);
            close.gameObject.AddComponent<KitBrighten>().image = ci;
            var ct = HudKit.AddText(close, "閉じる", 16f, theme.ink, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Title);
            HudKit.Stretch(ct.rectTransform);

            var ok = HudKit.NewRect("Confirm", box);
            UILayout.Place(ok, half - 6f, y, half + 6f, FootH);
            confirmImg = HudKit.AddImage(ok, theme.blue, raycast: true);
            ok.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 18f);
            confirm = ok.gameObject.AddComponent<Button>();
            confirm.transition = Selectable.Transition.None; confirm.targetGraphic = confirmImg;
            confirm.onClick.AddListener(Confirm);
            ok.gameObject.AddComponent<KitBrighten>().image = confirmImg;
            var ot = HudKit.AddText(ok, "確定", 16f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Title);
            HudKit.Stretch(ot.rectTransform);
        }

        static Button Step(RectTransform parent, string name, string glyph, float x, float y, UnityEngine.Events.UnityAction onClick, out Image img)
        {
            var theme = UITheme.Current;
            var b = HudKit.NewRect(name, parent);
            UILayout.Place(b, x, y, 32f, 28f);
            img = HudKit.AddImage(b, theme.ink, raycast: true);
            b.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 6f);
            var btn = b.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None; btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            b.gameObject.AddComponent<KitBrighten>().image = img;
            var t = HudKit.AddText(b, glyph, 16f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Display);
            HudKit.Stretch(t.rectTransform);
            return btn;
        }

        static RectTransform Bar(RectTransform parent, float x, float y, float w, float h, Color c)
        {
            var track = HudKit.NewRect("BarTrack", parent);
            UILayout.Place(track, x, y, w, h);
            HudKit.AddImage(track, new Color(0f, 0f, 0f, .12f));
            var fill = HudKit.NewRect("BarFill", track);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.offsetMin = fill.offsetMax = Vector2.zero;
            HudKit.AddImage(fill, c);
            return fill;
        }

        // ---- open / close ------------------------------------------------------------------------------------------------------
        /// <summary>Opens the panel on `operatorData` (its growth from the session's save, or level 1 without one).</summary>
        public void Open(OperatorData operatorData) => Open(operatorData, SaveSystem.Current);

        /// <summary>Previews: on `operatorData` with `data` (a sample save).</summary>
        public void Open(OperatorData operatorData, SaveData data)
        {
            op = operatorData;
            var d = data ?? SaveData.Fresh();
            prog = d.OperatorOf(op != null ? op.key : "");
            var gun = op != null ? Loadout.Of(op, true) : null;
            wprog = d.WeaponOf(gun != null ? gun.key : "");
            for (int i = 0; i < 5; i++) add[i] = 0;
            editable = data != null;

            var theme = UITheme.Current;
            face.sprite = op != null ? OperatorArt.Face(op.key) : null;
            face.enabled = face.sprite != null;
            band.GetComponent<Image>().color = op != null ? op.color : theme.blue;
            nameText.SetText(op != null ? op.DisplayName : "");
            kanaText.SetText(op != null ? op.kr + " · " + op.role : "");
            levelText.SetText("Lv {0}", (float)prog.level);
            int need = ProgressRules.LevelNeed(prog.level);
            if (need > 0) expText.SetText("{0} / {1} EXP", (float)prog.exp, (float)need); else expText.SetText("MAX");
            expFill.anchorMax = new Vector2(need > 0 ? Mathf.Clamp01(prog.exp / (float)need) : 1f, 1f);
            weaponText.SetText(gun != null ? gun.weaponName : "—");
            masteryText.SetText("熟練度 Lv {0}", (float)wprog.level);
            int wneed = ProgressRules.MasteryNeed(wprog.level);
            masteryFill.anchorMax = new Vector2(wneed > 0 ? Mathf.Clamp01(wprog.exp / (float)wneed) : 1f, 1f);
            float wd = (Mathf.Max(1, wprog.level) - 1) * ProgressBonus.MasteryDamage * 100f, wa = (Mathf.Max(1, wprog.level) - 1) * ProgressBonus.MasteryAccuracy;
            masteryEffect.SetText("ダメージ +{0:0}%  命中 +{1:1}", wd, wa);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Refresh();
            clock = 0f;
            running = Application.isPlaying;
            Evaluate(running ? 0f : Duration);
        }

        bool editable;

        public void Close() { running = false; gameObject.SetActive(false); }

        int Left => prog.points - add[0] - add[1] - add[2] - add[3] - add[4];

        int Points(int i)
        {
            switch (i) { case 0: return prog.hp; case 1: return prog.def; case 2: return prog.atk; case 3: return prog.mob; default: return prog.prec; }
        }

        void Change(int i, int d)
        {
            if (!editable) return;
            if (d > 0 && Left <= 0) return;
            if (d < 0 && add[i] <= 0) return;
            add[i] += d;
            Refresh();
        }

        void Refresh()
        {
            var theme = UITheme.Current;
            int left = Left;
            pointsText.SetText("残りポイント  {0}", (float)left);
            pointsText.color = left > 0 ? Color.Lerp(Gold, theme.ink, .25f) : theme.dim;
            for (int i = 0; i < 5; i++)
            {
                int v = Points(i) + add[i];
                values[i].SetText("{0}", (float)v);
                values[i].color = add[i] > 0 ? theme.blue : theme.ink;
                switch (i)
                {
                    case 0: effects[i].SetText("+{0} HP", (float)(v * ProgressBonus.HpPerPoint)); break;
                    case 1: effects[i].SetText("-{0:1}%", Mathf.Min(ProgressBonus.DefMax, v * ProgressBonus.DefPerPoint) * 100f); break;
                    case 2: effects[i].SetText("+{0:0}%", v * ProgressBonus.AtkPerPoint * 100f); break;
                    case 3: effects[i].SetText("+{0:1}m", v * ProgressBonus.MovePerPoint); break;
                    default: effects[i].SetText("+{0:1}", v * ProgressBonus.AccPerPoint); break;
                }
                bool canPlus = editable && left > 0, canMinus = editable && add[i] > 0;
                plus[i].interactable = canPlus; minus[i].interactable = canMinus;
                plusImg[i].color = canPlus ? theme.ink : Color.Lerp(theme.paper2, theme.ink, .15f);
                minusImg[i].color = canMinus ? theme.ink : Color.Lerp(theme.paper2, theme.ink, .15f);
            }
            bool any = editable && (add[0] + add[1] + add[2] + add[3] + add[4]) > 0;
            confirm.interactable = any;
            confirmImg.color = any ? theme.blue : Color.Lerp(theme.paper2, theme.blue, .35f);
        }

        void Confirm()
        {
            if (!editable) return;
            int sum = add[0] + add[1] + add[2] + add[3] + add[4];
            if (sum <= 0 || sum > prog.points) return;
            prog.hp += add[0]; prog.def += add[1]; prog.atk += add[2]; prog.mob += add[3]; prog.prec += add[4];
            prog.points -= sum;
            for (int i = 0; i < 5; i++) add[i] = 0;
            SaveSystem.Write(false);          // (the SAVE notice)
            Refresh();
        }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public float Duration => Mathf.Max(DimTime, PopTime);

        public void Evaluate(float t)
        {
            var c = DimColor; c.a *= Mathf.Clamp01(t / DimTime);
            if (dim.color != c) dim.color = c;
            float p = UIEase.Tactical(Mathf.Clamp01(t / PopTime));
            float s = UIMotionSettings.reduceMotion ? 1f : Mathf.Lerp(.9f, 1f, p);
            if (pop.localScale.x != s) pop.localScale = new Vector3(s, s, 1f);
            if (popGroup.alpha != p) popGroup.alpha = p;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed) { EscapeGate.Consume(); Close(); return; }
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }
    }
}
