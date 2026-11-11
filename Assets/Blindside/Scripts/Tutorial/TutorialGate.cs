// The switches the tutorial (TutorialDirector) uses to hold the player's controls: which commands of the menu are allowed, whether any
// battlefield input reaches PlayerSelectionController at all, whether END TURN / the automatic turn end / a plain-click auto path work.
// PlayerSelectionController reads them only while Active, so a normal match never sees any of this. It also reports back what the player
// did (a finished move, a fired shot) for the tutorial to judge.
namespace Blindside.Integration
{
    public static class TutorialGate
    {
        /// <summary>A tutorial match is running (every other switch is ignored while false).</summary>
        public static bool Active;
        /// <summary>No pointer / keyboard input reaches the battlefield (the radio is talking, the camera is being shown around).</summary>
        public static bool BlockInput;
        /// <summary>Holds every battlefield input even outside a tutorial match (the deployment briefing after the tutorial talks first).</summary>
        public static bool HoldInput;
        /// <summary>Input is held right now (the tutorial's BlockInput, or HoldInput).</summary>
        public static bool Holding => (Active && BlockInput) || HoldInput;
        /// <summary>A plain click in MOVE does not plan an automatic path: the path has to be drawn.</summary>
        public static bool NoAutoPath;
        /// <summary>The command ids the menu offers as usable (move, face, fire, skill, gadget, wait); everything else is greyed out.</summary>
        public static readonly System.Collections.Generic.HashSet<string> Allowed = new System.Collections.Generic.HashSet<string>();

        /// <summary>The player's unit finished walking a MOVE (after the walk, before anything else is chosen).</summary>
        public static event System.Action<SimUnit> MoveFinished;

        /// <summary>The player's unit finished a FIRE: what the line of fire met (a unit, or null) and whether it was an enemy gadget.</summary>
        public static event System.Action<SimUnit, Blindside.Combat.ICombatUnit, bool> ShotFinished;

        public static void RaiseShotFinished(SimUnit u, Blindside.Combat.ICombatUnit target, bool gadget) { if (Active) ShotFinished?.Invoke(u, target, gadget); }

        /// <summary>The last FIRE's direction (grid radians) and how many walls its line went through (the dummy lesson judges it).</summary>
        public static float LastShotAngle;
        public static int LastShotWalls;

        /// <summary>The player's drone finished a flight (the unit's turn is then spent).</summary>
        public static event System.Action<SimUnit> DroneFinished;
        public static void RaiseDroneFinished(SimUnit u) { if (Active) DroneFinished?.Invoke(u); }

        /// <summary>The player's unit finished a throw (it has landed and gone off): where it landed.</summary>
        public static event System.Action<SimUnit, UnityEngine.Vector2> ThrowFinished;
        public static void RaiseThrowFinished(SimUnit u, UnityEngine.Vector2 land) { if (Active) ThrowFinished?.Invoke(u, land); }

        /// <summary>Which wall panels a panel ability may target (null: every valid one) — the lesson's one wall.</summary>
        public static System.Func<int, bool> PanelFilter;

        public static bool Allows(string commandId) => !Active || Allowed.Contains(commandId);

        public static void Allow(params string[] ids)
        {
            Allowed.Clear();
            foreach (var id in ids) Allowed.Add(id);
        }

        public static void RaiseMoveFinished(SimUnit u) { if (Active) MoveFinished?.Invoke(u); }

        /// <summary>Everything back to a normal match.</summary>
        public static void Reset()
        {
            Active = false; BlockInput = false; NoAutoPath = false; HoldInput = false;
            Allowed.Clear();
            MoveFinished = null;
            ShotFinished = null;
            DroneFinished = null;
            ThrowFinished = null;
            PanelFilter = null;
            Blindside.Combat.CombatSystem.FloorHP = 0;
        }
    }
}
