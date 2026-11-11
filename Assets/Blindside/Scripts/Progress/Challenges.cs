// An operation's challenges, which make its rank (RankRules): each map has its own list — the built-in house here, a catalog map in its
// entry (MapCatalog.Entry.challenges; empty: the default list). All cleared is S, one missed A, two missed B, more than that C.
using System.Collections.Generic;
using Blindside.Flow;

namespace Blindside.Progress
{
    /// <summary>One challenge as it turned out.</summary>
    public class ChallengeOutcome
    {
        public string text = "";
        public bool done;
    }

    public static class Challenges
    {
        public const int DefaultRounds = 10;

        // メインクエスト 1 (the built-in house)
        static readonly MapChallenge[] House =
        {
            new MapChallenge { kind = ChallengeKind.NoLosses },
            new MapChallenge { kind = ChallengeKind.WithinRounds, value = DefaultRounds },
            new MapChallenge { kind = ChallengeKind.AllCameras },
        };

        // a catalog map that names none of its own
        static readonly MapChallenge[] Default =
        {
            new MapChallenge { kind = ChallengeKind.NoLosses },
            new MapChallenge { kind = ChallengeKind.WithinRounds, value = DefaultRounds },
            new MapChallenge { kind = ChallengeKind.AllEnemies },
        };

        /// <summary>The challenges of the map `stem` ("" = the built-in house, "@id" = a catalog map).</summary>
        public static MapChallenge[] Of(string stem)
        {
            if (string.IsNullOrEmpty(stem)) return House;
            var e = MapCatalog.Find(MapCatalog.IdOf(stem));
            return e != null && e.challenges != null && e.challenges.Length > 0 ? e.challenges : Default;
        }

        public static string Text(MapChallenge c)
        {
            switch (c.kind)
            {
                case ChallengeKind.NoLosses: return "味方を一人も失わない";
                case ChallengeKind.WithinRounds: return c.value + "ラウンド以内にクリア";
                case ChallengeKind.AllCameras: return "監視カメラをすべて破壊";
                default: return "敵を全員制圧";
            }
        }

        static bool Done(MapChallenge c, RankResult r, int camerasLeft)
        {
            switch (c.kind)
            {
                case ChallengeKind.NoLosses: return r.down == 0;
                case ChallengeKind.WithinRounds: return r.rounds <= c.value;
                case ChallengeKind.AllCameras: return camerasLeft == 0;
                default: return r.killed >= r.enemies;
            }
        }

        /// <summary>Fills r.challenges for the map and returns how many were missed.</summary>
        public static int Judge(string stem, RankResult r, int camerasLeft)
        {
            int missed = 0;
            r.challenges = new List<ChallengeOutcome>();
            foreach (var c in Of(stem))
            {
                if (c == null) continue;
                bool done = Done(c, r, camerasLeft);
                if (!done) missed++;
                r.challenges.Add(new ChallengeOutcome { text = Text(c), done = done });
            }
            return missed;
        }
    }
}
