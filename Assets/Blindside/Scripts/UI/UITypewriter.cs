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
    //  Typewriter (intro caption typeIn)
    // ------------------------------------------------------------------
    public class UITypewriter : MonoBehaviour
    {
        public TMP_Text text; public float duration = 1.2f; public float delay = .15f;
        public void Play(string s) { StopAllCoroutines(); text.text = s; StartCoroutine(Run()); }
        IEnumerator Run()
        {
            text.ForceMeshUpdate(); int n = text.textInfo.characterCount; text.maxVisibleCharacters = 0;
            yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < duration) { t += Time.unscaledDeltaTime; text.maxVisibleCharacters = Mathf.CeilToInt(n * Mathf.Clamp01(t / duration)); yield return null; }
            text.maxVisibleCharacters = n;
        }
    }
}
