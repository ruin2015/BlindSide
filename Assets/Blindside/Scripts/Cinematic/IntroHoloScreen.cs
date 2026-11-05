// The screen side of the intro's switch to the hologram (IntroHologram renders it into a RenderTexture): first thin lines are drawn
// across the picture — three across, two down — then the hologram view is wiped in from the left behind a bright scan bar, and the
// lines fade. One run of the clock (ITimedEffect.Evaluate(t), Duration = Wipe end + LinesOut), started by Play; unscaled time,
// no allocation per frame. After the run the view stays up until Hide.
//   lines   LineDraw s each, staggered LineGap, eased (UIEase.Tactical)
//   wipe    WipeStart … WipeStart + WipeTime (ui_spec: 화면 전환 와이프 0.95 in all)
using UnityEngine;
using UnityEngine.UI;
using Blindside.UI;

namespace Blindside.Cinematic
{
    public class IntroHoloScreen : MonoBehaviour, ITimedEffect
    {
        public const float LineDraw = .32f, LineGap = .06f, WipeStart = .3f, WipeTime = .65f, LinesOut = .35f;
        static readonly Color LineColor = new Color(.75f, .93f, 1f, .9f);
        static readonly Color BarColor = new Color(.55f, .9f, 1f, 1f);

        public RectTransform root;
        public RawImage view;            // the hologram camera's picture
        public RectTransform viewRect;
        public RectTransform[] across, down;
        public Image[] lineImages;
        public RectTransform bar;        // the scan bar at the wipe's edge
        public CanvasGroup barGroup;

        float clock; bool running;

        public float Duration => WipeStart + WipeTime + LinesOut;

        public void Play(Texture holo)
        {
            view.texture = holo;
            root.gameObject.SetActive(true);
            clock = 0f; running = true;
            Evaluate(0f);
        }

        public void Hide()
        {
            running = false;
            view.texture = null;
            root.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }

        public void Evaluate(float t)
        {
            float W = root.rect.width, H = root.rect.height;
            // the lines, drawn one after another (across: left → right, down: top → bottom), then faded out together
            float fade = 1f - Mathf.Clamp01((t - WipeStart - WipeTime) / LinesOut);
            for (int i = 0; i < across.Length; i++)
            {
                float p = UIEase.Tactical((t - i * LineGap) / LineDraw);
                across[i].sizeDelta = new Vector2(W * p, across[i].sizeDelta.y);
            }
            for (int i = 0; i < down.Length; i++)
            {
                float p = UIEase.Tactical((t - (across.Length + i) * LineGap) / LineDraw);
                down[i].sizeDelta = new Vector2(down[i].sizeDelta.x, H * p);
            }
            for (int i = 0; i < lineImages.Length; i++)
            {
                var c = LineColor; c.a *= fade;
                if (lineImages[i].color != c) lineImages[i].color = c;
            }

            // the wipe: the view's width and its uv rect grow together (the picture doesn't stretch)
            float w = UIEase.Tactical((t - WipeStart) / WipeTime);
            viewRect.sizeDelta = new Vector2(W * w, 0f);
            view.uvRect = new Rect(0f, 0f, w, 1f);
            view.enabled = w > 0f;
            bar.anchoredPosition = new Vector2(W * w, 0f);
            barGroup.alpha = w > 0f && w < 1f ? 1f : 0f;
        }

        // ================================================================
        //  Construction (runtime, behind the letterbox and the subtitles)
        // ================================================================
        public static IntroHoloScreen Build(Transform canvas, int siblingIndex)
        {
            var root = Rect("IntroHoloScreen", canvas);
            root.SetSiblingIndex(siblingIndex);
            Stretch(root);
            var s = root.gameObject.AddComponent<IntroHoloScreen>();
            s.root = root;

            s.viewRect = Rect("HoloView", root);
            s.viewRect.anchorMin = new Vector2(0f, 0f); s.viewRect.anchorMax = new Vector2(0f, 1f); s.viewRect.pivot = new Vector2(0f, .5f);
            s.viewRect.sizeDelta = Vector2.zero;
            s.view = s.viewRect.gameObject.AddComponent<RawImage>(); s.view.raycastTarget = false;

            float[] ys = { .26f, .5f, .74f }, xs = { .3f, .7f };
            s.across = new RectTransform[ys.Length]; s.down = new RectTransform[xs.Length];
            s.lineImages = new Image[ys.Length + xs.Length];
            for (int i = 0; i < ys.Length; i++)
            {
                var l = Rect("DecoHoloLineH" + i, root);
                l.anchorMin = l.anchorMax = new Vector2(0f, ys[i]); l.pivot = new Vector2(0f, .5f); l.sizeDelta = new Vector2(0f, 2f);
                s.across[i] = l; s.lineImages[i] = Img(l, LineColor);
            }
            for (int i = 0; i < xs.Length; i++)
            {
                var l = Rect("DecoHoloLineV" + i, root);
                l.anchorMin = l.anchorMax = new Vector2(xs[i], 1f); l.pivot = new Vector2(.5f, 1f); l.sizeDelta = new Vector2(2f, 0f);
                s.down[i] = l; s.lineImages[ys.Length + i] = Img(l, LineColor);
            }

            s.bar = Rect("DecoHoloScanBar", root);
            s.bar.anchorMin = new Vector2(0f, 0f); s.bar.anchorMax = new Vector2(0f, 1f); s.bar.pivot = new Vector2(.5f, .5f);
            s.bar.sizeDelta = new Vector2(3f, 0f);
            Img(s.bar, BarColor);
            var glow = Rect("DecoHoloScanGlow", s.bar);
            Stretch(glow); glow.sizeDelta = new Vector2(46f, 0f);
            Img(glow, new Color(BarColor.r, BarColor.g, BarColor.b, .18f));
            s.barGroup = s.bar.gameObject.AddComponent<CanvasGroup>();

            root.gameObject.SetActive(false);
            return s;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        static Image Img(RectTransform rt, Color c)
        {
            var i = rt.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
            return i;
        }
    }
}
