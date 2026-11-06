// Runs the squad HQ scene (SceneRole.Hub): builds the screen layer (HubScreen) on its canvas, finds the stations (HubNpc) and the
// player, and each frame picks the station in reach — the nearest one within its radius, in front of the player rather than
// behind — for the prompt; F / Enter / the pad's south button opens it (the NPC turns to the player). While a panel is open the
// player and the camera hold still (InputBlocked). No allocation per frame.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blindside.Hub
{
    public class HubDirector : MonoBehaviour
    {
        public static HubDirector Active { get; private set; }
        /// <summary>A panel is open: no walking, no camera.</summary>
        public static bool InputBlocked => Active != null && Active.screen != null && Active.screen.AnyOpen;

        public Transform player;
        public Canvas canvas;
        public Camera view;

        HubScreen screen;
        readonly List<HubNpc> npcs = new List<HubNpc>(12);
        HubNpc near;

        public HubScreen Screen => screen;

        void Awake()
        {
            Active = this;
            if (view == null) view = Camera.main;
            npcs.AddRange(FindObjectsByType<HubNpc>(FindObjectsInactive.Exclude));
            if (canvas != null)
            {
                screen = HubScreen.Build(canvas.transform, view);
                screen.SetStations(npcs);
                screen.SetSigns(FindObjectsByType<HubSign>(FindObjectsInactive.Exclude));
            }
            else Debug.LogError("[Hub] no canvas");
            Time.timeScale = 1f;
        }

        void OnDestroy() { if (Active == this) Active = null; }

        void Update()
        {
            if (screen == null || player == null) return;
            near = InputBlocked ? null : Nearest();
            screen.Tick(near);
            if (near == null) return;
            var kb = Keyboard.current; var pad = Gamepad.current;
            bool use = (kb != null && (kb.fKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                       || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool talk = near.CanTalk && ((kb != null && kb.gKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame));
            if (!use && !talk) return;
            near.Face(player.position);
            if (use) screen.Open(near); else screen.OpenTalk(near);
        }

        HubNpc Nearest()
        {
            HubNpc best = null;
            float bestScore = float.MaxValue;
            var p = player.position; var f = player.forward;
            for (int i = 0; i < npcs.Count; i++)
            {
                var n = npcs[i];
                if (n == null || !n.isActiveAndEnabled) continue;
                var d = n.transform.position - p; d.y = 0f;
                float dist = d.magnitude;
                if (dist > n.radius) continue;
                float facing = dist > .01f ? Vector3.Dot(f, d / dist) : 1f;   // −1 behind … 1 ahead
                float score = dist * (1.6f - facing * .6f);
                if (score < bestScore) { bestScore = score; best = n; }
            }
            return best;
        }
    }
}
