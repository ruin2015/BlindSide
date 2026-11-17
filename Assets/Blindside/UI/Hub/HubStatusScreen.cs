// The HQ's status screen (ESC → 隊員情報; menu canvas 1440 × 810) — replaces the squad list + growth panel as the place stat points are
// given (the old list is still SquadInfoContent, the old panel KitGrowthPanel).
//   Ground    ink; a wide band in the operator's colour leaning across; DecoWord "STATUS" 210 display, faint, along the bottom
//   Head      top left the tag 「隊員情報」 (ink2 plate 330 × 58, "STATUS" blue); beside it the switcher (Q · n / N · E); the back bar
//             top right
//   Left      the codename 60 display, a bar in the operator's colour, kana · role 18; "Lv." 30 + the level 112 display; under it,
//             smaller: NEXT · the experience still needed 18 and a bar; the unspent points (gold chip) when there are any
//   Stats     (480, 110) 470 × 440 ink2 card: "STATUS // ステータス"; one row a stat (ProgressBonus.Shown: 攻撃力, ヘッドショット率,
//             防御力, 回避力): the name 20, the short English 15, the points 34 display, − / + 30, a bar of the points (the pending
//             ones lighter) and what they give 15. Under it 「決定」 (blue) and 「リセット」 — the points are given on 決定
//   Right     the 3D figure (HubStatusStage: the left arm raised) in a 420 × 660 frame, its HP and 機動力 over the frame's lower left
// Q / E (← →) step through the squad, ESC (or 戻る) closes. Entrance (ITimedEffect.Evaluate(t), unscaled): the screen fades in (.2), the
// left column and the card slide in from the left, the figure's frame from the right (.35). Texts change only on a change.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.Hub;
using Blindside.Progress;
using Blindside.Save;

namespace Blindside.UI
{
    public class HubStatusScreen : MonoBehaviour, ITimedEffect
    {
        public const float W = 1440f, H = 810f;
        public const float LeftX = 60f, CardX = 480f, CardY = 110f, CardW = 470f, RowY = 58f, RowStep = 92f, ViewX = 980f, ViewY = 96f, ViewW = 420f, ViewH = 660f;
        public const float FadeIn = .2f, SideIn = .35f, BarMax = 30f;

        HubScreen screen;
        HubStatusStage stage;
        CanvasGroup group;
        RectTransform left, card, view;
        Vector2 leftHome, cardHome, viewHome;
        Image band, nameBar, expFill;
        RawImage figure;
        TMP_Text codename, kana, level, nextText, pointsText, switcher, hpText, mobText;
        RectTransform pointsChip;
        Button confirm, reset;
        Image confirmImg;
        readonly TMP_Text[] values = new TMP_Text[4], effects = new TMP_Text[4];
        readonly RectTransform[] fills = new RectTransform[4], pending = new RectTransform[4];
        readonly Button[] plus = new Button[4], minus = new Button[4];
        readonly int[] add = new int[5];
        readonly List<OperatorData> ops = new List<OperatorData>(8);
        int sel;
        float clock = 99f;

        public bool IsOpen => gameObject.activeSelf;
        SaveData D => screen.Data;
        bool Editable => screen.Editable;
        OperatorProgress P => D.OperatorOf(ops[sel].key);

        // ---- build ----------------------------------------------------------------------------------------------------------------
        public static HubStatusScreen Build(Transform parent, HubScreen screen)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect("HubStatus", parent);
            HudKit.Stretch(root);
            var s = root.gameObject.AddComponent<HubStatusScreen>();
            s.screen = screen;
            s.group = root.gameObject.AddComponent<CanvasGroup>();
            HudKit.AddImage(root, theme.ink, raycast: true);

            var bandRt = HudKit.NewRect("DecoBand", root);
            UILayout.Place(bandRt, 300f, 300f, 1500f, 260f);
            bandRt.localRotation = Quaternion.Euler(0f, 0f, 20f);
            s.band = HudKit.AddImage(bandRt, theme.blue);
            var word = HudKit.AddText(root, "STATUS", 210f, new Color(1f, 1f, 1f, .06f), true, TextAlignmentOptions.BottomLeft, "DecoWord", UITheme.TextRole.Display);
            word.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(word.rectTransform, 20f, H - 230f, 1100f, 230f);

            s.BuildHead(root, theme);
            s.BuildLeft(root, theme);
            s.BuildCard(root, theme);
            s.BuildView(root, theme);
            root.gameObject.SetActive(false);
            return s;
        }

        void BuildHead(RectTransform root, UITheme theme)
        {
            var plate = HudKit.NewRect("Tag", root);
            UILayout.Place(plate, 0f, 24f, 330f, 58f);
            HudKit.AddImage(plate, theme.ink2);
            plate.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 18f);
            var t = HudKit.AddText(plate, "隊員情報", 24f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Name", UITheme.TextRole.Title);
            UILayout.Place(t.rectTransform, 40f, 0f, 160f, 58f);
            var e = HudKit.AddText(plate, "STATUS", 15f, theme.blue, true, TextAlignmentOptions.MidlineRight, "En", UITheme.TextRole.Display);
            e.characterSpacing = 10f;
            UILayout.Place(e.rectTransform, 190f, 0f, 116f, 58f);

            var sw = HudKit.NewRect("Switcher", root);
            UILayout.Place(sw, 360f, 30f, 240f, 46f);
            HudKit.AddImage(sw, new Color(theme.ink2.r, theme.ink2.g, theme.ink2.b, .9f));
            sw.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 12f);
            var q = HudKit.AddText(sw, "Q", 18f, theme.blue, true, TextAlignmentOptions.Center, "Q", UITheme.TextRole.Display);
            UILayout.Place(q.rectTransform, 12f, 0f, 30f, 46f);
            var ee = HudKit.AddText(sw, "E", 18f, theme.blue, true, TextAlignmentOptions.Center, "E", UITheme.TextRole.Display);
            UILayout.Place(ee.rectTransform, 196f, 0f, 30f, 46f);
            switcher = HudKit.AddText(sw, "", 18f, Color.white, true, TextAlignmentOptions.Center, "Count", UITheme.TextRole.Display);
            switcher.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(switcher.rectTransform, 46f, 0f, 146f, 46f);
            AddClick(q.rectTransform, () => Step(-1));
            AddClick(ee.rectTransform, () => Step(1));

            var back = HubTiles.Back(root, Close);
            UILayout.Place(back, W - 40f - 176f, 30f, 176f, 46f);
        }

        void BuildLeft(RectTransform root, UITheme theme)
        {
            left = HudKit.NewRect("Left", root);
            UILayout.Place(left, 0f, 0f, 460f, H);
            leftHome = left.anchoredPosition;
            codename = Text(left, "", 60f, Color.white, TextAlignmentOptions.BottomLeft, "Codename", UITheme.TextRole.Display, LeftX, 104f, 400f, 76f);
            var bar = HudKit.NewRect("DecoNameBar", left);
            UILayout.Place(bar, LeftX, 184f, 300f, 6f);
            nameBar = HudKit.AddImage(bar, theme.blue);
            kana = Text(left, "", 18f, Color.Lerp(theme.dim, Color.white, .55f), TextAlignmentOptions.MidlineLeft, "Kana", UITheme.TextRole.Body, LeftX, 196f, 400f, 28f);
            Text(left, "Lv.", 30f, theme.acc, TextAlignmentOptions.BottomLeft, "LvLabel", UITheme.TextRole.Display, LeftX, 262f, 70f, 50f);
            level = Text(left, "", 112f, Color.white, TextAlignmentOptions.BottomLeft, "Level", UITheme.TextRole.Display, LeftX + 66f, 216f, 260f, 120f);
            nextText = Text(left, "", 18f, Color.Lerp(theme.dim, Color.white, .6f), TextAlignmentOptions.MidlineLeft, "Next", UITheme.TextRole.Display, LeftX, 342f, 340f, 26f);
            var track = HudKit.NewRect("ExpTrack", left);
            UILayout.Place(track, LeftX, 372f, 340f, 6f);
            HudKit.AddImage(track, new Color(1f, 1f, 1f, .15f));
            var fill = HudKit.NewRect("ExpFill", track);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.pivot = new Vector2(0f, .5f); fill.offsetMin = fill.offsetMax = Vector2.zero;
            expFill = HudKit.AddImage(fill, theme.acc);
            pointsChip = HudKit.NewRect("PointsChip", left);
            UILayout.Place(pointsChip, LeftX, 396f, 300f, 36f);
            HudKit.AddImage(pointsChip, theme.acc);
            pointsChip.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
            pointsText = Text(pointsChip, "", 18f, theme.ink, TextAlignmentOptions.MidlineLeft, "Points", UITheme.TextRole.Body, 14f, 0f, 270f, 36f);
            HubTiles.Help(left, LeftX, H - 160f, false, "Q E", "隊員の切替", "ESC", "戻る");
        }

        void BuildCard(RectTransform root, UITheme theme)
        {
            var shown = ProgressBonus.Shown;
            card = HudKit.NewRect("Card", root);
            UILayout.Place(card, CardX, CardY, CardW, RowY + RowStep * shown.Length + 20f);
            cardHome = card.anchoredPosition;
            HudKit.AddImage(card, new Color(theme.ink2.r, theme.ink2.g, theme.ink2.b, .95f));
            card.gameObject.AddComponent<CutCornerGraphic>().Set(24f, CutCornerGraphic.Corners.TopRight);
            var chip = HudKit.NewRect("Chip", card);
            UILayout.Place(chip, 18f, 14f, 250f, 28f);
            HudKit.AddImage(chip, theme.acc);
            chip.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
            Text(chip, "STATUS // ステータス", 15f, theme.ink, TextAlignmentOptions.MidlineLeft, "Label", UITheme.TextRole.Body, 10f, 0f, 235f, 28f);
            for (int r = 0; r < shown.Length; r++)
            {
                int stat = shown[r], row = r;
                float y = RowY + r * RowStep;
                Text(card, ProgressBonus.StatNames[stat], 20f, Color.white, TextAlignmentOptions.MidlineLeft, "StatName" + r, UITheme.TextRole.Title, 24f, y, 190f, 34f);
                Text(card, ProgressBonus.StatEn[stat], 15f, Color.Lerp(theme.dim, Color.white, .35f), TextAlignmentOptions.MidlineLeft, "StatEn" + r, UITheme.TextRole.Display, 216f, y, 60f, 34f);
                values[r] = Text(card, "", 34f, Color.white, TextAlignmentOptions.MidlineRight, "Value" + r, UITheme.TextRole.Display, 276f, y - 4f, 100f, 42f);
                minus[r] = SmallButton(card, "−", 386f, y + 2f, () => Change(row, -1), "Minus" + r);
                plus[r] = SmallButton(card, "+", 424f, y + 2f, () => Change(row, 1), "Plus" + r);
                var track = HudKit.NewRect("Track" + r, card);
                UILayout.Place(track, 24f, y + 40f, 430f, 10f);
                HudKit.AddImage(track, new Color(1f, 1f, 1f, .1f));
                var pend = HudKit.NewRect("Pending", track);
                pend.anchorMin = Vector2.zero; pend.anchorMax = new Vector2(0f, 1f); pend.pivot = new Vector2(0f, .5f); pend.offsetMin = pend.offsetMax = Vector2.zero;
                HudKit.AddImage(pend, new Color(theme.blue.r, theme.blue.g, theme.blue.b, .45f));
                pending[r] = pend;
                var fill = HudKit.NewRect("Fill", track);
                fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.pivot = new Vector2(0f, .5f); fill.offsetMin = fill.offsetMax = Vector2.zero;
                HudKit.AddImage(fill, theme.blue);
                fills[r] = fill;
                effects[r] = Text(card, "", 15f, Color.Lerp(theme.dim, Color.white, .55f), TextAlignmentOptions.MidlineLeft, "Effect" + r, UITheme.TextRole.Body, 24f, y + 54f, 430f, 24f);
            }
            float by = CardY + RowY + RowStep * shown.Length + 34f;
            confirm = WideButton(root, "Confirm", "決定", CardX, by, 228f, theme.blue, Confirm, out confirmImg);
            reset = WideButton(root, "Reset", "リセット", CardX + 242f, by, 228f, theme.ink3, ResetAdd, out _);
        }

        void BuildView(RectTransform root, UITheme theme)
        {
            view = HudKit.NewRect("View", root);
            UILayout.Place(view, ViewX, ViewY, ViewW, ViewH);
            viewHome = view.anchoredPosition;
            var frame = HudKit.NewRect("DecoFrame", view);
            HudKit.Stretch(frame);
            HudKit.AddImage(frame, new Color(1f, 1f, 1f, .08f));
            var img = HudKit.NewRect("Figure", view);
            UILayout.Place(img, 2f, 2f, ViewW - 4f, ViewH - 4f);
            figure = img.gameObject.AddComponent<RawImage>();
            figure.raycastTarget = false;
            var tag = Text(view, "PARAMETER", 15f, theme.blue, TextAlignmentOptions.MidlineLeft, "Tag", UITheme.TextRole.Display, 16f, 10f, 200f, 24f);
            tag.characterSpacing = 8f;
            // HP and 機動力 over the frame's lower left
            Text(view, "HP", 18f, theme.blue, TextAlignmentOptions.BottomLeft, "HpLabel", UITheme.TextRole.Display, 18f, ViewH - 168f, 60f, 30f);
            hpText = Text(view, "", 46f, Color.white, TextAlignmentOptions.BottomLeft, "Hp", UITheme.TextRole.Display, 18f, ViewH - 142f, 190f, 56f);
            Text(view, "機動力", 18f, theme.acc, TextAlignmentOptions.BottomLeft, "MobLabel", UITheme.TextRole.Body, 18f, ViewH - 84f, 80f, 30f);
            mobText = Text(view, "", 40f, Color.white, TextAlignmentOptions.BottomLeft, "Mob", UITheme.TextRole.Display, 18f, ViewH - 58f, 190f, 50f);
        }

        static TMP_Text Text(Transform parent, string s, float size, Color c, TextAlignmentOptions align, string name, UITheme.TextRole role,
                             float x, float y, float w, float h)
        {
            var t = HudKit.AddText(parent, s, size, c, true, align, name, role);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(t.rectTransform, x, y, w, h);
            return t;
        }

        static void AddClick(RectTransform rt, UnityEngine.Events.UnityAction act)
        {
            var img = rt.gameObject.GetComponent<Graphic>();
            if (img != null) img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { Sfx.Ui(); act(); });
        }

        static Button SmallButton(RectTransform parent, string glyph, float x, float y, UnityEngine.Events.UnityAction act, string name)
        {
            var theme = UITheme.Current;
            var rt = HudKit.NewRect(name, parent);
            UILayout.Place(rt, x, y, 30f, 30f);
            var img = HudKit.AddImage(rt, theme.paper, raycast: true);
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            b.onClick.AddListener(() => { Sfx.Ui(); act(); });
            var t = HudKit.AddText(rt, glyph, 20f, theme.ink, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Title);
            HudKit.Stretch(t.rectTransform);
            return b;
        }

        static Button WideButton(RectTransform parent, string name, string label, float x, float y, float w, Color c, UnityEngine.Events.UnityAction act, out Image img)
        {
            var rt = HudKit.NewRect(name, parent);
            UILayout.Place(rt, x, y, w, 50f);
            img = HudKit.AddImage(rt, c, raycast: true);
            rt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 12f);
            var b = rt.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = img;
            b.onClick.AddListener(() => { Sfx.Ui(); act(); });
            var t = HudKit.AddText(rt, label, 20f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Title);
            HudKit.Stretch(t.rectTransform);
            return b;
        }

        // ---- open / close ----------------------------------------------------------------------------------------------------------
        public void Open(int index = 0)
        {
            ops.Clear();
            foreach (var o in HubCatalog.Squad) if (o != null) ops.Add(o);
            if (ops.Count == 0) return;
            sel = Mathf.Clamp(index, 0, ops.Count - 1);
            if (stage == null) stage = HubStatusStage.Build();
            figure.texture = stage.Texture;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ResetAdd();
            Fill();
            stage.SetLive(Application.isPlaying);
            clock = 0f;
            Evaluate(Application.isPlaying ? 0f : Duration);
        }

        public void Open(OperatorData op)
        {
            int i = 0;
            var squad = HubCatalog.Squad;
            for (int k = 0; k < squad.Count; k++) if (squad[k] != null && op != null && squad[k].key == op.key) { i = k; break; }
            Open(i);
        }

        public void Close()
        {
            if (stage != null) stage.SetLive(false);
            gameObject.SetActive(false);
        }

        void OnDestroy() { if (stage != null) Kill(stage.gameObject); }

        static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        void Step(int dir)
        {
            if (ops.Count == 0) return;
            sel = (sel + dir + ops.Count) % ops.Count;
            ResetAdd();
            Fill();
            clock = FadeIn;   // (slide in again, no fade)
        }

        // ---- fill -------------------------------------------------------------------------------------------------------------------
        void Fill()
        {
            var theme = UITheme.Current;
            var op = ops[sel];
            var p = P;
            band.color = new Color(op.color.r, op.color.g, op.color.b, .5f);
            nameBar.color = op.color;
            codename.SetText(op.DisplayName);
            kana.SetText((op.kr ?? "") + "  ·  " + (op.role ?? ""));
            level.SetText("{0}", (float)p.level);
            int need = ProgressRules.LevelNeed(p.level);
            if (need > 0)
            {
                nextText.SetText("NEXT  {0} EXP", (float)Mathf.Max(0, need - p.exp));
                expFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(p.exp / (float)need), 1f);
            }
            else { nextText.SetText("NEXT  MAX"); expFill.rectTransform.anchorMax = Vector2.one; }
            switcher.SetText("{0} / {1}", (float)(sel + 1), (float)ops.Count);

            // HP (the operator's own, the meal's on top) and 機動力 (the gun's move range with the fitted parts)
            int hp = op.hp + (D.mealBuff ? Campaign.MealHp : 0);
            hpText.SetText("{0}", (float)hp);
            var gun = Loadout.Of(op, true);
            float mob = WeaponData.MoveRangeOf(op.mv, gun);
            var fit = gun != null ? p.FindFit(gun.key) : null;
            var gear = fit != null ? Attachments.Sum(fit.parts) : null;
            if (gear != null && gear.mobilityPct != 0f) mob *= 1f + gear.mobilityPct / 100f;
            mobText.SetText("{0:1}m", mob);

            if (stage != null)
            {
                stage.Show(op);
                if (!Application.isPlaying) stage.RenderNow();
            }
            Refresh();
        }

        int Left => P.points - add[0] - add[1] - add[2] - add[3] - add[4];

        void Change(int row, int d)
        {
            if (!Editable) return;
            int stat = ProgressBonus.Shown[row];
            if (d > 0 && Left <= 0) return;
            if (d < 0 && add[stat] <= 0) return;
            add[stat] += d;
            Refresh();
        }

        void ResetAdd() { for (int i = 0; i < add.Length; i++) add[i] = 0; if (gameObject.activeSelf && ops.Count > 0) Refresh(); }

        void Confirm()
        {
            if (!Editable) return;
            var p = P;
            int sum = add[0] + add[1] + add[2] + add[3] + add[4];
            if (sum <= 0 || sum > p.points) return;
            for (int i = 0; i < add.Length; i++) if (add[i] > 0) p.AddStat(i, add[i]);
            p.points -= sum;
            for (int i = 0; i < add.Length; i++) add[i] = 0;
            if (SaveSystem.Current != null) SaveSystem.Write(false);
            Refresh();
        }

        void Refresh()
        {
            var theme = UITheme.Current;
            var p = P;
            var shown = ProgressBonus.Shown;
            int leftPts = Left;
            pointsChip.gameObject.SetActive(p.points > 0);
            pointsText.SetText("未分配ポイント  {0}", (float)leftPts);
            bool any = false;
            for (int r = 0; r < shown.Length; r++)
            {
                int stat = shown[r];
                int now = p.StatPoints(stat), v = now + add[stat];
                any |= add[stat] > 0;
                values[r].SetText("{0}", (float)v);
                values[r].color = add[stat] > 0 ? theme.blue : Color.white;
                fills[r].anchorMax = new Vector2(Mathf.Clamp01(now / BarMax), 1f);
                pending[r].anchorMax = new Vector2(Mathf.Clamp01(v / BarMax), 1f);
                effects[r].SetText(ProgressBonus.Effect(stat, v));
                plus[r].interactable = Editable && leftPts > 0;
                minus[r].interactable = Editable && add[stat] > 0;
                plus[r].gameObject.SetActive(p.points > 0);
                minus[r].gameObject.SetActive(p.points > 0);
            }
            confirm.interactable = any;
            confirmImg.color = any ? theme.blue : Color.Lerp(theme.ink3, theme.blue, .35f);
            reset.interactable = any;
        }

        // ---- ITimedEffect ---------------------------------------------------------------------------------------------------------
        public float Duration => FadeIn + SideIn;

        public void Evaluate(float t)
        {
            float a = Mathf.Clamp01(t / FadeIn);
            if (group.alpha != a) group.alpha = a;
            float s = UIMotionSettings.reduceMotion ? 1f : UIEase.Tactical(Mathf.Clamp01((t - FadeIn * .5f) / SideIn));
            var lp = leftHome + new Vector2(-(1f - s) * 120f, 0f);
            if (left.anchoredPosition != lp) left.anchoredPosition = lp;
            var cp = cardHome + new Vector2(-(1f - s) * 200f, 0f);
            if (card.anchoredPosition != cp) card.anchoredPosition = cp;
            var vp = viewHome + new Vector2((1f - s) * 160f, 0f);
            if (view.anchoredPosition != vp) view.anchoredPosition = vp;
        }

        void Update()
        {
            if (clock < Duration + .05f) { clock += Time.unscaledDeltaTime; Evaluate(clock); }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed) { EscapeGate.Consume(); Close(); return; }
            if (kb.qKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Step(-1);
            if (kb.eKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Step(1);
        }

        /// <summary>Previews: posed settled on member `index`, `give` points pending on the first stat.</summary>
        public void Pose(int index, int give = 0)
        {
            Open(index);
            if (give > 0) { int stat = ProgressBonus.Shown[0]; add[stat] = Mathf.Min(give, P.points); Refresh(); }
            Evaluate(Duration + 1f);
        }
    }
}
