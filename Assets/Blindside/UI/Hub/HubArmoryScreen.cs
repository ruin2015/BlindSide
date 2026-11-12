// IGNIS's ロードアウト (menu canvas 1440 × 810): the guns and the gadgets of each operator in one place, over a 3D armoury
// (HubArmoryStage: a dark tactical room, the selected operator standing on a plate in its idle, the carried gun in hand).
//   Stage         the room's render, full screen
//   DecoShade     ink .72 over the left 520 (the list reads on it), DecoShadeEdge fading it out to the right
//   Head (40, 34) "LOADOUT" 12 display blue, spaced; 「ロードアウト」 30 title
//   Squad (40, 112, 380 wide)  a row 60 per operator: face 48, codename 18 display, role 12 (dim); the gun it carries, 12, right; the
//                 one shown blue — a click stands it on the plate and opens its page
//   Its page      「‹ 隊員選択に戻る」, a card (face 60, codename 24, kana · role 12), then 装備: two tiles 380 × 70 — 「銃」 (the carried gun ·
//                 its sub gun) and 「ガジェット」 (the item slots' contents); 銃 turns to the gun, ガジェット to the gadgets (below)
//   The pickers (parts, gadgets) stand at the right (x 1040, 360 wide), so a part the camera closes in on sits in the middle; a part
//                 picker lists every part made for the slot — those without a free copy dimmed (未所持 / 在庫なし).
//   Name (right, bottom)  the codename 46 display + kana · role 14, under the figure (not in the gun)
//   Footer        hint 12 (dim) bottom left, 「閉じる」 220 × 48 bottom right
// 銃: the figure raises its gun (HubArmoryStage.Aim) and the camera swings to its right side (GunShot); the column becomes
//   「‹ 戻る」 40, 武器 (a row 40 per gun of the operator: name 14, 装備中 / 所持 / 未所持 12 — a click puts it in the hands, and carries it when
//   owned), サブ武器 (co-op rank 6: the other owned guns, a click toggles), パーツ (a row 40 per slot: the slot 12 blue + the fitted part 14 /
//   なし). A slot: the camera closes in on that point of the held gun (SlotShot — the optic from above, the grip from below …) and the
//   picker opens beside the column (360 wide: なし + every part of the slot with a free copy or on this gun — art 46, name 14, effects 11,
//   残り N / 装着中; a click fits it, one copy per gun). ESC / 戻る: the picker, then the gun, then the screen.
// ガジェット: the camera drops low behind the figure at its belt (GadgetShot) and the figure, turned away, looks back down at it over its
//   shoulder (HubLookBack: the turn shared down the spine, neck and head); the column becomes 「‹ 戻る」 and the item slots 1–3 (a row 56:
//   アイテム枠 N 12 blue + the item 15 / なし / （コープ Rank 4 / 7 で解放）, 所持 N right); a slot opens the picker (なし + every gadget owned:
//   name 14, note 11, 所持 N / 装備中 — a copy carried per copy owned).
// Rows and tiles take HubHover (slide, tint, the accent bar widens). ESC closes (not while the item list is over it).
// Motion (unscaled; ITimedEffect.Evaluate(t) = the opening): the camera dollies in (the stage), the left column slides in from −30 and
// fades in .45 s, the name rises 16 and fades in from .2; another operator: the name again. Nothing is allocated per frame.
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
    public class HubArmoryScreen : MonoBehaviour, ITimedEffect
    {
        public const float ListX = 40f, ListY = 112f, ListW = 380f, RowH = 60f, RowGap = 6f, TileH = 70f, TileGap = 10f;
        public const float NameX = 560f, NameY = 640f, ShadeW = 520f;   // (left of the figure, now framed close at the right)
        public const float InTime = .45f, NameDelay = .2f, NameTime = .35f, SlideIn = 30f, NameRise = 16f;

        HubScreen screen;
        HubArmoryStage stage;
        RectTransform root, left, nameRoot;
        RawImage stageImage;
        CanvasGroup leftG, nameG;
        TMP_Text nameText, kanaText, hint;
        readonly List<GameObject> rows = new List<GameObject>(16);
        Vector2 leftPos, namePos;
        int opIndex;
        float clock = 99f, nameClock = 99f;
        bool wasCovered;
        // the gun
        // the picker stands at the right (the part the camera closes in on sits in the middle, between it and the column)
        public const float GunRowH = 40f, PickerW = 360f, PickerX = 0f, PickRowH = 62f;   // (x inside the right wall)
        public const float ToGunTime = .9f, ToSlotTime = .6f;
        bool gunMode, gadgetMode, opChosen;
        // the angled panels: a world-space canvas just in front of the room's camera (its child: it never moves in the picture), the
        // column turned away on its left edge and the picker wall on its right edge — real perspective, clicked through that camera
        public const float PanelDist = .15f, LeftYaw = -28f, RightYaw = 26f, SlabOffset = 6f, RowLift = 14f;
        Canvas panelCanvas;
        RectTransform panelRoot, rightWall, shadeRt, edgeRt;
        int shownGun = -1, pickSlot = -1, itemSlot = -1;
        RectTransform picker;

        public bool IsOpen => gameObject.activeSelf;
        public float Duration => InTime;
        SaveData D => screen.Data;
        static List<OperatorData> Squad => HubCatalog.Squad;
        OperatorData Op => opIndex >= 0 && opIndex < Squad.Count ? Squad[opIndex] : null;

        /// <summary>Something opened over this screen (the item list): it waits.</summary>
        bool Covered => screen.List != null && screen.List.IsOpen;

        public static HubArmoryScreen Build(Transform parent, HubScreen screen)
        {
            var theme = UITheme.Current;
            var r = HudKit.NewRect("HubArmory", parent);
            HudKit.Stretch(r);
            var s = r.gameObject.AddComponent<HubArmoryScreen>();
            s.screen = screen; s.root = r;

            var st = HudKit.NewRect("Stage", r);
            HudKit.Stretch(st);
            s.stageImage = st.gameObject.AddComponent<RawImage>();
            s.stageImage.color = Color.white;
            s.stageImage.raycastTarget = true;   // takes the clicks off the HQ under it (the angled panels come first: ArmoryPanelRaycaster)

            var shade = HudKit.NewRect("DecoShade", r);
            UILayout.Place(shade, 0f, 0f, ShadeW, 810f);
            HudKit.AddImage(shade, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .84f));
            s.shadeRt = shade;
            var edge = HudKit.NewRect("DecoShadeEdge", r);
            s.edgeRt = edge;
            UILayout.Place(edge, ShadeW, 0f, 160f, 810f);
            var eg = edge.gameObject.AddComponent<KitGradient>();
            eg.solidTo = 0f; eg.clearAt = 1f;
            eg.Set(new Color(theme.ink.r, theme.ink.g, theme.ink.b, .84f), new Color(.06f, .065f, .075f, 1f));
            eg.raycastTarget = false;

            s.left = HudKit.NewRect("Left", r);
            UILayout.Place(s.left, 0f, 0f, ShadeW, 810f);
            s.leftG = s.left.gameObject.AddComponent<CanvasGroup>();
            s.leftPos = s.left.anchoredPosition;
            var en = HudKit.AddText(s.left, "LOADOUT", 12f, theme.blue, true, TextAlignmentOptions.TopLeft, "En", UITheme.TextRole.Display);
            en.characterSpacing = 10f;
            UILayout.Place(en.rectTransform, ListX, 34f, 300f, 16f);
            var title = HudKit.AddText(s.left, "ロードアウト", 30f, Color.white, true, TextAlignmentOptions.TopLeft, "Title", UITheme.TextRole.Title);
            UILayout.Place(title.rectTransform, ListX, 52f, 400f, 40f);

            s.nameRoot = HudKit.NewRect("Name", r);
            UILayout.Place(s.nameRoot, NameX, NameY, 600f, 90f);
            s.nameG = s.nameRoot.gameObject.AddComponent<CanvasGroup>();
            s.namePos = s.nameRoot.anchoredPosition;
            s.nameText = HudKit.AddText(s.nameRoot, "", 46f, Color.white, true, TextAlignmentOptions.TopLeft, "Codename", UITheme.TextRole.Display);
            s.nameText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(s.nameText.rectTransform, 0f, 0f, 600f, 54f);
            s.kanaText = HudKit.AddText(s.nameRoot, "", 14f, Color.Lerp(theme.dim, Color.white, .35f), false, TextAlignmentOptions.TopLeft, "Kana", UITheme.TextRole.Body);
            s.kanaText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(s.kanaText.rectTransform, 2f, 58f, 600f, 20f);

            s.hint = HudKit.AddText(r, "", 12f, theme.dim, false, TextAlignmentOptions.MidlineLeft, "Hint", UITheme.TextRole.Body);
            s.hint.textWrappingMode = TextWrappingModes.NoWrap; s.hint.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(s.hint.rectTransform, ListX, 748f, 640f, 40f);

            var close = HudKit.NewRect("Close", r);
            UILayout.Place(close, 1440f - 40f - 220f, 744f, 220f, 48f);
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

        // ---- open / close ------------------------------------------------------------------------------------------------------
        public void Open(int operatorIndex = 0)
        {
            if (stage == null) stage = HubArmoryStage.Build();
            if (Application.isPlaying) stage.FitScreen(Screen.width, Screen.height);
            stageImage.texture = stage.Texture;
            stage.SetLive(Application.isPlaying);
            EnsurePanels();
            panelCanvas.gameObject.SetActive(true);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            opIndex = Mathf.Clamp(operatorIndex, 0, Mathf.Max(0, Squad.Count - 1));
            gunMode = false; pickSlot = -1; shownGun = -1;
            gadgetMode = false; itemSlot = -1; opChosen = false;
            stage.Aim(false);
            stage.GadgetPose(false);
            nameRoot.gameObject.SetActive(true);
            Refresh();
            bool play = Application.isPlaying && !UIMotionSettings.reduceMotion;
            if (play) stage.PlayEntrance();
            clock = play ? 0f : 99f;
            nameClock = play ? -NameDelay : 99f;
            Evaluate(clock); EvaluateName(nameClock);
        }

        public void Close()
        {
            CloseAsk();
            ClosePicker();
            if (stage != null) stage.SetLive(false);
            if (panelCanvas != null) panelCanvas.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        void OnDestroy() { if (stage != null) HudKit.DestroySafe(stage.gameObject); }

        /// <summary>Previews: `operatorIndex` on the plate, everything in, the room drawn now.</summary>
        public void Pose(int operatorIndex)
        {
            Open(operatorIndex);
            clock = nameClock = 99f;
            Evaluate(99f); EvaluateName(99f);
            stage.RenderNow();
        }

        /// <summary>Previews: `operatorIndex` chosen (its page), at rest.</summary>
        public void PoseOperator(int operatorIndex)
        {
            Pose(operatorIndex);
            SelectOperator(operatorIndex);
            clock = nameClock = 99f; Evaluate(99f); EvaluateName(99f);
            stage.RenderNow();
        }

        /// <summary>Previews: `operatorIndex`'s gun, with `slot`'s picker open (−1: none), at rest.</summary>
        public void PoseGun(int operatorIndex, int slot)
        {
            Pose(operatorIndex);
            EnterGun();
            if (slot >= 0) SelectSlot(slot);
            clock = 99f; Evaluate(99f);
            stage.Evaluate(99f);
            stage.RenderNow();
        }

        // ---- the angled panels -------------------------------------------------------------------------------------------------
        void EnsurePanels()
        {
            if (panelCanvas == null)
            {
                var go = new GameObject("ArmoryPanels", typeof(RectTransform));
                go.transform.SetParent(stage.View.transform, false);
                panelCanvas = go.AddComponent<Canvas>();
                panelCanvas.renderMode = RenderMode.WorldSpace;
                panelCanvas.worldCamera = stage.View;
                go.AddComponent<ArmoryPanelRaycaster>();   // (ahead of the overlays)
                panelRoot = (RectTransform)go.transform;
                // the shade goes behind the panels (flat, on the camera's plane), not over the picture they are in
                shadeRt.SetParent(panelRoot, false);
                shadeRt.GetComponent<Image>().color = new Color(0f, 0f, 0f, .5f);
                edgeRt.gameObject.SetActive(false);
                left.SetParent(panelRoot, false);
                left.localRotation = Quaternion.Euler(0f, LeftYaw, 0f);   // pivot on its left edge: the right side recedes
                rightWall = HudKit.NewRect("RightWall", panelRoot);
                rightWall.anchorMin = rightWall.anchorMax = rightWall.pivot = new Vector2(1f, 1f);
                rightWall.anchoredPosition = new Vector2(-40f, 0f);
                rightWall.sizeDelta = new Vector2(PickerW, 810f);
                rightWall.localRotation = Quaternion.Euler(0f, RightYaw, 0f);   // pivot on its right edge: the left side recedes
            }
            FitPanels();
        }

        // the canvas fills the camera's view at PanelDist (1440 × 810 units high, as wide as the render's aspect)
        void FitPanels()
        {
            var cam = stage.View;
            float h = 2f * PanelDist * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
            float aspect = stage.Texture != null ? (float)stage.Texture.width / stage.Texture.height : 16f / 9f;
            panelRoot.sizeDelta = new Vector2(810f * aspect, 810f);
            panelRoot.localPosition = new Vector3(0f, 0f, PanelDist);
            panelRoot.localRotation = Quaternion.identity;
            panelRoot.localScale = Vector3.one * (h / 810f);
            Canvas.ForceUpdateCanvases();
        }

        void Update()
        {
            if (Application.isPlaying && stage != null && stage.FitScreen(Screen.width, Screen.height)) { stageImage.texture = stage.Texture; FitPanels(); }
            float dt = Time.unscaledDeltaTime;
            if (clock < InTime) { clock += dt; Evaluate(clock); }
            if (nameClock < NameTime) { nameClock += dt; EvaluateName(nameClock); }
            if (Covered) { wasCovered = true; return; }
            if (wasCovered) { wasCovered = false; Refresh(); }   // back from the item list: what changed shows
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed) { EscapeGate.Consume(); if (Asking) CloseAsk(); else Back(); }
        }

        /// <summary>ESC / 戻る: the picker, then the gun / the gadgets, then the screen.</summary>
        void Back()
        {
            if (pickSlot >= 0) { ClosePicker(); stage.GunShot(ToSlotTime); BuildLeft(); return; }
            if (itemSlot >= 0) { ClosePicker(); BuildLeft(); return; }
            if (gunMode) { ExitGun(); return; }
            if (gadgetMode) { ExitGadget(); return; }
            if (opChosen) { opChosen = false; Refresh(); Replay(); return; }
            Close();
        }

        /// <summary>Previews: `operatorIndex`'s gadgets, with item slot `slot`'s picker open (−1: none), at rest.</summary>
        public void PoseGadget(int operatorIndex, int slot)
        {
            Pose(operatorIndex);
            stage.GadgetShot(0f);
            EnterGadget();
            if (slot >= 0) SelectItemSlot(slot);
            clock = 99f; Evaluate(99f);
            stage.Evaluate(99f);
            stage.GadgetPose(true);   // (edit mode: the look is posed once, from where the camera now is)
            stage.RenderNow();
        }

        // ---- the gadgets -------------------------------------------------------------------------------------------------------
        void EnterGadget()
        {
            if (Op == null) return;
            gadgetMode = true; itemSlot = -1; opChosen = true;
            stage.GadgetShot(ToGunTime);
            stage.GadgetPose(true);
            nameRoot.gameObject.SetActive(false);
            Refresh();
            Replay();
        }

        void ExitGadget()
        {
            ClosePicker();
            gadgetMode = false;
            stage.GadgetPose(false);
            stage.IdleShot(ToGunTime);
            nameRoot.gameObject.SetActive(true);
            Refresh();
            Replay();
            if (Application.isPlaying && !UIMotionSettings.reduceMotion) { nameClock = -NameDelay; EvaluateName(nameClock); }
        }

        void SelectItemSlot(int slot)
        {
            if (itemSlot == slot) { ClosePicker(); BuildLeft(); return; }
            ClosePicker();
            itemSlot = slot;
            BuildLeft();
            BuildItemPicker();
        }

        /// <summary>Puts gadget `g` (null: none) in the open slot — one copy carried per one owned, across the whole squad. Every copy
        /// already carried: the one an ally carries can be taken over (asked first: 付け替えますか？).</summary>
        void PutItem(HubCatalog.Gadget g)
        {
            var op = Op;
            if (op == null || itemSlot < 0 || !screen.Editable) return;
            var p = D.OperatorOf(op.key);
            int slot = itemSlot;
            if (g != null && p.Slot(slot) != g.key && GadgetFree(g.key, op.key, slot) <= 0)
            {
                if (!GadgetHolder(g.key, op.key, slot, out var holder, out var hs)) return;
                Ask(Name(holder) + "が装備中です", g.name + "を" + Name(holder) + "から外して、" + op.DisplayName + "に付け替えますか？", () =>
                {
                    D.OperatorOf(holder).SetSlot(hs, "");
                    p.SetSlot(slot, g.key);
                    SaveSystem.Write(false);
                    BuildLeft(); BuildItemPicker();
                });
                return;
            }
            p.SetSlot(slot, g != null ? g.key : "");
            SaveSystem.Write(false);
            BuildLeft();
            BuildItemPicker();
        }

        // ---- who carries what (the squad shares the stock: a gadget / a part on one is not free for another) ---------------------
        /// <summary>Copies of gadget `key` left to carry, `op`'s slot `slot` not counted (stock − every open slot of the squad holding it).</summary>
        int GadgetFree(string key, string op, int slot)
        {
            int carried = 0;
            foreach (var o in D.operators)
            {
                if (o == null) continue;
                int slots = Blindside.Progress.CoopPerks.GadgetSlots(D, o.key);
                for (int s = 0; s < slots; s++) if (o.Slot(s) == key && !(o.key == op && s == slot)) carried++;
            }
            return D.GadgetOf(key).count - carried;
        }

        /// <summary>Another operator (or another slot) carrying gadget `key`.</summary>
        bool GadgetHolder(string key, string op, int slot, out string holder, out int holderSlot)
        {
            holder = null; holderSlot = -1;
            // another operator first (taking a copy off one's own other slot is just moving it)
            for (int pass = 0; pass < 2; pass++)
                foreach (var o in D.operators)
                {
                    if (o == null || (pass == 0) == (o.key == op)) continue;
                    int slots = Blindside.Progress.CoopPerks.GadgetSlots(D, o.key);
                    for (int s = 0; s < slots; s++)
                        if (o.Slot(s) == key && !(o.key == op && s == slot)) { holder = o.key; holderSlot = s; return true; }
                }
            return false;
        }

        /// <summary>A gun of the squad with part `key` fitted (not `op`'s `weapon` at `slot`): its operator and gun.</summary>
        bool PartHolder(string key, string op, string weapon, int slot, out string holder, out string holderWeapon, out int holderSlot)
        {
            holder = holderWeapon = null; holderSlot = -1;
            for (int pass = 0; pass < 2; pass++)
                foreach (var o in D.operators)
                {
                    if (o == null || o.fits == null || (pass == 0) == (o.key == op)) continue;
                    foreach (var f in o.fits)
                        if (f != null && f.parts != null)
                            for (int s = 0; s < f.parts.Length; s++)
                                if (f.parts[s] == key && !(o.key == op && f.weapon == weapon && s == slot)) { holder = o.key; holderWeapon = f.weapon; holderSlot = s; return true; }
                }
            return false;
        }

        static string Name(string key) { var o = BuildAssetRegistry.Operator(key); return o != null ? o.DisplayName : key; }

        /// <summary>Who holds it, for the picker's row: 「CRUSHER 装備中」 (another of one's own guns: 「SCAR に装着中」).</summary>
        static string HeldBy(string holder, string holderWeapon, string self, bool part)
        {
            if (holder == self && holderWeapon != null)
            {
                string wn = holderWeapon;
                foreach (var o in Squad) foreach (var w in HubCatalog.GunsOf(o)) if (w != null && w.key == holderWeapon) wn = w.weaponName;
                return wn + " に装着中";
            }
            return Name(holder) + (part ? " 装着中" : " 装備中");
        }

        // ---- the confirmation (付け替えますか？) ----------------------------------------------------------------------------------
        DialogView dialog;
        System.Action pendingYes;

        void Ask(string head, string body, System.Action yes)
        {
            if (dialog == null)
            {
                // in the angled panels' canvas (flat on the camera's plane): it is clicked through the same raycaster, over the panels
                dialog = DialogView.Build(panelRoot != null ? (Transform)panelRoot : root, () => { var y = pendingYes; CloseAsk(); y?.Invoke(); }, CloseAsk);
            }
            pendingYes = yes;
            dialog.transform.SetAsLastSibling();
            dialog.Open(head, "SWAP", body, "付け替える", "キャンセル");
        }

        void CloseAsk()
        {
            pendingYes = null;
            if (dialog != null) dialog.Close();
        }

        bool Asking => dialog != null && dialog.Root.gameObject.activeSelf;

        /// <summary>Previews: the open item slot asked to take gadget `key` (an ally carries every copy: the 付け替え dialog, at rest).</summary>
        public void PoseTake(string key)
        {
            PutItem(HubCatalog.GadgetOf(key));
            if (dialog != null) dialog.Evaluate(99f);
            if (stage != null) stage.RenderNow();
        }

        void BuildGadgetColumn(float y)
        {
            var theme = UITheme.Current;
            var op = Op;
            var back = Row("Back", y, 40f, false, theme.paper2, Back);
            var bt = HudKit.AddText(back, "‹  戻る", 15f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Title);
            UILayout.Place(bt.rectTransform, 18f, 0f, ListW - 30f, 40f);
            y += 48f;
            if (op == null) return;
            Tag("ガジェット — " + op.DisplayName, y); y += 22f;
            var p = D.OperatorOf(op.key);
            int slots = Blindside.Progress.CoopPerks.GadgetSlots(D, op.key);
            for (int s = 0; s < OperatorProgress.ItemSlots; s++)
            {
                int slot = s;
                bool open = s < slots, on = s == itemSlot;
                var g = open ? HubCatalog.GadgetOf(p.Slot(s)) : null;
                var row = Row("Item" + s, y, 56f, on, g != null ? theme.acc : theme.paper2, () => SelectItemSlot(slot));
                if (!open) row.GetComponent<Button>().interactable = false;
                var sn = HudKit.AddText(row, "アイテム枠 " + (s + 1), 12f, on ? Color.white : open ? theme.blue : theme.dim, true, TextAlignmentOptions.TopLeft, "Slot", UITheme.TextRole.Body);
                sn.textWrappingMode = TextWrappingModes.NoWrap;
                UILayout.Place(sn.rectTransform, 18f, 8f, 160f, 18f);
                string main = !open ? "（コープ Rank " + (s == 1 ? 4 : 7) + " で解放）" : g != null ? g.name : "なし";
                var pn = HudKit.AddText(row, main, 15f, open && (g != null || on) ? Color.white : theme.dim, g != null, TextAlignmentOptions.TopLeft, "Item", UITheme.TextRole.Body);
                pn.textWrappingMode = TextWrappingModes.NoWrap; pn.overflowMode = TextOverflowModes.Ellipsis;
                UILayout.Place(pn.rectTransform, 18f, 28f, ListW - 120f, 22f);
                if (g != null)
                {
                    var c = HudKit.AddText(row, "所持 " + D.GadgetOf(g.key).count, 12f, on ? Color.white : theme.dim, true, TextAlignmentOptions.MidlineRight, "Count", UITheme.TextRole.Body);
                    UILayout.Place(c.rectTransform, ListW - 110f, 0f, 90f, 56f);
                }
                y += 60f;
            }
        }

        void BuildItemPicker()
        {
            if (picker != null) HudKit.DestroySafe(picker.gameObject);
            picker = null;
            var op = Op;
            if (op == null || itemSlot < 0) return;
            var theme = UITheme.Current;
            var p = D.OperatorOf(op.key);
            string on = p.Slot(itemSlot);
            var list = new List<HubCatalog.Gadget>(8);
            foreach (var g in HubCatalog.Gadgets) if (g.key == on || D.GadgetOf(g.key).count > 0) list.Add(g);

            picker = HudKit.NewRect("Picker", rightWall);
            float h = 44f + PickRowH * (list.Count + 1) + (list.Count == 0 ? 26f : 0f);
            var slotRow = left.Find("Item" + itemSlot) as RectTransform;
            float sy = slotRow != null ? -slotRow.anchoredPosition.y : 300f;
            float py = Mathf.Clamp(sy - 44f, 96f, 810f - 70f - h);
            UILayout.Place(picker, PickerX, py, PickerW, h);
            HudKit.AddImage(picker, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .95f), raycast: true);
            picker.gameObject.AddComponent<CutCornerGraphic>().Set(14f, CutCornerGraphic.Corners.TopRight);
            var head = HudKit.AddText(picker, "アイテム枠 " + (itemSlot + 1) + "  <size=11><color=#9AA0A8>" + op.DisplayName + "</color></size>", 15f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Head", UITheme.TextRole.Body);
            head.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(head.rectTransform, 16f, 8f, PickerW - 32f, 28f);
            float y = 44f;
            ItemRow(null, string.IsNullOrEmpty(on), y); y += PickRowH;
            foreach (var g in list) { ItemRow(g, g.key == on, y); y += PickRowH; }
            if (list.Count == 0)
            {
                var n = HudKit.AddText(picker, "所持しているガジェットがありません (装備購入 / 医務室)", 11f, theme.dim, false, TextAlignmentOptions.MidlineLeft, "Empty", UITheme.TextRole.Body);
                n.textWrappingMode = TextWrappingModes.NoWrap;
                UILayout.Place(n.rectTransform, 16f, y, PickerW - 32f, 22f);
            }
        }

        void ItemRow(HubCatalog.Gadget g, bool on, float y)
        {
            var theme = UITheme.Current;
            var row = HudKit.NewRect(g != null ? "Pick_" + g.key : "Pick_None", picker);
            UILayout.Place(row, 8f, y, PickerW - 16f, PickRowH - 4f);
            // every copy carried: by an ally — marked with who (amber), 所持 0, a click asks to take it over
            var op = Op;
            int free = g != null && op != null ? GadgetFree(g.key, op.key, itemSlot) : 0;
            string holder = null;
            bool heldElsewhere = g != null && !on && free <= 0 && op != null && GadgetHolder(g.key, op.key, itemSlot, out holder, out _);
            var baseC = on ? Color.Lerp(theme.ink2, theme.blue, .45f) : heldElsewhere ? Color.Lerp(theme.ink2, theme.acc, .12f) : theme.ink2;
            var img = HudKit.AddImage(row, baseC, raycast: true);
            var b = row.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None; b.targetGraphic = img;
            b.interactable = screen.Editable;
            b.onClick.AddListener(() => PutItem(g));
            var hv = row.gameObject.AddComponent<HubHover>();
            hv.target = row; hv.graphic = img; hv.baseColor = baseC; hv.hoverColor = Color.Lerp(baseC, theme.blue, .3f);
            hv.shift = new Vector2(6f, 0f); hv.scale = 1.01f;
            hv.SetBase(row.anchoredPosition);
            float x = 12f;
            if (g != null)
            {
                // the item's picture, as the parts have
                var f = HudKit.NewRect("Art", row);
                UILayout.Place(f, 6f, 6f, 46f, 46f);
                HudKit.AddImage(f, new Color(.82f, .85f, .88f, .95f));
                var ar = HudKit.NewRect("Photo", f); HudKit.Stretch(ar);
                var ai = ar.gameObject.AddComponent<Image>(); ai.sprite = HubCatalog.ArtOf(g); ai.preserveAspect = true; ai.raycastTarget = false;
                ai.enabled = ai.sprite != null;
                x = 60f;
            }
            var n = HudKit.AddText(row, g != null ? g.name : "なし (外す)", 14f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Body);
            n.textWrappingMode = TextWrappingModes.NoWrap; n.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(n.rectTransform, x, 6f, PickerW - 16f - x - 122f, 20f);
            string state = on ? "装備中" : heldElsewhere ? HeldBy(holder, null, op.key, false) : g != null ? "所持 " + Mathf.Max(0, free) : "";
            var v = HudKit.AddText(row, state, 12f, on ? theme.blue : heldElsewhere ? theme.acc : theme.dim, true, TextAlignmentOptions.TopRight, "Count", UITheme.TextRole.Body);
            v.textWrappingMode = TextWrappingModes.NoWrap; v.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(v.rectTransform, PickerW - 16f - 128f, 6f, 118f, 18f);
            if (heldElsewhere)
            {
                var z = HudKit.AddText(row, "所持 0", 11f, theme.dim, false, TextAlignmentOptions.TopRight, "Free", UITheme.TextRole.Body);
                UILayout.Place(z.rectTransform, PickerW - 16f - 128f, 24f, 118f, 16f);
            }
            if (g != null)
            {
                var e = HudKit.AddText(row, g.note + (g.medical ? " (医務室)" : ""), 11f, Color.Lerp(theme.dim, Color.white, .3f), false, TextAlignmentOptions.TopLeft, "Note", UITheme.TextRole.Body);
                e.textWrappingMode = TextWrappingModes.Normal;
                UILayout.Place(e.rectTransform, x, 27f, PickerW - 16f - x - (heldElsewhere ? 70f : 8f), 30f);
            }
        }

        // ---- the gun -----------------------------------------------------------------------------------------------------------
        void EnterGun()
        {
            var op = Op;
            if (op == null) return;
            gunMode = true; pickSlot = -1; opChosen = true;
            shownGun = Mathf.Clamp(Loadout.Choice(op), 0, Mathf.Max(0, HubCatalog.GunsOf(op).Length - 1));
            stage.Aim(true);
            stage.GunShot(ToGunTime);
            nameRoot.gameObject.SetActive(false);
            Refresh();
            Replay();
        }

        void ExitGun()
        {
            ClosePicker();
            gunMode = false; shownGun = -1;
            stage.Aim(false);
            stage.IdleShot(ToGunTime);
            nameRoot.gameObject.SetActive(true);
            Refresh();
            Replay();
            if (Application.isPlaying && !UIMotionSettings.reduceMotion) { nameClock = -NameDelay; EvaluateName(nameClock); }
        }

        // the column slides in again (another page of it)
        void Replay()
        {
            if (!Application.isPlaying || UIMotionSettings.reduceMotion) { clock = 99f; Evaluate(99f); return; }
            clock = 0f; Evaluate(0f);
        }

        WeaponData ShownGun
        {
            get
            {
                var op = Op;
                if (op == null) return null;
                var guns = HubCatalog.GunsOf(op);
                return shownGun >= 0 && shownGun < guns.Length ? guns[shownGun] : CarriedGun(op);
            }
        }

        bool ShownOwned => ShownGun != null && HubCatalog.Owns(D, ShownGun);

        void SelectGun(int i)
        {
            var op = Op;
            var guns = op != null ? HubCatalog.GunsOf(op) : null;
            if (guns == null || i < 0 || i >= guns.Length || guns[i] == null) return;
            shownGun = i;
            if (HubCatalog.Owns(D, guns[i]) && screen.Editable) Loadout.SetChoice(op, i);   // the gun in the hands is carried
            ClosePicker();
            stage.ShowOperator(op, guns[i]);
            stage.GunChanged();
            stage.GunShot(ToSlotTime);
            BuildLeft();
            if (!Application.isPlaying) stage.RenderNow();
        }

        void ToggleSecond(int i)
        {
            var op = Op;
            if (op == null || !screen.Editable) return;
            Loadout.SetChoice2(op, Loadout.Choice2(op) == i ? -1 : i);
            BuildLeft();
        }

        void SelectSlot(int slot)
        {
            if (pickSlot == slot) { ClosePicker(); stage.GunShot(ToSlotTime); BuildLeft(); return; }
            pickSlot = slot;
            stage.SlotShot(slot, ToSlotTime);
            BuildLeft();
            BuildPicker();
        }

        void Pick(Attachment a)
        {
            var w = ShownGun; var op = Op;
            if (w == null || op == null || !screen.Editable || pickSlot < 0) return;
            var fit = D.OperatorOf(op.key).FitOf(w.key);
            int slot = pickSlot;
            if (a == null) fit.parts[slot] = "";
            else if (fit.parts[slot] != a.key)
            {
                if (D.AttachmentsFree(a.key) <= 0)
                {
                    // every copy is on a gun: the one an ally (or another gun) has can be moved here, asked first
                    if (!PartHolder(a.key, op.key, w.key, slot, out var holder, out var hw, out var hs)) return;
                    Ask(HeldBy(holder, hw, op.key, true), a.name + "を外して、" + op.DisplayName + "の" + w.weaponName + "に付け替えますか？", () =>
                    {
                        var hf = D.OperatorOf(holder).FitOf(hw);
                        if (hf != null && hs >= 0 && hs < hf.parts.Length && hf.parts[hs] == a.key) hf.parts[hs] = "";
                        fit.parts[slot] = a.key;
                        SaveSystem.Write(false);
                        BuildLeft(); BuildPicker();
                    });
                    return;
                }
                fit.parts[slot] = a.key;   // (the slot's old part goes back to the stock: counts come from the fits)
            }
            SaveSystem.Write(false);
            BuildLeft();
            BuildPicker();
        }

        void ClosePicker()
        {
            pickSlot = -1; itemSlot = -1;
            if (picker != null) HudKit.DestroySafe(picker.gameObject);
            picker = null;
        }

        void BuildGunColumn(float y)
        {
            var theme = UITheme.Current;
            var op = Op;
            var back = Row("Back", y, 40f, false, theme.paper2, Back);
            var bt = HudKit.AddText(back, "‹  戻る", 15f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Title);
            UILayout.Place(bt.rectTransform, 18f, 0f, ListW - 30f, 40f);
            y += 48f;
            if (op == null) return;
            Tag("武器 — " + op.DisplayName, y); y += 22f;
            var guns = HubCatalog.GunsOf(op);
            int carried = Loadout.Choice(op);
            for (int i = 0; i < guns.Length; i++)
            {
                var w = guns[i];
                if (w == null) continue;
                int idx = i;
                bool on = i == shownGun, owned = HubCatalog.Owns(D, w);
                var row = Row("Gun" + i, y, GunRowH, on, op.color, () => SelectGun(idx));
                RowText(row, w.weaponName, i == carried && owned ? "装備中" : owned ? "所持" : "未所持", on, owned);
                y += GunRowH + 4f;
            }
            if (Blindside.Progress.CoopPerks.HasTwoGuns(D, op.key))
            {
                y += 8f;
                Tag("サブ武器 (二丁持ち)", y); y += 22f;
                int c2 = Loadout.Choice2(op);
                for (int i = 0; i < guns.Length; i++)
                {
                    var w = guns[i];
                    if (w == null || i == carried || !HubCatalog.Owns(D, w)) continue;
                    int idx = i;
                    bool on = i == c2;
                    var row = Row("Sub" + i, y, GunRowH, on, theme.acc, () => ToggleSecond(idx));
                    RowText(row, w.weaponName, on ? "サブ装備中" : "所持", on, true);
                    y += GunRowH + 4f;
                }
            }
            var gun = ShownGun;
            if (gun == null) return;
            y += 8f;
            Tag("パーツ — " + gun.weaponName, y); y += 22f;
            var fit = D.OperatorOf(op.key).FindFit(gun.key);
            bool owned2 = ShownOwned;
            for (int s = 0; s < Attachments.SlotCount; s++)
            {
                int slot = s;
                var part = fit != null ? Attachments.Of(fit.parts[s]) : null;
                bool on = s == pickSlot;
                var row = Row("Slot" + s, y, GunRowH, on, part != null ? theme.acc : theme.paper2, () => SelectSlot(slot));
                if (!owned2) row.GetComponent<Button>().interactable = false;
                var sn = HudKit.AddText(row, Attachments.Slots[s], 12f, on ? Color.white : theme.blue, true, TextAlignmentOptions.MidlineLeft, "Slot", UITheme.TextRole.Body);
                sn.textWrappingMode = TextWrappingModes.NoWrap;
                UILayout.Place(sn.rectTransform, 18f, 0f, 92f, GunRowH);
                var pn = HudKit.AddText(row, part != null ? part.name : "なし", 14f, part != null || on ? Color.white : theme.dim, part != null, TextAlignmentOptions.MidlineLeft, "Part", UITheme.TextRole.Body);
                pn.textWrappingMode = TextWrappingModes.NoWrap; pn.overflowMode = TextOverflowModes.Ellipsis;
                UILayout.Place(pn.rectTransform, 112f, 0f, ListW - 150f, GunRowH);
                y += GunRowH + 4f;
            }
        }

        void RowText(RectTransform row, string name, string state, bool on, bool owned)
        {
            var theme = UITheme.Current;
            var n = HudKit.AddText(row, name, 14f, on ? Color.white : owned ? theme.paper : theme.dim, true, TextAlignmentOptions.MidlineLeft, "Name", UITheme.TextRole.Body);
            n.textWrappingMode = TextWrappingModes.NoWrap; n.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(n.rectTransform, 18f, 0f, ListW - 130f, GunRowH);
            var v = HudKit.AddText(row, state, 12f, on ? Color.white : theme.dim, true, TextAlignmentOptions.MidlineRight, "State", UITheme.TextRole.Body);
            v.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(v.rectTransform, ListW - 120f, 0f, 100f, GunRowH);
        }

        void BuildPicker()
        {
            if (picker != null) HudKit.DestroySafe(picker.gameObject);
            picker = null;
            var w = ShownGun; var op = Op;
            if (w == null || op == null || pickSlot < 0 || !ShownOwned) return;
            var theme = UITheme.Current;
            var fit = D.OperatorOf(op.key).FindFit(w.key);
            string on = fit != null ? fit.parts[pickSlot] : "";
            var list = new List<Attachment>(8);
            // every part made for this slot: the ones with a free copy (or on this gun) to pick, the rest shown as 未所持 / 在庫なし
            foreach (var a in Attachments.All)
                if (a.slot == pickSlot) list.Add(a);

            picker = HudKit.NewRect("Picker", rightWall);
            float h = 44f + PickRowH * (list.Count + 1) + (list.Count == 0 ? 26f : 0f);
            var slotRow = left.Find("Slot" + pickSlot) as RectTransform;
            float sy = slotRow != null ? -slotRow.anchoredPosition.y : 300f;
            float py = Mathf.Clamp(sy - 44f, 96f, 810f - 70f - h);
            UILayout.Place(picker, PickerX, py, PickerW, h);
            HudKit.AddImage(picker, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .95f), raycast: true);
            picker.gameObject.AddComponent<CutCornerGraphic>().Set(14f, CutCornerGraphic.Corners.TopRight);
            var head = HudKit.AddText(picker, Attachments.Slots[pickSlot] + "  <size=11><color=#9AA0A8>" + w.weaponName + "</color></size>", 15f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Head", UITheme.TextRole.Body);
            head.textWrappingMode = TextWrappingModes.NoWrap; head.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(head.rectTransform, 16f, 8f, PickerW - 32f, 28f);
            float y = 44f;
            PickRow(null, string.IsNullOrEmpty(on), y); y += PickRowH;
            foreach (var a in list) { PickRow(a, a.key == on, y); y += PickRowH; }
            if (list.Count == 0)
            {
                var n = HudKit.AddText(picker, "このスロットの空きパーツがありません (装備購入)", 11f, theme.dim, false, TextAlignmentOptions.MidlineLeft, "Empty", UITheme.TextRole.Body);
                n.textWrappingMode = TextWrappingModes.NoWrap;
                UILayout.Place(n.rectTransform, 16f, y, PickerW - 32f, 22f);
            }
        }

        void PickRow(Attachment a, bool on, float y)
        {
            var theme = UITheme.Current;
            var row = HudKit.NewRect(a != null ? "Pick_" + a.key : "Pick_None", picker);
            UILayout.Place(row, 8f, y, PickerW - 16f, PickRowH - 4f);
            // a part with no free copy (none bought, or every copy on other guns) is listed but can't be fitted
            int free = a != null ? D.AttachmentsFree(a.key) : 0;
            // every copy fitted: on an ally's gun (or another of this operator's) — marked with where, a click asks to take it over
            var w = ShownGun; var op = Op;
            string holder = null, holderWeapon = null;
            bool heldElsewhere = a != null && !on && free <= 0 && w != null && op != null && PartHolder(a.key, op.key, w.key, pickSlot, out holder, out holderWeapon, out _);
            bool usable = a == null || on || free > 0 || heldElsewhere;
            var baseC = on ? Color.Lerp(theme.ink2, theme.blue, .45f) : heldElsewhere ? Color.Lerp(theme.ink2, theme.acc, .12f) : usable ? theme.ink2 : new Color(theme.ink2.r, theme.ink2.g, theme.ink2.b, .45f);
            var img = HudKit.AddImage(row, baseC, raycast: true);
            var b = row.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None; b.targetGraphic = img;
            b.interactable = screen.Editable && usable;
            b.onClick.AddListener(() => Pick(a));
            var hv = row.gameObject.AddComponent<HubHover>();
            hv.target = row; hv.graphic = img; hv.baseColor = baseC; hv.hoverColor = Color.Lerp(baseC, theme.blue, .3f);
            hv.shift = new Vector2(6f, 0f); hv.scale = 1.01f;
            hv.interactable = usable;
            hv.SetBase(row.anchoredPosition);
            if (!usable) row.gameObject.AddComponent<CanvasGroup>().alpha = .55f;
            float x = 10f;
            if (a != null)
            {
                var f = HudKit.NewRect("Art", row);
                UILayout.Place(f, 6f, 6f, 46f, 46f);
                HudKit.AddImage(f, new Color(.82f, .85f, .88f, .95f));
                var ar = HudKit.NewRect("Photo", f); HudKit.Stretch(ar);
                var ai = ar.gameObject.AddComponent<Image>(); ai.sprite = Attachments.ArtOf(a); ai.preserveAspect = true; ai.raycastTarget = false;
                x = 60f;
            }
            var n = HudKit.AddText(row, a != null ? a.name : "なし (外す)", 14f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Body);
            n.textWrappingMode = TextWrappingModes.NoWrap; n.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(n.rectTransform, x, 6f, PickerW - 16f - x - 122f, 20f);
            string state = on ? "装着中" : a == null ? "" : free > 0 ? "残り " + free : heldElsewhere ? HeldBy(holder, holderWeapon, op.key, true) : D.AttachmentsFitted(a.key) > 0 ? "在庫なし" : "未所持";
            var v = HudKit.AddText(row, state, 12f, on ? theme.blue : heldElsewhere ? theme.acc : theme.dim, true, TextAlignmentOptions.TopRight, "Count", UITheme.TextRole.Body);
            v.textWrappingMode = TextWrappingModes.NoWrap; v.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(v.rectTransform, PickerW - 16f - 128f, 6f, 118f, 18f);
            if (heldElsewhere)
            {
                var z = HudKit.AddText(row, "残り 0", 11f, theme.dim, false, TextAlignmentOptions.TopRight, "Free", UITheme.TextRole.Body);
                UILayout.Place(z.rectTransform, PickerW - 16f - 128f, 24f, 118f, 16f);
            }
            if (a != null)
            {
                var e = HudKit.AddText(row, a.Summary, 11f, Color.Lerp(theme.dim, Color.white, .3f), false, TextAlignmentOptions.TopLeft, "Effect", UITheme.TextRole.Body);
                e.textWrappingMode = TextWrappingModes.Normal;
                UILayout.Place(e.rectTransform, x, 27f, PickerW - 16f - x - (heldElsewhere ? 70f : 8f), 30f);
            }
        }

        // ---- selection ---------------------------------------------------------------------------------------------------------
        void SelectOperator(int i)
        {
            opIndex = i;
            opChosen = true;
            Refresh();
            Replay();
            if (Application.isPlaying && !UIMotionSettings.reduceMotion) { nameClock = 0f; EvaluateName(0f); }
        }

        void Refresh()
        {
            var op = Op;
            stage.ShowOperator(op, gunMode ? ShownGun : CarriedGun(op));
            if (!Application.isPlaying) stage.RenderNow();
            nameText.SetText(op != null ? op.DisplayName : "");
            kanaText.SetText(op != null ? op.kr + "  ·  " + op.role : "");
            nameRoot.gameObject.SetActive(!opChosen);   // (the chosen operator's card says who it is)
            BuildLeft();
            hint.SetText(!screen.Editable ? "セーブデータがありません (変更はできません)"
                : gadgetMode ? "アイテム枠を選んでガジェットを装備 · 所持数まで持たせられます · ESC 戻る"
                : gunMode ? (ShownOwned ? "パーツの部位を選んで装着 · パーツ1つは1丁の武器にのみ装着できます · ESC 戻る" : "この武器はまだ所持していません (装備購入で購入できます) · ESC 戻る")
                : opChosen ? "銃かガジェットを選択 · ESC 戻る"
                : "隊員を選択 · ESC 閉じる");
        }

        static WeaponData CarriedGun(OperatorData op)
        {
            if (op == null) return null;
            var guns = HubCatalog.GunsOf(op);
            if (guns == null || guns.Length == 0) return op.weapon;
            int c = Mathf.Clamp(Loadout.Choice(op), 0, guns.Length - 1);
            return guns[c] != null ? guns[c] : op.weapon;
        }

        // the gadgets carried: the open item slots' contents
        string ItemsLine(OperatorData op)
        {
            var p = D.OperatorOf(op.key);
            int slots = Blindside.Progress.CoopPerks.GadgetSlots(D, op.key);
            var sb = new System.Text.StringBuilder();
            for (int s = 0; s < slots; s++)
            {
                var g = HubCatalog.GadgetOf(p.Slot(s));
                if (s > 0) sb.Append("  ·  ");
                sb.Append(g != null ? g.name : "なし");
            }
            return sb.ToString();
        }

        // ---- the left column ---------------------------------------------------------------------------------------------------
        void BuildLeft()
        {
            foreach (var g in rows) HudKit.DestroySafe(g);
            rows.Clear();
            var theme = UITheme.Current;
            float y = ListY;
            if (gunMode) { BuildGunColumn(y); return; }
            if (gadgetMode) { BuildGadgetColumn(y); return; }
            if (opChosen) { BuildOperatorPage(y); return; }
            Tag("隊員", y); y += 22f;
            for (int i = 0; i < Squad.Count; i++)
            {
                var op = Squad[i];
                int idx = i;
                bool on = i == opIndex;
                var row = Row("Op" + i, y, RowH, on, op.color, () => SelectOperator(idx));
                var f = HudKit.NewRect("Face", row);
                UILayout.Place(f, 14f, 6f, 48f, 48f);
                HudKit.AddImage(f, Color.Lerp(theme.paper2, theme.ink, .15f));
                f.gameObject.AddComponent<RectMask2D>();
                var ph = HudKit.NewRect("Photo", f); HudKit.Stretch(ph);
                var im = ph.gameObject.AddComponent<Image>(); im.sprite = OperatorArt.Face(op.key); im.preserveAspect = true; im.raycastTarget = false;
                im.enabled = im.sprite != null;
                var n = HudKit.AddText(row, op.DisplayName, 18f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Display);
                n.textWrappingMode = TextWrappingModes.NoWrap;
                UILayout.Place(n.rectTransform, 74f, 8f, 170f, 24f);
                var rl = HudKit.AddText(row, op.role, 12f, on ? Color.white : theme.dim, false, TextAlignmentOptions.TopLeft, "Role", UITheme.TextRole.Body);
                rl.textWrappingMode = TextWrappingModes.NoWrap; rl.overflowMode = TextOverflowModes.Ellipsis;
                UILayout.Place(rl.rectTransform, 74f, 34f, 170f, 18f);
                var gun = CarriedGun(op);
                var gl = HudKit.AddText(row, gun != null ? gun.weaponName : "", 12f, on ? Color.white : Color.Lerp(theme.dim, Color.white, .25f), false, TextAlignmentOptions.MidlineRight, "Gun", UITheme.TextRole.Body);
                gl.textWrappingMode = TextWrappingModes.NoWrap; gl.overflowMode = TextOverflowModes.Ellipsis;
                UILayout.Place(gl.rectTransform, 244f, 0f, ListW - 260f, RowH);
                y += RowH + RowGap;
            }
        }

        // the operator chosen: who it is, then 銃 / ガジェット
        void BuildOperatorPage(float y)
        {
            var theme = UITheme.Current;
            var back = Row("Back", y, 40f, false, theme.paper2, Back);
            var bt = HudKit.AddText(back, "‹  隊員選択に戻る", 15f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Title);
            UILayout.Place(bt.rectTransform, 18f, 0f, ListW - 30f, 40f);
            y += 52f;
            var cur = Op;
            if (cur == null) return;
            var card = HudKit.NewRect("OpCard", left);
            UILayout.Place(card, ListX, y, ListW, 76f);
            HudKit.AddImage(card, new Color(theme.ink2.r, theme.ink2.g, theme.ink2.b, .9f));
            var bar = HudKit.NewRect("DecoBar", card);
            UILayout.Place(bar, 0f, 0f, 4f, 76f);
            HudKit.AddImage(bar, cur.color);
            var f = HudKit.NewRect("Face", card);
            UILayout.Place(f, 14f, 8f, 60f, 60f);
            HudKit.AddImage(f, Color.Lerp(theme.paper2, theme.ink, .15f));
            f.gameObject.AddComponent<RectMask2D>();
            var ph = HudKit.NewRect("Photo", f); HudKit.Stretch(ph);
            var im = ph.gameObject.AddComponent<Image>(); im.sprite = OperatorArt.Face(cur.key); im.preserveAspect = true; im.raycastTarget = false;
            im.enabled = im.sprite != null;
            var n = HudKit.AddText(card, cur.DisplayName, 24f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Display);
            n.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(n.rectTransform, 88f, 10f, ListW - 100f, 30f);
            var rl = HudKit.AddText(card, cur.kr + "  ·  " + cur.role, 12f, Color.Lerp(theme.dim, Color.white, .35f), false, TextAlignmentOptions.TopLeft, "Role", UITheme.TextRole.Body);
            rl.textWrappingMode = TextWrappingModes.NoWrap; rl.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(rl.rectTransform, 88f, 44f, ListW - 100f, 18f);
            rows.Add(card.gameObject);
            y += 76f + 18f;
            Tag("装備", y); y += 22f;
            var g1 = CarriedGun(cur);
            var w2 = Blindside.Progress.CoopPerks.HasTwoGuns(D, cur.key) ? Loadout.Second(cur) : null;
            Tile("Gun", "銃", (g1 != null ? g1.weaponName : "—") + (w2 != null ? "  ·  サブ " + w2.weaponName : ""), y, EnterGun);
            y += TileH + TileGap;
            Tile("Gadget", "ガジェット", ItemsLine(cur), y, EnterGadget);
        }

        void Tag(string text, float y)
        {
            var t = HudKit.AddText(left, text, 12f, UITheme.Current.blue, true, TextAlignmentOptions.BottomLeft, "Tag_" + text, UITheme.TextRole.Body);
            UILayout.Place(t.rectTransform, ListX + 2f, y, ListW, 18f);
            rows.Add(t.gameObject);
        }

        RectTransform Row(string name, float y, float h, bool on, Color accent, UnityEngine.Events.UnityAction click)
        {
            var theme = UITheme.Current;
            // a slab under it, the row raised off it toward the camera (the panel is in 3D: the gap shows as depth)
            var slab = HudKit.NewRect("Deco" + name + "Slab", left);
            UILayout.Place(slab, ListX + SlabOffset, y + SlabOffset, ListW, h);
            HudKit.AddImage(slab, new Color(0f, 0f, 0f, .55f));
            slab.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
            slab.gameObject.AddComponent<LintIgnoreOverlap>().reason = "the row's slab sits under it on purpose";
            rows.Add(slab.gameObject);
            var row = HudKit.NewRect(name, left);
            UILayout.Place(row, ListX, y, ListW, h);
            row.anchoredPosition3D = new Vector3(row.anchoredPosition.x, row.anchoredPosition.y, -RowLift);
            var baseC = on ? theme.blue : new Color(theme.ink2.r, theme.ink2.g, theme.ink2.b, .9f);
            var img = HudKit.AddImage(row, baseC, raycast: true);
            row.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
            var bar = HudKit.NewRect("DecoBar", row);
            UILayout.Place(bar, 0f, 0f, 4f, h);
            HudKit.AddImage(bar, on ? Color.white : accent);
            var b = row.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None; b.targetGraphic = img;
            b.onClick.AddListener(click);
            var hv = row.gameObject.AddComponent<HubHover>();
            hv.target = row; hv.graphic = img; hv.baseColor = baseC;
            hv.hoverColor = on ? baseC : Color.Lerp(baseC, theme.blue, .35f);
            hv.shift = new Vector2(10f, 0f); hv.scale = 1.02f;
            hv.grow = bar; hv.growFrom = 4f; hv.growTo = 8f;
            hv.SetBase(row.anchoredPosition);
            rows.Add(row.gameObject);
            return row;
        }

        void Tile(string name, string label, string sub, float y, UnityEngine.Events.UnityAction click)
        {
            var theme = UITheme.Current;
            var row = Row("Tile" + name, y, TileH, false, theme.acc, click);
            // the tile's English name, big and faint across its right side (the menu's look: 編成 / SQUADS)
            var en = HudKit.AddText(row, name == "Gun" ? "WEAPON" : "GADGET", 34f, new Color(1f, 1f, 1f, .07f), true, TextAlignmentOptions.MidlineRight, "DecoEn", UITheme.TextRole.Display);
            en.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(en.rectTransform, ListW - 260f, 0f, 200f, TileH);
            var t = HudKit.AddText(row, label, 22f, Color.white, true, TextAlignmentOptions.TopLeft, "Label", UITheme.TextRole.Title);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(t.rectTransform, 22f, 8f, 200f, 30f);
            var s = HudKit.AddText(row, sub, 13f, Color.Lerp(theme.dim, Color.white, .4f), false, TextAlignmentOptions.TopLeft, "Sub", UITheme.TextRole.Body);
            s.textWrappingMode = TextWrappingModes.NoWrap; s.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(s.rectTransform, 22f, 42f, ListW - 70f, 20f);
            var go = HudKit.AddText(row, "›", 26f, theme.blue, true, TextAlignmentOptions.MidlineRight, "Arrow", UITheme.TextRole.Body);
            UILayout.Place(go.rectTransform, ListW - 48f, 0f, 30f, TileH);
        }

        // ---- motion ------------------------------------------------------------------------------------------------------------
        public void Evaluate(float t)
        {
            float k = UIMotionSettings.reduceMotion ? 1f : UIEase.Tactical(Mathf.Clamp01(t / InTime));
            var p = leftPos + new Vector2(-SlideIn * (1f - k), 0f);
            if (left.anchoredPosition != p) left.anchoredPosition = p;
            if (leftG.alpha != k) leftG.alpha = k;
        }

        void EvaluateName(float t)
        {
            float k = UIMotionSettings.reduceMotion ? 1f : UIEase.Tactical(Mathf.Clamp01(t / NameTime));
            var p = namePos + new Vector2(0f, -NameRise * (1f - k));
            if (nameRoot.anchoredPosition != p) nameRoot.anchoredPosition = p;
            if (nameG.alpha != k) nameG.alpha = k;
        }
    }
}
