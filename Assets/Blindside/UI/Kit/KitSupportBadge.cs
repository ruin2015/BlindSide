// The 支援射撃 badge of the battle stage (BattleStage.PlaySupport): while the ally who joins in has its face in close-up, a band across the
// lower middle of the screen — its small face, "SUPPORT FIRE" over 「支援射撃」 with a glitch burst, its codename and kana. On its own overlay
// canvas, over the letterbox. One unscaled clock (ITimedEffect.Evaluate(t)): slides in, holds, fades; nothing allocated per frame.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    public class KitSupportBadge : MonoBehaviour, ITimedEffect
    {
        public const float W = 640f, H = 112f, Rise = 190f, Slant = 22f, FaceSize = 96f, BarW = 8f;
        public const float InTime = .18f, Hold = .85f, OutTime = .2f, SlideFrom = 90f;
        public const int SortingOrder = 62;

        Canvas canvas;
        RectTransform band;
        CanvasGroup group;
        Image bar;
        UIGlitchImage face;
        TMP_Text en, kr;
        UIGlitchText glitch;
        float clock;
        bool running;

        public float Duration => InTime + Hold + OutTime;

        /// <summary>Shows the badge for this operator.</summary>
        public void Play(string key, string name, string kana, Color color)
        {
            Fill(key, name, kana, color);
            gameObject.SetActive(true);
            if (canvas != null) canvas.enabled = true;
            clock = 0f; running = true;
            if (glitch != null) glitch.Burst(.7f);
            Evaluate(0f);
        }

        /// <summary>Previews: `t` seconds in.</summary>
        public void PoseAt(string key, string name, string kana, Color color, float t)
        {
            Fill(key, name, kana, color);
            gameObject.SetActive(true);
            running = false;
            Evaluate(t);
        }

        void Fill(string key, string name, string kana, Color color)
        {
            EngagePreview.SetFace(face, key);
            bar.color = color;
            en.text = name; en.color = color;
            kr.text = kana;
        }

        public void Evaluate(float t)
        {
            bool reduce = UIMotionSettings.reduceMotion;
            float i = reduce ? 1f : UIEase.Tactical(Mathf.Clamp01(t / InTime));
            float o = Mathf.Clamp01((t - InTime - Hold) / OutTime);
            float a = i * (1f - o);
            if (group.alpha != a) group.alpha = a;
            var pos = new Vector2(-SlideFrom * (1f - i), Rise * -1f);
            if (band.anchoredPosition != pos) band.anchoredPosition = pos;
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock < Duration) return;
            running = false;
            gameObject.SetActive(false);
        }

        /// <param name="onCanvas">build onto this canvas instead of a new overlay one (Render Previews' stage)</param>
        public static KitSupportBadge Build(Transform parent, Canvas onCanvas = null)
        {
            var theme = UITheme.Current;
            var c = onCanvas;
            if (c == null)
            {
                var cgo = new GameObject("SupportBadge_Canvas", typeof(RectTransform));
                cgo.transform.SetParent(parent, false);
                c = cgo.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = SortingOrder;
                theme.ApplyTo(cgo.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Hud);
            }
            var root = HudKit.NewRect("SupportBadge", c.transform);
            HudKit.Stretch(root);
            var b = root.gameObject.AddComponent<KitSupportBadge>();
            b.canvas = c;
            b.group = root.gameObject.AddComponent<CanvasGroup>();
            b.group.blocksRaycasts = false; b.group.interactable = false;

            b.band = HudKit.NewRect("Band", root);
            b.band.anchorMin = b.band.anchorMax = b.band.pivot = new Vector2(.5f, .5f);
            b.band.sizeDelta = new Vector2(W, H);
            var bg = HudKit.NewRect("DecoBand", b.band);
            HudKit.Stretch(bg);
            HudKit.AddImage(bg, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .94f));
            bg.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, Slant);

            var barRt = HudKit.NewRect("DecoBar", b.band);
            UILayout.Place(barRt, Slant + 4f, 0f, BarW, H);
            b.bar = HudKit.AddImage(barRt, theme.acc);

            var frame = HudKit.NewRect("Frame", b.band);
            UILayout.Place(frame, Slant + 18f, (H - FaceSize) * .5f, FaceSize, FaceSize);
            HudKit.AddImage(frame, Color.white).sprite = OperatorArt.PaperGradient;
            var f = HudKit.NewRect("Face", frame);
            HudKit.Stretch(f);
            b.face = f.gameObject.AddComponent<UIGlitchImage>();
            b.face.raycastTarget = false;

            float x = Slant + 18f + FaceSize + 18f;
            var tag = HudKit.AddText(b.band, "SUPPORT FIRE", 14f, theme.acc, true, TextAlignmentOptions.TopLeft, "Tag", UITheme.TextRole.Display);
            tag.characterSpacing = 14f;
            UILayout.Place(tag.rectTransform, x, 14f, 260f, 18f);
            var title = HudKit.AddText(b.band, "支援射撃", 44f, Color.white, true, TextAlignmentOptions.TopLeft, "Title", UITheme.TextRole.Title);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(title.rectTransform, x, 34f, 220f, 62f);
            b.glitch = title.gameObject.AddComponent<UIGlitchText>();
            b.glitch.source = title; b.glitch.loop = false; b.glitch.maxOffset = 8f;

            float rx = W - Slant - 20f - 170f;
            b.en = HudKit.AddText(b.band, "", 24f, Color.white, true, TextAlignmentOptions.BottomRight, "Name", UITheme.TextRole.Display);
            b.en.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(b.en.rectTransform, rx, 26f, 170f, 32f);
            b.kr = HudKit.AddText(b.band, "", 14f, Color.Lerp(theme.ink, Color.white, .75f), false, TextAlignmentOptions.TopRight, "Kana", UITheme.TextRole.Body);
            b.kr.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(b.kr.rectTransform, rx, 60f, 170f, 22f);

            root.gameObject.SetActive(false);
            return b;
        }
    }
}
