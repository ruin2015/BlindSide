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
    /// <summary>Hit stop: freezes gameplay (timeScale 0) while UI keeps animating on unscaled time.</summary>
    public class HitStop : MonoBehaviour
    {
        static HitStop inst; static float resumeScale = 1f; static Coroutine running;
        public static void Do(float seconds)
        {
            if (!inst) inst = new GameObject("HitStop").AddComponent<HitStop>();
            if (running == null) resumeScale = Time.timeScale;
            else inst.StopCoroutine(running);
            running = inst.StartCoroutine(inst.Run(seconds));
        }
        IEnumerator Run(float s)
        {
            Time.timeScale = 0f;
            try { yield return new WaitForSecondsRealtime(s); }
            finally { Time.timeScale = resumeScale; running = null; }
        }
    }
}
