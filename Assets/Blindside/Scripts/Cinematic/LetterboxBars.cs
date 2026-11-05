// Port of the intro letterbox (`barIn`: height 0 -> 11% over 0.7s, per UI_ANIMATION_SPEC.md's
// animation table). Drives two full-width bars from a single 0..1 value so a Timeline
// AnimationTrack only needs one float curve.
// BattleDirector reuses the same bars for engagements through SetOverride (its own bar height as a
// fraction of the screen); ClearOverride hands them back to openAmount.
using UnityEngine;

namespace Blindside.Cinematic
{
    public class LetterboxBars : MonoBehaviour
    {
        public RectTransform topBar, bottomBar;
        [Range(0f, 1f)] public float openAmount;

        const float BarHeightPx = 118f; // ~11% of a 1080-tall reference canvas

        float overrideAmount = -1f, overrideFraction;

        /// <summary>Takes the bars over: `amount01` of a bar `heightFraction` of the canvas height tall.</summary>
        public void SetOverride(float amount01, float heightFraction)
        {
            overrideAmount = Mathf.Clamp01(amount01);
            overrideFraction = heightFraction;
            Update();
        }

        /// <summary>Back to openAmount (the intro Timeline's value).</summary>
        public void ClearOverride()
        {
            overrideAmount = -1f;
            Update();
        }

        void Update()
        {
            float h = openAmount * BarHeightPx;
            if (overrideAmount >= 0f)
            {
                var canvas = GetComponentInParent<Canvas>();
                float canvasH = canvas != null ? ((RectTransform)canvas.rootCanvas.transform).rect.height : 1080f;
                h = overrideAmount * overrideFraction * canvasH;
            }
            if (topBar) topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, h);
            if (bottomBar) bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, h);
        }
    }
}
