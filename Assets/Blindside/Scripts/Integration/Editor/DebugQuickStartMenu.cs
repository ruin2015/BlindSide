// Blindside > Debug > Start Attack Phase: the menu half of DebugQuickStart (Integration/DebugQuickStart.cs). In Play mode only, it starts an
// attack-side match on the default map with the fixed seed, skipping the intro and the deployment (it calls TitleSelectScreen.Launch with
// skipIntro and presses [作戦開始] the way the START OP button does) and stops at round 1's attack phase. Nothing in the game logic is changed.
// Blindside > Debug > Auto Clear Map: wins the running match for the player (the HUD's DEBUG 自動クリア button does the same).
using UnityEditor;
using UnityEngine;
using Blindside.Integration;

namespace Blindside.UITools.EditorTools
{
    public static class DebugQuickStartMenu
    {
        const string Path = "Blindside/Debug/Start Attack Phase";

        [MenuItem(Path)]
        public static void Start() { DebugQuickStart.Run(); }

        [MenuItem(Path, true)]
        static bool Validate() { return Application.isPlaying; }

        // Blindside > Debug > Auto Clear Map: the running match won for the player at once (PlayerSelectionController.DebugClearMap)
        const string ClearPath = "Blindside/Debug/Auto Clear Map";

        [MenuItem(ClearPath)]
        public static void AutoClear()
        {
            var ps = Object.FindAnyObjectByType<PlayerSelectionController>();
            if (ps != null) ps.DebugClearMap();
        }

        [MenuItem(ClearPath, true)]
        static bool ValidateClear() { return Application.isPlaying; }
    }
}
