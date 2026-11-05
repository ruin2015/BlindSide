// Split out of BlindsideUIAnim.cs: Unity only serializes one MonoScript "main class" per file
// reliably, so every MonoBehaviour that gets AddComponent'd (and, above all, ever saved into a
// prefab) needs its own file, or its component reference silently rots into "Missing (Mono Script)"
// on the next reload.
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    // ------------------------------------------------------------------
    //  Headshot: lock-on reticle on the head + tactical badge + a short white screen tick
    // ------------------------------------------------------------------
    //  0.00–0.22  the four corner brackets close in on the head (rotating 45° → 0), the badge slides in from the left
    //  0.22–0.40  lock: brackets blink white twice (steps), the crosshair arms draw out, the underline sweeps
    //  0.20–0.45  the detail line types in
    //  …–1.15     hold; the reticle fades out by 1.0
    //  1.15–1.50  the badge slides right and fades
    // Late in LateUpdate: it re-projects its world points after the camera (BattlefieldCamera runs at -100)
    // has been posed for this frame, so the badge and reticle stay on the unit while a battle cinematic moves the camera.
    [DefaultExecutionOrder(100)]
    public class UIHeadshotBadge : MonoBehaviour, ITimedEffect
    {
        public RectTransform badge;            // icon block + text block (pivot centre-bottom). Must be a direct child of the canvas root with centred anchors.
        public RectTransform icon;             // crosshair icon (spins in)
        public Image iconBg, textUnderline;
        /// <summary>Phase 4-4 kit skin: the diagonal hazard-stripe line along the badge's bottom (Deco) and the title's glitch burst.</summary>
        public HazardStripe hazard;
        public UIGlitchText glitch;
        public TMP_Text title, detail;         // "HEADSHOT!", "ヘッドショット · クリティカル ×2 · -40"
        public CanvasGroup group;
        public RawImage flash;                 // full-screen white tick (no texture)
        public Canvas canvas;

        [Header("Lock-on reticle")]
        public RectTransform reticle;          // direct child of the canvas root, centred anchors; placed on the head
        public CanvasGroup reticleGroup;
        public RectTransform[] brackets;       // 4 corner brackets: TL, TR, BL, BR (pivot at their corner)
        public RectTransform[] arms;           // 4 crosshair arms: left, right, up, down (pivot at the inner end)
        public Graphic[] reticleGraphics;      // every bracket/arm image (tinted, blinked on lock)

        bool inited;
        Color tint = UIPalette.Accent;

        void Init()
        {
            if (inited) return;
            inited = true;
            if (flash) { flash.texture = null; flash.raycastTarget = false; flash.color = new Color(1, 1, 1, 0); }
        }

        void Awake()
        {
            group.alpha = 0f;
            if (reticleGroup) reticleGroup.alpha = 0f;
            Init();
        }

        const float BadgeDur = 1.5f, FlashDur = .18f, ReticleDur = 1.0f;
        const float BracketFrom = 110f, BracketTo = 30f, ArmGap = 34f, ArmLen = 46f, SlideIn = 40f, SlideOut = 30f;
        public float Duration => BadgeDur;

        /// <summary>The badge's colours and damage line, without playing it.</summary>
        public void SetContent(int damage, bool enemyScored)
        {
            Color c = enemyScored ? UIPalette.Foe : UIPalette.Accent;
            tint = c;
            if (hazard != null) { hazard.stripeColor = c; hazard.SetVerticesDirty(); }
            iconBg.color = c; textUnderline.color = c; title.color = enemyScored ? UIPalette.Hex("#FF8A98") : UIPalette.Accent;
            detail.text = $"ヘッドショット · クリティカル ×2 · -{damage}";
            if (reticleGraphics != null) foreach (var g in reticleGraphics) if (g) g.color = c;
        }

        // The world points the badge and the reticle stay pinned to, re-projected every frame.
        Vector3 trackWorld, reticleWorld; Camera trackCam; bool tracking, reticleTracking; float rise;
        Vector2 badgeBase;

        /// <param name="worldPos">Where the badge sits (the unit's overhead label point).</param>
        /// <param name="enemyScored">true when an enemy headshots our unit → red variant.</param>
        /// <param name="headWorld">Where the reticle locks (the head); defaults to just under the badge point.</param>
        public void Play(Vector3 worldPos, Camera cam, int damage, bool enemyScored, Vector3? headWorld = null)
        {
            trackWorld = worldPos; trackCam = cam; tracking = true; rise = 0f;
            reticleWorld = headWorld ?? worldPos; reticleTracking = reticle != null;
            badgeBase = Project(trackWorld);
            badge.anchoredPosition = badgeBase;
            if (reticle) reticle.anchoredPosition = Project(reticleWorld);
            SetContent(damage, enemyScored);
            if (glitch) glitch.Burst(.6f);
            StopAllCoroutines(); StartCoroutine(Run());
        }

        Vector2 Project(Vector3 world)
        {
            if (trackCam == null) return badge.anchoredPosition;
            Vector2 screen = trackCam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
            return local;
        }

        void LateUpdate()
        {
            if (tracking) badgeBase = Project(trackWorld) + new Vector2(0f, rise);
            if (reticleTracking && reticle) reticle.anchoredPosition = Project(reticleWorld);
        }

        IEnumerator Run()
        {
            float t = 0f;
            while (t < BadgeDur)
            {
                t += Time.unscaledDeltaTime;
                Evaluate(t);
                if (t > 1.1f) rise += 12f * Time.unscaledDeltaTime;
                yield return null;
            }
            Evaluate(BadgeDur);
            tracking = false; reticleTracking = false;
        }

        /// <summary>Badge, reticle and flash `t` seconds into a play. The badge is posed around badgeBase (the caller's
        /// placement in a preview); t ≥ 1.5: gone, as after a play.</summary>
        public void Evaluate(float t)
        {
            Init();
            if (!tracking) badgeBase = badge.anchoredPosition - BadgeOffset(lastT);
            lastT = t;
            PoseBadge(t);
            PoseReticle(t);
            PoseFlash(t);
        }

        float lastT = -1f;   // -1: nothing posed yet, so the badge's current position is its base

        // The badge's own slide relative to its base point.
        static Vector2 BadgeOffset(float t)
        {
            if (t < 0f) return Vector2.zero;
            if (t < .18f) return new Vector2(-SlideIn * (1f - UIEase.Tactical(t / .18f)), 0f);
            if (t > 1.15f) return new Vector2(SlideOut * UIEase.Tactical(Mathf.Clamp01((t - 1.15f) / .35f)), 0f);
            return Vector2.zero;
        }

        void PoseBadge(float t)
        {
            if (t >= BadgeDur) { group.alpha = 0f; return; }
            badge.anchoredPosition = badgeBase + BadgeOffset(t);
            badge.localScale = Vector3.one;
            badge.localRotation = Quaternion.identity;
            group.alpha = t < .18f ? UIEase.Tactical(t / .18f) : t > 1.15f ? Mathf.Clamp01(1f - (t - 1.15f) / .35f) : 1f;

            float sp = UIEase.Tactical(Mathf.Clamp01(t / .3f));
            icon.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(90f, 0f, sp));
            icon.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, sp);

            float u = UIEase.Tactical(Mathf.Clamp01((t - .1f) / .3f));
            textUnderline.rectTransform.localScale = new Vector3(u, 1f, 1f);

            int len = detail.text != null ? detail.text.Length : 0;
            detail.maxVisibleCharacters = t >= .45f ? 99999 : Mathf.FloorToInt(len * Mathf.Clamp01((t - .2f) / .25f));
        }

        void PoseReticle(float t)
        {
            if (reticle == null || reticleGroup == null) return;
            if (t >= ReticleDur) { reticleGroup.alpha = 0f; return; }
            float close = UIEase.Tactical(Mathf.Clamp01(t / .22f));
            float d = Mathf.Lerp(BracketFrom, BracketTo, close);
            reticle.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(45f, 0f, close));
            reticleGroup.alpha = t < .08f ? t / .08f : t > .85f ? Mathf.Clamp01(1f - (t - .85f) / .15f) : 1f;
            if (brackets != null)
                for (int i = 0; i < brackets.Length && i < 4; i++)
                    if (brackets[i]) brackets[i].anchoredPosition = new Vector2(i % 2 == 0 ? -d : d, i < 2 ? d : -d);

            float arm = UIEase.Tactical(Mathf.Clamp01((t - .22f) / .18f));
            if (arms != null)
                for (int i = 0; i < arms.Length && i < 4; i++)
                    if (arms[i]) arms[i].localScale = i < 2 ? new Vector3(arm, 1f, 1f) : new Vector3(1f, arm, 1f);

            // lock blink: white at .22–.26 and .30–.34 (steps), the tint otherwise
            bool white = (t >= .22f && t < .26f) || (t >= .30f && t < .34f);
            if (reticleGraphics != null)
                foreach (var g in reticleGraphics) if (g) g.color = white ? Color.white : tint;
        }

        void PoseFlash(float t)
        {
            if (!flash) return;
            float a = t < FlashDur ? .3f * (1f - t / FlashDur) : 0f;
            flash.color = new Color(.92f, .96f, 1f, a);
        }
    }
}
