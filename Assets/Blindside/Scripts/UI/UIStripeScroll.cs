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
    /// <summary>stripes: scroll a tiling stripe texture (hazard lines, dashed underline).</summary>
    public class UIStripeScroll : MonoBehaviour
    {
        public RawImage image; public float uvPerSecond = .7f;
        void Update() { if (!image) return; var r = image.uvRect; r.x = (r.x + uvPerSecond * Time.unscaledDeltaTime) % 1f; image.uvRect = r; }
    }
}
