// GHOST's 心拍センサー while it is on (HeartbeatAbility): from the moment it is switched on until his side's next turn begins, every
// PingEvery seconds the enemies inside his view cone (its width, 1.3 × its length — HeartbeatAbility.Sense, through walls) are sensed
// again, wherever he has walked or turned: a shockwave ring where each one stands (the player's side only) and a Spotted mark for his
// side (MarkBoard). The models stay hidden — only where they are shows. One per match (MatchBootstrap); unscaled time; nothing is
// allocated per frame besides the rings themselves.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Data;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.Vision;

namespace Blindside.Integration
{
    public class GhostSense : MonoBehaviour
    {
        public const float PingEvery = .9f, RingSize = 1.4f;
        static readonly Color GhostPurple = new Color32(0xb0, 0x5c, 0xff, 0xff);

        sealed class On { public IAbilityUnit u; public OperatorTeam team; public float next; }

        readonly List<On> active = new List<On>(2);
        readonly List<IAbilityUnit> found = new List<IAbilityUnit>(8);
        AbilitySystem abilities;
        AbilityContext ctx;
        TurnManager turns;
        MarkBoard marks;
        OperatorTeam? playerTeam;

        public bool AnyOn => active.Count > 0;

        public void Bind(AbilitySystem abilities, AbilityContext ctx, TurnManager turns, MarkBoard marks, OperatorTeam? playerTeam)
        {
            Unbind();
            this.abilities = abilities; this.ctx = ctx; this.turns = turns; this.marks = marks; this.playerTeam = playerTeam;
            abilities.OnHeartbeat += SwitchOn;
            turns.OnPhaseBegin += OnPhaseBegin;
        }

        public void Unbind()
        {
            if (abilities != null) abilities.OnHeartbeat -= SwitchOn;
            if (turns != null) turns.OnPhaseBegin -= OnPhaseBegin;
            abilities = null; turns = null;
            active.Clear();
        }

        void OnDestroy() => Unbind();

        // switched on: the first sweep was the ability's own (the presenter shows it); the next comes PingEvery later
        void SwitchOn(IAbilityUnit u, List<IAbilityUnit> first)
        {
            for (int i = active.Count - 1; i >= 0; i--) if (active[i].u == u) active.RemoveAt(i);
            active.Add(new On { u = u, team = u.Team, next = Time.unscaledTime + PingEvery });
        }

        // his side's next turn: off
        void OnPhaseBegin(MatchPhase phase)
        {
            if (phase != MatchPhase.Attack && phase != MatchPhase.Defense) return;
            var acting = TurnManager.ActingTeam(phase);
            for (int i = active.Count - 1; i >= 0; i--) if (active[i].team == acting) active.RemoveAt(i);
        }

        void Update()
        {
            if (active.Count == 0 || ctx == null) return;
            float now = Time.unscaledTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var a = active[i];
                if (a.u == null || !a.u.Alive) { active.RemoveAt(i); continue; }
                if (now < a.next) continue;
                a.next = now + PingEvery;
                HeartbeatAbility.Sense(a.u, ctx, found);
                bool mine = playerTeam.HasValue && a.team == playerTeam.Value;
                for (int k = 0; k < found.Count; k++)
                {
                    var e = found[k];
                    marks?.Add(a.team, e.X, e.Y, MarkKind.Spotted);
                    if (mine) FxPulse.Scan(GridSpace.GridToWorld(e.X, e.Y), GhostPurple, RingSize, 2, k * .05f);
                }
            }
        }
    }
}
