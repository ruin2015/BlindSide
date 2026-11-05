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
    /// <summary>scanY: faint blue band sweeping top→bottom across the HUD every 9s.</summary>
    public class UIScanLine : MonoBehaviour
    {
        public float period = 9f; public float bandHeight = 120f;
        RectTransform rt, parent;
        void Awake() { rt = (RectTransform)transform; parent = (RectTransform)transform.parent; }
        void Update()
        {
            float h = parent.rect.height, p = (Time.unscaledTime % period) / period;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, Mathf.Lerp(bandHeight, -h - bandHeight, p));
        }
    }
}
