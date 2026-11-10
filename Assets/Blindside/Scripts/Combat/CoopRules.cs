// FLARE's co-op ranks in a match (Progress.CoopPerks lists them): the unit side of it. A unit that carries a rank (ICoopRanked — the
// player's operators in a real match, set by ProgressBonus) gets its perks from these checks; everyone else has rank 0 and plays as before.
// A unit whose heartbeat GHOST (rank 8) has sensed is 弱点露出 (IExposable.Exposed) until the defense's next phase: shots at it have
// twice the crit chance and do twice the damage (CombatSystem.Shoot).
namespace Blindside.Combat
{
    public interface ICoopRanked
    {
        /// <summary>FLARE's co-op rank with this operator (0: none, or not the player's side).</summary>
        int CoopRank { get; }
        /// <summary>The operator's key (CRUSHER …).</summary>
        string CoopKey { get; }
    }

    public interface IExposable
    {
        bool Exposed { get; set; }
    }

    public static class CoopRules
    {
        public const int Pursuit = 2, Unique3 = 3, Unique8 = 8;
        public const int MaxPursuers = 2;

        public static int Rank(object u) => u is ICoopRanked c ? c.CoopRank : 0;
        public static string Key(object u) => u is ICoopRanked c ? c.CoopKey : "";
        public static bool Has(object u, string key, int rank) => u is ICoopRanked c && c.CoopRank >= rank && c.CoopKey == key;

        /// <summary>BASTION at rank 8 (ハイブリッド): fires from behind the raised shield.</summary>
        public static bool CanFireShielded(object u) => Has(u, "BASTION", Unique8);

        public static bool IsExposed(object u) => u is IExposable e && e.Exposed;
    }
}
