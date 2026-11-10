// One match's record per operator, for the result screen and the experience (ProgressRules). It only listens — the combat / ability
// systems' events — and never changes the match:
//   damageTaken / damageDealt   every hitting shot's damage (and gas)
//   attacks                     every volley fired (CombatSystem.OnResolved)
//   shots / hits                every round fired / that hit
//   walls                       walls broken by the operator's ability (AbilitySystem.OnBreachComplete)
//   kills / assists             the kill, and an assist for every other ally who had damaged the victim in this match
using System.Collections.Generic;
using Blindside.Abilities;
using Blindside.Combat;
using Blindside.Data;
using Blindside.Integration;

namespace Blindside.Progress
{
    [System.Serializable]
    public class UnitRecord
    {
        public string key = "";            // operator key (CRUSHER …)
        public string weapon = "";         // the weapon key it used
        public OperatorTeam team;
        public bool alive = true;
        public bool captive;               // held at the start (the rescue): not part of the squad's own damage
        public int maxHp = 100;
        public int damageTaken, damageDealt, attacks, shots, hits, walls, kills, assists;
    }

    public class MatchRecord
    {
        /// <summary>Off: matches keep no record (the A/B check that recording never changes a match's outcome — SimBatch with and without).</summary>
        public static bool Enabled = true;

        public readonly List<UnitRecord> Lines = new List<UnitRecord>();
        /// <summary>Rounds the match took (set at its end).</summary>
        public int Rounds;
        /// <summary>Every kill's helpers: (the killer's key, the helper's key) — Campaign's combat bond.</summary>
        public readonly List<(string killer, string helper)> Assists = new List<(string, string)>();
        /// <summary>Who spotted whom (a drone, a camera, a sensor of the same side): the spotter helps the kill too.</summary>
        readonly Dictionary<object, object> spottedBy = new Dictionary<object, object>();
        readonly Dictionary<object, UnitRecord> byUnit = new Dictionary<object, UnitRecord>();
        readonly Dictionary<object, HashSet<object>> damagedBy = new Dictionary<object, HashSet<object>>();
        CombatSystem combat; AbilitySystem abilities;

        public MatchRecord(IEnumerable<SimUnit> units)
        {
            foreach (var u in units)
            {
                if (u == null) continue;
                var r = new UnitRecord
                {
                    key = u.Op != null ? u.Op.key : u.Id,
                    weapon = u.Weapon != null ? u.Weapon.key : "",
                    team = u.Team,
                    captive = u.Captive,
                    maxHp = u.MaxHP,
                };
                byUnit[u] = r;
                Lines.Add(r);
            }
        }

        /// <summary>`spotter` revealed `target` (its drone / scan): if an ally of the spotter kills it, the spotter assisted.</summary>
        public void NoteSpot(object spotter, object target)
        {
            if (spotter != null && target != null) spottedBy[target] = spotter;
        }

        /// <summary>Max HP after the growth bonuses (set once the units are ready).</summary>
        public void RefreshMaxHp()
        {
            foreach (var kv in byUnit) if (kv.Key is SimUnit u) kv.Value.maxHp = u.MaxHP;
        }

        public UnitRecord Of(object unit) => unit != null && byUnit.TryGetValue(unit, out var r) ? r : null;

        /// <summary>The lines of `team`, in the match's order.</summary>
        public List<UnitRecord> Team(OperatorTeam team)
        {
            var list = new List<UnitRecord>();
            foreach (var r in Lines) if (r.team == team) list.Add(r);
            return list;
        }

        public void Bind(CombatSystem c, AbilitySystem a)
        {
            Unbind();
            combat = c; abilities = a;
            if (combat != null)
            {
                combat.OnShot += OnShot;
                combat.OnResolved += OnResolved;
                combat.OnKill += OnKill;
            }
            if (abilities != null)
            {
                abilities.OnBreachComplete += OnBreach;
                abilities.OnGasDamage += OnGas;
            }
        }

        public void Unbind()
        {
            if (combat != null) { combat.OnShot -= OnShot; combat.OnResolved -= OnResolved; combat.OnKill -= OnKill; }
            if (abilities != null) { abilities.OnBreachComplete -= OnBreach; abilities.OnGasDamage -= OnGas; }
            combat = null; abilities = null;
        }

        /// <summary>The end of the match: who is still standing.</summary>
        public void Close()
        {
            foreach (var kv in byUnit) if (kv.Key is SimUnit u) kv.Value.alive = u.Alive;
            Unbind();
        }

        void OnShot(ShotEvent e)
        {
            var a = Of(e.Attacker);
            if (a != null) { a.shots++; if (e.Hit) a.hits++; }
            if (!e.Hit || e.Damage <= 0) return;
            Damage(e.Attacker, e.Target, e.Damage);
        }

        void OnResolved(ICombatUnit attacker, ICombatUnit target, ShotResult r)
        {
            var a = Of(attacker);
            if (a != null) a.attacks++;
        }

        void OnKill(ICombatUnit victim, ICombatUnit killer) => Kill(victim, killer);

        void OnBreach(IAbilityUnit unit, Blindside.Map.WallPanel panel, Blindside.Map.TileType state, bool wasReinforced)
        {
            var r = Of(unit);
            if (r != null) r.walls++;
        }

        void OnGas(IAbilityUnit source, IAbilityUnit victim, int damage, bool killed)
        {
            if (damage > 0) Damage(source, victim, damage);
            if (killed) Kill(victim, source);
        }

        void Damage(object attacker, object target, int amount)
        {
            var a = Of(attacker); var t = Of(target);
            if (a != null && (t == null || t.team != a.team)) a.damageDealt += amount;
            if (t != null) t.damageTaken += amount;
            if (attacker == null || target == null) return;
            if (!damagedBy.TryGetValue(target, out var set)) damagedBy[target] = set = new HashSet<object>();
            set.Add(attacker);
        }

        void Kill(object victim, object killer)
        {
            var v = Of(victim);
            if (v == null) return;                     // (the hostage)
            var k = Of(killer);
            if (k != null && k.team != v.team) k.kills++;
            var helpers = new HashSet<object>();
            if (damagedBy.TryGetValue(victim, out var set)) foreach (var who in set) helpers.Add(who);
            if (spottedBy.TryGetValue(victim, out var sp)) helpers.Add(sp);
            foreach (var who in helpers)
            {
                if (who == killer) continue;
                var h = Of(who);
                if (h == null || h.team == v.team) continue;
                h.assists++;
                if (k != null && k.team == h.team) Assists.Add((k.key, h.key));
            }
        }
    }
}
