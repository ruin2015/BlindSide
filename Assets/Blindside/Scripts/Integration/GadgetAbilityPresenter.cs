// The presentation half of the 'gas' / 'ghost' / 'mirror' / 'cam' cases of useAbility(u,target) in
// blindside_source/04_gameplay_combat_ai.js (~238, 243-248), plus beginPhase's gas bookkeeping (~354-355).
// GadgetAbilities/SupportAbilities only change game state and raise events; this turns them into the source's
// fx/sfx/popup/log, for the player's and the AI's use alike (same split as BreachAbilityPresenter):
//   gas:    fxArc(u,x,y,0x8cff5a,550); … sfx('gas'); log(`${u.kr}: 毒ガス弾を投擲`,mine);
//           per enemy inside: popup(e,'-20 GAS','#8cff5a'); if(e.hp<=0) kill(e,u)
//   ghost:  popup(u,'JAMMING','#c9a0ff'); sfx('ui'); if(u.team===G.side) log(`${u.kr}: 電波妨害 — ドローン・カメラに捕捉されない`,'a')
//   mirror: sfx('ui'); fxRing(c.x,c.y,0x2fe4ff,600,.8) per cell; if(u.team===G.side) log(`${u.kr}: ブラックミラー設置`,'a')
//   cam:    sfx('ui'); if(u.team===G.side) log(`${u.kr}: 監視カメラ設置`,'a')
// Gas over time: every Defense phase start, each living attacker standing in gasSet takes 15
// (popup '-15 GAS', log(`${u.kr}: 毒ガスのダメージ 15`,'w'), kill by {team:'def',kr:'毒ガス'}), then every cloud's ttl
// ticks down and expired clouds leave (refreshGas). NOTE: the source deals the 15 at the *attack* phase start
// (`if(team==='atk')`) and only ticks ttl at the defense start; this follows the requested timing instead —
// damage first, then ttl, so a cloud still hits twice over its 2 turns.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Data;
using Blindside.Map;
using Blindside.Movement;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Integration
{
    public class GadgetAbilityPresenter
    {
        static readonly Color GasGreen = new Color32(0x8c, 0xff, 0x5a, 0xff);     // 0x8cff5a
        static readonly Color JamPurple = new Color32(0xc9, 0xa0, 0xff, 0xff);    // #c9a0ff
        static readonly Color MirrorCyan = new Color32(0x2f, 0xe4, 0xff, 0xff);   // 0x2fe4ff
        static readonly Color KiaRed = new Color32(0xff, 0x3b, 0x54, 0xff);       // 0xff3b54

        readonly AbilitySystem abilities;
        readonly AbilityContext ctx;
        readonly TurnManager turns;
        readonly SimHostage hostage;
        readonly IReadOnlyList<SimUnit> units;

        readonly IReadOnlyList<SimHostage> hostages;

        public GadgetAbilityPresenter(AbilitySystem abilities, AbilityContext ctx, TurnManager turns, IReadOnlyList<SimUnit> units, SimHostage hostage,
                                      IReadOnlyList<SimHostage> hostages = null)
        {
            this.abilities = abilities; this.ctx = ctx; this.turns = turns; this.units = units; this.hostage = hostage;
            this.hostages = hostages ?? (hostage != null ? new[] { hostage } : new SimHostage[0]);
            abilities.OnGasLaunched += OnGasLaunched;
            abilities.OnGasThrown += OnGasThrown;
            abilities.OnGasDamage += OnGasDamage;
            abilities.OnGhostToggle += OnGhost;
            abilities.OnMirrorPlaced += OnMirror;
            abilities.OnCameraPlaced += OnCamera;
            turns.OnPhaseBegin += OnPhaseBegin;
        }

        public void Unbind()
        {
            abilities.OnGasLaunched -= OnGasLaunched;
            abilities.OnGasThrown -= OnGasThrown;
            abilities.OnGasDamage -= OnGasDamage;
            abilities.OnGhostToggle -= OnGhost;
            abilities.OnMirrorPlaced -= OnMirror;
            abilities.OnCameraPlaced -= OnCamera;
            turns.OnPhaseBegin -= OnPhaseBegin;
        }

        static string Kr(IAbilityUnit u) => u is IOperatorNamed n && !string.IsNullOrEmpty(n.Kr) ? n.Kr : u.Id;

        void OnGasLaunched(IAbilityUnit u, float x, float y) => FxArc.Spawn(u.X, u.Y, x, y, GasGreen, .55f);

        void OnGasThrown(IAbilityUnit u, GasCloud g)
        {
            SfxNoise.Gas();
            HudLog.Add($"{Kr(u)}: 毒ガス弾を投擲", HudLog.TeamType(u.Team));
        }

        void OnGasDamage(IAbilityUnit u, IAbilityUnit victim, int dmg, bool killed)
        {
            WorldPopup.Show(victim.X, victim.Y, $"-{dmg} GAS", GasGreen);
            if (killed) Kill(victim as SimUnit);
        }

        void OnGhost(IAbilityUnit u, bool on)
        {
            WorldPopup.Show(u.X, u.Y, "JAMMING", JamPurple);
            SfxNoise.Ui();
            if (u.Team == HudLog.Side) HudLog.Add($"{Kr(u)}: 電波妨害 — ドローン・カメラに捕捉されない", HudLogType.Ally);
        }

        void OnMirror(IAbilityUnit u, WallPanel p)
        {
            SfxNoise.Ui();
            foreach (var c in p.cells) CombatFx.Ring(GridSpace.GridToWorld(c.x, c.y), MirrorCyan, .6f, .8f);
            if (u.Team == HudLog.Side) HudLog.Add($"{Kr(u)}: ブラックミラー設置", HudLogType.Ally);
        }

        void OnCamera(IAbilityUnit u, SurveillanceCamera c)
        {
            SfxNoise.Ui();
            if (u.Team == HudLog.Side) HudLog.Add($"{Kr(u)}: 監視カメラ設置", HudLogType.Ally);
        }

        // beginPhase(team)'s gas lines, at the Defense phase start (see file header for the timing).
        void OnPhaseBegin(MatchPhase phase)
        {
            if (phase != MatchPhase.Defense) return;
            var gas = ctx.Vision.GasCells;
            if (gas.Count > 0)
                foreach (var u in units)
                {
                    if (!u.Alive || u.Team != OperatorTeam.Attack) continue;
                    int k = Mathf.FloorToInt(u.Y + .5f) * MapGrid.Width + Mathf.FloorToInt(u.X + .5f);   // idx(Math.round(x),Math.round(y))
                    if (!gas.Contains(k)) continue;
                    u.HP = Mathf.Max(0, u.HP - 15);
                    WorldPopup.Show(u.X, u.Y, "-15 GAS", GasGreen);
                    HudLog.Add($"{u.Kr}: 毒ガスのダメージ 15", HudLogType.Warn);
                    if (u.HP <= 0) Kill(u);
                }
            if (ctx.Gases.Count == 0) return;
            foreach (var g in ctx.Gases) g.ttl--;
            ctx.Gases.RemoveAll(g => g.ttl <= 0);
            GasAbility.RefreshGas(ctx);
        }

        /// <summary>kill(t,by) for a gas death: hp 0, dead, popup 'K.I.A' + boom(0xff3b54,12), the escort release and
        /// `${t.kr}が倒された`. checkWin() runs right after (AbilityRoutine / RunMatch's post-BeginPhase check).</summary>
        /// <summary>kill(u,{team:'def'})'s `G.stats[by.team]++` — every gas death is the defense's kill.</summary>
        public System.Action<SimUnit> OnGasKill;

        void Kill(SimUnit t)
        {
            if (t == null) return;
            t.HP = 0; t.Alive = false;
            OnGasKill?.Invoke(t);
            t.View?.PlayDeath();
            WorldPopup.Show(t.X, t.Y, "K.I.A", KiaRed);
            CombatFx.Boom(GridSpace.GridToWorld(t.X, t.Y), KiaRed, 12);
            foreach (var h in hostages)
                if (h != null && ReferenceEquals(h.Escort, t))
                {
                    h.SetEscort(null);
                    HudLog.Add("護送中だった人質はその場で待機します", HudLogType.Warn);
                }
            HudLog.Add($"{t.Kr}が倒された", t.Team == HudLog.Side ? HudLogType.Foe : HudLogType.Ally);
        }
    }
}
