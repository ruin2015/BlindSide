// The HQ's screen layer (menu canvas 1440 × 810), over the third-person view:
//   Topbar   top left 24/20: ink plate 360 × 46 (blue band 12, 「本部」 18 title + "SQUAD HQ" 11 display), under it 30 high: money
//            (¥, 14 display) and the rank (12 body) on paper2; right of it (392) the place the player is in: ink plate 260 × 46,
//            ◎ (BSTactical, gold) + the HubArea name 16 title
//   Minimap  top right (HubMinimap): the floor plan, the stations' icons, the player's arrow
//   Menu     ESC (HubMenu): 隊員情報 (SquadInfoContent — the stat points are given there), 設定, 保存, タイトルへ, 再開
//   Labels   a floating name over every station (HubNpc): name 15 title (white, dark outline) + caption 11 (blue); hidden past 16 m
//            or behind the camera. A pool made once; moved every frame (no allocation). Room signs (HubSign) the same, the caption in
//            gold, shown within 11 m
//   Prompt   bottom centre, 150 up: ink plate 380 × 44, a key cap "F" (blue 30 × 30) + 「作戦室 — CRUSHER」 15 body; an operator also
//            gets a second plate under it (380 × 34): key cap "G" (paper2) + 「話す」. Shown while a station is in reach
//   Hint     bottom left: 「WASD 移動 · Shift 走る · 右ドラッグ 視点 · F 調べる · G 話す」 11 (dim)
//   Panels   HubListPanel (the stations), KitGrowthPanel (over the list), HubTalkPanel (会話)
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.Save;
using Blindside.UI;

namespace Blindside.Hub
{
    public class HubScreen : MonoBehaviour
    {
        public const float LabelRange = 16f;

        RectTransform root, labelsRoot, prompt, talkPrompt;
        TMP_Text promptText, moneyText, rankText;
        HubListPanel list;
        HubTalkPanel talk;
        KitGrowthPanel growth;
        Camera cam;
        Canvas canvas;

        sealed class Label { public RectTransform rt; public TMP_Text name, caption; public HubNpc npc; public Transform sign; }
        readonly List<Label> labels = new List<Label>(12);

        ShopContent shopC; MedicalContent medC; TrainingContent trainC; OperationsContent opsC;
        LoadoutContent loadC, itemsC; IgnisContent ignisC; SquadInfoContent squadC;
        HubLoadoutScreen loadout;
        public HubLoadoutScreen LoadoutScreen => loadout;

        /// <summary>IGNIS's ロードアウト: the workbench screen.</summary>
        public void OpenLoadout(int operatorIndex = 0) => loadout.Open(operatorIndex);
        HubMenu menu;
        public HubMenu Menu => menu;

        /// <summary>Previews: a sample save shown instead of the session's.</summary>
        public SaveData previewData;
        public SaveData Data => SaveSystem.Current ?? previewData ?? (previewData = SaveData.Fresh());
        /// <summary>Changes are kept (a save session, or a preview's sample).</summary>
        public bool Editable => SaveSystem.Current != null || (!Application.isPlaying && previewData != null);

        public bool AnyOpen => (list != null && list.IsOpen) || (talk != null && talk.IsOpen) || (growth != null && growth.IsOpen)
                               || (menu != null && menu.IsOpen) || (loadout != null && loadout.IsOpen) || leaving;
        public HubListPanel List => list;
        public HubTalkPanel Talk => talk;
        public KitGrowthPanel Growth => growth;
        bool leaving;

        public static HubScreen Build(Transform canvasRoot, Camera camera)
        {
            var s = canvasRoot.gameObject.AddComponent<HubScreen>();
            s.cam = camera;
            s.canvas = canvasRoot.GetComponentInParent<Canvas>();
            s.root = HudKit.NewRect("HubScreen", canvasRoot);
            HudKit.Stretch(s.root);
            s.labelsRoot = HudKit.NewRect("Labels", s.root);
            HudKit.Stretch(s.labelsRoot);
            s.BuildTopbar();
            s.BuildPrompt();
            s.BuildHint();
            s.list = HubListPanel.Build(s.root);
            s.growth = KitGrowthPanel.Build(s.root);
            s.talk = HubTalkPanel.Build(s.root);
            s.list.EscBlocked = () => s.growth.IsOpen;
            s.shopC = new ShopContent(s); s.medC = new MedicalContent(s); s.trainC = new TrainingContent(s);
            s.opsC = new OperationsContent(s); s.loadC = new LoadoutContent(s); s.itemsC = new LoadoutContent(s, true);
            s.ignisC = new IgnisContent(s, s.itemsC, s.shopC);
            s.loadout = HubLoadoutScreen.Build(s.root, s);
            s.squadC = new SquadInfoContent(s);
            s.menu = HubMenu.Build(s.root, () => s.list.Open(s.squadC), s.SaveNow, s.ToTitle);
            s.menu.Covered = () => s.list.IsOpen || s.growth.IsOpen;
            s.RefreshTopbar();
            SaveSystem.Saved += s.OnSaved;
            return s;
        }

        void OnDestroy() => SaveSystem.Saved -= OnSaved;
        void OnSaved(bool auto) => RefreshTopbar();

        // ---- build -------------------------------------------------------------------------------------------------------------
        void BuildTopbar()
        {
            var theme = UITheme.Current;
            var plate = HudKit.NewRect("Topbar", root);
            UILayout.Place(plate, 24f, 20f, 360f, 46f);
            HudKit.AddImage(plate, theme.ink);
            plate.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 14f);
            var band = HudKit.NewRect("DecoBand", plate);
            UILayout.Place(band, 0f, 0f, 12f, 46f);
            HudKit.AddImage(band, theme.blue);
            var t = HudKit.AddText(plate, "本部", 18f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Title", UITheme.TextRole.Title);
            UILayout.Place(t.rectTransform, 30f, 0f, 90f, 46f);
            var en = HudKit.AddText(plate, "SQUAD HQ", 11f, Color.Lerp(theme.ink, Color.white, .65f), true, TextAlignmentOptions.MidlineLeft, "En", UITheme.TextRole.Display);
            en.characterSpacing = 10f;
            UILayout.Place(en.rectTransform, 120f, 0f, 200f, 46f);

            var info = HudKit.NewRect("Info", root);
            UILayout.Place(info, 24f, 66f, 330f, 30f);
            HudKit.AddImage(info, new Color(theme.paper2.r, theme.paper2.g, theme.paper2.b, .92f));
            info.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
            moneyText = HudKit.AddText(info, "", 14f, theme.ink, true, TextAlignmentOptions.MidlineLeft, "Money", UITheme.TextRole.Display);
            moneyText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(moneyText.rectTransform, 16f, 0f, 150f, 30f);
            rankText = HudKit.AddText(info, "", 12f, theme.ink, true, TextAlignmentOptions.MidlineRight, "Rank", UITheme.TextRole.Body);
            rankText.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(rankText.rectTransform, 160f, 0f, 150f, 30f);

            // where the player is now (HubArea), right of 「本部」
            var loc = HudKit.NewRect("Location", root);
            UILayout.Place(loc, 392f, 20f, 260f, 46f);
            HudKit.AddImage(loc, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .78f));
            loc.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 14f);
            var pin = HudKit.AddText(loc, "◎", 14f, theme.acc, true, TextAlignmentOptions.Center, "Pin", UITheme.TextRole.Display);
            HudKit.UseSymbolFont(pin);
            UILayout.Place(pin.rectTransform, 12f, 0f, 24f, 46f);
            locationText = HudKit.AddText(loc, "", 16f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Name", UITheme.TextRole.Title);
            locationText.textWrappingMode = TextWrappingModes.NoWrap; locationText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(locationText.rectTransform, 42f, 0f, 200f, 46f);
        }

        TMP_Text locationText;
        string location;

        /// <summary>The place name next to 「本部」 (only redrawn when it changes).</summary>
        public void SetLocation(string name)
        {
            if (name == location) return;
            location = name;
            locationText.SetText(string.IsNullOrEmpty(name) ? "—" : name);
        }

        HubMinimap minimap;

        /// <summary>The minimap, top right (once, from the scene's walls).</summary>
        public void BuildMinimap(IList<Bounds> walls, IList<HubNpc> npcs, IList<HubArea> areas, Transform player)
        {
            if (minimap != null) HudKit.DestroySafe(minimap.gameObject);
            minimap = HubMinimap.Build(root, walls, npcs, areas, player);
            minimap.transform.SetSiblingIndex(labelsRoot.GetSiblingIndex() + 1);   // under the panels
        }

        void BuildPrompt()
        {
            var theme = UITheme.Current;
            prompt = HudKit.NewRect("Prompt", root);
            prompt.anchorMin = prompt.anchorMax = prompt.pivot = new Vector2(.5f, 0f);
            prompt.anchoredPosition = new Vector2(0f, 150f);
            prompt.sizeDelta = new Vector2(380f, 44f);
            HudKit.AddImage(prompt, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .9f));
            prompt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 12f);
            var key = HudKit.NewRect("Key", prompt);
            UILayout.Place(key, 14f, 7f, 30f, 30f);
            HudKit.AddImage(key, theme.blue);
            var kt = HudKit.AddText(key, "F", 16f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Display);
            HudKit.Stretch(kt.rectTransform);
            promptText = HudKit.AddText(prompt, "", 15f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Body);
            promptText.textWrappingMode = TextWrappingModes.NoWrap; promptText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(promptText.rectTransform, 56f, 0f, 310f, 44f);
            prompt.gameObject.SetActive(false);

            // an operator: 「G 話す」 under it
            talkPrompt = HudKit.NewRect("TalkPrompt", root);
            talkPrompt.anchorMin = talkPrompt.anchorMax = talkPrompt.pivot = new Vector2(.5f, 0f);
            talkPrompt.anchoredPosition = new Vector2(0f, 110f);
            talkPrompt.sizeDelta = new Vector2(380f, 34f);
            HudKit.AddImage(talkPrompt, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .8f));
            talkPrompt.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
            var gk = HudKit.NewRect("Key", talkPrompt);
            UILayout.Place(gk, 14f, 5f, 24f, 24f);
            HudKit.AddImage(gk, theme.paper2);
            var gt = HudKit.AddText(gk, "G", 13f, theme.ink, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Display);
            HudKit.Stretch(gt.rectTransform);
            var tt = HudKit.AddText(talkPrompt, "話す", 13f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Body);
            tt.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(tt.rectTransform, 50f, 0f, 300f, 34f);
            talkPrompt.gameObject.SetActive(false);
        }

        void BuildHint()
        {
            var theme = UITheme.Current;
            var h = HudKit.AddText(root, "WASD 移動 · Shift 走る · マウス 視点 · F 調べる · G 話す · ESC メニュー", 11f, Color.Lerp(theme.dim, Color.white, .45f), true, TextAlignmentOptions.BottomLeft, "Hint", UITheme.TextRole.Body);
            h.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = h.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(24f, 20f);
            rt.sizeDelta = new Vector2(520f, 20f);
        }

        /// <summary>A floating label for every station (made once).</summary>
        public void SetStations(IList<HubNpc> npcs)
        {
            foreach (var l in labels) if (l.rt != null) HudKit.DestroySafe(l.rt.gameObject);
            labels.Clear();
            foreach (var n in npcs)
            {
                if (n == null) continue;
                var l = MakeLabel(n.name, n.displayName, n.caption);
                l.npc = n;
                labels.Add(l);
            }
        }

        /// <summary>A floating name for every room sign (made once, after SetStations; shown within SignRange).</summary>
        public void SetSigns(IList<HubSign> signs)
        {
            var theme = UITheme.Current;
            foreach (var s in signs)
            {
                if (s == null) continue;
                var l = MakeLabel(s.name, s.displayName, s.caption);
                l.sign = s.transform;
                l.caption.color = Color.Lerp(theme.acc, Color.white, .2f);
                labels.Add(l);
            }
        }

        public const float SignRange = 11f;

        Label MakeLabel(string id, string name, string caption)
        {
            var theme = UITheme.Current;
            var rt = HudKit.NewRect("Label_" + id, labelsRoot);
            rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(.5f, 0f);
            rt.sizeDelta = new Vector2(220f, 42f);
            var bg = HudKit.NewRect("DecoPlate", rt);
            UILayout.Place(bg, 30f, 0f, 160f, 42f);
            HudKit.AddImage(bg, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .72f));
            bg.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
            var nm = HudKit.AddText(rt, name, 15f, Color.white, true, TextAlignmentOptions.Bottom, "Name", UITheme.TextRole.Title);
            nm.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(nm.rectTransform, 0f, 2f, 220f, 20f);
            var cp = HudKit.AddText(rt, caption, 11f, Color.Lerp(theme.blue, Color.white, .25f), true, TextAlignmentOptions.Top, "Caption", UITheme.TextRole.Body);
            cp.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(cp.rectTransform, 0f, 23f, 220f, 16f);
            return new Label { rt = rt, name = nm, caption = cp };
        }

        /// <summary>Previews: the prompt as if a station were in reach.</summary>
        public void PosePrompt(string text, bool talkToo = false)
        {
            prompt.gameObject.SetActive(true); promptText.SetText(text);
            talkPrompt.gameObject.SetActive(talkToo);
        }

        /// <summary>Previews: a floating label at `pos` (canvas units from the bottom left).</summary>
        public void PoseLabel(string name, string caption, Vector2 pos)
        {
            var l = MakeLabel(name, name, caption);
            l.rt.anchoredPosition = pos;
        }

        // ---- per frame (no allocation) -----------------------------------------------------------------------------------------
        /// <summary>Moves the labels over their stations; `near` gets the prompt (null: none).</summary>
        public void Tick(HubNpc near)
        {
            bool show = near != null && !AnyOpen;
            if (prompt.gameObject.activeSelf != show) prompt.gameObject.SetActive(show);
            bool talkShow = show && near.CanTalk && near.station != HubStation.Talk;
            if (talkPrompt.gameObject.activeSelf != talkShow) talkPrompt.gameObject.SetActive(talkShow);
            if (show && lastPromptNpc != near) promptText.SetText(PromptFor(near));
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            var camPos = cam.transform.position;
            // the minimap's corner (labels under it are hidden: it is see-through)
            var full = root.rect;
            float mapX = full.width - HubMinimap.Right - HubMinimap.Size - 110f, mapY = full.height - HubMinimap.Top - HubMinimap.Size - 42f;
            bool hasMap = minimap != null;
            for (int i = 0; i < labels.Count; i++)
            {
                var l = labels[i];
                if (l.npc == null && l.sign == null) continue;
                var p = l.npc != null ? l.npc.LabelPoint : l.sign.position;
                float range = l.npc != null ? LabelRange : SignRange;
                var sp = cam.WorldToScreenPoint(p);
                var at = new Vector2(sp.x / scale, sp.y / scale);
                bool vis = !AnyOpen && sp.z > .2f && (p - camPos).sqrMagnitude < range * range
                           && !(hasMap && at.x > mapX && at.y > mapY);   // (a label is 220 wide, centred; 42 high from its foot)
                if (l.rt.gameObject.activeSelf != vis) l.rt.gameObject.SetActive(vis);
                if (!vis) continue;
                l.rt.anchoredPosition = at;
                float a = l.npc == null || l.npc == near ? 1f : .78f;
                if (l.name.alpha != a) l.name.alpha = a;
            }
        }

        string lastPrompt;
        HubNpc lastPromptNpc;

        string PromptFor(HubNpc n)
        {
            if (n == lastPromptNpc) return lastPrompt;
            lastPromptNpc = n;
            lastPrompt = n.station == HubStation.Talk ? "話す — " + n.displayName : n.caption + " — " + n.displayName;
            return lastPrompt;
        }

        // ---- stations ----------------------------------------------------------------------------------------------------------
        /// <summary>F: the NPC's station (a talk-only NPC talks).</summary>
        public void Open(HubNpc n)
        {
            if (n == null || AnyOpen) return;
            if (n.station == HubStation.Talk) { OpenTalk(n); return; }
            OpenStation(n.station);
        }

        /// <summary>G: an operator's talk.</summary>
        public void OpenTalk(HubNpc n)
        {
            if (n == null || AnyOpen || !n.CanTalk) return;
            talk.Open(BuildAssetRegistry.Operator(n.operatorKey), HubCatalog.Lines(n.operatorKey));
        }

        /// <summary>A station's panel by kind (tab `tab`; previews too). The old Growth / Loadout stations open the management panel.</summary>
        public void OpenStation(HubStation st, int tab = 0)
        {
            switch (st)
            {
                case HubStation.Shop: list.Open(shopC, tab); break;
                case HubStation.Medical: list.Open(medC, tab); break;
                case HubStation.Training: list.Open(trainC, tab); break;
                case HubStation.Operations: list.Open(opsC, tab); break;
                case HubStation.Management: list.Open(ignisC, tab); break;                       // IGNIS: ロードアウト / 装備購入 first
                case HubStation.Loadout: list.Open(loadC, tab); break;
                case HubStation.Growth: list.Open(squadC, tab); break;
            }
        }

        public void OpenGrowth(OperatorData op)
        {
            if (op == null) return;
            growth.Open(op, SaveSystem.Current ?? (Application.isPlaying ? null : previewData));
        }
        bool growthWasOpen;

        void Update()
        {
            // the growth panel closed: the squad list under it shows the new points
            bool g = growth != null && growth.IsOpen;
            if (growthWasOpen && !g && list.IsOpen) list.Refresh();
            growthWasOpen = g;
        }

        public void RefreshTopbar()
        {
            var d = Data;
            if (SaveSystem.Current == null && Application.isPlaying) { moneyText.SetText("¥ —"); rankText.SetText("—"); return; }
            moneyText.SetText(HubContentBase.Yen("", d.money));
            rankText.SetText(Blindside.Progress.RankTable.Name(Blindside.Progress.RankTable.RankOf(d.rankPoints)));
        }

        public void Sortie(HubCatalog.Operation o)
        {
            if (leaving || o == null) return;
            leaving = true;
            AutoSave.Sortie(OperatorTeam.Attack, o.stem);
            var req = new Blindside.Flow.LaunchRequest
            {
                team = OperatorTeam.Attack,
                // the house: the default map; a catalog map with its own scene: that scene's map; without one: its data here
                map = o.entry == null ? Blindside.Map.MapLibrary.LoadDefault() : (o.scene == null ? MapOf(o.entry) : null),
                sceneName = o.scene,
            };
            if (!Application.isPlaying) return;
            KitWipe.Play(() => Blindside.Flow.SceneFlow.GoBattle(req));
        }

        static Blindside.Map.MapData MapOf(Blindside.Flow.MapCatalog.Entry e)
        {
            if (e == null || e.mapJson == null) return Blindside.Map.MapLibrary.LoadDefault();
            if (!Blindside.MapEditor.MapFile.TryParse(e.mapJson.text, out var file, out var why)) { Debug.LogError("[Hub] " + e.id + ": " + why); return Blindside.Map.MapLibrary.LoadDefault(); }
            try { return file.ToModel().BuildGameData(); }
            catch (System.Exception ex) { Debug.LogError("[Hub] " + e.id + ": " + ex.Message); return Blindside.Map.MapLibrary.LoadDefault(); }
        }

        /// <summary>ESC: the HQ menu (when nothing else is open).</summary>
        public void OpenMenu()
        {
            if (AnyOpen) return;
            menu.Open();
        }

        void SaveNow()
        {
            if (SaveSystem.Current != null) SaveSystem.Write(false);   // (the SAVE notice)
        }

        /// <summary>The menu's タイトルへ: saved, then the title.</summary>
        void ToTitle()
        {
            if (leaving) return;
            leaving = true;
            if (SaveSystem.Current != null) SaveSystem.Write(true, notify: false);
            if (!Application.isPlaying) return;
            KitWipe.Play(Blindside.Flow.SceneFlow.GoTitle);
        }
    }
}
