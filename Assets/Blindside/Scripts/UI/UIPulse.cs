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
    /// <summary>pulse: a glow Image behind the button grows and fades (END TURN when all units are done).</summary>
    public class UIPulse : MonoBehaviour
    {
        public Image glow; public float period = 1.2f; public float growPixels = 12f;
        RectTransform grt; Vector2 baseSize;
        void Awake() { if (glow) { grt = glow.rectTransform; baseSize = grt.sizeDelta; glow.raycastTarget = false; } }
        /// <summary>Phase 4-5: the kit END TURN button; SetOn drives its brightness pulse (glow stays unused then).</summary>
        public KitEndButton kit;
        public void SetOn(bool on) { enabled = on; if (glow) glow.enabled = on; if (kit != null) kit.SetPulse(on); }
        void Update()
        {
            if (!glow) return;
            float p = (Time.unscaledTime % period) / period, k = Mathf.Sin(p * Mathf.PI);
            grt.sizeDelta = baseSize + Vector2.one * growPixels * 2f * k;
            var c = UIPalette.Blue; c.a = .7f * (1f - k); glow.color = c;
        }
    }
}
