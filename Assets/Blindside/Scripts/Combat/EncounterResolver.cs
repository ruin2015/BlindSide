// Ported from the encounter detection + resolution logic in blindside_source/04_gameplay_combat_ai.js:
// the sees(u,e)/sees(e,u)/trigM/trigE check inside moveUnit's step loop (~line 166-171) and
// resolveEncounter (~line 179-191). Alert/reveal timers and the panTo/showContact camera staging are
// presentation and stay with the caller; this class only classifies and resolves the fight itself.
// Called from MovementSystem.ResolveEncounter — the one moveAlong both the player's and the AI's moves use.
using System.Collections;
using Blindside.Movement;
using Blindside.UI;

namespace Blindside.Combat
{
    /// <summary>
    /// The three ways a moving unit ("mover") and an enemy can spot each other, checked every ~0.2m
    /// of movement. Numeric values match `kind` (1/2/3) in the source for easy cross-reference.
    /// </summary>
    public enum EncounterKind
    {
        SelfOnly = 1,  // 自分だけが敵を見る — mover sees the enemy, enemy hasn't spotted the mover yet
        Mutual = 2,    // 互いに見る — both see each other on the same step
        EnemyOnly = 3, // 敵だけが自分を見る — the enemy spotted the mover first
    }

    /// <summary>A unit's speed for the Mutual tie-break: `speedOf(u)` (mv, +1 while stimmed) in the source.</summary>
    public interface ISpeedRated
    {
        float Speed { get; }
    }

    public static class EncounterResolver
    {
        /// <summary>
        /// Port of:
        /// <code>
        /// const us=sees(u,e), es=sees(e,u);
        /// if((us||es)&&!canShoot(u,e)&&!canShoot(e,u)) { ...mark only... continue; }
        /// if(es&&!trigE.has(e.id)){ trigE.add(e.id); if(us) trigM.add(e.id); enc={e,kind:us?2:3}; break; }
        /// if(us&&!trigM.has(e.id)){ trigM.add(e.id); enc={e,kind:1}; break; }
        /// </code>
        /// `alreadyTriggeredByMover`/`alreadyTriggeredByEnemy` are this enemy's entries in trigM/trigE —
        /// per-enemy flags so a spotted pair doesn't re-trigger on every subsequent step of the same move.
        /// Returns null when neither side sees the other, or the encounter already triggered.
        /// </summary>
        public static EncounterKind? Classify(bool moverSees, bool enemySees,
            bool alreadyTriggeredByMover, bool alreadyTriggeredByEnemy)
        {
            if (!moverSees && !enemySees) return null;

            if (enemySees && !alreadyTriggeredByEnemy)
                return moverSees ? EncounterKind.Mutual : EncounterKind.EnemyOnly;

            if (moverSees && !alreadyTriggeredByMover)
                return EncounterKind.SelfOnly;

            return null;
        }

        /// <summary>
        /// Port of resolveEncounter's per-kind branch:
        /// - SelfOnly: mover may ignore and keep moving ('go'), or fire a preemptive shot and stop.
        ///   `engage` is the human choice (askEngage) or the AI's `hitChance(u,e,{preempt:true})>=30` check.
        /// - Mutual: whichever unit has the higher Speed fires first, with no preempt bonus either way.
        /// - EnemyOnly: the enemy always fires first, with a preemptive shot.
        /// Every branch goes through CombatSystem.Exchange (port of exchange()), which traces both lines of
        /// fire itself, stops after the first shot if that ended the match, and skips the return volley
        /// when the second unit died, is the hostage, or has no open line back.
        /// </summary>
        public static IEnumerator Resolve(CombatSystem combat, EncounterKind kind,
            ICombatUnit mover, ICombatUnit enemy, bool engage)
        {
            switch (kind)
            {
                case EncounterKind.SelfOnly:
                    if (!engage) yield break; // mover ignores the enemy and keeps moving
                    yield return combat.Exchange(mover, enemy, preempt: true);
                    break;

                case EncounterKind.Mutual:
                {
                    // const first=speedOf(u)>=speedOf(e)?u:e, second=first===u?e:u;
                    // log(`相互遭遇 — 速度判定: ${first.kr}が先攻 (SPD ${speedOf(first)} vs ${speedOf(second)})`,'w');
                    bool moverFirst = SpeedOf(mover) >= SpeedOf(enemy);
                    ICombatUnit first = moverFirst ? mover : enemy;
                    ICombatUnit second = moverFirst ? enemy : mover;
                    HudLog.Add($"相互遭遇 — 速度判定: {Kr(first)}が先攻 (SPD {SpeedOf(first)} vs {SpeedOf(second)})", HudLogType.Warn);
                    combat.NextMutual = true;   // staging only: the director closes in on the player's unit first
                    yield return combat.Exchange(first, second, preempt: false);
                    break;
                }

                case EncounterKind.EnemyOnly:
                    yield return combat.Exchange(enemy, mover, preempt: true);
                    break;
            }
        }

        /// <summary>speedOf(u) — 0 for a unit without a speed rating.</summary>
        public static float SpeedOf(ICombatUnit u) => u is ISpeedRated s ? s.Speed : 0f;

        static string Kr(ICombatUnit u) => u is IOperatorNamed n && !string.IsNullOrEmpty(n.Kr) ? n.Kr : "?";
    }
}
