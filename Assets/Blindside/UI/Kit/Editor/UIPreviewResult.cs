// Render Previews of a won operation's ending (OperationResultScreen) over the HUD, from a fixed sample report (the attack squad on the
// default house; CRUSHER the MVP with 3 kills, SERUM down; levels / mastery crossing, a promotion), posed at fixed times:
//   result_banner   1.0s: the radio's moment — 作戦成功 in the middle (the radio itself only plays in Play mode)
//   result_filling  1.6s into the result: the cards in, the bars filling, the reward counting
//   result_final    settled: every bar full, LEVEL UP / MASTERY UP lit, the promotion, 本部へ戻る
// "Blindside > UI > Measure Result GC" poses the whole ending for 300 frames and logs the managed bytes allocated (should be 0).
using UnityEditor;
using UnityEngine;
using Blindside.Data;
using Blindside.Progress;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class UIPreviewResult
    {
        public const string Banner = "result_banner", Filling = "result_filling", Final = "result_final";

        static UnitRecord Rec(string key, string weapon, int taken, int attacks, int walls, int kills, int assists, int dealt, bool alive = true) =>
            new UnitRecord { key = key, weapon = weapon, team = OperatorTeam.Attack, alive = alive, damageTaken = taken, attacks = attacks, walls = walls, kills = kills, assists = assists, damageDealt = dealt };

        static AwardLine Line(UnitRecord r, int lvFrom, int exFrom, int wlFrom, int wxFrom)
        {
            var l = new AwardLine { rec = r, levelFrom = lvFrom, expFrom = exFrom, wLevelFrom = wlFrom, wExpFrom = wxFrom };
            l.exp = ProgressRules.MatchExp(r);
            int lv = lvFrom, ex = exFrom, wl = wlFrom, wx = wxFrom;
            ProgressRules.AddExp(ref lv, ref ex, l.exp, ProgressRules.LevelNeed, ProgressRules.MaxLevel);
            ProgressRules.AddExp(ref wl, ref wx, l.exp, ProgressRules.MasteryNeed, ProgressRules.MaxMastery);
            l.levelTo = lv; l.expTo = ex; l.wLevelTo = wl; l.wExpTo = wx;
            return l;
        }

        public static AwardReport Sample()
        {
            var rep = new AwardReport { win = true, map = "", mapLabel = "基本邸宅", money = 30000, contribution = 400, pointsFrom = 300 };
            rep.lines.Add(Line(Rec("CRUSHER", "AR15", 42, 6, 3, 3, 1, 190), 1, 40, 1, 20));
            rep.lines.Add(Line(Rec("FLARE", "MP7", 18, 4, 2, 1, 2, 120), 1, 10, 1, 0));
            rep.lines.Add(Line(Rec("IGNIS", "M870", 0, 3, 1, 1, 0, 95), 2, 30, 1, 60));
            rep.lines.Add(Line(Rec("SERUM", "MP5", 100, 2, 0, 0, 1, 40, false), 1, 0, 1, 0));
            rep.lines.Add(Line(Rec("BASTION", "P90", 64, 3, 0, 0, 1, 60), 1, 70, 1, 10));
            rep.mvp = 0; rep.lines[0].mvp = true;
            rep.pointsTo = rep.pointsFrom + rep.contribution;
            rep.rankFrom = RankTable.RankOf(rep.pointsFrom); rep.rankTo = RankTable.RankOf(rep.pointsTo);
            rep.moneyTotal = 30000;
            return rep;
        }

        static OperationResultScreen Build(UIPreview.Stage s, float t)
        {
            UIPreview.BuildHudSelected(s);
            var canvas = s.NewCanvas("OperationResult_Canvas", UITheme.CanvasKind.Menu, 100);
            var host = new GameObject("OperationResultHost", typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var r = host.AddComponent<OperationResultScreen>();
            r.externalCanvas = canvas;
            r.PoseAt(Sample(), t);
            return r;
        }

        public static void BuildBanner(UIPreview.Stage s) => Build(s, 1f);
        public static void BuildFilling(UIPreview.Stage s) => Build(s, OperationResultScreen.ResultStart + 1.6f);
        public static void BuildFinal(UIPreview.Stage s) => Build(s, OperationResultScreen.ResultStart + OperationResultScreen.ButtonAt + 1f);

        [MenuItem("Blindside/UI/Measure Result GC")]
        public static void MeasureGc()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[ResultGC] edit mode only."); return; }
            var state = new UIPreview.State { name = Final, label = "result", build = BuildFinal };
            using (var stage = UIPreview.Build(state))
            {
                var r = Object.FindAnyObjectByType<OperationResultScreen>();
                long start = 0;
                for (int frame = 0; frame < 330; frame++)
                {
                    if (frame == 30) start = System.GC.GetAllocatedBytesForCurrentThread();
                    r.Evaluate(frame * (r.Duration / 330f));
                    Canvas.ForceUpdateCanvases();
                }
                long bytes = System.GC.GetAllocatedBytesForCurrentThread() - start;
                Debug.Log($"[ResultGC] 300 frames, the operation result: {bytes} bytes allocated ({bytes / 300f:0.##} per frame)");
            }
        }
    }
}
