// What a won match gives, worked out once at its end and written into the save (the result screen only plays it back):
//   every operator of the player's side   ProgressRules.MatchExp × the stage's expScale → level (and stat points), the same to the
//                                         mastery of the weapon it used
//   the commander                         the stage's contribution → 貢献度 (RankTable), its money → yen
//   最優秀隊員 (MVP)                        the most kills; ties: more damage dealt, then more assists, then the earlier operator
// A lost match gives nothing (the failure screen offers 再挑戦 / タイトルへ). Without a save session (a scene played on its own) the
// numbers are worked out on a throw-away copy so the screen still shows them; nothing is written.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Save;

namespace Blindside.Progress
{
    public class AwardLine
    {
        public UnitRecord rec;
        public int exp;
        public int levelFrom, expFrom, levelTo, expTo, pointsGained;
        public int wLevelFrom, wExpFrom, wLevelTo, wExpTo;
        public bool mvp;
        public int LevelsGained => levelTo - levelFrom;
        public int MasteryGained => wLevelTo - wLevelFrom;
    }

    public class AwardReport
    {
        public bool win;
        public string map = "", mapLabel = "";
        public int money, contribution, pointsFrom, pointsTo, rankFrom, rankTo, moneyTotal;
        public float expScale = 1f;
        public readonly List<AwardLine> lines = new List<AwardLine>();
        public int mvp = -1;
        public bool saved;
        /// <summary>How well it went (RankRules) — it scaled the money, the contribution and the experience.</summary>
        public RankResult rank = new RankResult();
        public float rankScale = 1f;
        /// <summary>褒賞休暇 days this result earned (S / A).</summary>
        public int leaveGained;
        /// <summary>Co-op levels reached in this match (from the kills' helpers): "FLARE · IGNIS" …</summary>
        public readonly List<string> coopUps = new List<string>();
        /// <summary>Squad members left at the HQ who earned a share anyway (CoopPerks: co-op rank 1 / 3 / 5): key and experience.</summary>
        public readonly List<(string key, int exp)> bench = new List<(string, int)>();
        public bool Promoted => rankTo > rankFrom;
    }

    public static class MatchAward
    {
        /// <summary>`live`: the experience was given engagement by engagement (ProgressLive) — the rest is given now, and the report's lines
        /// run from the match's start to now. Without it the match's whole experience is given here (a win only).</summary>
        public static AwardReport Grant(MatchRecord record, OperatorTeam side, bool win, ProgressLive live = null)
        {
            var save = SaveSystem.Current;
            var data = live != null ? live.Data : save ?? SaveData.Fresh();
            if (live != null) return GrantLive(record, side, win, live, save, data);
            string map = save != null ? save.lastMap ?? "" : "";
            var stage = StageRewards.For(map);
            var rep = new AwardReport { win = win, map = map, mapLabel = stage.label ?? "", expScale = stage.expScale, saved = save != null };
            if (record == null) return rep;
            Rate(rep, record, side, win);

            var lines = record.Team(side);
            foreach (var r in lines)
            {
                var line = new AwardLine { rec = r };
                var op = data.OperatorOf(r.key);
                line.levelFrom = op.level; line.expFrom = op.exp;
                var wp = data.WeaponOf(r.weapon);
                line.wLevelFrom = wp.level; line.wExpFrom = wp.exp;
                if (win)
                {
                    line.exp = Mathf.RoundToInt(ProgressRules.MatchExp(r) * Mathf.Max(0f, stage.expScale) * rep.rankScale);
                    int gained = ProgressRules.AddExp(ref op.level, ref op.exp, line.exp, ProgressRules.LevelNeed, ProgressRules.MaxLevel);
                    line.pointsGained = gained * ProgressRules.PointsPerLevel;
                    op.points += line.pointsGained;
                    if (!string.IsNullOrEmpty(r.weapon))
                        ProgressRules.AddExp(ref wp.level, ref wp.exp, line.exp, ProgressRules.MasteryNeed, ProgressRules.MaxMastery);
                }
                line.levelTo = op.level; line.expTo = op.exp;
                line.wLevelTo = wp.level; line.wExpTo = wp.exp;
                rep.lines.Add(line);
            }

            Finish(rep, data, win, StageRewards.For(map), record);
            return rep;
        }

        static AwardReport GrantLive(MatchRecord record, OperatorTeam side, bool win, ProgressLive live, SaveData save, SaveData data)
        {
            string map = save != null ? save.lastMap ?? "" : "";
            var stage = StageRewards.For(map);
            var rep = new AwardReport { win = win, map = map, mapLabel = stage.label ?? "", expScale = stage.expScale, saved = save != null };
            if (record == null) return rep;
            Rate(rep, record, side, win);
            live.FlushAll();
            foreach (var r in record.Team(side))
            {
                var s = live.StartOf(r);
                var op = data.OperatorOf(r.key); var wp = data.WeaponOf(r.weapon);
                // a good rank adds to the experience already given during the match (a poor one only cuts the money and contribution)
                if (win && rep.rankScale > 1f)
                {
                    int bonus = Mathf.RoundToInt(live.TotalExp(r) * (rep.rankScale - 1f));
                    int lv = ProgressRules.AddExp(ref op.level, ref op.exp, bonus, ProgressRules.LevelNeed, ProgressRules.MaxLevel);
                    op.points += lv * ProgressRules.PointsPerLevel;
                    if (!string.IsNullOrEmpty(r.weapon)) ProgressRules.AddExp(ref wp.level, ref wp.exp, bonus, ProgressRules.MasteryNeed, ProgressRules.MaxMastery);
                }
                rep.lines.Add(new AwardLine
                {
                    rec = r, exp = Mathf.RoundToInt(live.TotalExp(r) * (win ? Mathf.Max(1f, rep.rankScale) : 1f)),
                    levelFrom = s.level, expFrom = s.exp, levelTo = op.level, expTo = op.exp, pointsGained = (op.level - s.level) * ProgressRules.PointsPerLevel,
                    wLevelFrom = s.wLevel, wExpFrom = s.wExp, wLevelTo = wp.level, wExpTo = wp.exp,
                });
            }
            Finish(rep, data, win, stage, record);
            return rep;
        }

        /// <summary>The members who didn't go out: each with co-op rank 1 / 3 / 5 gets 1/3, 1/2 or all of the sortie's average experience.</summary>
        static void Bench(AwardReport rep, SaveData data)
        {
            if (rep.lines.Count == 0) return;
            int total = 0;
            var went = new HashSet<string>();
            foreach (var l in rep.lines) { total += l.exp; went.Add(l.rec.key); }
            int avg = total / rep.lines.Count;
            if (avg <= 0) return;
            foreach (var op in Blindside.Hub.HubCatalog.Squad)
            {
                if (op == null || went.Contains(op.key)) continue;
                float share = CoopPerks.BenchShare(data, op.key);
                if (share <= 0f) continue;
                int exp = Mathf.RoundToInt(avg * share);
                var p = data.OperatorOf(op.key);
                int lv = ProgressRules.AddExp(ref p.level, ref p.exp, exp, ProgressRules.LevelNeed, ProgressRules.MaxLevel);
                p.points += lv * ProgressRules.PointsPerLevel;
                rep.bench.Add((op.key, exp));
            }
        }

        /// <summary>The rank (RankRules) and its scale.</summary>
        static void Rate(AwardReport rep, MatchRecord record, OperatorTeam side, bool win)
        {
            rep.rank = RankRules.Evaluate(record, side, record.Rounds, win);
            rep.rankScale = win ? RankRules.Multiplier(rep.rank.rank) : 1f;
        }

        // the MVP, the money and the contribution; the campaign (rank -> leave, the main quests' progress, the kills' bonds)
        static void Finish(AwardReport rep, SaveData data, bool win, StageRewards.Entry stage, MatchRecord record = null)
        {
            for (int i = 0; i < rep.lines.Count; i++)
            {
                if (rep.mvp < 0) { rep.mvp = i; continue; }
                var a = rep.lines[i].rec; var b = rep.lines[rep.mvp].rec;
                if (a.kills > b.kills || (a.kills == b.kills && (a.damageDealt > b.damageDealt || (a.damageDealt == b.damageDealt && a.assists > b.assists))))
                    rep.mvp = i;
            }
            if (rep.mvp >= 0) rep.lines[rep.mvp].mvp = true;

            rep.pointsFrom = data.rankPoints;
            rep.rankFrom = RankTable.RankOf(data.rankPoints);
            if (win)
            {
                rep.money = Mathf.RoundToInt(stage.money * rep.rankScale);
                rep.contribution = Mathf.RoundToInt(stage.contribution * rep.rankScale);
                data.money += rep.money;
                data.rankPoints += rep.contribution;
                rep.leaveGained = RankRules.LeaveDays(rep.rank.rank);
            }
            if (win) Bench(rep, data);
            if (record != null && win)
                foreach (var pair in record.Assists)
                    if (Campaign.AddCoop(data, pair.killer, pair.helper, Campaign.CoopCombat)) rep.coopUps.Add(pair.killer + " · " + pair.helper);
            Campaign.QuestEnded(data, rep.map, win, rep.rank.rank);
            rep.pointsTo = data.rankPoints;
            rep.rankTo = RankTable.RankOf(data.rankPoints);
            rep.moneyTotal = data.money;
        }
    }
}
