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
    /// <summary>Camera shake + zoom punch (headshot: Kick(.32f) + ZoomPunch(.93f, .32f)).
    /// Put this on a child object of the camera rig (or the camera itself if nothing else moves it).</summary>
    public class CameraPunch : MonoBehaviour
    {
        public Camera cam; public float decay = .86f;
        float shake; Vector3 offset;
        public void Kick(float amount) { if (UIMotionSettings.reduceMotion) return; shake = Mathf.Max(shake, amount); }   // 「モーション軽減」: no camera shake
        public void ZoomPunch(float factor = .93f, float back = .32f) { StartCoroutine(Zoom(factor, back)); }
        IEnumerator Zoom(float factor, float back)
        {
            if (!cam) yield break;
            bool ortho = cam.orthographic; float b = ortho ? cam.orthographicSize : cam.fieldOfView;
            float t = 0f;
            while (t < back)
            {
                t += Time.unscaledDeltaTime; float v = Mathf.Lerp(b * factor, b, UIEase.InOut(t / back));
                if (ortho) cam.orthographicSize = v; else cam.fieldOfView = v; yield return null;
            }
            if (ortho) cam.orthographicSize = b; else cam.fieldOfView = b;
        }
        void LateUpdate()
        {
            transform.localPosition -= offset;
            offset = shake > .02f ? new Vector3(Random.Range(-shake, shake), 0, Random.Range(-shake, shake)) : Vector3.zero;
            transform.localPosition += offset;
            shake *= Mathf.Pow(decay, Time.unscaledDeltaTime * 60f);
        }
    }
}
