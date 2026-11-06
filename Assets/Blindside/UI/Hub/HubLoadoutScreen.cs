// IGNIS's ロードアウト (menu canvas 1440 × 810): the gun on the workbench, a part per slot, like a weapon-modding bench —
//   DecoBackdrop  ink, takes every click
//   Head          「ロードアウト」 26 title + "WEAPON LOADOUT" 11 display (blue) centred at the top; under it the gun and whose it is, 15 body
//   Left (24, 70, 280 wide)
//     隊員          the five operators: rows 50 (face 42, codename 15 display, role 11); the one shown is blue
//     武器          its guns: rows 44 (name 14, 装備中 / 所持 / 未所持 12); a click shows that gun — and carries it, when owned
//     ステータス    (from 470) the carried gun with its parts: 命中率, 近 / 中 / 遠距離, ヘッドショット, ダメージ × 回数, 射程, 機動力, SPD, 銃声 —
//                   base → with the parts (blue when better, red when worse)
//   View (320, 70, 1096 × 616)  the workbench render (HubWorkbench: the gun's own model on a blueprint, muzzle left)
//     Slots        a 96 box per slot (マズル / バレル / 光学機器 over the gun, グリップ / マガジン / ストック under it): the part's art or 「なし」,
//                  the slot name 12 above, the part's name 11 under; a leader line (1.5, blue .7) from the box to the slot's point on the
//                  model with an 8 dot there. Boxes follow their points along x, kept 108 apart
//     Picker       a click on a box: 340 wide, beside it — 「なし」 then every part of that slot with a free copy or on this gun (art 40,
//                  name 14, its effects 11, 残り N / 装着中); a click fits it (one copy per gun: SaveData.AttachmentsFree) and closes
//   Footer       hint 12 (dim) bottom left of the view, 「閉じる」 (220 × 48, ink, slanted) bottom right
// The parts' copies: a part fitted here leaves the stock for every other gun; taking it off gives it back. Without a save session it
// shows but changes nothing. ESC closes the picker, then the screen. Built once; rows / boxes are rebuilt on a click only.
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
    public class HubLoadoutScreen : MonoBehaviour
    {
        public const float LeftX = 24f, LeftW = 280f, ViewX = 320f, ViewY = 70f, ViewW = 1096f, ViewH = 616f;
        public const float BoxS = 96f, BoxGap = 108f, PickerW = 380f, PickRowH = 66f;
        // slot order on screen: over the gun, then under it
        static readonly int[] TopSlots = { 0, 4, 1 }, BottomSlots = { 2, 3, 5 };

        HubScreen screen;
        HubWorkbench bench;
        RectTransform root, left, statsRoot, view, slotsRoot, picker;
        RawImage viewImage;
        TMP_Text gunLine, statusLine;
        readonly List<GameObject> leftRows = new List<GameObject>(16), statRows = new List<GameObject>(16), slotObjs = new List<GameObject>(24);
        int opIndex, gunIndex = -1, pickSlot = -1;

        public bool IsOpen => gameObject.activeSelf;
        SaveData D => screen.Data;
        bool Editable => screen.Editable;
        static List<OperatorData> Squad => HubCatalog.Squad;

        public static HubLoadoutScreen Build(Transform parent, HubScreen screen)
        {
            var theme = UITheme.Current;
            var r = HudKit.NewRect("HubLoadout", parent);
            HudKit.Stretch(r);
            var s = r.gameObject.AddComponent<HubLoadoutScreen>();
            s.screen = screen; s.root = r;
            var bg = HudKit.NewRect("DecoBackdrop", r);
            HudKit.Stretch(bg);
            HudKit.AddImage(bg, new Color(.03f, .035f, .045f, 1f), raycast: true);

            var title = HudKit.AddText(r, "ロードアウト", 26f, Color.white, true, TextAlignmentOptions.Center, "Title", UITheme.TextRole.Title);
            Top(title.rectTransform, 10f, 34f);
            var en = HudKit.AddText(r, "WEAPON LOADOUT", 11f, theme.blue, true, TextAlignmentOptions.Center, "En", UITheme.TextRole.Display);
            en.characterSpacing = 12f;
            Top(en.rectTransform, 44f, 14f);

            s.left = HudKit.NewRect("Left", r);
            UILayout.Place(s.left, LeftX, ViewY, LeftW, 420f);
            s.statsRoot = HudKit.NewRect("Stats", r);
            UILayout.Place(s.statsRoot, LeftX, 496f, LeftW, 290f);
            HudKit.AddImage(s.statsRoot, new Color(theme.ink2.r, theme.ink2.g, theme.ink2.b, .9f));

            s.view = HudKit.NewRect("View", r);
            UILayout.Place(s.view, ViewX, ViewY, ViewW, ViewH);
            s.viewImage = s.view.gameObject.AddComponent<RawImage>();
            s.viewImage.color = Color.white; s.viewImage.raycastTarget = false;
            s.gunLine = HudKit.AddText(s.view, "", 15f, Color.white, true, TextAlignmentOptions.TopLeft, "Gun", UITheme.TextRole.Body);
            s.gunLine.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(s.gunLine.rectTransform, 16f, 10f, 600f, 22f);
            s.slotsRoot = HudKit.NewRect("Slots", s.view);
            HudKit.Stretch(s.slotsRoot);

            s.statusLine = HudKit.AddText(r, "", 12f, theme.dim, false, TextAlignmentOptions.MidlineLeft, "Hint", UITheme.TextRole.Body);
            s.statusLine.textWrappingMode = TextWrappingModes.NoWrap; s.statusLine.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(s.statusLine.rectTransform, ViewX, ViewY + ViewH + 14f, ViewW - 240f, 48f);

            var close = HudKit.NewRect("Close", r);
            UILayout.Place(close, ViewX + ViewW - 220f, ViewY + ViewH + 14f, 220f, 48f);
            var ci = HudKit.AddImage(close, theme.ink, raycast: true);
            close.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 16f);
            var cb = close.gameObject.AddComponent<Button>();
            cb.transition = Selectable.Transition.None; cb.targetGraphic = ci;
            cb.onClick.AddListener(s.Close);
            close.gameObject.AddComponent<KitBrighten>().image = ci;
            var ct = HudKit.AddText(close, "閉じる", 16f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Title);
            HudKit.Stretch(ct.rectTransform);

            r.gameObject.SetActive(false);
            return s;
        }

        static void Top(RectTransform rt, float y, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y); rt.sizeDelta = new Vector2(0f, h);
        }

        // ---- open / close ------------------------------------------------------------------------------------------------------
        public void Open(int operatorIndex = 0)
        {
            if (bench == null) bench = HubWorkbench.Build();
            viewImage.texture = bench.Texture;
            bench.SetLive(Application.isPlaying);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            opIndex = Mathf.Clamp(operatorIndex, 0, Mathf.Max(0, Squad.Count - 1));
            gunIndex = -1;
            SelectOperator(opIndex);
        }

        public void Close()
        {
            if (pickSlot >= 0) { ClosePicker(); return; }
            if (bench != null) bench.SetLive(false);
            gameObject.SetActive(false);
        }

        void OnDestroy() { if (bench != null) HudKit.DestroySafe(bench.gameObject); }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed) { EscapeGate.Consume(); Close(); }
        }

        /// <summary>Previews: render the bench now and open the picker on `slot` (−1: none).</summary>
        public void Pose(int operatorIndex, int slot)
        {
            Open(operatorIndex);
            bench.RenderNow();
            if (slot >= 0) OpenPicker(slot);
        }

        // ---- selection ---------------------------------------------------------------------------------------------------------
        OperatorData Op => opIndex < Squad.Count ? Squad[opIndex] : null;

        void SelectOperator(int i)
        {
            opIndex = i;
            var op = Op;
            gunIndex = op != null ? Loadout.Choice(op) : -1;
            RefreshAll();
        }

        void SelectGun(int i)
        {
            var op = Op;
            if (op == null) return;
            var guns = HubCatalog.GunsOf(op);
            if (i < 0 || i >= guns.Length || guns[i] == null) return;
            gunIndex = i;
            if (HubCatalog.Owns(D, guns[i]) && (Editable || !Application.isPlaying)) Loadout.SetChoice(op, i);   // the shown gun is carried
            RefreshAll();
        }

        WeaponData Gun
        {
            get
            {
                var op = Op;
                if (op == null) return null;
                var guns = HubCatalog.GunsOf(op);
                return gunIndex >= 0 && gunIndex < guns.Length ? guns[gunIndex] : null;
            }
        }

        bool GunOwned => Gun != null && HubCatalog.Owns(D, Gun);

        void RefreshAll()
        {
            ClosePicker();
            var op = Op; var gun = Gun;
            bench.Show(gun);
            if (!Application.isPlaying) bench.RenderNow();
            gunLine.SetText(gun != null ? gun.weaponName + "   <size=12><color=#9AA0A8>" + (op != null ? op.DisplayName : "") + "</color></size>" : "—");
            BuildLeft();
            BuildStats();
            BuildSlots();
            statusLine.SetText(!Editable ? "セーブデータがありません (変更はできません)"
                : !GunOwned ? "この武器はまだ所持していません (装備購入で購入できます)"
                : "スロットをクリックしてパーツを選択 · パーツ1つは1丁の武器にのみ装着できます · ESC 閉じる");
        }

        // ---- left column -------------------------------------------------------------------------------------------------------
        void BuildLeft()
        {
            foreach (var g in leftRows) HudKit.DestroySafe(g);
            leftRows.Clear();
            var theme = UITheme.Current;
            float y = 0f;
            Tag(left, "隊員", y); y += 22f;
            for (int i = 0; i < Squad.Count; i++)
            {
                var op = Squad[i];
                int idx = i;
                bool on = i == opIndex;
                var row = Row(left, "Op" + i, y, 50f, on, () => SelectOperator(idx));
                var f = HudKit.NewRect("Face", row);
                UILayout.Place(f, 6f, 4f, 42f, 42f);
                HudKit.AddImage(f, Color.Lerp(theme.paper2, theme.ink, .1f));
                f.gameObject.AddComponent<RectMask2D>();
                var ph = HudKit.NewRect("Photo", f); HudKit.Stretch(ph);
                var im = ph.gameObject.AddComponent<Image>(); im.sprite = OperatorArt.Face(op.key); im.preserveAspect = true; im.raycastTarget = false;
                im.enabled = im.sprite != null;
                var n = HudKit.AddText(row, op.DisplayName, 15f, on ? Color.white : theme.paper, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Display);
                n.textWrappingMode = TextWrappingModes.NoWrap;
                UILayout.Place(n.rectTransform, 58f, 6f, LeftW - 66f, 20f);
                var rl = HudKit.AddText(row, op.role, 11f, on ? Color.white : theme.dim, false, TextAlignmentOptions.TopLeft, "Role", UITheme.TextRole.Body);
                rl.textWrappingMode = TextWrappingModes.NoWrap; rl.overflowMode = TextOverflowModes.Ellipsis;
                UILayout.Place(rl.rectTransform, 58f, 28f, LeftW - 66f, 16f);
                y += 54f;
            }
            y += 6f;
            Tag(left, "武器", y); y += 22f;
            var cur = Op;
            if (cur == null) return;
            var guns = HubCatalog.GunsOf(cur);
            int carried = Loadout.Choice(cur);
            for (int i = 0; i < guns.Length; i++)
            {
                var w = guns[i];
                if (w == null) continue;
                int idx = i;
                bool on = i == gunIndex, owned = HubCatalog.Owns(D, w);
                var row = Row(left, "Gun" + i, y, 44f, on, () => SelectGun(idx));
                var n = HudKit.AddText(row, w.weaponName, 14f, on ? Color.white : (owned ? theme.paper : theme.dim), true, TextAlignmentOptions.MidlineLeft, "Name", UITheme.TextRole.Body);
                n.textWrappingMode = TextWrappingModes.NoWrap; n.overflowMode = TextOverflowModes.Ellipsis;
                UILayout.Place(n.rectTransform, 12f, 0f, LeftW - 100f, 44f);
                var v = HudKit.AddText(row, i == carried && owned ? "装備中" : owned ? "所持" : "未所持", 12f, on ? Color.white : theme.dim, true, TextAlignmentOptions.MidlineRight, "State", UITheme.TextRole.Body);
                UILayout.Place(v.rectTransform, LeftW - 90f, 0f, 80f, 44f);
                y += 48f;
            }
        }

        void Tag(RectTransform parent, string text, float y)
        {
            var t = HudKit.AddText(parent, text, 12f, UITheme.Current.blue, true, TextAlignmentOptions.BottomLeft, "Tag_" + text, UITheme.TextRole.Body);
            UILayout.Place(t.rectTransform, 2f, y, LeftW, 18f);
            leftRows.Add(t.gameObject);
        }

        RectTransform Row(RectTransform parent, string name, float y, float h, bool on, UnityEngine.Events.UnityAction click)
        {
            var theme = UITheme.Current;
            var row = HudKit.NewRect(name, parent);
            UILayout.Place(row, 0f, y, LeftW, h);
            var img = HudKit.AddImage(row, on ? theme.blue : new Color(theme.ink2.r, theme.ink2.g, theme.ink2.b, .92f), raycast: true);
            row.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
            var b = row.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None; b.targetGraphic = img;
            b.onClick.AddListener(click);
            if (!on) row.gameObject.AddComponent<KitBrighten>().image = img;
            leftRows.Add(row.gameObject);
            return row;
        }

        // ---- stats -------------------------------------------------------------------------------------------------------------
        void BuildStats()
        {
            foreach (var o in statRows) HudKit.DestroySafe(o);
            statRows.Clear();
            var theme = UITheme.Current;
            var w = Gun;
            var head = HudKit.AddText(statsRoot, "ステータス", 12f, theme.blue, true, TextAlignmentOptions.TopLeft, "Head", UITheme.TextRole.Body);
            UILayout.Place(head.rectTransform, 14f, 10f, 200f, 18f);
            statRows.Add(head.gameObject);
            if (w == null) return;
            var fit = Op != null ? D.OperatorOf(Op.key).FindFit(w.key) : null;
            var g = fit != null ? Attachments.Sum(fit.parts) : null;
            float acc = g != null ? g.acc : 0f;
            int shots = w.shots + (g != null ? g.extraShots : 0);
            float dmg = w.damage * (g != null ? 1f + g.damagePct / 100f : 1f);
            float y = 34f;
            Stat("命中率", w.accuracy + "%", (w.accuracy + acc).ToString("0.#") + "%", acc, ref y);
            Stat("近距離命中", "±0", Signed(g != null ? g.accNear : 0f) + "%", g != null ? g.accNear : 0f, ref y);
            Stat("中距離命中", "±0", Signed(g != null ? g.accMid : 0f) + "%", g != null ? g.accMid : 0f, ref y);
            Stat("遠距離命中", "±0", Signed(g != null ? g.accFar : 0f) + "%", g != null ? g.accFar : 0f, ref y);
            Stat("ヘッドショット", "±0", Signed(g != null ? g.headshot : 0f) + "%", g != null ? g.headshot : 0f, ref y);
            Stat("ダメージ", w.damage + "×" + w.shots, Mathf.RoundToInt(dmg) + "×" + shots, (dmg * shots) - (w.damage * w.shots), ref y);
            Stat("距離減衰", w.damageFalloffPerTile > 0f ? "あり" : "なし", g != null && g.noFalloff ? "なし" : (w.damageFalloffPerTile > 0f ? "あり" : "なし"), g != null && g.noFalloff && w.damageFalloffPerTile > 0f ? 1f : 0f, ref y);
            Stat("機動力", "±0", Signed(g != null ? g.mobilityPct : 0f) + "%", g != null ? g.mobilityPct : 0f, ref y);
            Stat("連射 (SPD)", "±0", Signed(g != null ? g.speed : 0f), g != null ? g.speed : 0f, ref y);
            Stat("銃声", "表示", g != null && g.silenced ? "非表示" : "表示", g != null && g.silenced ? 1f : 0f, ref y);
        }

        static string Signed(float v) => v > 0 ? "+" + v.ToString("0.#") : v < 0 ? "−" + (-v).ToString("0.#") : "±0";

        void Stat(string label, string from, string to, float delta, ref float y)
        {
            var theme = UITheme.Current;
            var l = HudKit.AddText(statsRoot, label, 12f, theme.paper, false, TextAlignmentOptions.MidlineLeft, "Label", UITheme.TextRole.Body);
            l.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(l.rectTransform, 14f, y, 110f, 24f);
            var a = HudKit.AddText(statsRoot, from, 12f, theme.dim, true, TextAlignmentOptions.MidlineRight, "From", UITheme.TextRole.Body);
            a.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(a.rectTransform, 120f, y, 64f, 24f);
            var b = HudKit.AddText(statsRoot, to, 13f, delta > 0f ? theme.blue : delta < 0f ? theme.foe : Color.white, true, TextAlignmentOptions.MidlineRight, "To", UITheme.TextRole.Body);
            b.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(b.rectTransform, 190f, y, 76f, 24f);
            statRows.Add(l.gameObject); statRows.Add(a.gameObject); statRows.Add(b.gameObject);
            y += 25f;
        }

        // ---- slots -------------------------------------------------------------------------------------------------------------
        void BuildSlots()
        {
            foreach (var g in slotObjs) HudKit.DestroySafe(g);
            slotObjs.Clear();
            var w = Gun;
            if (w == null) return;
            var fit = Op != null ? D.OperatorOf(Op.key).FindFit(w.key) : null;
            Row(TopSlots, true, fit);
            Row(BottomSlots, false, fit);
        }

        void Row(int[] slots, bool top, WeaponFit fit)
        {
            // each box follows its point along x, the row kept BoxGap apart and inside the view
            var xs = new float[slots.Length]; var pts = new Vector2[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var v = bench.ViewportOf(slots[i]);
                pts[i] = new Vector2(v.x * ViewW, (1f - v.y) * ViewH);   // view space, y down
                xs[i] = pts[i].x;
            }
            var order = new int[slots.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            System.Array.Sort(order, (a, b) => xs[a].CompareTo(xs[b]));
            float lo = 20f + BoxS * .5f, hi = ViewW - 20f - BoxS * .5f;
            float prev = float.MinValue;
            foreach (int k in order) { xs[k] = Mathf.Clamp(Mathf.Max(xs[k], prev + BoxGap), lo, hi); prev = xs[k]; }
            for (int i = order.Length - 2; i >= 0; i--) xs[order[i]] = Mathf.Min(xs[order[i]], xs[order[i + 1]] - BoxGap);
            float boxY = top ? 64f : ViewH - 64f - BoxS;
            for (int i = 0; i < slots.Length; i++)
                Box(slots[i], new Vector2(xs[i] - BoxS * .5f, boxY), pts[i], top, fit);
        }

        void Box(int slot, Vector2 at, Vector2 point, bool top, WeaponFit fit)
        {
            var theme = UITheme.Current;
            var part = fit != null ? Attachments.Of(fit.parts[slot]) : null;
            // the leader line: from the box's edge to the slot's point, and a dot there
            var from = new Vector2(at.x + BoxS * .5f, top ? at.y + BoxS + 16f : at.y - 18f);
            var line = HudKit.NewRect("DecoLine" + slot, slotsRoot);
            line.anchorMin = line.anchorMax = new Vector2(0f, 1f); line.pivot = new Vector2(0f, .5f);
            var d = point - from;
            line.anchoredPosition = new Vector2(from.x, -from.y);
            line.sizeDelta = new Vector2(d.magnitude, 1.5f);
            line.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            HudKit.AddImage(line, new Color(theme.blue.r, theme.blue.g, theme.blue.b, .7f));
            slotObjs.Add(line.gameObject);
            var dot = HudKit.NewRect("DecoDot" + slot, slotsRoot);
            dot.anchorMin = dot.anchorMax = new Vector2(0f, 1f); dot.pivot = new Vector2(.5f, .5f);
            dot.anchoredPosition = new Vector2(point.x, -point.y); dot.sizeDelta = new Vector2(8f, 8f);
            HudKit.AddImage(dot, part != null ? theme.acc : Color.white);
            slotObjs.Add(dot.gameObject);

            // the box
            var name = HudKit.AddText(slotsRoot, Attachments.Slots[slot], 12f, theme.paper, true, TextAlignmentOptions.BottomLeft, "SlotName" + slot, UITheme.TextRole.Body);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            Place(name.rectTransform, at.x, at.y - 18f, BoxS + 40f, 16f);
            slotObjs.Add(name.gameObject);
            var box = HudKit.NewRect("Slot" + slot, slotsRoot);
            Place(box, at.x, at.y, BoxS, BoxS);
            var img = HudKit.AddImage(box, part != null ? new Color(.82f, .85f, .88f, .95f) : new Color(.1f, .12f, .15f, .9f), raycast: true);
            var btn = box.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None; btn.targetGraphic = img;
            btn.interactable = GunOwned;
            int s = slot;
            btn.onClick.AddListener(() => OpenPicker(s));
            box.gameObject.AddComponent<KitBrighten>().image = img;
            var edge = HudKit.NewRect("DecoEdge", box);
            UILayout.Place(edge, 0f, BoxS - 3f, BoxS, 3f);
            HudKit.AddImage(edge, part != null ? theme.acc : new Color(theme.blue.r, theme.blue.g, theme.blue.b, .6f));
            if (part != null)
            {
                var a = HudKit.NewRect("Art", box);
                UILayout.Place(a, 6f, 6f, BoxS - 12f, BoxS - 12f);
                var ai = a.gameObject.AddComponent<Image>();
                ai.sprite = Attachments.ArtOf(part); ai.preserveAspect = true; ai.raycastTarget = false;
            }
            else
            {
                var n = HudKit.AddText(box, "なし", 13f, theme.dim, true, TextAlignmentOptions.Center, "None", UITheme.TextRole.Body);
                HudKit.Stretch(n.rectTransform);
            }
            slotObjs.Add(box.gameObject);
            var pn = HudKit.AddText(slotsRoot, part != null ? part.name : "", 11f, Color.white, true, TextAlignmentOptions.TopLeft, "PartName" + slot, UITheme.TextRole.Body);
            pn.textWrappingMode = TextWrappingModes.NoWrap;
            Place(pn.rectTransform, at.x, at.y + BoxS + 2f, BoxS + 60f, 14f);
            slotObjs.Add(pn.gameObject);
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }

        // ---- the part picker ---------------------------------------------------------------------------------------------------
        void OpenPicker(int slot)
        {
            ClosePicker();
            var w = Gun; var op = Op;
            if (w == null || op == null || !GunOwned) return;
            pickSlot = slot;
            var theme = UITheme.Current;
            var fit = D.OperatorOf(op.key).FindFit(w.key);
            string on = fit != null ? fit.parts[slot] : "";
            var list = new List<Attachment>(8);
            foreach (var a in Attachments.All)
                if (a.slot == slot && (a.key == on || D.AttachmentsFree(a.key) > 0)) list.Add(a);

            picker = HudKit.NewRect("Picker", slotsRoot);
            float h = 40f + PickRowH * (list.Count + 1) + (list.Count == 0 ? 24f : 0f);
            var boxRt = slotsRoot.Find("Slot" + slot) as RectTransform;
            float bx = boxRt != null ? -boxRt.anchoredPosition.y : 64f, px = boxRt != null ? boxRt.anchoredPosition.x : 20f;
            bool topRow = System.Array.IndexOf(TopSlots, slot) >= 0;
            float py = topRow ? bx + BoxS + 22f : bx - 6f - h;
            px = Mathf.Clamp(px + BoxS + 12f, 8f, ViewW - PickerW - 8f);
            py = Mathf.Clamp(py, 8f, ViewH - h - 8f);
            Place(picker, px, py, PickerW, h);
            HudKit.AddImage(picker, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .97f), raycast: true);
            picker.gameObject.AddComponent<CutCornerGraphic>().Set(14f, CutCornerGraphic.Corners.TopRight);
            var head = HudKit.AddText(picker, Attachments.Slots[slot] + "  <size=11><color=#9AA0A8>" + w.weaponName + "</color></size>", 14f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Head", UITheme.TextRole.Body);
            head.textWrappingMode = TextWrappingModes.NoWrap; head.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(head.rectTransform, 14f, 6f, PickerW - 28f, 28f);

            float y = 40f;
            PickRow(null, string.IsNullOrEmpty(on), y); y += PickRowH;
            foreach (var a in list) { PickRow(a, a.key == on, y); y += PickRowH; }
            if (list.Count == 0)
            {
                var n = HudKit.AddText(picker, "このスロットの空きパーツがありません (装備購入)", 11f, theme.dim, false, TextAlignmentOptions.MidlineLeft, "Empty", UITheme.TextRole.Body);
                n.textWrappingMode = TextWrappingModes.NoWrap;
                UILayout.Place(n.rectTransform, 14f, y, PickerW - 28f, 22f);
            }
        }

        void PickRow(Attachment a, bool on, float y)
        {
            var theme = UITheme.Current;
            var row = HudKit.NewRect(a != null ? "Pick_" + a.key : "Pick_None", picker);
            UILayout.Place(row, 8f, y, PickerW - 16f, PickRowH - 4f);
            var img = HudKit.AddImage(row, on ? Color.Lerp(theme.ink2, theme.blue, .45f) : theme.ink2, raycast: true);
            var b = row.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None; b.targetGraphic = img;
            b.interactable = Editable;
            b.onClick.AddListener(() => Pick(a));
            row.gameObject.AddComponent<KitBrighten>().image = img;
            float x = 8f;
            if (a != null)
            {
                var f = HudKit.NewRect("Art", row);
                UILayout.Place(f, 6f, 6f, 50f, 50f);
                HudKit.AddImage(f, new Color(.82f, .85f, .88f, .95f));
                var ar = HudKit.NewRect("Photo", f); HudKit.Stretch(ar);
                var ai = ar.gameObject.AddComponent<Image>(); ai.sprite = Attachments.ArtOf(a); ai.preserveAspect = true; ai.raycastTarget = false;
                x = 58f;
            }
            var n = HudKit.AddText(row, a != null ? a.name : "なし (外す)", 14f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Body);
            n.textWrappingMode = TextWrappingModes.NoWrap; n.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(n.rectTransform, x, 6f, PickerW - 16f - x - 76f, 20f);
            var v = HudKit.AddText(row, on ? "装着中" : a != null ? "残り " + D.AttachmentsFree(a.key) : "", 12f, on ? theme.blue : theme.dim, true, TextAlignmentOptions.TopRight, "Count", UITheme.TextRole.Body);
            UILayout.Place(v.rectTransform, PickerW - 16f - 84f, 6f, 76f, 18f);
            if (a != null)
            {
                // the effects, up to two lines
                var e = HudKit.AddText(row, a.Summary, 11f, Color.Lerp(theme.dim, Color.white, .3f), false, TextAlignmentOptions.TopLeft, "Effect", UITheme.TextRole.Body);
                e.textWrappingMode = TextWrappingModes.Normal;
                UILayout.Place(e.rectTransform, x, 27f, PickerW - 16f - x - 8f, 32f);
            }
        }

        void Pick(Attachment a)
        {
            var w = Gun; var op = Op;
            if (w == null || op == null || !Editable) return;
            var fit = D.OperatorOf(op.key).FitOf(w.key);
            int slot = pickSlot;
            if (a == null) fit.parts[slot] = "";
            else if (fit.parts[slot] != a.key)
            {
                if (D.AttachmentsFree(a.key) <= 0) return;
                fit.parts[slot] = a.key;   // (the slot's old part goes back to the stock: counts come from the fits)
            }
            SaveSystem.Write(false);
            RefreshAll();
        }

        void ClosePicker()
        {
            pickSlot = -1;
            if (picker != null) HudKit.DestroySafe(picker.gameObject);
            picker = null;
        }
    }
}
