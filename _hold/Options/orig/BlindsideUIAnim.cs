// =====================================================================
//  BLINDSIDE — UI animation kit for Unity uGUI (+ TextMeshPro)
//  Ports the web version's CSS animations. No external tween library needed.
//  All UI motion uses unscaled time so it keeps running during HitStop.
//  Requires: Unity 2020.1+ (RectMask2D.padding), TextMeshPro.
//
//  Every MonoBehaviour that used to live in this file (UIStaggerIn, UIBlink, UIPulse,
//  UIStripeScroll, UIScanLine, UIBounce, UITypewriter, UIGlitchText, UIPhaseBanner,
//  UIContactAlert, UIOperatorArtCard, UIHeadshotBadge, HitStop, CameraPunch) has moved to its
//  own file — Unity only reliably serializes one MonoScript "main class" per file, and every
//  other class defined here was silently rotting into "Missing (Mono Script)" the moment it got
//  saved into a prefab. Only the plain static helpers (never themselves a Component) stay here.
// =====================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    // ------------------------------------------------------------------
    //  Easing / palette
    // ------------------------------------------------------------------
    public static class UIEase
    {
        /// <summary>Approximates CSS cubic-bezier(.2,.9,.2,1): fast out, soft stop.</summary>
        public static float Tactical(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 4f); }
        public static float InOut(float t) { t = Mathf.Clamp01(t); return t < .5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f; }
        public static float Linear(float t) { return Mathf.Clamp01(t); }
    }

    public static class UIUtil
    {
        /// <summary>Safe get-or-add (the ?? operator is unreliable with Unity's fake-null objects).</summary>
        public static T GetOrAdd<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();
        }
    }

    public static class UIPalette
    {
        public static readonly Color Paper = Hex("#ECEEF0");
        public static readonly Color Ink = Hex("#14161A");
        public static readonly Color Ink2 = Hex("#23272C");
        public static readonly Color Blue = Hex("#1F96E8");
        public static readonly Color Foe = Hex("#E5364D");
        public static readonly Color Accent = Hex("#FFC81E");
        public static readonly Color Dim = Hex("#6A7078");
        public static readonly Color GlitchRed = Hex("#FF3E5C");
        public static readonly Color GlitchBlue = Hex("#27B4FF");
        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    }

    // ------------------------------------------------------------------
    //  Generic coroutines (start them from any MonoBehaviour)
    // ------------------------------------------------------------------
    public static class UIAnim
    {
        /// <summary>dropIn / slideInL / slideInR / hintIn / logIn / combatIn / capIn.</summary>
        public static IEnumerator SlideFade(Transform tr, CanvasGroup cg, Vector3 basePos, Vector2 fromOffset, float dur, float delay = 0f)
        {
            Vector3 start = basePos + (Vector3)fromOffset;
            tr.localPosition = start; if (cg) cg.alpha = 0f;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float p = UIEase.Tactical(t / dur);
                tr.localPosition = Vector3.LerpUnclamped(start, basePos, p);
                if (cg) cg.alpha = Mathf.Clamp01(t / dur * 1.6f);
                yield return null;
            }
            tr.localPosition = basePos; if (cg) cg.alpha = 1f;
        }

        /// <summary>panelIn: reveal left→right (or right→left) with RectMask2D padding, alpha .4→1.</summary>
        public static IEnumerator ClipReveal(RectMask2D mask, CanvasGroup cg, float dur, float delay = 0f, bool leftToRight = true)
        {
            var rt = (RectTransform)mask.transform;
            float w = rt.rect.width;
            SetPad(mask, leftToRight, w); if (cg) cg.alpha = .4f;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float p = UIEase.Tactical(t / dur);
                SetPad(mask, leftToRight, w * (1f - p));
                if (cg) cg.alpha = Mathf.Lerp(.4f, 1f, p);
                yield return null;
            }
            mask.padding = Vector4.zero; if (cg) cg.alpha = 1f;
        }
        static void SetPad(RectMask2D m, bool leftToRight, float amount)
        {
            // padding: x=left, y=bottom, z=right, w=top
            m.padding = leftToRight ? new Vector4(0, 0, amount, 0) : new Vector4(amount, 0, 0, 0);
        }

        /// <summary>shakeX: left/right jitter.</summary>
        public static IEnumerator Shake(Transform tr, float amp, float dur, int times = 1)
        {
            Vector3 b = tr.localPosition;
            for (int n = 0; n < times; n++)
            {
                float t = 0f;
                while (t < dur)
                {
                    t += Time.unscaledDeltaTime; float p = t / dur;
                    float x = p < .25f ? -amp : p < .5f ? 0f : p < .75f ? amp : 0f;
                    tr.localPosition = b + new Vector3(x, 0, 0);
                    yield return null;
                }
            }
            tr.localPosition = b;
        }

        /// <summary>critPulse: color keys evenly over dur.</summary>
        public static IEnumerator ColorFlash(Graphic g, float dur, params Color[] keys)
        {
            if (keys.Length < 2) yield break;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / dur) * (keys.Length - 1);
                int i = Mathf.Min(keys.Length - 2, Mathf.FloorToInt(p));
                g.color = Color.Lerp(keys[i], keys[i + 1], p - i);
                yield return null;
            }
            g.color = keys[keys.Length - 1];
        }

        /// <summary>barIn / growX: animate size along one axis.</summary>
        public static IEnumerator Size(RectTransform rt, bool width, float from, float to, float dur, float delay = 0f)
        {
            var s = rt.sizeDelta; if (width) s.x = from; else s.y = from; rt.sizeDelta = s;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime; float v = Mathf.LerpUnclamped(from, to, UIEase.Tactical(t / dur));
                s = rt.sizeDelta; if (width) s.x = v; else s.y = v; rt.sizeDelta = s; yield return null;
            }
            s = rt.sizeDelta; if (width) s.x = to; else s.y = to; rt.sizeDelta = s;
        }
    }
}
