// Ported from the PHASES block in blindside_source/04_gameplay_combat_ai.js (beginPhase/phase/endPhase,
// ~line 349-373) and checkWin()/finish() in blindside_source/05_ui_input_intro.js (~line 91-99, 443-453).
// Presentation (banners, HUD refresh, camera pans, the result screen) is the caller's job via the
// events below; this class only owns phase/round state and win detection, matching the split already
// used by CombatSystem/AbilitySystem. Unit figures are touched only through IUnitView. The source's two alternate starting branches (deploy for an
// attack-side player, prep for a defense-side player) are unified into a single Deploy-then-Prep
// sequence here so an AI-vs-AI match always runs both teams' setup before round 1, matching
// `if(G.autoplay){ G.round=1; phase('atk'); }`'s intent for a fully automated match.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Map;
using Blindside.Abilities;
using Blindside.Vision;
using Blindside.UI;

namespace Blindside.Turns
{
    public enum MatchPhase { Deploy, Prep, Attack, Defense }
    public enum MatchResult { None, AttackWins, DefenseWins }

    /// <summary>
    /// Read/write view of a unit as far as turn bookkeeping and AI movement are concerned. Extends
    /// IAbilityUnit (already covers combat + charges + move budget) with the two per-phase flags
    /// beginPhase resets that AbilityUsed alone didn't cover (`acted`, `ended`), `Placed` (needed
    /// wherever this unit is passed to VisionSystem as an IVisionAgent — via an explicit cast, since
    /// ICombatUnit and IVisionAgent independently declare X/Y/Facing/Team/Alive with identical
    /// signatures, and inheriting both directly into one interface makes every one of those members
    /// ambiguous, CS0229), and a movement mutator — ICombatUnit's X/Y are get-only by design (combat
    /// never moves anyone), so the turn/AI layer needs its own way to relocate a unit (port of
    /// moveAlong's end state, without the mid-path animation/encounter checks that belong to a
    /// not-yet-built MovementSystem). A concrete unit class implements IVisionAgent alongside this
    /// interface so the cast succeeds at runtime.
    /// </summary>
    public interface ITurnUnit : IAbilityUnit
    {
        bool Acted { get; set; }
        bool Ended { get; set; }
        bool Placed { get; }
        /// <summary>Sets the logical position and snaps the drawn position (RX/RY) to it.</summary>
        void MoveTo(float x, float y);
        /// <summary>Drawn position (the source's u.rx/u.ry): what the figure shows, which trails the
        /// logical X/Y while MovementSystem.MoveAlong tweens a step.</summary>
        float RX { get; set; }
        float RY { get; set; }
    }

    /// <summary>Read/write view of the hostage: escort state and who killed it, for checkWin's hostage
    /// branch and the attacker's escort/extract goal logic.</summary>
    public interface ITurnHostage
    {
        float X { get; }
        float Y { get; }
        bool Alive { get; }
        ITurnUnit Escort { get; }
        void SetEscort(ITurnUnit u);
        OperatorTeam? KilledByTeam { get; }
    }

    public class TurnManager
    {
        // Port of `G.round>G.maxRound` where maxRound=15 (defined at game start, 05_ui_input_intro.js).
        public const int MaxRound = 15;

        readonly MapGrid map;
        readonly IReadOnlyList<ITurnUnit> units;
        readonly ITurnHostage hostage;

        public MatchPhase Phase { get; private set; }
        public int Round { get; private set; }
        public int PhaseCount { get; private set; }
        public bool Over { get; private set; }
        public MatchResult Result { get; private set; }
        public string ResultReason { get; private set; }

        /// <summary>The human-controlled side for this match, if any (set by RunMatch's playerTeam
        /// argument). Null means both sides are AI-driven, same as before this was added.</summary>
        public OperatorTeam? PlayerTeam { get; private set; }
        bool playerEndTurnRequested;

        public TurnManager(MapGrid map, IReadOnlyList<ITurnUnit> units, ITurnHostage hostage)
        {
            this.map = map; this.units = units; this.hostage = hostage;
        }

        /// <summary>Fires once per BeginPhase call, after the reset — presentation's cue for showBanner()/updateHUD().</summary>
        public event System.Action<MatchPhase> OnPhaseBegin;
        /// <summary>Fires once, the first time CheckWin finds a terminal condition — port of finish()'s `if(G.over) return` guard.</summary>
        public event System.Action<MatchResult, string> OnFinish;

        static float MoveRange(ITurnUnit u) => u.Mv * 3f + 1f;

        static OperatorTeam? TeamForPhase(MatchPhase phase) => phase switch
        {
            MatchPhase.Attack or MatchPhase.Deploy => OperatorTeam.Attack,
            MatchPhase.Defense or MatchPhase.Prep => OperatorTeam.Defense,
            _ => null,
        };

        /// <summary>The team that acts (moves/fights) during this phase — Deploy/Attack for the attack
        /// team, Prep/Defense for the defense team. Same mapping as <see cref="TeamForPhase"/>; exposed
        /// for EnemyAI to know who to run.</summary>
        public static OperatorTeam ActingTeam(MatchPhase phase) => TeamForPhase(phase) ?? OperatorTeam.Attack;

        /// <summary>
        /// Port of beginPhase(team): resets that team's units for the phase about to run
        /// (moveLeft/acted/abilityUsed/ended/shield/ghost/stim). `gadgetUsed` and the gas-tick/gas-damage
        /// side effects have no gadget/gas system wired up here, so they're omitted — this class only
        /// owns phase/round/win bookkeeping, not abilities.
        /// </summary>
        public void BeginPhase(MatchPhase phase)
        {
            // phase(): `clearUI(); beginPhase(team);` — clearUI() drops every selection ring. The
            // figure is only ever reached through IUnitView, never its prefab.
            foreach (var u in units) u.View?.SetSelected(false);

            Phase = phase; PhaseCount++;
            var team = TeamForPhase(phase);
            if (team.HasValue)
            {
                foreach (var u in units)
                {
                    if (!u.Alive || u.Team != team.Value) continue;
                    u.MoveLeft = MoveRange(u);
                    u.Acted = false;
                    u.AbilityUsed = false;
                    u.Ended = false;
                    u.Shield = false;
                    u.Ghost = false;
                    u.Stim = false;
                }
            }
            if (phase == MatchPhase.Attack)
            {
                int atkAlive = 0, defAlive = 0;
                foreach (var u in units)
                {
                    if (!u.Alive) continue;
                    if (u.Team == OperatorTeam.Attack) atkAlive++; else defAlive++;
                }
                Debug.Log($"Round {Round}: 攻撃 {atkAlive}名 / 防衛 {defAlive}名 生存");
            }
            OnPhaseBegin?.Invoke(phase);
            CheckWin();   // phase(): `beginPhase(team); if(checkWin()) return;` — a gas death at the phase start ends it here

            // startGame()'s setup-phase log lines, shown to the side that owns that setup phase:
            //   atk: playIntro().then(()=>showBanner('deploy')).then(()=>{ ... log('防衛チームは人質のいる部屋に集まっています。降下地点を決めてください。','w') })
            //   def: showBanner('prep').then(()=>{ ... log('準備フェーズ: 壁の強化(最大10)と装置の設置後、[作戦開始]を押してください。','w') })
            // (The attack side's deploy line is added by WaitForPlayerDeploy, once the banner is done, and
            // taken off again when the deployment ends — it only belongs to that phase.)
            if (phase == MatchPhase.Prep && PlayerTeam == OperatorTeam.Defense)
                HudLog.Add("準備フェーズ: 壁の強化(最大10)と装置の設置後、[作戦開始]を押してください。", HudLogType.Warn);
        }

        /// <summary>
        /// Port of checkWin(): hostage killed, one side wiped (attack-wipe is ignored during Prep, same
        /// as the source's `G.phase!=='prep'` guard, since attackers haven't spawned yet), or the
        /// hostage's escort reached an Extract tile. Sets Over/Result/ResultReason and fires OnFinish
        /// exactly once. Returns true once the match is over (this or a previous call).
        /// </summary>
        public bool CheckWin()
        {
            if (Over) return true;

            if (hostage != null && !hostage.Alive)
            {
                bool killedByAttack = hostage.KilledByTeam == OperatorTeam.Attack;
                Finish(killedByAttack ? MatchResult.DefenseWins : MatchResult.AttackWins,
                    killedByAttack ? "攻撃チームの射撃により人質が死亡しました。" : "防衛チームの射撃により人質が死亡しました。");
                return true;
            }

            bool atkAlive = false, defAlive = false;
            foreach (var u in units)
            {
                if (!u.Alive) continue;
                if (u.Team == OperatorTeam.Attack) atkAlive = true; else defAlive = true;
            }

            if (Phase != MatchPhase.Prep && Phase != MatchPhase.Deploy && !atkAlive) { Finish(MatchResult.DefenseWins, "攻撃チームが全員制圧されました。"); return true; }
            if (!defAlive) { Finish(MatchResult.AttackWins, "防衛チームが全員制圧されました。"); return true; }

            // TLp(h.escort.x,h.escort.y)==='extract': the escort's grid cell (Math.round) is in one of the extraction zones.
            if (hostage != null && hostage.Escort != null && hostage.Escort.Alive &&
                ExfilZone.IsExfilTile(Mathf.FloorToInt(hostage.Escort.X + .5f), Mathf.FloorToInt(hostage.Escort.Y + .5f)))
            {
                Finish(MatchResult.AttackWins, "人質を救出地点まで無事に護送しました。");
                return true;
            }

            return false;
        }

        void Finish(MatchResult result, string reason)
        {
            if (Over) return;
            Over = true; Result = result; ResultReason = reason;
            OnFinish?.Invoke(result, reason);
        }

        /// <summary>
        /// Runs the whole match to completion: BeginPhase → CheckWin → let `ai` act out the acting
        /// team's units → advance to the next phase, looping until CheckWin (or the round-15 timeout)
        /// ends it. This collapses the source's `phase()`/`endPhase()` mutual recursion
        /// (`phase('atk')` ↔ `endPhase()` ↔ `phase('def')` ↔ ...) into one coroutine loop — the natural
        /// translation once both sides are AI-driven (no isHuman branch to pause on).
        /// </summary>
        /// <param name="playerTeam">
        /// If set, this side's Attack/Defense phases wait for <see cref="EndPlayerTurn"/> instead of
        /// running <paramref name="ai"/> — the other side is still fully AI-driven. Null (the default)
        /// keeps the original AI-vs-AI behavior.
        /// </param>
        public IEnumerator RunMatch(EnemyAI ai, MatchPhase startPhase = MatchPhase.Deploy, OperatorTeam? playerTeam = null)
        {
            PlayerTeam = playerTeam;
            Phase = startPhase;
            while (!Over)
            {
                BeginPhase(Phase);
                yield return new WaitForSeconds(0.3f); // let the phase-begin banner/pose read before anyone acts
                if (CheckWin()) yield break;

                if (Phase == MatchPhase.Deploy && PlayerTeam == OperatorTeam.Attack)
                {
                    yield return WaitForPlayerDeploy();
                    if (Over) yield break;
                }
                else if (Phase == MatchPhase.Prep && PlayerTeam == OperatorTeam.Defense)
                {
                    yield return WaitForPlayerPrep();
                    if (Over) yield break;
                }
                else if (Phase == MatchPhase.Attack || Phase == MatchPhase.Defense)
                {
                    var acting = ActingTeam(Phase);
                    if (PlayerTeam.HasValue && acting == PlayerTeam.Value)
                        yield return WaitForPlayerTurn();
                    else
                        yield return ai.RunTeamPhase(this, acting);
                    if (Over) yield break;
                }
                // Deploy/Prep: no unit turns to run in this simplified port (attackers already spawned,
                // defenders already fortified by the caller before RunMatch starts) — they only exist so
                // BeginPhase's team-reset and the phase-order (Deploy → Prep → Attack → Defense → ...)
                // matches the source's setup-then-round-1 handoff.

                Phase = AdvancePhase();
            }
        }

        /// <summary>Blocks the acting phase until <see cref="EndPlayerTurn"/> is called — the human
        /// player's stand-in for `ai.RunTeamPhase`. No move/shoot UI exists yet, so the player's units
        /// simply hold; this only gates when the phase hands off, same as the source's END TURN button.</summary>
        IEnumerator WaitForPlayerTurn()
        {
            playerEndTurnRequested = false;
            AwaitingPlayer = true;
            while (!playerEndTurnRequested) yield return null;
            AwaitingPlayer = false;
        }

        public const string DeployLogLine = "防衛チームは人質のいる部屋に集まっています。降下地点を決めてください。";
        /// <summary>Length of the DEPLOYMENT banner (UIPhaseBanner.duration) — input opens once it is gone
        /// (`showBanner('deploy').then(()=>{ G.busy=false; ... })`).</summary>
        const float DeployBannerSeconds = 1.7f - 0.3f;   // RunMatch has already waited 0.3s

        bool deployFinished;
        /// <summary>True while the attack-side player is placing their squad (the source's `G.phase==='deploy'`
        /// with `G.busy` clear) — the player-input side's stand-in for playerCanAct() during Deploy.</summary>
        public bool AwaitingDeploy { get; private set; }
        /// <summary>Fires when the deployment opens for input (banner over) — cue to select the first unplaced unit.</summary>
        public event System.Action DeployOpened;

        /// <summary>Deploy for an attack-side player: after the banner, park until <see cref="FinishDeploy"/>
        /// (the START OP flow: remaining units dropped, helicopter leaving) is called.</summary>
        IEnumerator WaitForPlayerDeploy()
        {
            yield return new WaitForSecondsRealtime(DeployBannerSeconds);
            HudLog.Add(DeployLogLine, HudLogType.Warn);
            deployFinished = false;
            AwaitingDeploy = true;
            DeployOpened?.Invoke();
            while (!deployFinished && !Over) yield return null;
            AwaitingDeploy = false;
            HudLog.Remove(DeployLogLine);
        }

        /// <summary>Ends the deployment; RunMatch moves on to round 1's attack phase.</summary>
        public void FinishDeploy() => deployFinished = true;

        /// <summary>Fires when a defense-side player ends the preparation ([作戦開始]), before round 1 begins —
        /// endPhase()'s prep branch `spawnAttackers(true)`: the attackers appear on the spot, no rope drop.</summary>
        public event System.Action PrepEnded;

        /// <summary>Prep for a defense-side player: after the PREPARATION banner, the player's turn (moves, abilities,
        /// wall reinforcement — no attacks) lasts until <see cref="EndPlayerTurn"/> ([作戦開始]); then the
        /// attackers are spawned and RunMatch moves on to round 1's attack phase.</summary>
        IEnumerator WaitForPlayerPrep()
        {
            yield return new WaitForSecondsRealtime(DeployBannerSeconds);   // showBanner('prep').then(()=>{ G.busy=false; ... })
            playerEndTurnRequested = false;
            AwaitingPlayer = true;
            while (!playerEndTurnRequested && !Over) yield return null;
            AwaitingPlayer = false;
            if (!Over) PrepEnded?.Invoke();
        }

        /// <summary>True only while RunMatch is parked in <see cref="WaitForPlayerTurn"/> — the
        /// player-input side's stand-in for the source's playerCanAct() phase check
        /// (`G.phase===G.side`).</summary>
        public bool AwaitingPlayer { get; private set; }

        /// <summary>Call from the player-facing "ターン終了" control to close out the current player phase.
        /// A no-op outside a player-controlled Attack/Defense phase.</summary>
        public void EndPlayerTurn() => playerEndTurnRequested = true;

        /// <summary>
        /// Port of endPhase()'s dispatch, linearized for an AI-vs-AI match instead of the source's
        /// either/or branch (only one of deploy/prep ever ran, whichever matched the human player's
        /// side, then jumped straight to `G.round=1; phase('atk')`): here Deploy (attack team sets up)
        /// hands off to Prep (defense team sets up), Prep hands off to round 1's Attack phase, Attack
        /// always falls through to Defense, and Defense either loops back to Attack for the next round
        /// or, past round 15, ends the match for the defense team (`制限時間が終了しました` — the
        /// source's `finish('def', ...)` timeout branch).
        /// </summary>
        MatchPhase AdvancePhase()
        {
            switch (Phase)
            {
                case MatchPhase.Deploy:
                    // Attack-side player: intro → deploy → round 1 (the defense's prep was already applied at
                    // match start, aiDefPrep) — `G.round=1; phase('atk')` straight after the deployment.
                    if (PlayerTeam == OperatorTeam.Attack) { Round = 1; return MatchPhase.Attack; }
                    return MatchPhase.Prep;
                case MatchPhase.Prep:
                    Round = 1;
                    return MatchPhase.Attack;
                case MatchPhase.Attack:
                    return MatchPhase.Defense;
                default: // Defense
                    Round++;
                    if (Round > MaxRound)
                    {
                        Finish(MatchResult.DefenseWins, "制限時間が終了しました。攻撃チームは人質を救出できませんでした。");
                        return Phase;
                    }
                    return MatchPhase.Attack;
            }
        }
    }
}
