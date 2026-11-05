// The UI's design values in one place: colours, slant widths, fonts, canvas reference resolutions, element sizes and motion
// timings, read from the target mockup (blindside_ui_kit/ui_target.html). One asset in a Resources folder
// (Resources/UITheme.asset) so a build carries it; read it anywhere through UITheme.Current.
//
// Canvases never hard-code a reference resolution: they call ApplyTo(scaler, kind) (or carry a UIThemeCanvas), so the menu
// and HUD scale can be changed here alone.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [CreateAssetMenu(menuName = "Blindside/UI Theme", fileName = "UITheme")]
    public class UITheme : ScriptableObject
    {
        public enum CanvasKind { Menu, Hud }
        public enum TextRole { Display, Body, Title }

        static UITheme current;

        /// <summary>Resources/UITheme.asset, or a default-valued instance when the asset is missing.</summary>
        public static UITheme Current
        {
            get
            {
                if (current == null) current = Resources.Load<UITheme>("UITheme");
                if (current == null) { current = CreateInstance<UITheme>(); current.hideFlags = HideFlags.DontSave; }
                return current;
            }
        }

        // ---- colours (:root) ----------------------------------------------------------------------------------------------
        [Header("Colours")]
        public Color ink = Hex("#14161A");
        public Color ink2 = Hex("#1E2228");
        public Color ink3 = Hex("#2A3038");
        public Color paper = Hex("#ECEEF0");
        public Color paper2 = Hex("#D5D9DD");
        public Color blue = Hex("#1F96E8");
        public Color foe = Hex("#E5364D");
        public Color acc = Hex("#FFC81E");
        public Color dim = Hex("#5D646C");
        public Color glitchRed = Hex("#FF3E5C");
        public Color glitchBlue = Hex("#27B4FF");

        // ---- slant widths (clip-path offsets, px) ---------------------------------------------------------------------------
        [Header("Slant (px)")]
        public float slantLarge = 22f;       // .slant / .slantL
        public float slantCardMin = 14f;     // .rc, .rc .por
        public float slantCardMax = 16f;     // .sq .por
        public float slantSmallMin = 10f;    // #cmd button
        public float slantSmallMax = 12f;    // #log div, .meta div, .lbar

        // ---- fonts ------------------------------------------------------------------------------------------------------------
        [Header("Fonts — primary asset per role, then its fallbacks in order")]
        public TMP_FontAsset displayFont;                                       // BSTactical SDF (var(--fd), var(--fm))
        public List<TMP_FontAsset> displayFallbacks = new List<TMP_FontAsset>(); // NotoJP-Black SDF, NotoJP-Dynamic SDF
        public TMP_FontAsset bodyFont;                                          // NotoJP-Medium SDF (var(--fk))
        public List<TMP_FontAsset> bodyFallbacks = new List<TMP_FontAsset>();    // NotoJP-Dynamic SDF
        public TMP_FontAsset titleFont;                                         // NotoJP-Black SDF (big Japanese headings)
        public List<TMP_FontAsset> titleFallbacks = new List<TMP_FontAsset>();   // NotoJP-Dynamic SDF
        [Tooltip("Smallest text size allowed anywhere (px in canvas units).")]
        public float minTextSize = 11f;

        public TMP_FontAsset Font(TextRole role)
        {
            switch (role)
            {
                case TextRole.Display: return displayFont;
                case TextRole.Title: return titleFont;
                default: return bodyFont;
            }
        }

        // ---- canvas scale ---------------------------------------------------------------------------------------------------
        [Header("Canvas reference resolution")]
        public Vector2 menuReference = new Vector2(1440f, 810f);
        public Vector2 hudReference = new Vector2(1800f, 1013f);
        [Range(0f, 1f)] public float canvasMatch = .5f;

        public Vector2 Reference(CanvasKind kind) { return kind == CanvasKind.Hud ? hudReference : menuReference; }

        /// <summary>Scale With Screen Size at this kind's reference resolution, match width/height.</summary>
        public void ApplyTo(CanvasScaler scaler, CanvasKind kind)
        {
            if (scaler == null) return;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference(kind);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = canvasMatch;
        }

        // ---- game HUD sizes (HUD canvas units) ------------------------------------------------------------------------------
        [Serializable]
        public struct HudSizes
        {
            [Header("Roster card (.rc)")]
            public Vector2 rosterCard; public float rosterFace; public float rosterGap; public Vector2 rosterOrigin;
            [Header("Command menu (#cmd button)")]
            public Vector2 commandButton; public float commandGap;
            [Header("Unit panel (#up)")]
            public Vector2 unitPanel; public float unitPanelFace; public float unitPanelFaceRise;
            [Header("Illustration card (#oc)")]
            public Vector2 artCard; public float artCardTag; public float artCardName;
            [Header("End turn (#end)")]
            public Vector2 endTurn;
            [Header("Top bar (#tb)")]
            public float topbarHeight; public float logoWidth; public float phaseWidth; public float roundWidth; public Vector2 objective;
            [Header("Hint / log / banner / dialog")]
            public Vector2 hint; public float logLineHeight; public float logWidth; public float bannerHeight; public float dialogWidth;
        }

        [Header("Game HUD sizes")]
        public HudSizes hud = new HudSizes
        {
            rosterCard = new Vector2(250f, 62f), rosterFace = 62f, rosterGap = 5f, rosterOrigin = new Vector2(20f, 76f),
            commandButton = new Vector2(226f, 40f), commandGap = 3f,
            unitPanel = new Vector2(430f, 190f), unitPanelFace = 104f, unitPanelFaceRise = 30f,
            artCard = new Vector2(300f, 500f), artCardTag = 30f, artCardName = 96f,
            endTurn = new Vector2(300f, 56f),
            topbarHeight = 46f, logoWidth = 280f, phaseWidth = 290f, roundWidth = 160f, objective = new Vector2(480f, 58f),
            hint = new Vector2(400f, 48f), logLineHeight = 24f, logWidth = 400f, bannerHeight = 140f, dialogWidth = 420f,
        };

        // ---- title / side-select sizes (menu canvas units) ------------------------------------------------------------------
        [Serializable]
        public struct TitleSizes
        {
            public Vector2 sideCard;        // .sd
            public Vector2 mapCard;         // .mp
            public Vector2 editorButton;    // .ed
            public Vector2 winCondition;    // .wb
            public Vector2 startButton;     // .start
            public Vector2 squadRow;        // .sq
            public Vector2 squadFace;       // .sq .por
            public float squadStats;        // .sq .s
            public Vector2 spotlight;       // #spot
        }

        [Header("Title sizes")]
        public TitleSizes title = new TitleSizes
        {
            sideCard = new Vector2(343f, 176f), mapCard = new Vector2(520f, 74f), editorButton = new Vector2(166f, 74f),
            winCondition = new Vector2(343f, 106f), startButton = new Vector2(343f, 106f),
            squadRow = new Vector2(696f, 60f), squadFace = new Vector2(72f, 60f), squadStats = 196f,
            spotlight = new Vector2(474f, 330f),
        };

        // ---- motion (seconds); easing is UIEase.Tactical (cubic-bezier(.2,.9,.2,1)) ---------------------------------------
        [Serializable]
        public struct Motion
        {
            public float cardGlitch;        // .glc.play (ocShake / jit .8s)
            public float cardReveal;        // wipe .26s
            public float cardIn;            // rcIn .5s
            public float cardInStagger;     // 60ms per card
            public float commandIn;         // cmdIn .32s
            public float commandStagger;    // 40ms per button
            public float banner;            // bnIn / bnTx 1.9s
            public float dialog;            // pop .32s
            public float screenWipe;        // wipeX .95s
            public float hitShake;          // hitShake .42s
            public float hpGhostDelay;      // .seg b transition delay .45s
            public float hpGhost;           // .seg b transition .8s
        }

        [Header("Motion (s)")]
        public Motion motion = new Motion
        {
            cardGlitch = .8f, cardReveal = .26f, cardIn = .5f, cardInStagger = .06f,
            commandIn = .32f, commandStagger = .04f, banner = 1.9f, dialog = .32f, screenWipe = .95f,
            hitShake = .42f, hpGhostDelay = .45f, hpGhost = .8f,
        };

        // ---- full-screen effects (UIScreenFX; ui_spec.md "화면 전체 효과") ------------------------------------------------------
        [Serializable]
        public struct ScreenFx
        {
            [Header("Scanlines (#stage::after)")]
            public bool scanlines; public float scanlineAlpha; [Tooltip("Canvas units, rounded to whole screen pixels.")] public float scanlineSpacing;
            [Header("Light band (#title .scanband), colour = blue")]
            public bool band; public float bandAlpha; public float bandHeight; public float bandPeriod;
            [Header("Noise flicker")]
            public bool noise; public float noiseAlpha; public float noiseFps;
            [Header("Glitch burst (white / blue strips)")]
            public bool burst; public float burstAlpha; public float burstMinInterval; public float burstMaxInterval; public float burstTime;
        }

        [Header("Screen FX")]
        public ScreenFx screenFx = new ScreenFx
        {
            scanlines = true, scanlineAlpha = .03f, scanlineSpacing = 3f,
            band = true, bandAlpha = .07f, bandHeight = 140f, bandPeriod = 8f,
            noise = true, noiseAlpha = .02f, noiseFps = 12f,
            burst = true, burstAlpha = .1f, burstMinInterval = 8f, burstMaxInterval = 14f, burstTime = .12f,
        };

        public static float Ease(float t) { return UIEase.Tactical(t); }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    }
}
