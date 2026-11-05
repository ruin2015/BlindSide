// Split out of BlindsideUIAnim.cs: Unity only serializes one MonoScript "main class" per file
// reliably, so every MonoBehaviour that gets AddComponent'd (and, above all, ever saved into a
// prefab) needs its own file, or its component reference silently rots into "Missing (Mono Script)"
// on the next reload. Body copied verbatim from the original file.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    // ------------------------------------------------------------------
    //  Loops: blink / pulse / stripes / scan line / bounce
    // ------------------------------------------------------------------
    /// <summary>blink: steps(2) between 1 and lowAlpha.</summary>
    public class UIBlink : MonoBehaviour
    {
        public float period = 1f; public float lowAlpha = .35f;
        CanvasGroup cg;
        void Awake() { cg = UIUtil.GetOrAdd<CanvasGroup>(gameObject); }
        void Update() { cg.alpha = (Time.unscaledTime % period) < period * .5f ? 1f : lowAlpha; }
        void OnDisable() { if (cg) cg.alpha = 1f; }
    }
}
