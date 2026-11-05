// The title's squad roster (#title .sqh + #squad .sq, ui_spec "타이틀 크기"), built from the kit at x720 y414 of the 1440×810 title
// canvas (under TITLE_Top's 372 + 8, where the old Right/SquadHeader stood):
//   Header  696×32, ink, slant 22: 投入隊員 14 (title role) + SQUAD ROSTER // 05 11 (display, #8b939c) on the right; fadeUp (+18,
//           transparent → in place, .5s after .95s) when the title opens
//   Row0…4  696×60, 5 apart from y38: paper, slant 22 (the stencil the row's parts are clipped to) —
//           Cb (the operator colour, 10 wide; 18 on the shown operator), Face 72×60 at x10 (SlantedMask 16, the face covering it from
//           the top), Name 21 display + Kana · role 12.5 (body, dim) on its baseline, Desc 12.5 one line with an ellipsis within 392,
//           Stats 196 at the right (ink2, only its left edge leaning 22): HP and MOV, each a label 12 (#9aa3ad), a SegmentedBar 88×8
//           (6 / 2) at HP ÷ 140 and MOV ÷ 10 in the operator colour, the value 12 ("120", "7m") right-aligned.
//           Hover: 14 to the left in .2s and the operator goes to the spotlight (unless already there); click: the same, then RowClicked
//           (the title opens the operator's growth panel, KitGrowthPanel). The shown
//           operator's row stays 14 to the left with the 18 band.
//           Entrance (sqIn): from +90 and transparent in .5s, row i after i × 65ms — when the title opens and whenever the side changes.
// The rows follow the title's state: Bind hooks TitleSelectScreen.Changed; a new side refills the rows in roster order (GetSquad: attack
// CRUSHER, FLARE, IGNIS, SERUM, BASTION / defense VOLT, MIASMA, WRAITH, OBSIDIAN, ARGUS), replays the entrance and resets the spotlight to
// the first operator with its glitch. Every motion is a tween on one clock (Evaluate(t)); Update advances it with unscaled time while
// something runs. Nothing is allocated per frame (texts and layout change only on a new side).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Blindside.Data;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Title Squad")]
    public class KitTitleSquad : MonoBehaviour, ITimedEffect
    {
        public const float X = 720f, Y = 414f, W = 696f, HeaderH = 32f, HeaderGap = 6f, RowH = 60f, RowGap = 5f, Rows = 5;
        public const float H = HeaderH + HeaderGap + 5 * RowH + 4 * RowGap;
        public const float FaceX = 10f, FaceW = 72f, InfoX = FaceX + FaceW + 12f, StatsW = 196f, InfoW = W - InfoX - 14f - StatsW;
        public const float BandW = 10f, BandPick = 18f, HoverShift = 14f, HoverTime = .2f;
        public const float EnterShift = 90f, EnterTime = .5f, EnterStep = .065f, HeaderDelay = .95f, HeaderRise = 18f;
        public const float HpMax = 140f, MovMax = 10f;
        const float Never = -100f;
        static readonly Color EmC = new Color32(0x8b, 0x93, 0x9c, 0xff);
        static readonly Color DescC = new Color32(0x2b, 0x30, 0x36, 0xff);
        static readonly Color StatLabelC = new Color32(0x9a, 0xa3, 0xad, 0xff);
        static readonly Color TrackC = new Color32(0x42, 0x45, 0x4A, 0xff);    // white .16 over ink2

        struct Tween
        {
            public float from, to, start, dur;
            public float At(float t) => Mathf.LerpUnclamped(from, to, UIEase.Tactical(dur > 0f ? (t - start) / dur : 1f));
            public float End => start + dur;
            public void Settle(float v) { from = to = v; start = 0f; dur = 0f; }
        }

        [System.Serializable]
        public class Row
        {
            public RectTransform block, body, band;
            public CanvasGroup group;
            public SlantedMask face;
            public TMP_Text name, kana, desc, hpValue, movValue;
            public SegmentedBar hp, mov;
        }

        public TitleSelectScreen screen;
        public KitSpotlight spotlight;
        public RectTransform header, headerBody;
        public CanvasGroup headerGroup;
        public Row[] rows = new Row[5];

        readonly List<OperatorData> squad = new List<OperatorData>(5);

        /// <summary>A row was clicked (after it went to the spotlight): the title opens that operator's growth panel.</summary>
        public event System.Action<OperatorData> RowClicked;
        Tween[] hoverT = new Tween[5], pickT = new Tween[5];
        float clock, enterStart = Never, headerStart = Never;
        bool running, bound;
        int selected = -1;
        OperatorTeam teamShown = (OperatorTeam)(-1);

        public float Duration
        {
            get
            {
                float d = Mathf.Max(enterStart + (rows.Length - 1) * EnterStep + EnterTime, headerStart + HeaderDelay + EnterTime);
                for (int i = 0; i < rows.Length; i++) d = Mathf.Max(d, Mathf.Max(hoverT[i].End, pickT[i].End));
                return d;
            }
        }

        /// <summary>The index of the operator in the spotlight (-1 = none).</summary>
        public int Selected => selected;

        // ---- the title's state -----------------------------------------------------------------------------------------------
        /// <summary>Hooks the roster to `title` (its Changed event) and `spot`, and draws the current side.</summary>
        public void Bind(TitleSelectScreen title, KitSpotlight spot)
        {
            Build();
            spotlight = spot;
            if (bound && screen == title) { Redraw(); return; }
            if (screen != null) screen.Changed -= Redraw;
            screen = title;
            screen.Changed += Redraw;
            bound = true;
            Redraw();
        }

        void OnDestroy()
        {
            if (screen != null) screen.Changed -= Redraw;
        }

        /// <summary>A new side: the rows refilled in roster order, their entrance replayed, the spotlight back on the first operator.</summary>
        public void Redraw()
        {
            if (screen == null) return;
            var team = screen.SelectedTeam;
            if (team == teamShown) return;
            bool first = (int)teamShown < 0;
            teamShown = team;
            screen.GetSquad(team, squad);
            for (int i = 0; i < rows.Length; i++)
            {
                bool has = i < squad.Count;
                if (rows[i].block.gameObject.activeSelf != has) rows[i].block.gameObject.SetActive(has);
                if (has) Fill(rows[i], squad[i]);
            }

            Rebase();
            selected = squad.Count > 0 ? 0 : -1;
            for (int i = 0; i < rows.Length; i++) { pickT[i].Settle(i == selected ? 1f : 0f); }
            if (!first && Application.isPlaying) enterStart = clock;
            if (spotlight != null && selected >= 0) spotlight.Show(squad[0], 0, team, !first && Application.isPlaying);
            Run();
        }

        /// <summary>Puts row `i`'s operator in the spotlight (glitch included) unless it is already there.</summary>
        public void Select(int i)
        {
            if (i < 0 || i >= squad.Count || i == selected) return;
            Rebase();
            for (int k = 0; k < rows.Length; k++)
            {
                float to = k == i ? 1f : 0f, from = pickT[k].At(clock);
                if (from == to && pickT[k].to == to) continue;
                pickT[k].from = from; pickT[k].to = to; pickT[k].start = clock; pickT[k].dur = Application.isPlaying ? HoverTime : 0f;
            }
            selected = i;
            if (spotlight != null) spotlight.Show(squad[i], i, teamShown, true);
            Run();
        }

        void Hover(int i, bool on)
        {
            Rebase();
            hoverT[i].from = hoverT[i].At(clock); hoverT[i].to = on ? 1f : 0f; hoverT[i].start = clock; hoverT[i].dur = HoverTime;
            Run();
            if (on) Select(i);
        }

        void Fill(Row r, OperatorData op)
        {
            r.band.GetComponent<Image>().color = op.color;
            r.face.SetPhoto(OperatorArt.Face(op.key));
            r.name.text = op.DisplayName;
            r.kana.text = op.kr + " · " + op.role;
            r.desc.text = op.description;
            int mov = op.mv * 3 + 1;                   // tiles of movement → metres (TitleSelectScreen.BuildSquadRow's rule)
            r.hpValue.text = op.hp.ToString();
            r.movValue.text = mov + "m";
            r.hp.SetColors(op.color, op.color, TrackC); r.hp.SetStat(op.hp, HpMax);
            r.mov.SetColors(op.color, op.color, TrackC); r.mov.SetStat(mov, MovMax);

            // b small{margin-left:8px}: the kana · role right after the name, on its baseline, within the column
            float nameW = Mathf.Ceil(r.name.GetPreferredValues(op.DisplayName).x) + 1f;
            UILayout.Place(r.name.rectTransform, InfoX, 5f, nameW, 26f);
            r.name.ForceMeshUpdate();
            r.kana.ForceMeshUpdate();
            float nameBase = BaselineFromTop(r.name), kanaBase = BaselineFromTop(r.kana);
            float kx = InfoX + nameW + 8f;
            UILayout.Place(r.kana.rectTransform, kx, 5f + nameBase - kanaBase, Mathf.Max(10f, InfoX + InfoW - kx), 16f);
        }

        // the first line's baseline, measured from the top of the text's rect
        static float BaselineFromTop(TMP_Text t)
        {
            var info = t.textInfo;
            if (info == null || info.lineCount == 0) return t.fontSize * .8f;
            return t.rectTransform.rect.yMax - info.lineInfo[0].baseline;
        }

        // ---- motion ------------------------------------------------------------------------------------------------------------
        void OnEnable()
        {
            Build();
            if (Application.isPlaying) PlayEnter();
        }

        /// <summary>fadeUp of the header and sqIn of the rows, as when the title opens.</summary>
        public void PlayEnter()
        {
            Rebase();
            enterStart = clock; headerStart = clock;
            Run();
        }

        void Rebase()
        {
            if (running) return;
            float c = clock;
            for (int i = 0; i < rows.Length; i++) { hoverT[i].Settle(hoverT[i].At(c)); pickT[i].Settle(pickT[i].At(c)); }
            enterStart = Never; headerStart = Never;
            clock = 0f;
        }

        void Run()
        {
            running = Application.isPlaying && isActiveAndEnabled;
            Evaluate(clock);
        }

        public void Evaluate(float t)
        {
            if (header == null) return;
            bool reduce = UIMotionSettings.reduceMotion;
            float hp = t - headerStart - HeaderDelay;
            float he = headerStart <= Never ? 1f : hp <= 0f ? 0f : reduce ? 1f : UIEase.Tactical(hp / EnterTime);
            if (headerGroup.alpha != he) headerGroup.alpha = he;
            var hpos = new Vector2(0f, -HeaderRise * (1f - he));
            if (headerBody.anchoredPosition != hpos) headerBody.anchoredPosition = hpos;

            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                float p = t - enterStart - i * EnterStep;
                float e = enterStart <= Never ? 1f : p <= 0f ? 0f : reduce ? 1f : UIEase.Tactical(p / EnterTime);
                if (r.group.alpha != e) r.group.alpha = e;
                float pick = Mathf.Clamp01(pickT[i].At(t));
                float shift = HoverShift * Mathf.Max(Mathf.Clamp01(hoverT[i].At(t)), pick);
                var pos = new Vector2(EnterShift * (1f - e) - shift, 0f);
                if (r.body.anchoredPosition != pos) r.body.anchoredPosition = pos;
                float bw = Mathf.Lerp(BandW, BandPick, pick);
                if (r.band.sizeDelta.x != bw) r.band.sizeDelta = new Vector2(bw, RowH);
            }
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }

        // ---- build -------------------------------------------------------------------------------------------------------------
        /// <summary>Builds the header and the five rows on this object (once; the prefab holds only the root).</summary>
        public void Build()
        {
            if (header != null) return;
            var theme = UITheme.Current;
            var root = (RectTransform)transform;
            UILayout.Place(root, X, Y, W, H);

            // .sqh{padding:7px 18px 7px 30px;font:900 14px/1.25} em{11px;letter-spacing:.1em;#8b939c}
            header = HudKit.NewRect("Header", root);
            UILayout.Place(header, 0f, 0f, W, HeaderH);
            headerGroup = header.gameObject.AddComponent<CanvasGroup>();
            headerGroup.blocksRaycasts = false;
            headerBody = HudKit.NewRect("Body", header);
            HudKit.Stretch(headerBody);
            HudKit.AddImage(headerBody, theme.ink);
            headerBody.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Large);
            var main = HudKit.AddText(headerBody, "投入隊員", 14f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Title", UITheme.TextRole.Title);
            UILayout.Place(main.rectTransform, 30f, 0f, 200f, HeaderH);
            var em = HudKit.AddText(headerBody, "SQUAD ROSTER // 05", 11f, EmC, true, TextAlignmentOptions.MidlineRight, "Sub", UITheme.TextRole.Display);
            em.characterSpacing = 10f;
            UILayout.Place(em.rectTransform, W - 18f - 300f, 0f, 300f, HeaderH);

            for (int i = 0; i < rows.Length; i++) rows[i] = BuildRow(root, i);
        }

        Row BuildRow(RectTransform root, int index)
        {
            var theme = UITheme.Current;
            var r = new Row();
            r.block = HudKit.NewRect("Row" + index, root);
            UILayout.Place(r.block, 0f, HeaderH + HeaderGap + index * (RowH + RowGap), W, RowH);
            r.group = r.block.gameObject.AddComponent<CanvasGroup>();
            r.body = HudKit.NewRect("Body", r.block);
            HudKit.Stretch(r.body);

            // the row: paper in the slant-22 parallelogram, the stencil its parts are clipped to, and the hover / click target
            var panel = HudKit.NewRect("Panel", r.body);
            HudKit.Stretch(panel);
            HudKit.AddImage(panel, theme.paper, true);
            panel.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Large);
            panel.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            r.band = HudKit.NewRect("Cb", panel);
            UILayout.Place(r.band, 0f, 0f, BandW, RowH);
            HudKit.AddImage(r.band, theme.blue);

            // .por{width:72px;height:60px;margin-left:10px;clip-path slant 16} img{object-fit:cover; top}
            r.face = SlantedMask.Create(panel, "Face", new Vector2(FaceW, RowH), SlantedGraphic.Size.Custom);
            r.face.slant.Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 16f);
            UILayout.Place((RectTransform)r.face.transform, FaceX, 0f, FaceW, RowH);

            // .m{padding:5px 14px 0 12px} b{21px/1.25;letter-spacing:.06em} small{12.5px;dim;ml 8} p{12.5px/1.4;nowrap;ellipsis}
            r.name = HudKit.AddText(panel, "", 21f, theme.ink, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Display);
            r.name.characterSpacing = 6f;
            r.name.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.LineHeight(r.name, 1.25f);
            UILayout.Place(r.name.rectTransform, InfoX, 5f, 140f, 26f);
            r.kana = HudKit.AddText(panel, "", 12.5f, theme.dim, true, TextAlignmentOptions.TopLeft, "Kana", UITheme.TextRole.Body);
            r.kana.textWrappingMode = TextWrappingModes.NoWrap;
            r.kana.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(r.kana.rectTransform, InfoX + 148f, 12f, InfoW - 148f, 16f);
            r.desc = HudKit.AddText(panel, "", 12.5f, DescC, false, TextAlignmentOptions.TopLeft, "Desc", UITheme.TextRole.Body);
            r.desc.textWrappingMode = TextWrappingModes.NoWrap;
            r.desc.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.LineHeight(r.desc, 1.4f);
            UILayout.Place(r.desc.rectTransform, InfoX, 5f + 26.25f, InfoW, 18f);
            r.desc.gameObject.AddComponent<LintIgnoreClip>().reason = "p{white-space:nowrap;text-overflow:ellipsis}: one line within 392, cut with an ellipsis";

            // .s{width:196px;padding:9px 20px 0 28px;clip-path polygon(22px 0,100% 0,100% 100%,0 100%)} div{grid 30 88 1fr;gap 6;h 14;mb 5}
            var stats = HudKit.NewRect("Stats", panel);
            UILayout.Place(stats, W - StatsW, 0f, StatsW, RowH);
            HudKit.AddImage(stats, theme.ink2);
            stats.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Large, SlantedGraphic.Direction.LeftEdge);
            StatLine(stats, 9f, "HP", out r.hp, out r.hpValue);
            StatLine(stats, 9f + 14f + 5f, "MOV", out r.mov, out r.movValue);

            var pt = panel.gameObject.AddComponent<KitPointer>();
            pt.onHover = on => Hover(index, on);
            pt.onClick = () => { Select(index); if (index < squad.Count) RowClicked?.Invoke(squad[index]); };
            return r;
        }

        static void StatLine(RectTransform stats, float y, string label, out SegmentedBar bar, out TMP_Text value)
        {
            const float L = 28f, LabelW = 30f, Gap = 6f, TrackW = 88f, LineH = 14f;
            var lt = HudKit.AddText(stats, label, 12f, StatLabelC, true, TextAlignmentOptions.MidlineLeft, label + "Label", UITheme.TextRole.Display);
            UILayout.Place(lt.rectTransform, L, y, LabelW, LineH);
            var br = HudKit.NewRect(label + "Bar", stats);
            UILayout.Place(br, L + LabelW + Gap, y + (LineH - 8f) * .5f, TrackW, 8f);
            bar = br.gameObject.AddComponent<SegmentedBar>();
            bar.style = SegmentedBar.Style.Custom; bar.customSegment = 6f; bar.customGap = 2f;
            bar.raycastTarget = false;
            float vx = L + LabelW + Gap + TrackW + Gap;
            value = HudKit.AddText(stats, "", 12f, Color.white, true, TextAlignmentOptions.MidlineRight, label + "Value", UITheme.TextRole.Display);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(value.rectTransform, vx, y, StatsW - 14f - vx, LineH);
        }

        /// <summary>A roster object named `name` under `parent`, built (the prefab root is the same object without parts).</summary>
        public static KitTitleSquad Create(Transform parent, string name)
        {
            var rt = HudKit.NewRect(name, parent);
            var c = rt.gameObject.AddComponent<KitTitleSquad>();
            c.Build();
            return c;
        }
    }
}
