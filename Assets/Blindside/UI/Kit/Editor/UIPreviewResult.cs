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
            // SERUM down: the no-losses challenge missed → A
            rep.rank.rank = QuestRank.A; rep.rankScale = RankRules.Multiplier(QuestRank.A); rep.leaveGained = RankRules.LeaveDays(QuestRank.A);
            rep.rank.challenges.Add(new ChallengeOutcome { text = "味方を一人も失わない", done = false });
            rep.rank.challenges.Add(new ChallengeOutcome { text = "10ラウンド以内にクリア", done = true });
            rep.rank.challenges.Add(new ChallengeOutcome { text = "監視カメラをすべて破壊", done = true });
            return rep;
        }

        public const string RankScreen = "result_rank", RankSlam = "result_rank_slam";

        static void BuildRank(UIPreview.Stage s, float t)
        {
            UIPreview.BuildHudSelected(s);
            var canvas = s.NewCanvas("OperationResult_Canvas", UITheme.CanvasKind.Menu, 100);
            var host = new GameObject("OperationResultHost", typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var r = host.AddComponent<OperationResultScreen>();
            r.externalCanvas = canvas;
            r.PoseRank(Sample(), t);
        }

        /// <summary>result_rank: settled; result_rank_slam: the letter landing, its glitch on.</summary>
        public static void BuildRankFinal(UIPreview.Stage s) => BuildRank(s, OperationResultScreen.RankEnd);
        public static void BuildRankSlam(UIPreview.Stage s) => BuildRank(s, OperationResultScreen.LetterAt + .14f);

        public const string HubEntrance = "hub_entrance";

        /// <summary>hub_entrance: the 本部帰還 band of the squad's return (no 3D: over the plain backdrop).</summary>
        public static void BuildHubEntrance(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("HubEntrance_Canvas", UITheme.CanvasKind.Menu, 90);
            Blindside.Hub.HubEntrance.BuildBand(canvas.transform, "DAY 3 · 昼", out _, out _);
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

        public static void BuildBanner(UIPreview.Stage s) => Build(s, OperationResultScreen.BannerFrom + 1f);

        public const string ExpGain = "battle_exp_gain", ExpGainSupport = "battle_exp_gain_support";

        /// <summary>battle_exp_gain_support: the shooter and two allies who joined in with 支援射撃 (three cards), 1.0s in.</summary>
        public static void BuildExpGainSupport(UIPreview.Stage s)
        {
            UIPreview.BuildHudSelected(s);
            var canvas = s.NewCanvas("ExpGain_Canvas", UITheme.CanvasKind.Menu, 95);
            var host = new GameObject("ExpGainHost", typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var p = host.AddComponent<ExpGainPopup>();
            p.externalCanvas = canvas;
            var gains = new System.Collections.Generic.List<ProgressLive.Gain>
            {
                new ProgressLive.Gain { rec = Rec("CRUSHER", "AR15", 0, 1, 0, 1, 0, 120), exp = 190, killsNow = 1, levelFrom = 1, expFrom = 40, wLevelFrom = 2, wExpFrom = 90 },
                new ProgressLive.Gain { rec = Rec("FLARE", "MX50", 0, 1, 0, 0, 1, 50), exp = 70, levelFrom = 2, expFrom = 20, wLevelFrom = 1, wExpFrom = 10 },
                new ProgressLive.Gain { rec = Rec("IGNIS", "M870", 0, 1, 0, 0, 0, 0), exp = 20, levelFrom = 2, expFrom = 30, wLevelFrom = 1, wExpFrom = 60 },
            };
            p.PoseAt(gains, 1.0f);
        }

        /// <summary>battle_exp_gain: the experience beat after an exchange — CRUSHER (a kill: level up, mastery up) and FLARE, 1.0s in.</summary>
        public static void BuildExpGain(UIPreview.Stage s)
        {
            UIPreview.BuildHudSelected(s);
            var canvas = s.NewCanvas("ExpGain_Canvas", UITheme.CanvasKind.Menu, 95);
            var host = new GameObject("ExpGainHost", typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            var p = host.AddComponent<ExpGainPopup>();
            p.externalCanvas = canvas;
            var gains = new System.Collections.Generic.List<ProgressLive.Gain>
            {
                new ProgressLive.Gain { rec = Rec("CRUSHER", "AR15", 0, 1, 0, 1, 0, 120), exp = 190, killsNow = 1, levelFrom = 1, expFrom = 40, wLevelFrom = 2, wExpFrom = 90 },
                new ProgressLive.Gain { rec = Rec("FLARE", "MX50", 12, 1, 0, 0, 1, 30), exp = 70, levelFrom = 2, expFrom = 20, wLevelFrom = 1, wExpFrom = 10 },
            };
            p.PoseAt(gains, 1.0f);
        }
        public static void BuildFilling(UIPreview.Stage s) => Build(s, OperationResultScreen.ResultStart + 1.6f);
        public static void BuildFinal(UIPreview.Stage s) => Build(s, OperationResultScreen.ResultStart + OperationResultScreen.ButtonAt + 1f);

        public const string Growth = "growth_panel";

        /// <summary>growth_panel: the HQ's growth panel on CRUSHER (Lv 3, 4 points left, some already given, AR15 mastery Lv 4) over the title.</summary>
        public static void BuildGrowth(UIPreview.Stage s)
        {
            UIPreview.BuildTitleAttack(s);
            var canvas = s.NewCanvas("Growth_Canvas", UITheme.CanvasKind.Menu, 50);
            var root = Blindside.UI.HudKit.NewRect("Screen", canvas.transform);
            Blindside.UI.HudKit.Stretch(root);
            var d = Blindside.Save.SaveData.Fresh();
            var p = d.OperatorOf("CRUSHER"); p.level = 3; p.exp = 180; p.points = 4; p.hp = 1; p.atk = 1;
            var op = BuildAssetRegistry.Operator("CRUSHER");
            var gun = op != null ? Loadout.Of(op, false) : null;
            var w = d.WeaponOf(gun != null ? gun.key : ""); w.level = 4; w.exp = 60;
            var panel = KitGrowthPanel.Build(root);
            panel.Open(op, d);
        }

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
