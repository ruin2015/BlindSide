// Port of G.marks + addMark(team,x,y,kind) from blindside_source/04_gameplay_combat_ai.js (~19):
//   function addMark(team,x,y,kind){ G.marks=G.marks.filter(m=>!(m.team===team&&Math.hypot(m.x-x,m.y-y)<.7));
//     G.marks.push({team,x,y,kind,until:G.phaseCount+2,t0:now()}); }
// A mark is `team`'s memory of a position (gunfire heard, a wall breaking, an enemy spotted). It stays live
// while `until > G.phaseCount`, i.e. through the phase it was made in and the next one.
using System.Collections.Generic;
using UnityEngine;
using Blindside.Data;

namespace Blindside.Vision
{
    /// <summary>m.kind: 'shot' (銃声) · 'breach' (破壊音) · 'drone' (敵を捕捉). Gadget (設置物を捕捉) is this port's
    /// addition: a drone's circle finding an enemy camera / electric field leaves the same kind of mark an enemy unit does.</summary>
    public enum MarkKind { Shot, Breach, Spotted, Gadget }

    public class Mark
    {
        public OperatorTeam Team;
        public float X, Y;
        public MarkKind Kind;
        public int Until;      // G.phaseCount+2 at creation
        public float T0;       // now() — Time.unscaledTime, seconds
    }

    public class MarkBoard
    {
        readonly System.Func<int> phaseCount;

        public readonly List<Mark> Marks = new List<Mark>();

        /// <param name="phaseCount">G.phaseCount (TurnManager.PhaseCount).</param>
        public MarkBoard(System.Func<int> phaseCount) { this.phaseCount = phaseCount; }

        public int PhaseCount => phaseCount != null ? phaseCount() : 0;

        /// <summary>addMark(team,x,y,kind): replaces `team`'s own mark within .7 of (x,y).</summary>
        public void Add(OperatorTeam team, float x, float y, MarkKind kind)
        {
            Marks.RemoveAll(m => m.Team == team && Mathf.Sqrt((m.X - x) * (m.X - x) + (m.Y - y) * (m.Y - y)) < .7f);
            Marks.Add(new Mark { Team = team, X = x, Y = y, Kind = kind, Until = PhaseCount + 2, T0 = Time.unscaledTime });
        }

        /// <summary>`m.until > G.phaseCount`.</summary>
        public bool IsLive(Mark m) => m.Until > PhaseCount;

        /// <summary>other(team).</summary>
        public static OperatorTeam Other(OperatorTeam team) => team == OperatorTeam.Attack ? OperatorTeam.Defense : OperatorTeam.Attack;
    }
}
