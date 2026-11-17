// Editor check for the scripted first operation (TutorialOperation): Step() does whatever the radio is waiting for — a click, Tab, or the
// guided action (move, shot, ability, drone flight; the hostage secured and the squad gathered in the free part) — through the same
// calls the tutorial's demonstrations use, and raises the TutorialGate event the real input would. One action at a time; Play only.
// (Run through MCP execute_code: Blindside.Tutorial.EditorTools.TutorialOperationDriver.Step())
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Blindside.Data;
using Blindside.Integration;
using Blindside.Map;

namespace Blindside.Tutorial.EditorTools
{
    public static class TutorialOperationDriver
    {
        static bool busy;
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static string Step()
        {
            if (!Application.isPlaying) return "not playing";
            if (Blindside.Save.SaveSystem.Current != null) return "a save is loaded: not driving";
            var mb = Object.FindFirstObjectByType<MatchBootstrap>();
            if (mb == null || mb.ActiveTurns == null) return "no match";
            var turns = mb.ActiveTurns;
            string state = $"{turns.Phase} r{turns.Round}{(turns.Over ? " OVER " + turns.Result : "")}";
            if (busy) return state + " | busy";
            var radio = Object.FindFirstObjectByType<TutorialRadio>();
            string line = radio != null ? radio.line.text : "";
            bool wants = radio != null && (bool)typeof(TutorialRadio).GetField("wantsClick", Any).GetValue(radio);
            if (wants && !radio.Advanced) { typeof(TutorialRadio).GetProperty("Advanced").SetValue(radio, true); return state + " | click: " + Flat(line); }
            if (DeployHologram.Pinned) DeployHologram.DebugHold = false;
            if (line == TutorialOperation.LD3) { DeployHologram.DebugHold = true; return state + " | Tab"; }
            var sel = mb.selection;
            if (sel.Busy) return state + " | selection busy";

            SimUnit crusher = Unit(mb, "CRUSHER"), flare = Unit(mb, "FLARE"), ignis = Unit(mb, "IGNIS");
            switch (line)
            {
                case TutorialOperation.LCrusherMove: return Run(mb, Move(mb, crusher, TutorialOperation.CrusherSpot), state + " | CRUSHER moves");
                case TutorialOperation.LHammerMove: return Run(mb, Move(mb, crusher, TutorialOperation.HammerSpot), state + " | CRUSHER to the wall");
                case TutorialOperation.LFlareMove: return Run(mb, Move(mb, flare, TutorialOperation.FlareSpot), state + " | FLARE moves");
                case TutorialOperation.LCrusherFire: return Run(mb, Fire(mb, crusher, Foe(mb, new Vector2(7, 12))), state + " | CRUSHER fires");
                case TutorialOperation.LHoleFire:
                case TutorialOperation.LMissed:
                case TutorialOperation.LMissedContact:
                    var who = Foe(mb, new Vector2(7, 12)) != null ? crusher : flare;
                    return Run(mb, Fire(mb, who, Foe(mb, who == crusher ? new Vector2(7, 12) : new Vector2(15, 13))), state + " | fires again");
                case TutorialOperation.LCamFire: return Run(mb, FireCamera(mb, flare), state + " | FLARE shoots the camera");
                case TutorialOperation.LHammer2: return Run(mb, Ability(mb, crusher, TutorialOperation.SoftCell), state + " | hammer");
                case TutorialOperation.LLauncherTry:
                case TutorialOperation.LOpen: return Run(mb, Ability(mb, flare, TutorialOperation.ReinforcedCell), state + " | launcher");
                case TutorialOperation.LIgnisTry: return Run(mb, Ability(mb, ignis, TutorialOperation.OuterCell), state + " | thermite");
                case TutorialOperation.LDrone3:
                case TutorialOperation.LDroneMiss: return Run(mb, Drone(mb, crusher, TutorialOperation.DroneSpot), state + " | drone");
                case TutorialOperation.LFree: return Gather(mb, flare) + " | " + state;
                case TutorialOperation.LExitTry:
                    var esc = mb.ActiveHostage != null ? mb.ActiveHostage.Escort as SimUnit : flare;
                    return Run(mb, Move(mb, esc ?? flare, TutorialOperation.ExfilSpot), state + " | to the extraction point");
            }
            return state + " | waiting: " + Flat(line);
        }

        static string Flat(string s) => s.Replace("\n", " ");

        static SimUnit Unit(MatchBootstrap mb, string key)
        {
            foreach (var u in mb.ActiveUnits) if (u.Team == OperatorTeam.Attack && u.Op != null && u.Op.key == key) return u;
            return null;
        }

        static SimUnit Foe(MatchBootstrap mb, Vector2 p)
        {
            foreach (var u in mb.ActiveUnits) if (u.Team == OperatorTeam.Defense && u.Alive && !u.Reserve && Vector2.Distance(new Vector2(u.X, u.Y), p) < 1.5f) return u;
            return null;
        }

        static string Run(MatchBootstrap mb, IEnumerator r, string what)
        {
            busy = true;
            mb.StartCoroutine(Wrap(r));
            return what;
        }

        static IEnumerator Wrap(IEnumerator r) { try { yield return r; } finally { busy = false; } }

        static IEnumerator Move(MatchBootstrap mb, SimUnit u, Vector2 to)
        {
            var sel = mb.selection;
            var path = sel.TutorialPlanPath(u, to, u.MoveLeft);
            if (path == null) { Debug.LogWarning($"[Driver] no path for {u.Id} to {to} (move {u.MoveLeft})"); yield break; }
            yield return sel.TutorialDemoMove(u, path, .4f);
            Debug.Log($"[Driver] {u.Id} moved to ({u.X:0.0},{u.Y:0.0})");
            TutorialGate.RaiseMoveFinished(u);
        }

        static IEnumerator Fire(MatchBootstrap mb, SimUnit u, SimUnit foe)
        {
            if (foe == null) { Debug.LogWarning("[Driver] no foe"); yield break; }
            float a = Mathf.Atan2(foe.Y - u.Y, foe.X - u.X);
            yield return mb.selection.TutorialDemoFire(u, a, a, .2f, true);
            Debug.Log($"[Driver] {u.Id} fired at {foe.Id}: alive={foe.Alive} hp={foe.HP}");
            TutorialGate.RaiseShotFinished(u, foe, false);
        }

        static IEnumerator FireCamera(MatchBootstrap mb, SimUnit u)
        {
            var c = TutorialOperation.CameraCell;
            float a = Mathf.Atan2(c.y - u.Y, c.x - u.X);
            yield return mb.selection.TutorialDemoFire(u, a, a, .2f, true);
            Debug.Log("[Driver] camera shot");
            TutorialGate.RaiseShotFinished(u, null, true);
        }

        static IEnumerator Ability(MatchBootstrap mb, SimUnit u, Vector2Int cell)
        {
            int pid = mb.ActiveMap.PanelIndexAt[cell.y * MapGrid.Width + cell.x];
            yield return mb.selection.TutorialDemoAbility(u, pid);
            Debug.Log($"[Driver] {u.Id} ability on {cell}: tile now {mb.ActiveMap.TileAt(cell.x, cell.y)}");
        }

        static IEnumerator Drone(MatchBootstrap mb, SimUnit u, Vector2 to)
        {
            var sel = mb.selection;
            var ops = (DroneOps)typeof(PlayerSelectionController).GetField("droneOps", Any).GetValue(sel);
            var mv = (Blindside.Movement.MovementSystem)typeof(PlayerSelectionController).GetField("movement", Any).GetValue(sel);
            var d = ops.Deploy(u);
            d.revealsGadgets = false;
            var path = mv.PlanPath(new Vector2(d.x, d.y), to.x, to.y, true, u.Team, d.moveLeft);
            if (path == null) { Debug.LogWarning("[Driver] no drone path"); yield break; }
            yield return ops.MoveDrone(d, path.GetRange(1, path.Count - 1));
            u.GadgetUsed = true;
            Debug.Log($"[Driver] drone at ({d.x:0.0},{d.y:0.0})");
            TutorialGate.RaiseDroneFinished(u);
        }

        // the free part, short-cut: the squad stood in the hostage's room, FLARE escorting
        static string Gather(MatchBootstrap mb, SimUnit flare)
        {
            var h = mb.ActiveHostage;
            if (h == null) return "no hostage";
            var spots = new[] { new Vector2(h.X - 1f, h.Y), new Vector2(h.X + 1f, h.Y + 1f), new Vector2(h.X - 1f, h.Y + 1f) };
            int i = 0;
            foreach (var u in mb.ActiveUnits)
                if (u.Team == OperatorTeam.Attack && u.Alive && u.Placed && !u.Reserve) { var s = spots[Mathf.Min(i++, spots.Length - 1)]; u.MoveTo(s.x, s.y); }
            h.SetEscort(flare);
            return "gathered, escorted by FLARE";
        }
    }
}
