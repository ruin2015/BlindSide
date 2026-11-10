// Runs the squad HQ scene (SceneRole.Hub): builds the screen layer (HubScreen) on its canvas — the stations' labels, the room signs, the
// minimap (from the walls under HubWorld) — finds the stations (HubNpc), the areas (HubArea) and the player, and each frame:
//   · picks the station in reach — the nearest one within its radius, in front of the player rather than behind — for the prompt;
//     F / Enter / the pad's south button opens it, G / the pad's north button talks to an operator (the NPC turns to the player)
//   · shows the smallest area holding the player as the place name (HubScreen.SetLocation, redrawn only when it changes)
//   · ESC (nothing open) opens the HQ menu
//   · locks the cursor while walking (the camera turns with the mouse), frees it while a panel or the menu is open
// While a panel is open the player and the camera hold still (InputBlocked). No allocation per frame.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Blindside.UI;

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
        [Tooltip("Where the walls are (the minimap draws every child named Wall). Empty: the object named HubWorld.")]
        public Transform world;

        HubScreen screen;
        readonly List<HubNpc> npcs = new List<HubNpc>(12);
        readonly List<HubArea> areas = new List<HubArea>(12);
        HubNpc near;

        public HubScreen Screen => screen;

        void Awake()
        {
            Active = this;
            if (view == null) view = Camera.main;
            // an operator who hasn't joined the squad yet (GHOST, SILENCE before the rescue) isn't at the HQ
            foreach (var n in FindObjectsByType<HubNpc>(FindObjectsInactive.Exclude))
            {
                if (!n.CanTalk) continue;
                bool inSquad = false;
                foreach (var o in HubCatalog.Squad) if (o != null && o.key == n.operatorKey) { inSquad = true; break; }
                if (!inSquad) n.gameObject.SetActive(false);
            }
            npcs.AddRange(FindObjectsByType<HubNpc>(FindObjectsInactive.Exclude));
            areas.AddRange(FindObjectsByType<HubArea>(FindObjectsInactive.Exclude));
            if (canvas != null)
            {
                screen = HubScreen.Build(canvas.transform, view);
                screen.SetStations(npcs);
                screen.SetSigns(FindObjectsByType<HubSign>(FindObjectsInactive.Exclude));
                screen.BuildMinimap(Walls(), npcs, areas, player);
            }
            else Debug.LogError("[Hub] no canvas");
            Time.timeScale = 1f;
        }

        List<Bounds> Walls()
        {
            var list = new List<Bounds>(64);
            if (world == null) { var w = GameObject.Find("HubWorld"); if (w != null) world = w.transform; }
            if (world == null) return list;
            foreach (var r in world.GetComponentsInChildren<Renderer>())
                if (r.name == "Wall") list.Add(r.bounds);
            return list;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        void OnApplicationFocus(bool focus) { if (!focus) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }

        void Update()
        {
            if (screen == null || player == null) return;
            bool open = screen.AnyOpen;
            // the cursor: locked while walking, free over a panel
            var wantLock = open || !Application.isFocused ? CursorLockMode.None : CursorLockMode.Locked;
            if (Cursor.lockState != wantLock) { Cursor.lockState = wantLock; Cursor.visible = wantLock != CursorLockMode.Locked; }

            screen.SetLocation(AreaName());
            near = open ? null : Nearest();
            screen.Tick(near);

            var kb = Keyboard.current; var pad = Gamepad.current;
            if (!open && ((kb != null && kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed) || (pad != null && pad.startButton.wasPressedThisFrame)))
            {
                EscapeGate.Consume();
                screen.OpenMenu();
                return;
            }
            if (near == null) return;
            bool use = (kb != null && (kb.fKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                       || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool talk = near.CanTalk && ((kb != null && kb.gKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame));
            if (!use && !talk) return;
            near.Face(player.position);
            if (use) screen.Open(near); else screen.OpenTalk(near);
        }

        /// <summary>The smallest area holding the player ("" outside every one).</summary>
        string AreaName()
        {
            HubArea best = null;
            var p = player.position;
            for (int i = 0; i < areas.Count; i++)
            {
                var a = areas[i];
                if (a == null || !a.Contains(p)) continue;
                if (best == null || a.Extent < best.Extent) best = a;
            }
            return best != null ? best.displayName : "";
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
