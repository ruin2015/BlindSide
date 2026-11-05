// Every number the battle cinematic (BattleDirector) uses. Seconds are unscaled (real) time; lengths are
// metres. Values marked "(derived)" weren't part of the spec and are this implementation's own choices.
using UnityEngine;

namespace Blindside.Cinematic
{
    [CreateAssetMenu(fileName = "BattleCamProfile", menuName = "Blindside/Battle Cam Profile")]
    public class BattleCamProfile : ScriptableObject
    {
        [Header("Two-shot framing")]
        [Tooltip("Camera height above the floor.")]
        public float cameraHeight = 2.4f;
        [Tooltip("Distance to the pair = clamp(gap × distanceFactor + distanceOffset, minDistance, maxDistance).")]
        public float distanceFactor = 0.9f;
        public float distanceOffset = 3f;
        public float minDistance = 4.5f;
        public float maxDistance = 14f;
        [Tooltip("Battle FOV (vertical).")]
        public float fov = 30f;
        [Tooltip("The normal game camera's FOV (BattlefieldCamera.Fov) — reference only.")]
        public float normalFov = 38f;
        [Tooltip("Screen x (0..1) of the shooter; the target sits mirrored at 1 - this.")]
        public float shooterScreenX = 1f / 3f;
        [Tooltip("(derived) Height the two-shot aims at, between torso (.5) and head (.8).")]
        public float frameHeight = 0.75f;
        [Tooltip("(derived) Distance multipliers of the six camera candidates (× swing toward shooter / toward target).")]
        public float[] candidateDistanceScales = { 1f, 0.8f, 1.25f };

        [Header("Timing")]
        public float enterSeconds = 0.4f;
        public float exitSeconds = 0.4f;
        public float hitCutSeconds = 0.25f;
        public float counterSeconds = 0.25f;

        [Header("Hit cut")]
        [Tooltip("Weapons firing this many shots or more only cut on a headshot or a death.")]
        public int manyShotThreshold = 4;
        [Tooltip("(derived) Camera distance from the hit unit.")]
        public float hitCutDistance = 3.2f;
        [Tooltip("(derived) Camera height for the hit cut.")]
        public float hitCutHeight = 1.7f;

        [Header("Headshot close-up")]
        public float headshotFov = 22f;
        [Tooltip("Camera distance in front of the head anchor.")]
        public float headshotDistance = 2.2f;
        [Tooltip("How long the close-up holds (blend-in included).")]
        public float headshotSeconds = 0.45f;
        [Tooltip("(derived) Blend into the close-up.")]
        public float headshotBlendIn = 0.08f;
        public float headshotSlowmoScale = 0.3f;
        public float headshotSlowmoSeconds = 0.35f;
        [Tooltip("Zoom punch layered on the close-up (CameraPunch's headshot values).")]
        public float zoomPunchFactor = 0.93f;
        public float zoomPunchSeconds = 0.32f;

        [Header("Death")]
        public float deathSlowmoScale = 0.25f;
        public float deathSlowmoSeconds = 0.5f;
        public float deathOrbitDegrees = 25f;
        public float deathOrbitSeconds = 0.9f;
        [Tooltip("(derived) How long the K.I.A popup stays up.")]
        public float kiaSeconds = 1.3f;

        [Header("Shake (every shot)")]
        public float shotShake = 0.03f;
        public float headshotShake = 0.08f;

        [Header("Screen")]
        [Tooltip("Letterbox bar height, fraction of the screen height.")]
        public float letterboxHeight = 0.08f;
        [Tooltip("How dark the rest of the map goes (vignette intensity).")]
        public float mapDarkness = 0.6f;
        [Tooltip("Colour Adjustments saturation during a battle.")]
        public float saturation = -25f;
        [Tooltip("HUD alpha during a battle (the #combat cut-in is left alone).")]
        public float hudAlpha = 0.25f;
        [Tooltip("(derived) Alpha of walls between the camera and the fighters.")]
        public float occluderAlpha = 0.25f;

        [Header("Battle stage (Fire Emblem-style, full mode)")]
        [Tooltip("Full mode: after the opening shot on the map, zoom on the unit shot at, fade, and play the fight on BattleStage.")]
        public bool useBattleStage = true;
        public float stageZoomSeconds = 0.3f;
        [Tooltip("How long the zoom on the unit shot at holds before the fade (blend included).")]
        public float stageZoomHold = 0.6f;
        public float stageZoomDistance = 3.4f;
        public float stageZoomHeight = 1.6f;
        public float stageZoomFov = 26f;
        public float stageFadeOutSeconds = 0.3f;
        public float stageFadeInSeconds = 0.3f;
        [Tooltip("Minimum time per shot on the stage (weapons under manyShotThreshold shots).")]
        public float stageShotSeconds = 0.34f;
        [Tooltip("Minimum time per shot on the stage for weapons with manyShotThreshold shots or more.")]
        public float stageBurstShotSeconds = 0.14f;
        [Tooltip("How long a death plays on the stage before the fight goes on / ends.")]
        public float stageDeathSeconds = 1.2f;
        public float stageFov = 30f;

        [Header("Short mode")]
        [Tooltip("(derived) 'Short': no camera move, just this FOV (from the normal 38).")]
        public float shortZoomFov = 34f;
    }
}
