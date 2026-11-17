// The HUD's top bar since the phase badge left it (the old one: UI/Legacy/KitTopbar.cs, kept on the HUD_Topbar prefab and switched off):
//   Clear      top centre, a paper panel ClearW wide: the chip CLEAR // 勝利条件 and the operation's clear condition, one or two lines
//   Objective  top right, a paper panel ObjW wide: the chip OBJECTIVES // 達成目標, the enemies left (残りの敵 N) on the right of the
//              same row, then one row per goal (the operation's challenges): a mark, the goal, and a tag — 達成 (green) / 失敗 (red).
//              A goal that fails flashes red at once (FailFlash), whatever else is going on.
// Every text is 15 or larger and inside its panel: the panels grow with their lines (no clipping, no ellipsis). Entrance (dropIn): each
// panel comes down DropShift and fades in over DropTime, the second DropGap later. All motion is one clock (ITimedEffect.Evaluate(t)),
// advanced with unscaled time; texts change only in the setters, when their values change — nothing is allocated per frame.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Mission Bar")]
    public class KitMissionBar : MonoBehaviour, ITimedEffect
    {
        public enum Goal { Pending, Done, Failed }

        public const float TopY = 12f, Side = 20f;
        public const float ClearW = 640f, ObjW = 480f, PadX = 34f, PadTop = 10f, PadBottom = 12f, ChipRowH = 26f, RowGap = 6f;
        public const float LineH = 25f, GoalRowH = 28f;
        public const float ChipSize = 15f, LineSize = 18f, GoalSize = 17f, TagSize = 15f, EnemyLabelSize = 15f, EnemyNumSize = 26f, MarkSize = 19f;
        public const float DropShift = 26f, DropTime = .5f, DropGap = .08f, FailFlash = .9f, FlashAlpha = .3f;
        public const int MaxLines = 2, MaxGoals = 4;
        public const string ClearTitle = "CLEAR // 勝利条件", ObjectiveTitle = "OBJECTIVES // 達成目標", EnemyLabel = "残りの敵";
        public const string DoneTag = "達成", FailTag = "失敗";
        static readonly Color Green = new Color32(0x1E, 0x9E, 0x5A, 0xFF);

        [Header("Parts")]
        public RectTransform clear, objective;
        public CanvasGroup clearGroup, objectiveGroup;
        public TacticalPanel clearPanel, objectivePanel;
        public TMP_Text[] clearLines = new TMP_Text[MaxLines];
        public TMP_Text enemyLabel, enemyNum;
        public RectTransform[] goalRows = new RectTransform[MaxGoals];
        public Image[] goalFlash = new Image[MaxGoals];
        public TMP_Text[] goalMark = new TMP_Text[MaxGoals], goalText = new TMP_Text[MaxGoals], goalTag = new TMP_Text[MaxGoals];

        int linesShown = -1, enemiesShown = int.MinValue, goalsShown = -1;
        readonly string[] lineText = new string[MaxLines];
        readonly string[] goalStr = new string[MaxGoals];
        readonly Goal[] goalState = new Goal[MaxGoals];
        readonly float[] failAt = new float[MaxGoals];
        float clock, dropStart = -100f;
        bool running;

        public float Duration
        {
            get
            {
                float d = dropStart + DropGap + DropTime;
                for (int i = 0; i < MaxGoals; i++) d = Mathf.Max(d, failAt[i] + FailFlash);
                return d;
            }
        }

        /// <summary>Puts a mission bar where `old` is (same parent and layer) and switches the old bar off (set it active again to go back).</summary>
        public static KitMissionBar Replace(Component old)
        {
            var go = new GameObject("HUD_MissionBar", typeof(RectTransform));
            go.layer = old.gameObject.layer;
            go.transform.SetParent(old.transform.parent, false);
            go.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
            var bar = go.AddComponent<KitMissionBar>();
            bar.Build();
            old.gameObject.SetActive(false);
            return bar;
        }

        // ---- the HUD's calls -----------------------------------------------------------------------------------------------------
        /// <summary>The clear condition: one or two lines (more are dropped).</summary>
        public void SetCondition(string line1, string line2 = null)
        {
            Build();
            int n = string.IsNullOrEmpty(line2) ? 1 : 2;
            bool changed = n != linesShown;
            if (lineText[0] != line1) { lineText[0] = line1; clearLines[0].text = line1 ?? ""; changed = true; }
            if (lineText[1] != line2) { lineText[1] = line2; clearLines[1].text = line2 ?? ""; changed = true; }
            if (!changed) return;
            linesShown = n;
            clearLines[1].gameObject.SetActive(n > 1);
            clear.sizeDelta = new Vector2(ClearW, PadTop + ChipRowH + RowGap + n * LineH + PadBottom);
        }

        /// <summary>The enemies still standing (negative: unknown, shown as —).</summary>
        public void SetEnemies(int left)
        {
            Build();
            if (left == enemiesShown) return;
            enemiesShown = left;
            enemyNum.text = left < 0 ? "—" : left.ToString();
        }

        /// <summary>The goals, in order, and how each stands. A goal turning Failed flashes at once.</summary>
        public void SetGoals(IReadOnlyList<string> texts, IReadOnlyList<Goal> states)
        {
            Build();
            int n = texts != null ? Mathf.Min(texts.Count, MaxGoals) : 0;
            var theme = UITheme.Current;
            bool flash = false;
            for (int i = 0; i < MaxGoals; i++)
            {
                bool on = i < n;
                if (goalRows[i].gameObject.activeSelf != on) goalRows[i].gameObject.SetActive(on);
                if (!on) continue;
                if (goalStr[i] != texts[i]) { goalStr[i] = texts[i]; goalText[i].text = texts[i]; }
                var st = states != null && i < states.Count ? states[i] : Goal.Pending;
                if (goalsShown == n && st == goalState[i]) continue;
                if (st == Goal.Failed && goalState[i] != Goal.Failed && goalsShown == n)
                {
                    if (!running && !flash) Rebase();   // (an idle bar's clock restarts first, so the flash starts now)
                    failAt[i] = clock; flash = true;
                }
                goalState[i] = st;
                goalMark[i].text = st == Goal.Failed ? "×" : st == Goal.Done ? "○" : "・";
                goalMark[i].color = st == Goal.Failed ? theme.foe : st == Goal.Done ? Green : theme.dim;
                goalText[i].color = st == Goal.Failed ? theme.foe : theme.ink;
                goalTag[i].text = st == Goal.Failed ? FailTag : st == Goal.Done ? DoneTag : "";
                goalTag[i].color = st == Goal.Failed ? theme.foe : Green;
            }
            if (goalsShown != n)
            {
                goalsShown = n;
                objective.sizeDelta = new Vector2(ObjW, PadTop + ChipRowH + RowGap + n * GoalRowH + PadBottom);
            }
            if (flash) Run();
        }

        // ---- entrance and the failure flash -------------------------------------------------------------------------------------
        void OnEnable()
        {
            Build();
            if (Application.isPlaying) PlayDropIn();
        }

        public void PlayDropIn()
        {
            Build();
            Rebase();
            dropStart = clock;
            Run();
        }

        void Rebase()
        {
            if (running) return;
            clock = 0f;
            dropStart = -100f;
            for (int i = 0; i < MaxGoals; i++) failAt[i] = -100f;
        }

        void Run()
        {
            running = Application.isPlaying && isActiveAndEnabled;
            Evaluate(clock);
        }

        public void Evaluate(float t)
        {
            float d = t - dropStart;
            Pose(clear, clearGroup, 0f, d);
            Pose(objective, objectiveGroup, DropGap, d);
            for (int i = 0; i < MaxGoals; i++)
            {
                float k = t - failAt[i];
                float a = k < 0f || k >= FailFlash ? 0f : FlashAlpha * (1f - k / FailFlash) * (.55f + .45f * Mathf.Cos(k * 18f));   // (light enough for the red line to read)
                if (UIMotionSettings.reduceMotion) a = k < 0f || k >= FailFlash ? 0f : FlashAlpha * .5f;
                var c = goalFlash[i].color;
                if (c.a != a) { c.a = a; goalFlash[i].color = c; }
            }
        }

        static void Pose(RectTransform block, CanvasGroup g, float delay, float d)
        {
            float p = d - delay;
            float e = d <= -50f ? 1f : p <= 0f ? 0f : UIEase.Tactical(Mathf.Clamp01(p / DropTime));
            if (UIMotionSettings.reduceMotion && d > -50f) e = p <= 0f ? 0f : 1f;
            float y = -TopY + DropShift * (1f - e);
            var pos = block.anchoredPosition;
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
        public void Build()
        {
            if (clear != null) return;
            var theme = UITheme.Current;
            var root = (RectTransform)transform;
            root.anchorMin = new Vector2(0f, 1f); root.anchorMax = new Vector2(1f, 1f); root.pivot = new Vector2(.5f, 1f);
            root.offsetMin = new Vector2(0f, -200f); root.offsetMax = Vector2.zero;
            for (int i = 0; i < MaxGoals; i++) failAt[i] = -100f;

            // ---- the clear condition ----
            clear = Block("Clear", new Vector2(.5f, 1f), new Vector2(.5f, 1f), 0f, ClearW, out clearGroup);
            clearPanel = TacticalPanel.Create(clear, "Panel", KitColor.Paper, SlantedGraphic.Size.Large);
            HudKit.Stretch(clearPanel.rectTransform);
            PlaceChip(clear, "TitleChip", ClearTitle);
            for (int i = 0; i < MaxLines; i++)
            {
                var t = HudKit.AddText(clear, "", LineSize, theme.ink, true, TextAlignmentOptions.MidlineLeft, "Line" + i, UITheme.TextRole.Body);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Overflow;
                t.enableAutoSizing = true; t.fontSizeMin = 15f; t.fontSizeMax = LineSize;   // (a long condition shrinks, never under 15)
                UILayout.Place(t.rectTransform, PadX, PadTop + ChipRowH + RowGap + i * LineH, ClearW - PadX * 2f, LineH);
                clearLines[i] = t;
            }

            // ---- the goals ----
            objective = Block("Objective", new Vector2(1f, 1f), new Vector2(1f, 1f), -Side, ObjW, out objectiveGroup);
            objectivePanel = TacticalPanel.Create(objective, "Panel", KitColor.Paper, SlantedGraphic.Size.Large);
            HudKit.Stretch(objectivePanel.rectTransform);
            PlaceChip(objective, "TitleChip", ObjectiveTitle);
            // 残りの敵 N, right-aligned on the chip's row
            enemyNum = HudKit.AddText(objective, "—", EnemyNumSize, theme.foe, true, TextAlignmentOptions.MidlineRight, "EnemyCount", UITheme.TextRole.Display);
            enemyNum.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(enemyNum.rectTransform, ObjW - PadX - 44f, PadTop - 3f, 44f, ChipRowH + 6f);
            enemyLabel = HudKit.AddText(objective, EnemyLabel, EnemyLabelSize, theme.dim, true, TextAlignmentOptions.MidlineRight, "EnemyLabel", UITheme.TextRole.Body);
            enemyLabel.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(enemyLabel.rectTransform, ObjW - PadX - 44f - 6f - 80f, PadTop, 80f, ChipRowH);
            for (int i = 0; i < MaxGoals; i++)
            {
                var row = HudKit.NewRect("Goal" + i, objective);
                UILayout.Place(row, PadX - 8f, PadTop + ChipRowH + RowGap + i * GoalRowH, ObjW - PadX * 2f + 8f, GoalRowH);
                var fl = HudKit.NewRect("DecoFailFlash", row); HudKit.Stretch(fl);
                goalFlash[i] = HudKit.AddImage(fl, new Color(theme.foe.r, theme.foe.g, theme.foe.b, 0f));
                var mk = HudKit.AddText(row, "・", MarkSize, theme.dim, true, TextAlignmentOptions.Center, "Mark", UITheme.TextRole.Body);
                UILayout.Place(mk.rectTransform, 4f, 0f, 24f, GoalRowH);
                var tx = HudKit.AddText(row, "", GoalSize, theme.ink, true, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Body);
                tx.textWrappingMode = TextWrappingModes.NoWrap;
                tx.enableAutoSizing = true; tx.fontSizeMin = 15f; tx.fontSizeMax = GoalSize;
                UILayout.Place(tx.rectTransform, 32f, 0f, ObjW - PadX * 2f + 8f - 32f - 56f, GoalRowH);
                var tg = HudKit.AddText(row, "", TagSize, Green, true, TextAlignmentOptions.MidlineRight, "Tag", UITheme.TextRole.Body);
                tg.textWrappingMode = TextWrappingModes.NoWrap;
                UILayout.Place(tg.rectTransform, ObjW - PadX * 2f + 8f - 52f, 0f, 48f, GoalRowH);
                goalMark[i] = mk; goalText[i] = tx; goalTag[i] = tg; goalRows[i] = row;
                row.gameObject.SetActive(false);
            }

            SetCondition("", null);
            objective.sizeDelta = new Vector2(ObjW, PadTop + ChipRowH + RowGap + PadBottom);
        }

        RectTransform Block(string name, Vector2 anchor, Vector2 pivot, float x, float w, out CanvasGroup g)
        {
            var rt = HudKit.NewRect(name, transform);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = new Vector2(x, -TopY); rt.sizeDelta = new Vector2(w, 80f);
            g = rt.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false; g.interactable = false;
            return rt;
        }

        static void PlaceChip(RectTransform parent, string name, string text)
        {
            var chip = Chip.Create(parent, name, text, KitColor.Ink, KitColor.White, ChipSize, 0f);
            chip.padX = 10f; chip.padY = 2f;
            chip.label.characterSpacing = 4f;
            chip.label.textWrappingMode = TextWrappingModes.NoWrap;
            UIFonts.Apply(chip.label, UIFonts.RoleFor(text, ChipSize, false));
            chip.Fit();
            var cr = (RectTransform)chip.transform;
            UILayout.Place(cr, PadX, PadTop + (ChipRowH - cr.sizeDelta.y) * .5f, cr.sizeDelta.x, cr.sizeDelta.y);
        }
    }
}
