// The tutorial's own map, built in code (never saved, never listed): a training yard on the west, a small building on the east.
// Grid as everywhere: x right, y down the rows.
//   yard        outdoor floor, FLARE starts at Start; the demo walks to DemoEnd, lesson 1 goes straight on to Move1, lesson 2 has to
//               go round the low wall (MoveWall*) to Move2
//   building    x 14..25, y 2..16 — ordinary (breakable) outer walls, a door on the west side, a partition at x 19 (solid at the top,
//               breakable below, one door) and a reinforced stretch in the cross wall at y 9 — the later lessons' walls
//   hostage     the map needs one on indoor floor: a 1-cell pocket walled in by solid blocks in the top-left corner, out of reach and
//               out of sight (the tutorial also hides its figure)
using UnityEngine;
using Blindside.Map;

namespace Blindside.Tutorial
{
    public static class TutorialMap
    {
        public const int W = 26, H = 18;
        public const string Name = "訓練場";

        public static readonly Vector2Int Start = new Vector2Int(2, 14);
        public static readonly Vector2[] DemoPath = { new Vector2(2f, 14f), new Vector2(3.4f, 13.6f), new Vector2(4.6f, 13.1f), new Vector2(5f, 13f) };
        public static Vector2 DemoEnd => DemoPath[DemoPath.Length - 1];
        public static readonly Vector2 Move1 = new Vector2(10f, 13f);
        public const float Move1Budget = 6.5f;
        public static readonly Vector2 Move2 = new Vector2(9f, 8f);
        public const float Move2Budget = 11f;
        public const int MoveWallY = 11, MoveWallX0 = 7, MoveWallX1 = 11;
        /// <summary>How close the unit has to stop to a target to count as there.</summary>
        public const float ArriveRadius = .9f;

        // stage 2 — shooting: the yard's camera (also the map's fixed camera), where CRUSHER stands for the duel, and the wall tour
        public static readonly Vector2Int CameraCell = new Vector2Int(5, 5);
        public static readonly Vector2 DuelSpot = new Vector2(4f, 8f);
        public static readonly Vector2 WallThrough = new Vector2(14f, 11f);   // ordinary (breakable) outer wall: shot through
        public static readonly Vector2 WallSolid = new Vector2(19f, 5f);      // solid partition: stops a shot
        // the training dummy behind the ordinary outer wall (from Move2 the line crosses the wall at (14,10))
        public static readonly Vector2 DummySpot = new Vector2(16f, 11f);

        // stage 3 — vision: CRUSHER walks from the duel spot round the low wall's west end and hides south of it, watching that end of the
        // wall. Coming round the west end walks into his cone (he sees first, or both at once); round the east end, FLARE comes up beside /
        // behind him and sees first.
        public static readonly Vector2[] HideRoute = { new Vector2(4f, 8f), new Vector2(5f, 12f), new Vector2(8f, 14f) };
        public static Vector2 HideSpot => HideRoute[HideRoute.Length - 1];
        public static readonly float HideFacing = Mathf.Atan2(MoveWallY - 14f, (MoveWallX0 - 1f) - 8f);   // toward (6,11): north-west
        public const float HideBudget = 9f;
        public const float LockBudget = 4f;

        // stage 4 — drone: CRUSHER goes into the building's south-west room (out of sight from the yard), FLARE waits outside the west
        // door and flies the drone in. Breaching: CRUSHER hammers the room's west wall from inside; FLARE walks in and opens the
        // reinforced stretch of the cross wall with two launcher charges (hole, then fully open).
        public static readonly Vector2[] DroneHideRoute = { new Vector2(8f, 14f), new Vector2(12f, 15.4f), new Vector2(14f, 13f), new Vector2(16.4f, 13f), new Vector2(17f, 11f) };
        public static readonly Vector2 DroneStart = new Vector2(12f, 13f);
        public static readonly Vector2[] HammerRoute = { new Vector2(17f, 11f), new Vector2(15f, 11f) };
        public static readonly Vector2Int HammerWall = new Vector2Int(14, 11);         // ordinary outer wall, beside CRUSHER
        public static readonly Vector2[] LauncherRoute = { new Vector2(12f, 13f), new Vector2(14f, 13f), new Vector2(16f, 12.6f) };
        public static readonly Vector2Int ReinforcedCell = new Vector2Int(16, 9);      // the reinforced stretch (16,9)-(17,9)
        public static readonly Vector2 ReinforcedCentre = new Vector2(16.5f, 9f);
        public static readonly Vector2 SolidCentre = new Vector2(19f, 5f);

        public static readonly Vector2Int CrusherHome = new Vector2Int(22, 13);
        public static readonly Vector2Int HostagePocket = new Vector2Int(1, 1);

        public static MapData Build()
        {
            var t = new MapTerrain[W * H];
            var crates = new bool[W * H];
            void Set(int x, int y, MapTerrain v) => t[y * W + x] = v;
            void Rect(int x0, int y0, int x1, int y1, MapTerrain v) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(x, y, v); }

            // hostage pocket
            Rect(0, 0, 2, 2, MapTerrain.Solid);
            Set(HostagePocket.x, HostagePocket.y, MapTerrain.In);

            // lesson 2's low wall across the yard
            Rect(MoveWallX0, MoveWallY, MoveWallX1, MoveWallY, MapTerrain.Solid);

            // the building
            Rect(14, 2, 25, 16, MapTerrain.Breakable);
            Rect(15, 3, 24, 15, MapTerrain.In);
            foreach (var c in new[] { (14, 2), (25, 2), (14, 16), (25, 16), (19, 2), (19, 16), (14, 9), (25, 9) }) Set(c.Item1, c.Item2, MapTerrain.Solid);
            Set(14, 13, MapTerrain.In);                                  // west door
            Rect(19, 3, 19, 8, MapTerrain.Solid);                        // partition: solid at the top …
            Rect(19, 9, 19, 15, MapTerrain.Breakable);                   // … breakable below
            Set(19, 12, MapTerrain.In);                                  // its door
            Rect(15, 9, 18, 9, MapTerrain.Breakable);                    // west cross wall
            Set(16, 9, MapTerrain.Reinforced); Set(17, 9, MapTerrain.Reinforced);
            Rect(20, 9, 24, 9, MapTerrain.Breakable);                    // east cross wall
            Set(22, 9, MapTerrain.In);                                   // its door
            crates[5 * W + 22] = true; crates[14 * W + 16] = true;

            var d = new MapData
            {
                name = Name, width = W, height = H, useHouseIntro = false,
                terrain = t, crates = crates,
                cameras = new[] { CameraCell },
                hostage = HostagePocket,
                exfils = new[] { new MapExfil { name = "A", x0 = 24, y0 = 0, x1 = 25, y1 = 0 } },
                defenderStarts = new MapDefenderStart[MapData.DefenderKeys.Length],
                anchors = new MapAnchors
                {
                    attackerDrops = new[] { Start, new Vector2Int(3, 16), new Vector2Int(4, 16), new Vector2Int(5, 16), new Vector2Int(6, 16) },
                    facingTarget = new Vector2(19.5f, 9f),
                },
            };
            // only the first start is used (CRUSHER's home); the map format wants all five
            for (int i = 0; i < d.defenderStarts.Length; i++)
                d.defenderStarts[i] = new MapDefenderStart { operatorKey = MapData.DefenderKeys[i], cell = i == 0 ? CrusherHome : new Vector2Int(20 + i, 15), facing = Mathf.PI };
            return d;
        }
    }
}
