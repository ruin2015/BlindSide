// The HUD's top bar (#tb, ui_spec "상단 바"), built from the kit on the HUD canvas, every block 46 high at top 12 and slanted 22 (.slant):
//   Logo       280×46 at left 20, TacticalPanel paper: "BLINDSIDE" 24 display + "TACTICAL BREACH OPERATION" 11 display (no barcode)
//   Phase      .ph at calc(50% − 218px): Badge 290×46 ink with PhaseMarker (10 wide, skewX −20°, the phase colour: ally blue / foe red /
//              preparation and deployment yellow), main 21 display + sub 12 body; RoundBox 160×46 paper overlapping it by 14 (436 in all):
//              "ROUND" 11 · "01" 30 · "/15" 12 on one baseline, padding 28 / 22, no wrapping, no ellipsis
//   Objective  480×58 at right 20, TacticalPanel paper, padding 5 34 0 36: row 1 (18 high) = Chip "OBJECTIVE // どちらか一方の達成で勝利"
//              on the left and the survivor pips on the right (ALLY + 5 blue, ENEMY + 5 red, 7×10 skewX −20°, a dead one at .25),
//              then two body lines 12 (the side's objective; "EXFIL" and the zone letter joined by a no-break space)
// The pips are laid out from the right (14 from the panel edge) and the chip from the left, so they never meet (chip 36–282, pips
// 286–466). Entrance (dropIn): each block comes down 26 and fades in over .5s, the logo at 0, the phase at 80ms, the
// objective at 160ms; a phase colour change runs .3s. Both are poses of one clock (Evaluate(t)); Update only advances it with unscaled
// time. Texts change only in the setters, when their values change: nothing is allocated per frame. Translucent mockup colours are
// given as the opaque sRGB colour they make over their background.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Topbar")]
    public class KitTopbar : MonoBehaviour, ITimedEffect
    {
        public enum Tone { Ally, Foe, Prep }

        public const float TopY = 12f, Side = 20f, BarH = 46f;
        public const float LogoW = 280f, PhaseW = 290f, RoundW = 160f, Overlap = 14f, ObjW = 480f, ObjH = 58f;
        public const float DropShift = 26f, DropTime = .5f, ToneTime = .3f;
        static readonly float[] Delays = { 0f, .08f, .16f };
        const int MaxPips = 6;
        const float PipW = 7f, PipH = 10f, PipGap = 3f, ObjPadL = 36f, ObjPadR = 34f, ObjPadT = 5f, RowH = 18f, PipsRight = 14f;
        const string ObjectiveTitle = "OBJECTIVE // どちらか一方の達成で勝利";
        static readonly string ExfilNoBreak = "EXFIL" + (char)0xA0;            // "EXFIL A" never splits at the line end

        [Header("Parts")]
        public RectTransform logo, phase, objective;
        public CanvasGroup logoGroup, phaseGroup, objectiveGroup;
        public TacticalPanel marker;
        public TMP_Text phaseMain, phaseSub, roundText, bodyText;
        public Chip titleChip;
        public TMP_Text allyLabel, enemyLabel;
        public TacticalPanel[] allyPips = new TacticalPanel[MaxPips], enemyPips = new TacticalPanel[MaxPips];

        [Tooltip("Shown nowhere (the old #modeTag had no place in the mockup); kept so the HUD's SetMode call still has a target.")]
        public string mode;

        Tone tone; bool hasTone;
        Color markerFrom, markerTo;
        float toneStart = -100f, dropStart = -100f, clock;
        bool running;
        int roundShown = -1, maxShown = -1;
        string bodyShown;
        readonly float[] baseY = new float[3];

        public float Duration => Mathf.Max(Delays[2] + DropTime, toneStart + ToneTime);

        // ---- the HUD's calls (HudTopbar / HudObjective's) -------------------------------------------------------------------------
        /// <summary>#phaseBadge: `MAIN` + `small` and its colour (.foe / .prep).</summary>
        public void SetPhase(string main, string sub, Tone t)
        {
            Build();
            if (phaseMain.text != main) phaseMain.text = main;
            if (phaseSub.text != sub) phaseSub.text = sub;
            if (hasTone && t == tone) return;
            Color to = ToneColor(t);
            if (hasTone && Application.isPlaying && isActiveAndEnabled)
            {
                Rebase();
                markerFrom = marker.color; markerTo = to; toneStart = clock;
                Run();
            }
            else { markerFrom = markerTo = to; marker.color = to; }
            tone = t; hasTone = true;
        }

        /// <summary>#roundNum (two digits; 00 before the first round) and #roundMax.</summary>
        public void SetRound(int round, int max)
        {
            Build();
            round = Mathf.Max(0, round);
            if (round == roundShown && max == maxShown) return;
            roundShown = round; maxShown = max;
            roundText.text = "<size=11><cspace=1.1><color=#5D646C>ROUND</color></cspace></size><space=4><size=30>" + round.ToString("00")
                + "</size><space=4><size=12><color=#5D646C>/" + max + "</color></size>";
        }

        public void SetMode(string text) { mode = text; }

        /// <summary>#objective's two lines and #teamCount's pips (true = alive), ally then enemy.</summary>
        public void SetObjective(string body, IReadOnlyList<bool> ally, IReadOnlyList<bool> enemy)
        {
            Build();
            if (body != bodyShown)
            {
                bodyShown = body;
                bodyText.text = body != null ? body.Replace("EXFIL ", ExfilNoBreak) : "";
            }
            var theme = UITheme.Current;
            SetPips(allyPips, ally, theme.blue, theme.paper);
            SetPips(enemyPips, enemy, theme.foe, theme.paper);
            LayoutPips(ally != null ? ally.Count : 0, enemy != null ? enemy.Count : 0);
        }

        static void SetPips(TacticalPanel[] pips, IReadOnlyList<bool> alive, Color on, Color paper)
        {
            Color off = Color.Lerp(paper, on, .25f);                     // .off{opacity:.25} over the paper
            int n = alive != null ? Mathf.Min(alive.Count, MaxPips) : 0;
            for (int i = 0; i < MaxPips; i++)
            {
                bool show = i < n;
                if (pips[i].gameObject.activeSelf != show) pips[i].gameObject.SetActive(show);
                if (!show) continue;
                Color c = alive[i] ? on : off;
                if (pips[i].color != c) pips[i].color = c;
            }
        }

        // right to left: enemy pips, ENEMY, ally pips, ALLY (.pips b{margin:0 3px 0 5px}, gap 3). The chip (246) and the pips (180) don't
        // fit the 410 content box together (the mockup's flex row overflows the same way), so the pips end PipsRight from the panel's
        // right edge, inside the 34 padding but still inside the slanted edge (≥ 471 at row 1): the pips start at 286, the chip ends at 282.
        void LayoutPips(int ally, int enemy)
        {
            ally = Mathf.Min(ally, MaxPips); enemy = Mathf.Min(enemy, MaxPips);
            float slant = Mathf.Tan(20f * Mathf.Deg2Rad) * PipH;
            float x = ObjW - PipsRight, y = ObjPadT + (RowH - PipH) * .5f;
            for (int i = enemy - 1; i >= 0; i--) { x -= PipW; PlacePip(enemyPips[i], x, y, slant); if (i > 0) x -= PipGap; }
            x -= PipGap + 3f;
            float ew = enemyLabel.rectTransform.sizeDelta.x;
            x -= ew; UILayout.Place(enemyLabel.rectTransform, x, ObjPadT, ew, RowH);
            x -= 5f + PipGap;
            for (int i = ally - 1; i >= 0; i--) { x -= PipW; PlacePip(allyPips[i], x, y, slant); if (i > 0) x -= PipGap; }
            x -= PipGap + 3f;
            float aw = allyLabel.rectTransform.sizeDelta.x;
            x -= aw; UILayout.Place(allyLabel.rectTransform, x, ObjPadT, aw, RowH);
        }

        // a 7 wide box sheared by −20°: the parallelogram is 7 + tan20·10 wide, centred on the box
        static void PlacePip(TacticalPanel p, float x, float y, float slant)
        {
            UILayout.Place(p.rectTransform, x - slant * .5f, y, PipW + slant, PipH);
        }

        Color ToneColor(Tone t)
        {
            var theme = UITheme.Current;
            return t == Tone.Foe ? theme.foe : t == Tone.Prep ? theme.acc : theme.blue;
        }

        // ---- entrance -----------------------------------------------------------------------------------------------------------
        void OnEnable()
        {
            Build();
            if (Application.isPlaying) PlayDropIn();
        }

        /// <summary>dropIn: every block from −26 and transparent, .5s, 0 / 80 / 160ms apart.</summary>
        public void PlayDropIn()
        {
            Build();
            Rebase();
            dropStart = clock;
            Run();
        }

        // an idle bar has finished everything: restart its clock at 0 with both motions settled
        void Rebase()
        {
            if (running) return;
            clock = 0f;
            dropStart = -100f;
            markerFrom = markerTo; toneStart = -100f;
        }

        void Run()
        {
            running = Application.isPlaying && isActiveAndEnabled;
            Evaluate(clock);
        }

        public void Evaluate(float t)
        {
            float d = t - dropStart;
            Pose(logo, logoGroup, 0, d);
            Pose(phase, phaseGroup, 1, d);
            Pose(objective, objectiveGroup, 2, d);

            float k = t - toneStart;
            Color c = k >= ToneTime ? markerTo : Color.Lerp(markerFrom, markerTo, UIEase.Tactical(Mathf.Clamp01(k / ToneTime)));
            if (marker.color != c) marker.color = c;
        }

        void Pose(RectTransform block, CanvasGroup g, int i, float d)
        {
            float p = d - Delays[i];
            float e = dropStart <= -100f ? 1f : p <= 0f ? 0f : UIEase.Tactical(p / DropTime);
            if (UIMotionSettings.reduceMotion && dropStart > -100f) e = p <= 0f ? 0f : 1f;
            var pos = block.anchoredPosition;
            float y = baseY[i] + DropShift * (1f - e);
            if (pos.y != y) block.anchoredPosition = new Vector2(pos.x, y);
            if (g.alpha != e) g.alpha = e;
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }

        // ---- build ------------------------------------------------------------------------------------------------------------
        /// <summary>Builds the bar's parts on this object (once; the prefab holds only the root).</summary>
        public void Build()
        {
            if (logo != null) { HideLogo(); return; }
            var theme = UITheme.Current;
            var root = (RectTransform)transform;
            root.anchorMin = new Vector2(0f, 1f); root.anchorMax = new Vector2(1f, 1f); root.pivot = new Vector2(.5f, 1f);
            root.offsetMin = new Vector2(0f, -(TopY + ObjH)); root.offsetMax = Vector2.zero;
            Color dim = theme.dim;

            // ---- .lg ----
            logo = Block("Logo", new Vector2(0f, 1f), Side, -TopY, LogoW, BarH, out logoGroup);
            var lp = TacticalPanel.Create(logo, "Panel", KitColor.Paper, SlantedGraphic.Size.Large);
            HudKit.Stretch(lp.rectTransform);
            var lb = HudKit.AddText(logo, "BLINDSIDE", 24f, theme.ink, true, TextAlignmentOptions.TopLeft, "LogoText", UITheme.TextRole.Display);
            lb.characterSpacing = 10f;
            UILayout.LineHeight(lb, 1f);
            UILayout.Place(lb.rectTransform, 30f, 6f, LogoW - 30f - 36f, 26f);
            var ls = HudKit.AddText(logo, "TACTICAL BREACH OPERATION", 11f, dim, true, TextAlignmentOptions.TopLeft, "LogoSub", UITheme.TextRole.Display);
            ls.characterSpacing = 8f;
            UILayout.Place(ls.rectTransform, 30f, 32f, LogoW - 30f - 36f, 14f);

            // ---- .ph: .pb + .rd ----
            float phW = PhaseW + RoundW - Overlap;
            phase = Block("Phase", new Vector2(.5f, 1f), -phW * .5f, -TopY, phW, BarH, out phaseGroup);
            var badge = TacticalPanel.Create(phase, "Badge", KitColor.Ink, SlantedGraphic.Size.Large);
            UILayout.Place(badge.rectTransform, 0f, 0f, PhaseW, BarH);
            // .pb::before{left:12px;width:10px;skewX(-20deg)}: a parallelogram 10 wide per row, sheared over the full height (2 right of the
            // CSS spot so its top stays inside the badge's own slant, which clip-path cut in the mockup)
            float ms = Mathf.Tan(20f * Mathf.Deg2Rad) * BarH;
            marker = TacticalPanel.Create(badge.transform, "PhaseMarker", KitColor.Custom, SlantedGraphic.Size.Custom, ms);
            UILayout.Place(marker.rectTransform, 14f + 5f - (10f + ms) * .5f, 0f, 10f + ms, BarH);
            marker.color = theme.blue; markerFrom = markerTo = theme.blue;
            phaseMain = HudKit.AddText(badge.transform, "", 21f, Color.white, true, TextAlignmentOptions.TopLeft, "PhaseMain", UITheme.TextRole.Display);
            phaseMain.characterSpacing = 10f;
            UILayout.LineHeight(phaseMain, 1f);
            UILayout.Place(phaseMain.rectTransform, 42f, 6f, PhaseW - 42f - 16f, 22f);
            phaseSub = HudKit.AddText(badge.transform, "", 12f, Color.Lerp(theme.ink, Color.white, .85f), false, TextAlignmentOptions.TopLeft, "PhaseSub", UITheme.TextRole.Body);
            UILayout.Place(phaseSub.rectTransform, 42f, 28f, PhaseW - 42f - 16f - 8f, 16f);

            var rd = TacticalPanel.Create(phase, "RoundBox", KitColor.Paper, SlantedGraphic.Size.Large);
            UILayout.Place(rd.rectTransform, PhaseW - Overlap, 0f, RoundW, BarH);
            roundText = HudKit.AddText(rd.transform, "", 30f, theme.ink, true, TextAlignmentOptions.MidlineLeft, "Round", UITheme.TextRole.Display);
            roundText.textWrappingMode = TextWrappingModes.NoWrap;
            roundText.overflowMode = TextOverflowModes.Overflow;
            UILayout.Place(roundText.rectTransform, 28f, 0f, RoundW - 28f - 22f, 40f);

            // ---- .ob ----
            objective = Block("Objective", new Vector2(1f, 1f), -Side - ObjW, -TopY, ObjW, ObjH, out objectiveGroup);
            var op = TacticalPanel.Create(objective, "Panel", KitColor.Paper, SlantedGraphic.Size.Large);
            HudKit.Stretch(op.rectTransform);
            titleChip = Chip.Create(objective, "TitleChip", ObjectiveTitle, KitColor.Ink, KitColor.White, 11f, 0f);
            titleChip.padX = 8f; titleChip.padY = 1f;
            titleChip.label.characterSpacing = 6f;
            titleChip.label.textWrappingMode = TextWrappingModes.NoWrap;
            UIFonts.Apply(titleChip.label, UITheme.TextRole.Display);
            titleChip.Fit();
            var cr = (RectTransform)titleChip.transform;
            UILayout.Place(cr, ObjPadL, ObjPadT + (RowH - cr.sizeDelta.y) * .5f, cr.sizeDelta.x, cr.sizeDelta.y);

            allyLabel = PipLabel("AllyLabel", "ALLY", dim);
            enemyLabel = PipLabel("EnemyLabel", "ENEMY", dim);
            for (int i = 0; i < MaxPips; i++)
            {
                allyPips[i] = Pip("AllyPip" + i, theme.blue);
                enemyPips[i] = Pip("EnemyPip" + i, theme.foe);
            }

            bodyText = HudKit.AddText(objective, "", 12f, theme.ink, true, TextAlignmentOptions.TopLeft, "Body", UITheme.TextRole.Body);
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.LineHeight(bodyText, 1.35f);
            UILayout.Place(bodyText.rectTransform, ObjPadL, ObjPadT + RowH + 1f, ObjW - ObjPadL - ObjPadR, ObjH - (ObjPadT + RowH + 1f));

            var five = new bool[] { true, true, true, true, true };
            SetPips(allyPips, five, theme.blue, theme.paper);
            SetPips(enemyPips, five, theme.foe, theme.paper);
            LayoutPips(5, 5);

            baseY[0] = logo.anchoredPosition.y; baseY[1] = phase.anchoredPosition.y; baseY[2] = objective.anchoredPosition.y;
            HideLogo();
        }

        // the battle screen no longer shows the BLINDSIDE plate (kept built, switched off: set active again to bring it back)
        void HideLogo() { if (logo.gameObject.activeSelf) logo.gameObject.SetActive(false); }

        RectTransform Block(string name, Vector2 anchor, float x, float y, float w, float h, out CanvasGroup g)
        {
            var rt = HudKit.NewRect(name, transform);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
            g = rt.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false; g.interactable = false;
            return rt;
        }

        TMP_Text PipLabel(string name, string text, Color c)
        {
            var t = HudKit.AddText(objective, text, 11f, c, true, TextAlignmentOptions.MidlineLeft, name, UITheme.TextRole.Display);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(t.GetPreferredValues(text).x) + 1f, RowH);
            return t;
        }

        TacticalPanel Pip(string name, Color c)
        {
            var p = TacticalPanel.Create(objective, name, KitColor.Custom, SlantedGraphic.Size.Custom, Mathf.Tan(20f * Mathf.Deg2Rad) * PipH);
            p.color = c;
            return p;
        }
    }
}
