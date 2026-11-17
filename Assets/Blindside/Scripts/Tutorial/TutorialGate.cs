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
        /// <summary>A real match is teaching (the first operation's lessons, TutorialDeployGuide): the menu offers only the Allowed commands and
        /// MoveFinished / ShotFinished are raised — nothing else of the tutorial mode (the match stays a normal one).</summary>
        public static bool Guided;
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

        public static void RaiseShotFinished(SimUnit u, Blindside.Combat.ICombatUnit target, bool gadget) { if (Active || Guided) ShotFinished?.Invoke(u, target, gadget); }

        /// <summary>The last FIRE's direction (grid radians) and how many walls its line went through (the dummy lesson judges it).</summary>
        public static float LastShotAngle;
        public static int LastShotWalls;

        /// <summary>The player's drone finished a flight (the unit's turn is then spent).</summary>
        public static event System.Action<SimUnit> DroneFinished;
        public static void RaiseDroneFinished(SimUnit u) { if (Active || Guided) DroneFinished?.Invoke(u); }

        /// <summary>The player's unit finished a throw (it has landed and gone off): where it landed.</summary>
        public static event System.Action<SimUnit, UnityEngine.Vector2> ThrowFinished;
        public static void RaiseThrowFinished(SimUnit u, UnityEngine.Vector2 land) { if (Active) ThrowFinished?.Invoke(u, land); }

        /// <summary>Which wall panels a panel ability may target (null: every valid one) — the lesson's one wall.</summary>
        public static System.Func<int, bool> PanelFilter;

        /// <summary>Which of the player's units can be selected (null: any) — the scripted operation's one unit at a time.</summary>
        public static System.Func<SimUnit, bool> Selectable;
        /// <summary>Where [作戦開始] drops a unit (null, or a null answer: the map's default points) — the scripted fixed deployment.</summary>
        public static System.Func<SimUnit, UnityEngine.Vector2?> FixedDrop;
        /// <summary>END TURN (and the automatic turn end when everyone has acted) is held during a guided match until this is set.</summary>
        public static bool EndTurnOk = true;
        /// <summary>The squad's radio calls (an enemy sighted, an engagement, a kill) stay off — the scripted operation's radio talks instead.</summary>
        public static bool NoCallouts;

        public static bool Allows(string commandId) => (!Active && !Guided) || Allowed.Contains(commandId);

        public static void Allow(params string[] ids)
        {
            Allowed.Clear();
            foreach (var id in ids) Allowed.Add(id);
        }

        public static void RaiseMoveFinished(SimUnit u) { if (Active || Guided) MoveFinished?.Invoke(u); }

        /// <summary>Everything back to a normal match.</summary>
        public static void Reset()
        {
            Active = false; BlockInput = false; NoAutoPath = false; HoldInput = false; Guided = false;
            Allowed.Clear();
            MoveFinished = null;
            ShotFinished = null;
            DroneFinished = null;
            ThrowFinished = null;
            PanelFilter = null;
            Selectable = null; FixedDrop = null; EndTurnOk = true; NoCallouts = false;
            Blindside.Turns.EnemyAI.HoldTeam = null;
            Blindside.Combat.CombatSystem.FloorHP = 0;
            Blindside.Combat.CombatSystem.FloorTeam = null;
            Blindside.Combat.CombatSystem.SureKillTeam = null;
            Blindside.Movement.MovementSystem.QuietMoves = false;
            Blindside.Movement.MovementSystem.EncounterHook = null;
        }
    }
}
