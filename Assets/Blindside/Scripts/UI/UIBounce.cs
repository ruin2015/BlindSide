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
    /// <summary>bounce: the "!" above a unit hops 6px.</summary>
    public class UIBounce : MonoBehaviour
    {
        public float height = 6f; public float halfPeriod = .32f; Vector3 b;
        void OnEnable() { b = transform.localPosition; }
        void Update() { float p = Mathf.PingPong(Time.unscaledTime / halfPeriod, 1f); transform.localPosition = b + Vector3.up * height * UIEase.InOut(p); }
        void OnDisable() { transform.localPosition = b; }
    }
}
