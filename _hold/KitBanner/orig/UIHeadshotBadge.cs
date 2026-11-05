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
    //  Headshot: badge + screen flash + hit stop
    // ------------------------------------------------------------------
    // Late in LateUpdate: it re-projects its world point after the camera (BattlefieldCamera runs at -100)
    // has been posed for this frame, so the badge stays on the head while a battle cinematic moves the camera.
    [DefaultExecutionOrder(100)]
    public class UIHeadshotBadge : MonoBehaviour, ITimedEffect
    {
        public RectTransform badge;            // icon block + text block (pivot centre-bottom). Must be a direct child of the canvas root with centred anchors.
        public RectTransform icon;             // crosshair icon (spins in)
        public Image iconBg, textUnderline;
        public TMP_Text title, detail;         // "HEADSHOT!", "ヘッドショット · クリティカル ×2 · -40"
        public CanvasGroup group;
        public RawImage flash;                 // full-screen overlay, texture generated at runtime
        public Canvas canvas;
        Texture2D speedLines; bool inited;

        // the flash's speed-line texture (Awake in Play; the first Evaluate in an edit-mode preview)
        void Init()
        {
            if (inited) return;
            inited = true;
            if (flash) { speedLines = MakeSpeedLines(512); flash.texture = speedLines; flash.raycastTarget = false; flash.color = new Color(1, 1, 1, 0); }
        }

        void Awake()
        {
            group.alpha = 0f;
            Init();
        }

        const float BadgeDur = 1.5f, FlashDur = .45f;
        public float Duration => BadgeDur;

        /// <summary>The badge's colours and damage line, without playing it.</summary>
        public void SetContent(int damage, bool enemyScored)
        {
            Color c = enemyScored ? UIPalette.Foe : UIPalette.Accent;
            iconBg.color = c; textUnderline.color = c; title.color = enemyScored ? UIPalette.Hex("#FF8A98") : UIPalette.Accent;
            detail.text = $"ヘッドショット · クリティカル ×2 · -{damage}";
        }

        /// <param name="enemyScored">true when an enemy headshots our unit → red variant.</param>
        // The world point the badge (and the flash's clear centre) stays pinned to, re-projected every frame.
        Vector3 trackWorld; Camera trackCam; bool tracking, flashTracking; float rise;

        public void Play(Vector3 worldPos, Camera cam, int damage, bool enemyScored)
        {
            trackWorld = worldPos; trackCam = cam; tracking = true; rise = 0f;
            var local = Project();
            badge.anchoredPosition = local;
            SetContent(damage, enemyScored);
            StopAllCoroutines(); StartCoroutine(RunBadge()); if (flash) StartCoroutine(RunFlash(local));
        }

        Vector2 Project()
        {
            if (trackCam == null) return badge.anchoredPosition;
            Vector2 screen = trackCam.WorldToScreenPoint(trackWorld);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
            return local;
        }

        void LateUpdate()
        {
            if (!tracking) return;
            var local = Project();
            badge.anchoredPosition = local + new Vector2(0f, rise);
            if (flashTracking && flash) flash.rectTransform.anchoredPosition = local;
        }

        IEnumerator RunBadge()
        {
            float t0 = 0f;
            while (t0 < BadgeDur)
            {
                t0 += Time.unscaledDeltaTime;
                PoseBadge(t0);
                rise += t0 > 1.1f ? 12f * Time.unscaledDeltaTime : 0f;
                yield return null;
            }
            group.alpha = 0f;
            tracking = false;
        }

        static readonly (float t, float s)[] ScaleKeys = { (0f, 2.4f), (.135f, .92f), (.225f, 1.06f), (.33f, 1f) };

        void PoseBadge(float t0)
        {
            var k = ScaleKeys;
            float scale = 1f;
            for (int i = 0; i < k.Length - 1; i++) if (t0 >= k[i].t && t0 < k[i + 1].t) scale = Mathf.Lerp(k[i].s, k[i + 1].s, UIEase.Tactical((t0 - k[i].t) / (k[i + 1].t - k[i].t)));
            badge.localScale = Vector3.one * scale;
            badge.localRotation = Quaternion.Euler(0, 0, t0 < .135f ? Mathf.Lerp(10f, 4f, t0 / .135f) : 4f);
            group.alpha = t0 < .135f ? t0 / .135f : t0 > 1.15f ? Mathf.Clamp01(1f - (t0 - 1.15f) / .35f) : 1f;
            float sp = UIEase.Tactical(Mathf.Clamp01(t0 / .6f));
            icon.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(120f, 0f, sp)); icon.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, sp);
        }

        IEnumerator RunFlash(Vector2 centre)
        {
            var rt = flash.rectTransform; rt.anchoredPosition = centre;
            flashTracking = true;
            float t = 0f;
            while (t < FlashDur)
            {
                t += Time.unscaledDeltaTime;
                PoseFlash(t);
                yield return null;
            }
            flash.color = new Color(1, 1, 1, 0);
            flashTracking = false;
        }

        void PoseFlash(float t)
        {
            float p = t / FlashDur;
            Color c = p < .25f ? Color.Lerp(Color.white, new Color(1f, .35f, .45f), p / .25f) : new Color(1f, .35f, .45f);
            c.a = p < .25f ? 1f : Mathf.Lerp(.9f, 0f, (p - .25f) / .75f);
            flash.color = c;
        }

        /// <summary>The badge and the screen flash `t` seconds into a play, where they stand (the caller places the badge; the slow rise
        /// after 1.1s is not applied). t ≥ 1.5: gone, as after RunBadge.</summary>
        public void Evaluate(float t)
        {
            Init();
            if (t >= BadgeDur) group.alpha = 0f; else PoseBadge(t);
            if (flash) { if (t < FlashDur) PoseFlash(t); else flash.color = new Color(1, 1, 1, 0); }
        }

        /// <summary>Radial speed lines with a clear centre (replaces the CSS conic-gradient + mask).</summary>
        static Texture2D MakeSpeedLines(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size]; float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c, r = Mathf.Sqrt(dx * dx + dy * dy) / c;
                    float ang = (Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360f) % 7f;       // 7° period, 1.4° ray
                    float ray = ang < 1.4f ? .42f : 0f;
                    float ring = Mathf.Abs(r - .24f) < .006f ? .55f : 0f;
                    float mask = Mathf.Clamp01((r - .27f) / .45f);                        // clear centre, fade in outward
                    byte a = (byte)(255 * Mathf.Clamp01(ray * mask + ring));
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px); tex.Apply(); return tex;
        }
    }
}
