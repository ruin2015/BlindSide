// The squad's radio calls during a match: when one of the player's operators runs into an enemy (an encounter), starts a fight
// (FIRE at an enemy), sees an enemy come into sight, or flies a drone onto one, that operator's face comes up on the radio
// (TutorialRadio, bottom left — over the unit panel while it's up) with a short line. Presentation only: no rolls, no game state.
// One call at a time, the next ones queued (at most QueueMax); each enemy is called out at most once per EnemyCooldown seconds.
// Off in the tutorial (CRUSHER's radio has the bottom left) and hidden while a battle cinematic is on screen. The sight check runs
// every SightPeriod seconds over the few units there are (no allocation).
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;
using Blindside.Drones;
using Blindside.Tutorial;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        public const float CalloutMin = 2.4f, CalloutPerChar = 1f / 30f, CalloutTail = 1.3f, EnemyCooldown = 8f, SightPeriod = .2f;
        public const int QueueMax = 2;
        /// <summary>How long a FIRE at an enemy holds for its call before the exchange (real time).</summary>
        public const float EngageCallHold = .7f;

        // ---- the lines (Japanese; {0} = the enemy's name) ----
        public const string CSight = "敵を視認。{0}だ。";
        public const string CDrone = "ドローンが敵を捕捉。\n{0}の位置を送る。";
        public const string CFirst = "{0}を発見。\n先手を取る。";
        public const string CSpotted = "まずい、{0}に見つかった！";
        public const string CMutual = "{0}と鉢合わせだ！";
        public const string CEngage = "{0}と交戦する！";
        public const string CKill = "{0}を倒した！";
        public const string CPreempted = "くっ、{0}の先制攻撃だ！";
        public const string CSupport = "支援する！";
        /// <summary>One of the squad down: another calls for them ({0} = the fallen one's name).</summary>
        public const string CDown = "おい！？ {0}、応答しろ！";
        /// <summary>The squad's first attack phase of an operation (as the first operation's CRUSHER opens it): {0} = its name.</summary>
        public const string COpStart = "作戦名「{0}」、開始する。";
        bool opStartCalled;

        /// <summary>The operation's opening call, once, at the first attack phase: CRUSHER says it (or whoever leads when he isn't out).</summary>
        void CallOperationStart()
        {
            var d = Blindside.Map.MapLibrary.Current;
            string name = d == null ? null : !string.IsNullOrEmpty(d.operationNameJa) ? d.operationNameJa : d.operationCode;
            if (string.IsNullOrEmpty(name) || units == null) return;
            SimUnit lead = null;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Team != side || !u.Alive || u.Op == null || u.Reserve) continue;
                if (u.Op.key == "CRUSHER") { lead = u; break; }
                if (lead == null) lead = u;
            }
            if (lead != null) Callout(lead, string.Format(COpStart, name), true);
        }
        /// <summary>The radio over the battle cinematic's letterbox (8% of 1013 + a gap).</summary>
        public const float CinematicLift = 92f;

        /// <summary>An enemy fell to one of ours: the shooter says so. One of ours down: another of the squad calls for them.</summary>
        void OnKillCallout(Blindside.Combat.ICombatUnit victim, Blindside.Combat.ICombatUnit killer)
        {
            if (!(victim is SimUnit v)) return;
            SimUnit speaker; string line;
            if (v.Team == side)
            {
                speaker = DownCaller(v);
                if (speaker == null) return;
                line = string.Format(CDown, Name(v));
            }
            else
            {
                if (!(killer is SimUnit k) || k.Team != side) return;
                CalledOut(v);
                speaker = k; line = string.Format(CKill, Name(v));
            }
            // on the battle stage the fall comes later (after the rounds land): the call waits for it (K.I.A on screen)
            if (Blindside.Cinematic.BattleDirector.StageHoldsHp && Blindside.Cinematic.BattleDirector.StageHandlesHeadshots)
            {
                killCallsHeld.Add((v, speaker, line));
                return;
            }
            Callout(speaker, line, true);
        }

        /// <summary>Who calls for a fallen squad member: CRUSHER when he is up, else the first of the squad still standing.</summary>
        SimUnit DownCaller(SimUnit down)
        {
            SimUnit pick = null;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == down || u.Team != side || !u.Alive || !u.Placed || u.Captive || u.Op == null) continue;
                if (u.Op.key == "CRUSHER") return u;
                if (pick == null) pick = u;
            }
            return pick;
        }

        readonly List<(SimUnit victim, SimUnit speaker, string line)> killCallsHeld = new List<(SimUnit, SimUnit, string)>(2);

        /// <summary>The battle stage shows a fall (BattleStage.Fell): a call held for it goes out now.</summary>
        void OnStageFell(Blindside.Combat.ICombatUnit u)
        {
            for (int i = killCallsHeld.Count - 1; i >= 0; i--)
            {
                if (killCallsHeld[i].victim != u) continue;
                Callout(killCallsHeld[i].speaker, killCallsHeld[i].line, true);
                killCallsHeld.RemoveAt(i);
            }
        }

        /// <summary>Held calls whose fall will never be shown (the stage skipped or gone): out now.</summary>
        void FlushKillCalls()
        {
            if (killCallsHeld.Count == 0 || Blindside.Cinematic.BattleDirector.StageHoldsHp) return;
            foreach (var c in killCallsHeld) Callout(c.speaker, c.line, true);
            killCallsHeld.Clear();
        }

        /// <summary>An ally joins in with 支援射撃: it says so on the radio.</summary>
        void OnSupportCallout(Blindside.Combat.ICombatUnit who, Blindside.Combat.ICombatUnit target)
        {
            if (who is SimUnit s && s.Team == side) Callout(s, CSupport, true);
        }

        /// <summary>An enemy opens fire on one of ours preemptively: the one hit calls it.</summary>
        void OnVolleyCallout(Blindside.Combat.ICombatUnit a, Blindside.Combat.ICombatUnit t, bool preempt)
        {
            if (!preempt || !(t is SimUnit ours) || !(a is SimUnit foe) || ours.Team != side || foe.Team == side) return;
            CalledOut(foe);
            Callout(ours, string.Format(CPreempted, Name(foe)), true);
        }

        TutorialRadio callRadio;
        readonly Queue<(SimUnit who, string line)> callQueue = new Queue<(SimUnit, string)>();
        readonly HashSet<SimUnit> enemiesInSight = new HashSet<SimUnit>();
        readonly Dictionary<SimUnit, float> calledAt = new Dictionary<SimUnit, float>();
        float callUntil = -1f, nextSight;
        bool cinematicOn;

        bool CalloutsOn => hasSide && !TutorialGate.Active && !TutorialGate.NoCallouts && turns != null && !turns.Over;

        /// <summary>`who` (one of the player's operators) says `line`. `urgent`: shown now, whatever is queued.</summary>
        void Callout(SimUnit who, string line, bool urgent = false)
        {
            if (!CalloutsOn || who == null || who.Op == null) return;
            if (urgent) { callQueue.Clear(); callUntil = -1f; }
            else if (callQueue.Count >= QueueMax) return;
            callQueue.Enqueue((who, line));
            if (callUntil < 0f) NextCallout();
        }

        /// <summary>Marks `foe` as just called out (the sight check stays quiet about it for a while).</summary>
        void CalledOut(SimUnit foe)
        {
            if (foe == null) return;
            calledAt[foe] = Time.unscaledTime;
            enemiesInSight.Add(foe);
        }

        bool RecentlyCalled(SimUnit foe) => calledAt.TryGetValue(foe, out var t) && Time.unscaledTime - t < EnemyCooldown;

        void NextCallout()
        {
            if (callQueue.Count == 0) { callUntil = -1f; if (callRadio != null) callRadio.Close(); return; }
            var (who, line) = callQueue.Dequeue();
            if (callRadio == null) callRadio = TutorialRadio.Build(transform);
            callRadio.SetOperator(who.Op.key, who.Op.DisplayName, who.Op.kr, who.Op.color);
            callRadio.Say(line, false);
            callUntil = Time.unscaledTime + Mathf.Max(CalloutMin, line.Length * CalloutPerChar + CalloutTail);
        }

        static string Name(ITurnUnit u) => u is SimUnit s && s.Op != null ? s.Op.kr : KrOf(u);

        /// <summary>An encounter (MovementSystem → ShowContact): the player's operator in it calls it.</summary>
        void CalloutContact(ITurnUnit u, ITurnUnit e, Blindside.Combat.EncounterKind kind)
        {
            bool pm = u.Team == side;
            var ally = (pm ? u : e.Team == side ? e : null) as SimUnit;
            var foe = (pm ? e : u) as SimUnit;
            if (ally == null || foe == null) return;
            string f;
            switch (kind)
            {
                case Blindside.Combat.EncounterKind.SelfOnly: f = pm ? CFirst : CSpotted; break;   // the mover saw first
                case Blindside.Combat.EncounterKind.Mutual: f = CMutual; break;
                default: f = pm ? CSpotted : CFirst; break;                                       // the other one saw first
            }
            CalledOut(foe);
            Callout(ally, string.Format(f, Name(foe)), true);
        }

        /// <summary>FIRE at an enemy: the shooter calls the fight before it starts (returns whether it called).</summary>
        bool CalloutEngage(SimUnit shooter, Blindside.Combat.ICombatUnit target)
        {
            if (!CalloutsOn || !(target is SimUnit foe) || foe.Team == shooter.Team) return false;
            CalledOut(foe);
            Callout(shooter, string.Format(CEngage, Name(foe)), true);
            return true;
        }

        void OnDroneSpotted(Drone d, SimUnit foe)
        {
            if (!CalloutsOn || foe == null || RecentlyCalled(foe)) return;
            var owner = d != null ? d.owner as SimUnit : null;
            if (owner == null || owner.Team != side) return;
            CalledOut(foe);
            Callout(owner, string.Format(CDrone, Name(foe)));
        }

        /// <summary>LateUpdate: the radio's place / visibility, the end of a call, and the sight check.</summary>
        void SyncCallouts()
        {
            if (!CalloutsOn)
            {
                if (callQueue.Count > 0 || callUntil >= 0f) { callQueue.Clear(); callUntil = -1f; if (callRadio != null) callRadio.Close(); }
                if (enemiesInSight.Count > 0) enemiesInSight.Clear();
                return;
            }
            float now = Time.unscaledTime;
            if (callRadio != null)
            {
                // over the unit panel, as the tutorial's radio does; in a battle cinematic over the letterbox and above its canvases
                callRadio.Lift = cinematicOn ? CinematicLift : UnitPanelUp ? 236f : 0f;
                callRadio.Compact = cinematicOn;   // (in a battle: the short form, left of the middle, off the duel cards)
                int order = cinematicOn ? EngagePreview.SortingOrder + 1 : 4;
                if (callRadio.canvas.sortingOrder != order) callRadio.canvas.sortingOrder = order;
                if (!callRadio.canvas.enabled) callRadio.canvas.enabled = true;
            }
            if (callUntil >= 0f && now >= callUntil && !modal) NextCallout();
            FlushKillCalls();
            if (!opStartCalled && side == OperatorTeam.Attack && turns.Phase == MatchPhase.Attack && turns.AwaitingPlayer && turns.Round <= 1)
            {
                opStartCalled = true;
                CallOperationStart();
            }

            // an enemy coming into one of our operators' sight
            if (now < nextSight || vision == null || units == null) return;
            nextSight = now + SightPeriod;
            if (turns.Phase != MatchPhase.Attack && turns.Phase != MatchPhase.Defense) return;
            for (int i = 0; i < units.Count; i++)   // (indexed: no enumerator boxed per check)
            {
                var foe = units[i];
                if (foe.Team == side) continue;
                SimUnit spotter = null;
                if (foe.Alive && foe.Placed)
                    for (int j = 0; j < units.Count; j++)
                    {
                        var a = units[j];
                        if (a.Team == side && a.Alive && a.Placed && vision.Sees(a, foe)) { spotter = a; break; }
                    }
                if (spotter == null) { enemiesInSight.Remove(foe); continue; }
                if (!enemiesInSight.Add(foe) || RecentlyCalled(foe)) continue;
                calledAt[foe] = now;
                Callout(spotter, string.Format(CSight, Name(foe)));
            }
        }

        void ResetCallouts()
        {
            callQueue.Clear(); callUntil = -1f; enemiesInSight.Clear(); calledAt.Clear(); cinematicOn = false; opStartCalled = false;
            if (callRadio != null) callRadio.Close();
        }
    }
}
