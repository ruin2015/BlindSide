// The screen-change wipe (#wipe i, ui_spec "화면 전환 와이프 0.95"): a band 130% of the screen wide and 120% high (−10% top and bottom),
// blue to 78%, a white line to 82%, ink to the end, skewX(−18°) about its centre, moved translateX −130% → −15% (50%) → +130% of its own
// width over .95s with cubic-bezier(.7, 0, .3, 1) on each half. At .475s (the middle, the screen covered) the caller's action runs —
// the mockup switches screens there. Drawn on the top canvas: as the last child of UIScreenFX's canvas (so over the screen effect),
// or on its own Screen Space Overlay canvas (order 32767) when there is none; no raycasts. Evaluate(t) poses it; Update advances an
// unscaled clock while it plays. One mesh of three parallelograms, re-built only while it moves; nothing allocated per frame.
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Wipe")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class KitWipe : MaskableGraphic, ITimedEffect
    {
        public const float WipeTime = .95f, Middle = .475f, Width = 1.3f, Over = .1f, Skew = 18f;
        const string CanvasName = "WipeCanvas", LayerName = "Wipe";

        static KitWipe instance;
        float clock, pos = float.NaN;
        bool running, fired;
        System.Action atMiddle;

        public float Duration => WipeTime;

        /// <summary>Plays the wipe; `onMiddle` runs once when the screen is covered (or at once if the wipe can't be shown).</summary>
        public static void Play(System.Action onMiddle)
        {
            var w = Instance;
            if (w == null) { onMiddle?.Invoke(); return; }
            w.transform.SetAsLastSibling();
            w.atMiddle = onMiddle; w.fired = false;
            w.clock = 0f; w.running = true;
            w.Evaluate(0f);
        }

        static KitWipe Instance
        {
            get
            {
                if (instance != null) return instance;
                Transform parent;
                var fx = UIScreenFX.Instance;
                if (fx != null && fx.canvas != null) parent = fx.canvas.transform;
                else
                {
                    var go = new GameObject(CanvasName, typeof(RectTransform));
                    DontDestroyOnLoad(go);
                    var c = go.AddComponent<Canvas>();
                    c.renderMode = RenderMode.ScreenSpaceOverlay;
                    c.sortingOrder = UIScreenFX.SortingOrder;
                    UITheme.Current.ApplyTo(go.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Menu);
                    parent = go.transform;
                }
                instance = Create(parent);
                return instance;
            }
        }

        /// <summary>A wipe layer stretched over `parent`, at rest (nothing drawn).</summary>
        public static KitWipe Create(Transform parent)
        {
            var go = new GameObject(LayerName, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            HudKit.Stretch(rt);
            var w = go.AddComponent<KitWipe>();
            w.raycastTarget = false;
            return w;
        }

        // CSS cubic-bezier(x1, y1, x2, y2) at x
        static float Bezier(float x, float x1, float y1, float x2, float y2)
        {
            x = Mathf.Clamp01(x);
            float u = x;
            for (int i = 0; i < 8; i++)
            {
                float iu = 1f - u;
                float bx = 3f * iu * iu * u * x1 + 3f * iu * u * u * x2 + u * u * u - x;
                float dx = 3f * iu * iu * x1 + 6f * iu * u * (x2 - x1) + 3f * u * u * (1f - x2);
                if (Mathf.Abs(bx) < 1e-5f || Mathf.Abs(dx) < 1e-6f) break;
                u = Mathf.Clamp01(u - bx / dx);
            }
            float w = 1f - u;
            return 3f * w * w * u * y1 + 3f * w * u * u * y2 + u * u * u;
        }

        /// <summary>Poses the wipe `t` seconds in (t ≥ Duration or t &lt; 0: nothing drawn).</summary>
        public void Evaluate(float t)
        {
            float p;
            if (t < 0f || t >= WipeTime) p = float.NaN;
            else
            {
                float h = WipeTime * .5f;
                p = t < h ? Mathf.LerpUnclamped(-1.3f, -.15f, Bezier(t / h, .7f, 0f, .3f, 1f))
                          : Mathf.LerpUnclamped(-.15f, 1.3f, Bezier((t - h) / h, .7f, 0f, .3f, 1f));
            }
            if (p == pos || (float.IsNaN(p) && float.IsNaN(pos))) return;
            pos = p;
            SetVerticesDirty();
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (!fired && clock >= Middle)
            {
                fired = true;
                var a = atMiddle; atMiddle = null;
                a?.Invoke();
            }
            if (clock >= WipeTime) running = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (float.IsNaN(pos)) return;
            Rect r = GetPixelAdjustedRect();
            float ew = r.width * Width, y0 = r.yMin - r.height * Over, y1 = r.yMax + r.height * Over, cy = r.center.y;
            float k = Mathf.Tan(Skew * Mathf.Deg2Rad);       // skewX(−18°): above the centre leans right (y up here)
            float left = r.xMin + pos * ew;
            var theme = UITheme.Current;
            Quad(vh, left, left + ew * .78f, y0, y1, cy, k, theme.blue);
            Quad(vh, left + ew * .78f, left + ew * .82f, y0, y1, cy, k, Color.white);
            Quad(vh, left + ew * .82f, left + ew, y0, y1, cy, k, theme.ink);
        }

        static void Quad(VertexHelper vh, float x0, float x1, float y0, float y1, float cy, float k, Color c)
        {
            int s = vh.currentVertCount;
            float b = k * (y0 - cy), t = k * (y1 - cy);
            var v = UIVertex.simpleVert;
            v.color = c;
            v.position = new Vector3(x0 + b, y0); vh.AddVert(v);
            v.position = new Vector3(x0 + t, y1); vh.AddVert(v);
            v.position = new Vector3(x1 + t, y1); vh.AddVert(v);
            v.position = new Vector3(x1 + b, y0); vh.AddVert(v);
            vh.AddTriangle(s, s + 1, s + 2);
            vh.AddTriangle(s + 2, s + 3, s);
        }
    }
}
