// Single source of truth for mapping the game's grid-plane coordinates (x right, y increasing the
// same direction as the MAP string's rows in MapData.terrain — row 0/"top" is the extract zone)
// into Unity world space. Everywhere that used to build a world position or rotation straight out of
// grid X/Y/facing should go through this instead. Game logic (VisionSystem, EnemyAI pathing,
// TurnManager, CombatSystem, ...) stays entirely in grid coordinates and never needs to know this
// mapping exists — only the rendering/presentation side calls into this class.
using UnityEngine;

namespace Blindside.Rendering
{
    public static class GridSpace
    {
        /// <summary>Grid (x,y) -> world position at ground height (world Y = 0).</summary>
        public static Vector3 GridToWorld(float x, float y) => new Vector3(x, 0f, -y);

        /// <summary>Grid (x,y) -> world position at a given world-space height.</summary>
        public static Vector3 GridToWorld(float x, float y, float height) => new Vector3(x, height, -y);

        /// <summary>
        /// A grid-plane facing angle (radians, atan2(dy,dx)-style over grid X/Y) -> a world-space unit
        /// direction, ready for Quaternion.LookRotation(dir, Vector3.up).
        /// </summary>
        public static Vector3 FacingToWorldDirection(float facingRadians) =>
            new Vector3(Mathf.Cos(facingRadians), 0f, -Mathf.Sin(facingRadians));
    }
}
