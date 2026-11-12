// GHOST's / SILENCE's abilities on screen and their per-phase countdown (ReconAbilities only change state and raise events):
//   heartbeat  a purple heartbeat (FxPulse lub-dubs) at every enemy sensed, 「心拍」 over it, a Spotted mark for the user's side (MarkBoard:
//              until its next turn), the log line (how many) — shown only for the player's side; the AI's side gets the marks alone
//   holo       a cyan scan sweeping out from SILENCE to the scan radius, a ping and 「無力化」 on every gadget it knocked out, the log line
//   per phase  HoloScanAbility.Tick: knocked-out gadgets count down and come back on (「…が復旧した」 when they do)
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Data;
using Blindside.Movement;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.UI;
using Blindside.Vision;

namespace Blindside.Integration
{
    public class ReconAbilityPresenter
    {
        static readonly Color GhostPurple = new Color32(0xb0, 0x5c, 0xff, 0xff);
        static readonly Color HoloCyan = new Color32(0x2f, 0xe8, 0xf0, 0xff);

        readonly AbilitySystem abilities;
        readonly AbilityContext ctx;
        readonly TurnManager turns;
        readonly MarkBoard marks;
        readonly OperatorTeam? playerTeam;

        public ReconAbilityPresenter(AbilitySystem abilities, AbilityContext ctx, TurnManager turns, MarkBoard marks, OperatorTeam? playerTeam)
        {
            this.abilities = abilities; this.ctx = ctx; this.turns = turns; this.marks = marks; this.playerTeam = playerTeam;
            abilities.OnHeartbeat += OnHeartbeat;
            abilities.OnHoloScan += OnHoloScan;
            turns.OnPhaseBegin += OnPhaseBegin;
        }

        public void Unbind()
        {
            abilities.OnHeartbeat -= OnHeartbeat;
            abilities.OnHoloScan -= OnHoloScan;
            turns.OnPhaseBegin -= OnPhaseBegin;
        }

        static string Kr(IAbilityUnit u) => u is IOperatorNamed n && !string.IsNullOrEmpty(n.Kr) ? n.Kr : u.Id;
        bool Mine(IAbilityUnit u) => playerTeam.HasValue && u.Team == playerTeam.Value;

        void OnHeartbeat(IAbilityUnit u, List<IAbilityUnit> found)
        {
            foreach (var e in found) marks?.Add(u.Team, e.X, e.Y, MarkKind.Spotted);
            if (!Mine(u)) return;
            FxPulse.Scan(GridSpace.GridToWorld(u.X, u.Y), GhostPurple, HeartbeatAbility.HeartbeatRange, 1);
            SfxNoise.Ui();
            float d = .25f;
            foreach (var e in found)
            {
                FxPulse.Heartbeat(GridSpace.GridToWorld(e.X, e.Y), GhostPurple, 3, 1f, d);
                WorldPopup.Show(e.X, e.Y, "心拍", GhostPurple);
                d += .1f;
            }
            HudLog.Add(found.Count > 0 ? $"{Kr(u)}: 心拍センサー — 敵 {found.Count} 名の心拍を感知" : $"{Kr(u)}: 心拍センサー — 反応なし",
                found.Count > 0 ? HudLogType.Ally : HudLogType.Normal);
        }

        void OnHoloScan(IAbilityUnit u, List<SurveillanceCamera> cams, List<ElectricField> fields)
        {
            if (!Mine(u)) return;
            // the EMP going off on SILENCE, out to the scan's reach (the knocked-out gadgets then short out: GadgetViews / ElectricFieldView)
            FxEmp.Blast(GridSpace.GridToWorld(u.X, u.Y, .75f), HoloScanAbility.Radius(u));
            if (BattlefieldCamera.Instance != null) BattlefieldCamera.Instance.Shake(.1f);
            SfxNoise.Ui();
            float d = .5f;
            foreach (var c in cams) { FxPulse.Heartbeat(GridSpace.GridToWorld(c.x, c.y), HoloCyan, 1, .8f, d); WorldPopup.Show(c.x, c.y, "無力化", HoloCyan); d += .08f; }
            foreach (var f in fields) { FxPulse.Heartbeat(GridSpace.GridToWorld(f.x, f.y), HoloCyan, 1, f.r, d); WorldPopup.Show(f.x, f.y, "無力化", HoloCyan); d += .08f; }
            int n = cams.Count + fields.Count;
            HudLog.Add(n > 0 ? $"{Kr(u)}: ホロスキャン — 監視カメラ {cams.Count}・電界 {fields.Count} を1ターン無力化" : $"{Kr(u)}: ホロスキャン — 周囲にガジェットなし",
                n > 0 ? HudLogType.Ally : HudLogType.Normal);
        }

        void OnPhaseBegin(MatchPhase phase)
        {
            int before = 0, after = 0;
            foreach (var c in ctx.Cameras) if (c.disabledLeft > 0) before++;
            foreach (var f in ctx.Electrics) if (f.disabledLeft > 0) before++;
            if (before == 0) return;
            HoloScanAbility.Tick(ctx);
            foreach (var c in ctx.Cameras) if (c.disabledLeft > 0) after++;
            foreach (var f in ctx.Electrics) if (f.disabledLeft > 0) after++;
            if (ctx.Vision != null) ctx.Vision.MapVersion++;
            if (after < before && playerTeam.HasValue) HudLog.Add($"無力化されていたガジェット {before - after} 台が復旧した", HudLogType.Warn);
        }
    }
}
