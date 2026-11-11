// Experience as it is earned: after every staged engagement (BattleDirector → MatchBootstrap's experience beat) the player's operators who
// took part get what the engagement added to their record (ProgressRules: attacks, damage dealt, kills, assists — a kill gives the most),
// × the stage's expScale, into their level and their weapon's mastery at once (the session's save in memory; written at the match's end).
// What was not shown that way (cinematics off, the last moments) is given by FlushAll when the match ends. The result screen shows the
// whole match: from Start (the values when the match began) to now. Without a save session a throw-away copy keeps the numbers.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Save;

namespace Blindside.Progress
{
    public class ProgressLive
    {
        public class Gain
        {
            public UnitRecord rec;
            public int exp, killsNow;
            public int levelFrom, expFrom, levelTo, expTo;
            public int wLevelFrom, wExpFrom, wLevelTo, wExpTo;
        }

        public struct Snapshot { public int level, exp, wLevel, wExp; }

        readonly MatchRecord record;
        readonly OperatorTeam side;
        readonly SaveData data;
        readonly float scale;
        readonly Dictionary<UnitRecord, Snapshot> start = new Dictionary<UnitRecord, Snapshot>();
        readonly Dictionary<UnitRecord, int> rawCredited = new Dictionary<UnitRecord, int>();
        readonly Dictionary<UnitRecord, int> credited = new Dictionary<UnitRecord, int>();
        readonly Dictionary<UnitRecord, int> killsCredited = new Dictionary<UnitRecord, int>();
        readonly List<Gain> gains = new List<Gain>(2);

        public SaveData Data => data;

        public ProgressLive(MatchRecord record, OperatorTeam side, SaveData data, float scale)
        {
            this.record = record; this.side = side; this.data = data ?? SaveData.Fresh(); this.scale = Mathf.Max(0f, scale);
            foreach (var r in record.Team(side))
            {
                var o = this.data.OperatorOf(r.key); var w = this.data.WeaponOf(r.weapon);
                start[r] = new Snapshot { level = o.level, exp = o.exp, wLevel = w.level, wExp = w.exp };
                rawCredited[r] = 0; credited[r] = 0; killsCredited[r] = 0;
            }
        }

        public Snapshot StartOf(UnitRecord r) => start.TryGetValue(r, out var s) ? s : default;
        public int TotalExp(UnitRecord r) => credited.TryGetValue(r, out var v) ? v : 0;

        /// <summary>Gives the experience `a` and `b` (the engagement's two units; the player's side only) earned since the last flush.
        /// Returns the gains to show (empty when nothing was earned); the list is reused.</summary>
        public List<Gain> Flush(object a, object b)
        {
            gains.Clear();
            Give(record.Of(a));
            if (b != a) Give(record.Of(b));
            // the allies who joined in with 支援射撃
            foreach (var s in record.SupportSinceFlush) if (s != a && s != b) Give(record.Of(s));
            record.SupportSinceFlush.Clear();
            return gains;
        }

        /// <summary>Gives everything not given yet (the match's end); nothing to show.</summary>
        public void FlushAll()
        {
            foreach (var r in record.Team(side)) Give(r);
            gains.Clear();
        }

        void Give(UnitRecord r)
        {
            if (r == null || r.team != side || !rawCredited.ContainsKey(r)) return;
            int raw = ProgressRules.MatchExp(r);
            int addRaw = raw - rawCredited[r];
            if (addRaw <= 0) return;
            // the scaled total so far minus what was already given: no rounding drift over many engagements
            int totalScaled = Mathf.RoundToInt(raw * scale);
            int add = totalScaled - credited[r];
            rawCredited[r] = raw;
            if (add <= 0) return;
            credited[r] = totalScaled;

            var o = data.OperatorOf(r.key); var w = data.WeaponOf(r.weapon);
            var g = new Gain { rec = r, exp = add, killsNow = r.kills - killsCredited[r], levelFrom = o.level, expFrom = o.exp, wLevelFrom = w.level, wExpFrom = w.exp };
            killsCredited[r] = r.kills;
            int levels = ProgressRules.AddExp(ref o.level, ref o.exp, add, ProgressRules.LevelNeed, ProgressRules.MaxLevel);
            o.points += levels * ProgressRules.PointsPerLevel;
            if (!string.IsNullOrEmpty(r.weapon)) ProgressRules.AddExp(ref w.level, ref w.exp, add, ProgressRules.MasteryNeed, ProgressRules.MaxMastery);
            g.levelTo = o.level; g.expTo = o.exp; g.wLevelTo = w.level; g.wExpTo = w.exp;
            gains.Add(g);
        }
    }
}
