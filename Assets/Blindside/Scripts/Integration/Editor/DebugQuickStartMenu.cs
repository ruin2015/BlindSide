// Blindside > Debug > Start Attack Phase: the menu half of DebugQuickStart (Integration/DebugQuickStart.cs). In Play mode only, it starts an
// attack-side match on the default map with the fixed seed, skipping the intro and the deployment (it calls TitleSelectScreen.Launch with
// skipIntro and presses [作戦開始] the way the START OP button does) and stops at round 1's attack phase. Nothing in the game logic is changed.
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
    }
}
