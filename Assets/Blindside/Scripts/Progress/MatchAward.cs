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
                    line.exp = Mathf.RoundToInt(ProgressRules.MatchExp(r) * Mathf.Max(0f, stage.expScale));
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

            Finish(rep, data, win, StageRewards.For(map));
            return rep;
        }

        static AwardReport GrantLive(MatchRecord record, OperatorTeam side, bool win, ProgressLive live, SaveData save, SaveData data)
        {
            string map = save != null ? save.lastMap ?? "" : "";
            var stage = StageRewards.For(map);
            var rep = new AwardReport { win = win, map = map, mapLabel = stage.label ?? "", expScale = stage.expScale, saved = save != null };
            if (record == null) return rep;
            live.FlushAll();
            foreach (var r in record.Team(side))
            {
                var s = live.StartOf(r);
                var op = data.OperatorOf(r.key); var wp = data.WeaponOf(r.weapon);
                rep.lines.Add(new AwardLine
                {
                    rec = r, exp = live.TotalExp(r),
                    levelFrom = s.level, expFrom = s.exp, levelTo = op.level, expTo = op.exp, pointsGained = (op.level - s.level) * ProgressRules.PointsPerLevel,
                    wLevelFrom = s.wLevel, wExpFrom = s.wExp, wLevelTo = wp.level, wExpTo = wp.exp,
                });
            }
            Finish(rep, data, win, stage);
            return rep;
        }

        // the MVP, the money and the contribution
        static void Finish(AwardReport rep, SaveData data, bool win, StageRewards.Entry stage)
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
                rep.money = stage.money;
                rep.contribution = stage.contribution;
                data.money += stage.money;
                data.rankPoints += stage.contribution;
            }
            rep.pointsTo = data.rankPoints;
            rep.rankTo = RankTable.RankOf(data.rankPoints);
            rep.moneyTotal = data.money;
        }
    }
}
