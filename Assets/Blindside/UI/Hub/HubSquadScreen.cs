// The sortie's squad (menu canvas 1440 × 810): an operation that lets the player pick who goes (MapCatalog.Entry.squadSize) opens this
// between the quest and the wipe — over a low-angle 3D lineup (HubSquadStage: the squad standing in a wedge in a hangar at dusk, the
// leader at the point).
//   Stage         the lineup's render, full screen; DecoTop / DecoBottom ink gradients so the head and the plates read on it
//   Head (40, 34) "SQUAD" 12 display blue, spaced; 「出撃メンバー編成」 30 title; the operation · 出撃 N名 14 (dim)
//   Slot numbers  over each figure's head: 1 … N, 22 display (the chosen slot blue)
//   Plates        under each figure's feet (projected from the stage): 210 × 62 — the codename 20 display, kana · role 11, a line 11
//                 (FLARE: 隊長 · 固定 / the others: コープ Rank N); the chosen one blue. A click chooses that slot
//   Roster (40, 150, 360 wide)  while a slot (not the leader's) is chosen: 「隊員を選ぶ — 枠 N」, a row 54 per operator who can go
//                 (face 42, codename 18, role 11, 編成中 (枠 k) / 出撃可 right); a click puts it in the slot (swapping with the slot it was in)
//   Footer        hint 12 (dim) bottom left · 「おまかせ編成」 200 × 44 · 「出撃」 260 × 56 (blue) bottom right · 「戻る」 ESC
// FLARE (SquadPick.Leader) always goes and stands at the point; a captive of the operation (QUIETPLACE's GHOST / SILENCE) can't be picked.
// Motion (unscaled; ITimedEffect.Evaluate(t) = the opening): the camera pushes in and rises (the stage), the head and the roster slide in
// from −30 and fade in .45 s, the plates rise 14 and fade in from .25. Nothing is allocated per frame (the plates follow the camera
// through a cached Vector3 each frame).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.Flow;
using Blindside.Hub;
using Blindside.Progress;
using Blindside.Save;

namespace Blindside.UI
{
    public class HubSquadScreen : MonoBehaviour, ITimedEffect
    {
        public const float ListX = 40f, ListY = 150f, ListW = 360f, RowH = 54f, RowGap = 6f;
        public const float PlateW = 196f, PlateH = 62f, PlateDrop = 14f, NumberLift = 4f, PlateGap = 8f;
        // the slots left to right in the picture (the wedge: 4 2 1 3 5)
        static readonly int[] ByX = { 3, 1, 0, 2, 4 };
        readonly float[] plateX = new float[HubSquadStage.MaxSlots];
        readonly int[] plateSlot = new int[HubSquadStage.MaxSlots];
        public const float InTime = .45f, PlateDelay = .25f, PlateTime = .4f, SlideIn = 30f, PlateRise = 14f;

        HubScreen screen;
        HubSquadStage stage;
        RectTransform root, head, roster;
        RawImage stageImage;
        CanvasGroup headG, rosterG;
        TMP_Text sub, hint;
        readonly RectTransform[] plates = new RectTransform[HubSquadStage.MaxSlots];
        readonly CanvasGroup[] plateG = new CanvasGroup[HubSquadStage.MaxSlots];
        readonly TMP_Text[] numbers = new TMP_Text[HubSquadStage.MaxSlots];
        readonly List<GameObject> rows = new List<GameObject>(12);
        readonly List<string> picks = new List<string>(HubSquadStage.MaxSlots);
        readonly List<string> barred = new List<string>(4);
        Vector2 headPos, rosterPos;
        int size = 3, chosen = 1;
        float clock = 99f, plateClock = 99f;
        Quest quest;
        System.Action<string[]> onGo;
        System.Action onCancel;

        public bool IsOpen => gameObject.activeSelf;
        public float Duration => InTime;
        SaveData D => screen.Data;

        public static HubSquadScreen Build(Transform parent, HubScreen screen)
        {
            var theme = UITheme.Current;
            var r = HudKit.NewRect("HubSquad", parent);
            HudKit.Stretch(r);
            var s = r.gameObject.AddComponent<HubSquadScreen>();
            s.screen = screen; s.root = r;

            var st = HudKit.NewRect("Stage", r);
            HudKit.Stretch(st);
            s.stageImage = st.gameObject.AddComponent<RawImage>();
            s.stageImage.raycastTarget = true;   // (takes the clicks off the HQ under it; a click on a figure chooses its slot)
            st.gameObject.AddComponent<StageClick>().owner = s;

            Band(r, "DecoTop", 0f, 200f, .8f, 0f, theme.ink);
            Band(r, "DecoBottom", 600f, 210f, 0f, .85f, theme.ink);

            s.head = HudKit.NewRect("Head", r);
            UILayout.Place(s.head, 0f, 0f, 700f, 130f);
            s.headG = s.head.gameObject.AddComponent<CanvasGroup>();
            s.headPos = s.head.anchoredPosition;
            var en = HudKit.AddText(s.head, "SQUAD", 12f, theme.blue, true, TextAlignmentOptions.TopLeft, "En", UITheme.TextRole.Display);
            en.characterSpacing = 10f;
            UILayout.Place(en.rectTransform, ListX, 34f, 300f, 16f);
            var title = HudKit.AddText(s.head, "出撃メンバー編成", 30f, Color.white, true, TextAlignmentOptions.TopLeft, "Title", UITheme.TextRole.Title);
            UILayout.Place(title.rectTransform, ListX, 52f, 500f, 40f);
            s.sub = HudKit.AddText(s.head, "", 14f, Color.Lerp(theme.dim, Color.white, .45f), false, TextAlignmentOptions.TopLeft, "Sub", UITheme.TextRole.Body);
            UILayout.Place(s.sub.rectTransform, ListX + 2f, 96f, 640f, 20f);

            s.roster = HudKit.NewRect("Roster", r);
            UILayout.Place(s.roster, 0f, 0f, ListX + ListW + 20f, 700f);
            s.rosterG = s.roster.gameObject.AddComponent<CanvasGroup>();
            s.rosterPos = s.roster.anchoredPosition;

            for (int i = 0; i < HubSquadStage.MaxSlots; i++) s.BuildPlate(i);

            s.hint = HudKit.AddText(r, "", 12f, theme.dim, false, TextAlignmentOptions.MidlineLeft, "Hint", UITheme.TextRole.Body);
            s.hint.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(s.hint.rectTransform, ListX, 752f, 560f, 40f);

            s.Button("Back", "戻る", 1440f - 40f - 260f - 12f - 200f - 12f - 140f, 750f, 140f, 44f, 15f, theme.ink, s.Back);
            s.Button("Auto", "おまかせ編成", 1440f - 40f - 260f - 12f - 200f, 750f, 200f, 44f, 15f, theme.ink2, s.AutoFill);
            s.Button("Go", "出撃", 1440f - 40f - 260f, 744f, 260f, 56f, 22f, theme.blue, s.Go);

            r.gameObject.SetActive(false);
            return s;
        }

        // a vertical fade in steps (alpha `a0` at the top to `a1` at the bottom)
        static void Band(RectTransform parent, string name, float y, float h, float a0, float a1, Color c)
        {
            const int Steps = 8;
            var band = HudKit.NewRect(name, parent);
            UILayout.Place(band, 0f, y, 1440f, h);
            band.anchorMin = new Vector2(0f, 1f); band.anchorMax = new Vector2(1f, 1f);
            band.sizeDelta = new Vector2(0f, h);
            for (int i = 0; i < Steps; i++)
            {
                var s = HudKit.NewRect(name + i, band);
                s.anchorMin = new Vector2(0f, 1f - (i + 1f) / Steps); s.anchorMax = new Vector2(1f, 1f - (float)i / Steps);
                s.offsetMin = s.offsetMax = Vector2.zero;
                HudKit.AddImage(s, new Color(c.r, c.g, c.b, Mathf.Lerp(a0, a1, (i + .5f) / Steps)));
            }
        }

        void Button(string name, string label, float x, float y, float w, float h, float fs, Color c, UnityEngine.Events.UnityAction click)
        {
            var b = HudKit.NewRect(name, root);
            UILayout.Place(b, x, y, w, h);
            var img = HudKit.AddImage(b, c, raycast: true);
            b.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 16f);
            var btn = b.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None; btn.targetGraphic = img;
            btn.onClick.AddListener(click);
            b.gameObject.AddComponent<KitBrighten>().image = img;
            var t = HudKit.AddText(b, label, fs, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Title);
            HudKit.Stretch(t.rectTransform);
        }

        void BuildPlate(int i)
        {
            var theme = UITheme.Current;
            var p = HudKit.NewRect("Plate" + (i + 1), root);
            UILayout.Place(p, 0f, 0f, PlateW, PlateH);
            p.pivot = new Vector2(.5f, 1f);
            plateG[i] = p.gameObject.AddComponent<CanvasGroup>();
            var img = HudKit.AddImage(p, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .88f), raycast: true);
            p.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
            var bar = HudKit.NewRect("DecoBar", p);
            UILayout.Place(bar, 0f, 0f, 4f, PlateH);
            HudKit.AddImage(bar, theme.blue);
            var n = HudKit.AddText(p, "", 20f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Display);
            UILayout.Place(n.rectTransform, 16f, 6f, PlateW - 26f, 24f);
            var k = HudKit.AddText(p, "", 11f, Color.Lerp(theme.dim, Color.white, .45f), false, TextAlignmentOptions.TopLeft, "Kana", UITheme.TextRole.Body);
            k.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(k.rectTransform, 16f, 31f, PlateW - 26f, 14f);
            var l = HudKit.AddText(p, "", 11f, theme.blue, true, TextAlignmentOptions.TopLeft, "Line", UITheme.TextRole.Body);
            UILayout.Place(l.rectTransform, 16f, 45f, PlateW - 26f, 14f);
            int slot = i;
            var b = p.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None; b.targetGraphic = img;
            b.onClick.AddListener(() => Choose(slot));
            plates[i] = p;
            // the slot number over the head (the lineup's 1 2 3 …)
            var num = HudKit.AddText(root, (i + 1).ToString(), 22f, Color.white, true, TextAlignmentOptions.Center, "Number" + (i + 1), UITheme.TextRole.Display);
            UILayout.Place(num.rectTransform, 0f, 0f, 40f, 28f);
            num.rectTransform.pivot = new Vector2(.5f, 0f);
            numbers[i] = num;
        }

        // ---- open / close ------------------------------------------------------------------------------------------------------
        /// <summary>The squad for `q` (its entry's squadSize): `go` gets the picked keys (FLARE first) on 出撃, `cancel` on 戻る / ESC.</summary>
        public void Open(Quest q, System.Action<string[]> go, System.Action cancel)
        {
            quest = q; onGo = go; onCancel = cancel;
            var e = q != null ? q.entry : null;
            size = Mathf.Clamp(e != null && e.squadSize > 0 ? e.squadSize : 3, 1, HubSquadStage.MaxSlots);
            // the operation's captives are held there: not in the squad
            barred.Clear();
            var data = e != null ? MapCatalog.DataOf(e) : null;
            if (data != null && data.rescueIntro) foreach (var c in data.RescueRoster()) barred.Add(c.operatorKey);
            picks.Clear();
            if (e != null) foreach (var k in SquadPick.Remembered(e)) if (!barred.Contains(k)) picks.Add(k);
            Fill();

            if (stage == null) stage = HubSquadStage.Build();
            if (Application.isPlaying) stage.FitScreen(Screen.width, Screen.height);
            stageImage.texture = stage.Texture;
            stage.SetLive(Application.isPlaying);
            stage.SetSlots(size);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            sub.SetText((q != null ? q.name : "") + "  ·  出撃 " + size + "名");
            chosen = size > 1 ? 1 : 0;
            Refresh();
            bool play = Application.isPlaying && !UIMotionSettings.reduceMotion;
            stage.PlayEntrance();
            if (!play) stage.Evaluate(99f);
            clock = play ? 0f : 99f;
            plateClock = play ? -PlateDelay : 99f;
            Evaluate(clock); EvaluatePlates(plateClock);
        }

        public void Close()
        {
            if (stage != null) stage.SetLive(false);
            gameObject.SetActive(false);
        }

        void OnDestroy() { if (stage != null) HudKit.DestroySafe(stage.gameObject); }

        /// <summary>Previews: `q` open, slot `slot` chosen, everything in, drawn now.</summary>
        public void Pose(Quest q, int slot)
        {
            Open(q, null, null);
            Choose(Mathf.Clamp(slot, 0, size - 1));
            clock = plateClock = 99f;
            Evaluate(99f); EvaluatePlates(99f);
            stage.Evaluate(99f);
            Canvas.ForceUpdateCanvases();
            stage.RenderNow();
            Follow();
        }

        // FLARE first, then the picks (no captives, no repeats), filled up from the roster in order
        void Fill()
        {
            var list = new List<string> { SquadPick.Leader };
            foreach (var k in picks) if (!list.Contains(k) && !barred.Contains(k) && list.Count < size) list.Add(k);
            foreach (var op in HubCatalog.Squad)
                if (op != null && list.Count < size && !list.Contains(op.key) && !barred.Contains(op.key)) list.Add(op.key);
            picks.Clear(); picks.AddRange(list);
        }

        void Update()
        {
            if (Application.isPlaying && stage != null && stage.FitScreen(Screen.width, Screen.height)) stageImage.texture = stage.Texture;
            float dt = Time.unscaledDeltaTime;
            if (clock < InTime) { clock += dt; Evaluate(clock); }
            if (plateClock < PlateTime) { plateClock += dt; EvaluatePlates(plateClock); }
            Follow();
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed) { EscapeGate.Consume(); Back(); }
        }

        // the plates and numbers follow the figures in the picture (the camera moves on the way in)
        void Follow()
        {
            if (stage == null || stage.View == null) return;
            var cam = stage.View;
            var rect = root.rect;
            // one row along the front plates' line, each under its figure, pushed apart left to right where they would touch
            float rowY = 0f, rise = PlateRise * (1f - plateK);
            int n = 0;
            for (int o = 0; o < ByX.Length; o++)
            {
                int i = ByX[o];
                if (i >= size) continue;
                var foot = cam.WorldToViewportPoint(stage.SlotFoot(i));
                plateX[n] = foot.x * rect.width; plateSlot[n] = i; n++;
                rowY = Mathf.Min(rowY, -(1f - foot.y) * rect.height - PlateDrop);
                var top = cam.WorldToViewportPoint(stage.SlotHead(i));
                var np = new Vector2(top.x * rect.width, -(1f - top.y) * rect.height + NumberLift);
                if (numbers[i].rectTransform.anchoredPosition != np) numbers[i].rectTransform.anchoredPosition = np;
            }
            for (int k = 1; k < n; k++) plateX[k] = Mathf.Max(plateX[k], plateX[k - 1] + PlateW + PlateGap);
            float over = n > 0 ? plateX[n - 1] + PlateW * .5f - (rect.width - 16f) : 0f;
            rowY = Mathf.Max(rowY, -(rect.height - PlateH - 70f)) - rise;   // (kept above the footer; rising into place)
            for (int k = 0; k < n; k++)
            {
                var pp = new Vector2(plateX[k] - Mathf.Max(0f, over), rowY);
                if (plates[plateSlot[k]].anchoredPosition != pp) plates[plateSlot[k]].anchoredPosition = pp;
            }
        }

        // ---- choosing ------------------------------------------------------------------------------------------------------------
        /// <summary>A click on the lineup: the slot whose figure is under it (between its feet and its head, within FigureHalfW of it).</summary>
        class StageClick : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
        {
            public HubSquadScreen owner;
            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e) { if (owner != null) owner.ClickStage(e); }
        }

        public const float FigureHalfW = 70f;

        void ClickStage(UnityEngine.EventSystems.PointerEventData e)
        {
            if (stage == null || stage.View == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(stageImage.rectTransform, e.position, e.pressEventCamera, out var lp)) return;
            var rect = stageImage.rectTransform.rect;
            var vp = new Vector2((lp.x - rect.xMin) / rect.width, (lp.y - rect.yMin) / rect.height);
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < size; i++)
            {
                var foot = stage.View.WorldToViewportPoint(stage.SlotFoot(i));
                var top = stage.View.WorldToViewportPoint(stage.SlotHead(i));
                if (vp.y < foot.y - .02f || vp.y > top.y + .02f) continue;
                float dx = Mathf.Abs(vp.x - (foot.x + top.x) * .5f) * rect.width;
                if (dx <= FigureHalfW && dx < bestD) { bestD = dx; best = i; }
            }
            if (best >= 0) Choose(best);
        }

        void Choose(int slot)
        {
            chosen = slot;
            Refresh();
        }

        /// <summary>Puts `key` in the chosen slot (it swaps with the slot it was already in).</summary>
        void Put(string key)
        {
            if (chosen <= 0 || chosen >= size || barred.Contains(key) || key == SquadPick.Leader) return;
            int was = picks.IndexOf(key);
            if (was == chosen) return;
            if (was > 0) picks[was] = picks[chosen];
            picks[chosen] = key;
            Refresh();
        }

        /// <summary>おまかせ編成: FLARE and the highest co-op ranks (the roster's order on a tie).</summary>
        void AutoFill()
        {
            var cands = new List<OperatorData>();
            foreach (var op in HubCatalog.Squad) if (op != null && op.key != SquadPick.Leader && !barred.Contains(op.key)) cands.Add(op);
            var d = D;
            var order = new List<OperatorData>(cands);
            order.Sort((a, b) => { int c = Campaign.Rank(d, b.key).CompareTo(Campaign.Rank(d, a.key)); return c != 0 ? c : cands.IndexOf(a).CompareTo(cands.IndexOf(b)); });
            picks.Clear(); picks.Add(SquadPick.Leader);
            foreach (var op in order) if (picks.Count < size) picks.Add(op.key);
            Fill();
            Refresh();
        }

        void Go()
        {
            if (quest != null && quest.entry != null) SquadPick.Set(quest.entry.id, picks);
            var keys = picks.ToArray();
            var go = onGo;
            Close();
            go?.Invoke(keys);
        }

        void Back()
        {
            var c = onCancel;
            Close();
            c?.Invoke();
        }

        // ---- drawing -------------------------------------------------------------------------------------------------------------
        void Refresh()
        {
            var theme = UITheme.Current;
            stage.Choose(chosen);
            for (int i = 0; i < HubSquadStage.MaxSlots; i++)
            {
                bool on = i < size;
                plates[i].gameObject.SetActive(on);
                numbers[i].gameObject.SetActive(on);
                if (!on) continue;
                var op = i < picks.Count ? BuildAssetRegistry.Operator(picks[i]) : null;
                stage.Show(i, op);
                bool sel = i == chosen;
                plates[i].GetComponent<Image>().color = sel ? theme.blue : new Color(theme.ink.r, theme.ink.g, theme.ink.b, .88f);
                plates[i].Find("DecoBar").GetComponent<Image>().color = sel ? Color.white : op != null ? op.color : theme.dim;
                plates[i].Find("Name").GetComponent<TMP_Text>().SetText(op != null ? op.DisplayName : "—");
                plates[i].Find("Kana").GetComponent<TMP_Text>().SetText(op != null ? op.kr + "  ·  " + op.role : "");
                var line = plates[i].Find("Line").GetComponent<TMP_Text>();
                line.SetText(i == 0 ? "隊長  ·  固定" : op != null ? "コープ Rank " + Campaign.Rank(D, op.key) : "");
                line.color = sel ? Color.white : i == 0 ? theme.acc : theme.blue;
                numbers[i].color = sel ? theme.blue : Color.white;
            }
            BuildRoster();
            hint.SetText(chosen == 0 ? "フレアは固定メンバーです · 入れ替える枠を選んでください · ESC 戻る"
                : "枠を選んで隊員を入れ替え · 「出撃」で作戦開始 · ESC 戻る");
            if (!Application.isPlaying && stage != null) { Canvas.ForceUpdateCanvases(); stage.RenderNow(); }
        }

        void BuildRoster()
        {
            foreach (var g in rows) HudKit.DestroySafe(g);
            rows.Clear();
            if (chosen <= 0) return;
            var theme = UITheme.Current;
            float y = ListY;
            var tag = HudKit.AddText(roster, "隊員を選ぶ — 枠 " + (chosen + 1), 12f, theme.blue, true, TextAlignmentOptions.BottomLeft, "Tag", UITheme.TextRole.Body);
            UILayout.Place(tag.rectTransform, ListX + 2f, y, ListW, 18f);
            rows.Add(tag.gameObject);
            y += 24f;
            foreach (var op in HubCatalog.Squad)
            {
                if (op == null || op.key == SquadPick.Leader || barred.Contains(op.key)) continue;
                var o = op;
                int at = picks.IndexOf(op.key);
                bool here = at == chosen;
                var row = HudKit.NewRect("Op_" + op.key, roster);
                UILayout.Place(row, ListX, y, ListW, RowH);
                var baseC = here ? theme.blue : new Color(theme.ink2.r, theme.ink2.g, theme.ink2.b, .92f);
                var img = HudKit.AddImage(row, baseC, raycast: true);
                row.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
                var bar = HudKit.NewRect("DecoBar", row);
                UILayout.Place(bar, 0f, 0f, 4f, RowH);
                HudKit.AddImage(bar, here ? Color.white : op.color);
                var f = HudKit.NewRect("Face", row);
                UILayout.Place(f, 14f, 6f, 42f, 42f);
                HudKit.AddImage(f, Color.Lerp(theme.paper2, theme.ink, .15f));
                f.gameObject.AddComponent<RectMask2D>();
                var ph = HudKit.NewRect("Photo", f); HudKit.Stretch(ph);
                var im = ph.gameObject.AddComponent<Image>(); im.sprite = OperatorArt.Face(op.key); im.preserveAspect = true; im.raycastTarget = false;
                im.enabled = im.sprite != null;
                var n = HudKit.AddText(row, op.DisplayName, 18f, Color.white, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Display);
                UILayout.Place(n.rectTransform, 66f, 7f, 160f, 24f);
                var rl = HudKit.AddText(row, op.role, 11f, here ? Color.white : theme.dim, false, TextAlignmentOptions.TopLeft, "Role", UITheme.TextRole.Body);
                rl.overflowMode = TextOverflowModes.Ellipsis;
                UILayout.Place(rl.rectTransform, 66f, 32f, 160f, 16f);
                var st = HudKit.AddText(row, at > 0 ? "編成中 (枠 " + (at + 1) + ")" : "出撃可", 12f, here ? Color.white : at > 0 ? theme.blue : Color.Lerp(theme.dim, Color.white, .3f), true, TextAlignmentOptions.MidlineRight, "State", UITheme.TextRole.Body);
                UILayout.Place(st.rectTransform, 226f, 0f, ListW - 240f, RowH);
                var b = row.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None; b.targetGraphic = img;
                b.onClick.AddListener(() => Put(o.key));
                var hv = row.gameObject.AddComponent<HubHover>();
                hv.target = row; hv.graphic = img; hv.baseColor = baseC;
                hv.hoverColor = here ? baseC : Color.Lerp(baseC, theme.blue, .35f);
                hv.shift = new Vector2(10f, 0f); hv.scale = 1.02f;
                hv.grow = bar; hv.growFrom = 4f; hv.growTo = 8f;
                hv.SetBase(row.anchoredPosition);
                rows.Add(row.gameObject);
                y += RowH + RowGap;
            }
        }

        // ---- motion ----------------------------------------------------------------------------------------------------------------
        float plateK = 1f;

        public void Evaluate(float t)
        {
            float k = UIMotionSettings.reduceMotion ? 1f : UIEase.Tactical(Mathf.Clamp01(t / InTime));
            var off = new Vector2(-SlideIn * (1f - k), 0f);
            if (head.anchoredPosition != headPos + off) head.anchoredPosition = headPos + off;
            if (roster.anchoredPosition != rosterPos + off) roster.anchoredPosition = rosterPos + off;
            if (headG.alpha != k) headG.alpha = k;
            if (rosterG.alpha != k) rosterG.alpha = k;
        }

        void EvaluatePlates(float t)
        {
            plateK = UIMotionSettings.reduceMotion ? 1f : UIEase.Tactical(Mathf.Clamp01(t / PlateTime));
            for (int i = 0; i < HubSquadStage.MaxSlots; i++)
            {
                if (plateG[i].alpha != plateK) plateG[i].alpha = plateK;
                if (numbers[i].alpha != plateK) numbers[i].alpha = plateK;
            }
        }
    }
}
