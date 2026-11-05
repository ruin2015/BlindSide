// The tutorial's pointers on screen, on the radio's canvas:
//   frame   a pulsing yellow frame round what the player should click — a button of another overlay canvas (FrameRect) or a point of
//           the battlefield (FrameWorld, e.g. the unit) — with a short chip above it ("ここをクリック")
//   target  a chip that floats over a point of the battlefield (TargetLabel, the destination marker's "目標地点")
// They follow their target every frame (LateUpdate, unscaled clock; corners into a cached array — no allocation).
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.UI;

namespace Blindside.Tutorial
{
    public class TutorialGuide : MonoBehaviour, ITimedEffect
    {
        public const float Pad = 6f, PadPulse = 6f, Edge = 3f, Period = .9f, WorldBox = 74f;

        public RectTransform root, frame, frameChip, targetChip;
        public RectTransform[] edges;
        public TMP_Text frameLabel, targetLabel;
        public CanvasGroup frameGroup;

        RectTransform frameTarget;
        Vector3 frameWorld, targetWorld;
        bool frameOn, frameIsWorld, targetOn;
        float clock;
        readonly Vector3[] corners = new Vector3[4];
        Canvas canvas;

        public float Duration => Period;

        public void FrameRect(RectTransform target, string label)
        {
            frameTarget = target; frameIsWorld = false; frameIsLocal = false; frameOn = target != null;
            SetLabel(frameLabel, frameChip, label);
            frame.gameObject.SetActive(frameOn);
        }

        public void FrameWorld(Vector3 world, string label)
        {
            frameWorld = world; frameIsWorld = true; frameIsLocal = false; frameOn = true; frameTarget = null;
            SetLabel(frameLabel, frameChip, label);
            frame.gameObject.SetActive(true);
        }

        public void HideFrame() { frameOn = false; frameTarget = null; frame.gameObject.SetActive(false); }

        // fixed places in this canvas's units (bottom-left origin) — for Render Previews, where there is no game camera
        bool frameIsLocal, targetIsLocal;
        Vector2 localMin, localMax, targetLocal;
        public void FrameLocal(Vector2 min, Vector2 max, string label)
        {
            FrameWorld(Vector3.zero, label);
            frameIsWorld = false; frameIsLocal = true; localMin = min; localMax = max;
        }
        public void TargetLocal(Vector2 at, string label) { TargetLabel(Vector3.zero, label); targetIsLocal = true; targetLocal = at; }

        public void TargetLabel(Vector3 world, string label)
        {
            targetWorld = world; targetOn = true; targetIsLocal = false;
            SetLabel(targetLabel, targetChip, label);
            targetChip.gameObject.SetActive(true);
        }

        public void HideTarget() { targetOn = false; targetChip.gameObject.SetActive(false); }

        // ---- a chip riding next to the player's own mouse pointer (「左クリックで射撃」 while aiming) ----
        public RectTransform mouseChip;
        public TMP_Text mouseLabel;
        bool mouseOn;
        public void MouseTip(string label)
        {
            mouseOn = true;
            SetLabel(mouseLabel, mouseChip, label);
            mouseChip.gameObject.SetActive(true);
        }
        public void HideMouseTip() { if (!mouseOn) return; mouseOn = false; mouseChip.gameObject.SetActive(false); }

        // ---- the demonstration's pointer ----
        public const float CursorFollow = 9f, ClickTime = .22f, RippleTime = .45f;
        public RectTransform cursor, ripple;
        Image rippleImg;
        Vector2 cursorPos, cursorGoal, clickAt;
        float clickT = -1f, lastT;

        /// <summary>The pointer goes to `screen` (pixels); `snap` puts it there at once.</summary>
        public void CursorTo(Vector2 screen, bool snap = false)
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            float sf = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            cursorGoal = screen / sf;
            if (snap || !cursor.gameObject.activeSelf) cursorPos = cursorGoal;
            cursor.gameObject.SetActive(true);
        }

        /// <summary>The pointer to a UI element's centre (another overlay canvas).</summary>
        public void CursorToRect(RectTransform target)
        {
            if (target == null) return;
            target.GetWorldCorners(corners);
            CursorTo((corners[0] + corners[2]) * .5f);
        }

        /// <summary>The pointer at once at a point in this canvas's units (Render Previews).</summary>
        public void CursorLocal(Vector2 local) { cursorGoal = cursorPos = local; cursor.gameObject.SetActive(true); }

        /// <summary>A click where the pointer is now (it dips; a ring spreads).</summary>
        public void Click() { clickT = clock; clickAt = cursorPos; }

        public void HideCursor() { cursor.gameObject.SetActive(false); ripple.gameObject.SetActive(false); clickT = -1f; }

        static Sprite cursorSprite, ringSprite;

        static Sprite CursorSprite()
        {
            if (cursorSprite != null) return cursorSprite;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            // a classic arrow pointer, tip at the top left: white with a dark rim (inside test on the outline polygon)
            var poly = new[] { new Vector2(4, 60), new Vector2(4, 12), new Vector2(16, 24), new Vector2(26, 2), new Vector2(34, 6), new Vector2(24, 27), new Vector2(42, 27) };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var q = new Vector2(x + .5f, y + .5f);
                    float d = EdgeDistance(q, poly);
                    bool inside = Inside(q, poly);
                    float a = inside ? 1f : Mathf.Clamp01(1f - (d - 2.2f));
                    Color c = inside && d > 2.6f ? Color.white : new Color(.08f, .09f, .11f);
                    c.a = a;
                    px[y * N + x] = c;
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            cursorSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(4f / N, 60f / N), 100f);
            cursorSprite.hideFlags = HideFlags.HideAndDontSave;
            return cursorSprite;
        }

        static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(N * .5f, N * .5f));
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 27f) / 3f);
                    px[y * N + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            ringSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f), 100f);
            ringSprite.hideFlags = HideFlags.HideAndDontSave;
            return ringSprite;
        }

        static bool Inside(Vector2 p, Vector2[] poly)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }

        static float EdgeDistance(Vector2 p, Vector2[] poly)
        {
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = poly[j]; var b = poly[i]; var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        static void SetLabel(TMP_Text t, RectTransform chip, string s)
        {
            if (t.text == s) return;
            t.text = s;
            var size = t.GetPreferredValues(s);
            chip.sizeDelta = new Vector2(Mathf.Ceil(size.x) + 24f, 30f);
        }

        void LateUpdate()
        {
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
        }

        public void Evaluate(float t)
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            float sf = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            float p = .5f - .5f * Mathf.Cos(Mathf.Repeat(t, Period) / Period * Mathf.PI * 2f);   // 0 → 1 → 0

            if (frameOn)
            {
                Vector2 min, max;
                bool ok = true;
                if (frameIsLocal) { min = localMin; max = localMax; }
                else if (frameIsWorld)
                {
                    var cam = Camera.main;
                    var s = cam != null ? cam.WorldToScreenPoint(frameWorld) : Vector3.back;
                    ok = s.z > 0f;
                    min = new Vector2(s.x / sf - WorldBox * .5f, s.y / sf - WorldBox * .5f);
                    max = min + new Vector2(WorldBox, WorldBox);
                }
                else if (frameTarget != null && frameTarget.gameObject.activeInHierarchy)
                {
                    frameTarget.GetWorldCorners(corners);   // overlay canvas: screen pixels
                    min = new Vector2(corners[0].x / sf, corners[0].y / sf);
                    max = new Vector2(corners[2].x / sf, corners[2].y / sf);
                }
                else { ok = false; min = max = Vector2.zero; }
                if (frameGroup.alpha != (ok ? 1f : 0f)) frameGroup.alpha = ok ? 1f : 0f;
                float pad = Pad + PadPulse * p;
                min -= new Vector2(pad, pad); max += new Vector2(pad, pad);
                frame.anchoredPosition = min;
                frame.sizeDelta = max - min;
                frameChip.anchoredPosition = new Vector2((max.x - min.x) * .5f, max.y - min.y + 8f + 4f * p);
                var col = new Color(UIPalette.Accent.r, UIPalette.Accent.g, UIPalette.Accent.b, .55f + .45f * (1f - p));
                for (int i = 0; i < edges.Length; i++) edges[i].GetComponent<Image>().color = col;
            }

            // the demonstration's hand: a pointer that glides to its spot, dips on a click, and a ring that spreads from the click
            if (cursor != null && cursor.gameObject.activeSelf)
            {
                float dt = Mathf.Max(0f, t - lastT);
                cursorPos = Vector2.Lerp(cursorPos, cursorGoal, 1f - Mathf.Exp(-dt * CursorFollow));
                cursor.anchoredPosition = cursorPos;
                float press = clickT >= 0f ? Mathf.Clamp01(1f - (t - clickT) / ClickTime) : 0f;
                float sc = 1f - .18f * Mathf.Sin(press * Mathf.PI);
                cursor.localScale = new Vector3(sc, sc, 1f);
                float r = clickT >= 0f ? Mathf.Clamp01((t - clickT) / RippleTime) : 1f;
                ripple.gameObject.SetActive(r < 1f);
                if (r < 1f)
                {
                    ripple.anchoredPosition = clickAt;
                    float size = Mathf.Lerp(12f, 70f, 1f - (1f - r) * (1f - r));
                    ripple.sizeDelta = new Vector2(size, size);
                    rippleImg.color = new Color(UIPalette.Accent.r, UIPalette.Accent.g, UIPalette.Accent.b, 1f - r);
                }
            }
            lastT = t;

            if (mouseOn)
            {
                var m = UnityEngine.InputSystem.Mouse.current;
                if (m != null)
                {
                    var mp = m.position.ReadValue();
                    mouseChip.anchoredPosition = new Vector2(mp.x / sf + 26f, mp.y / sf - 40f - 3f * p);
                }
            }

            if (targetOn && targetIsLocal) targetChip.anchoredPosition = targetLocal + new Vector2(0f, 6f * p);
            else if (targetOn)
            {
                var cam = Camera.main;
                var s = cam != null ? cam.WorldToScreenPoint(targetWorld) : Vector3.back;
                targetChip.gameObject.SetActive(s.z > 0f);
                targetChip.anchoredPosition = new Vector2(s.x / sf, s.y / sf + 6f * p);
            }
        }

        // ================================================================
        //  Construction (on the radio's canvas)
        // ================================================================
        /// <summary>Builds the guide on its own overlay canvas under `parent` (above the selection UI, so the frame shows over the menu).</summary>
        public static TutorialGuide Build(Transform parent, Canvas onCanvas = null)
        {
            var c = onCanvas;
            if (c == null)
            {
                var cgo = new GameObject("TutorialGuide_Canvas", typeof(RectTransform));
                cgo.transform.SetParent(parent, false);
                c = cgo.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 7;
                UITheme.Current.ApplyTo(cgo.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Hud);
                cgo.AddComponent<GraphicRaycaster>();   // (only the skip button takes pointer input; the frames and chips don't)
            }
            var root = NewRect("TutorialGuide", c.transform);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            var g = root.gameObject.AddComponent<TutorialGuide>();
            g.root = root;

            // frame: four edges round a bottom-left anchored box
            var frame = NewRect("GuideFrame", root);
            frame.anchorMin = frame.anchorMax = Vector2.zero; frame.pivot = Vector2.zero;
            g.frame = frame;
            g.frameGroup = frame.gameObject.AddComponent<CanvasGroup>();
            g.frameGroup.blocksRaycasts = false;
            g.edges = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                var e = NewRect("GuideEdge" + i, frame);
                bool horiz = i < 2;
                e.anchorMin = horiz ? new Vector2(0f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 0f);
                e.anchorMax = horiz ? new Vector2(1f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 1f);
                e.pivot = new Vector2(i == 3 ? 1f : 0f, i == 1 ? 1f : 0f);
                e.sizeDelta = horiz ? new Vector2(0f, Edge) : new Vector2(Edge, 0f);
                e.anchoredPosition = Vector2.zero;
                Img(e, UIPalette.Accent);
                g.edges[i] = e;
            }
            g.frameChip = Chip(frame, "GuideChip", out g.frameLabel, UIPalette.Accent, UIPalette.Ink);
            g.frameChip.anchorMin = g.frameChip.anchorMax = Vector2.zero; g.frameChip.pivot = new Vector2(.5f, 0f);
            g.frameChip.gameObject.AddComponent<LintIgnoreClip>().reason = "the chip sits above the frame on purpose";
            frame.gameObject.SetActive(false);

            g.targetChip = Chip(root, "TargetChip", out g.targetLabel, UIPalette.Ink, UIPalette.Blue);
            g.targetChip.anchorMin = g.targetChip.anchorMax = Vector2.zero; g.targetChip.pivot = new Vector2(.5f, 0f);
            g.targetChip.gameObject.SetActive(false);

            g.mouseChip = Chip(root, "MouseTipChip", out g.mouseLabel, UIPalette.Accent, UIPalette.Ink);
            g.mouseChip.anchorMin = g.mouseChip.anchorMax = Vector2.zero; g.mouseChip.pivot = new Vector2(0f, 1f);
            g.mouseChip.gameObject.SetActive(false);

            // the demonstration's pointer (last: over everything else of the guide)
            g.ripple = NewRect("ClickRipple", root);
            g.ripple.anchorMin = g.ripple.anchorMax = Vector2.zero; g.ripple.pivot = new Vector2(.5f, .5f);
            g.rippleImg = Img(g.ripple, UIPalette.Accent);
            g.rippleImg.sprite = RingSprite();
            g.ripple.gameObject.SetActive(false);
            g.cursor = NewRect("DemoCursorIcon", root);
            g.cursor.anchorMin = g.cursor.anchorMax = Vector2.zero; g.cursor.pivot = new Vector2(4f / 64f, 60f / 64f);
            g.cursor.sizeDelta = new Vector2(40f, 40f);
            Img(g.cursor, Color.white).sprite = CursorSprite();
            g.cursor.gameObject.SetActive(false);
            return g;
        }

        static RectTransform Chip(RectTransform parent, string name, out TMP_Text label, Color bg, Color fg)
        {
            var chip = NewRect(name, parent);
            chip.sizeDelta = new Vector2(120f, 30f);
            Img(chip, bg);
            var line = NewRect("DecoChipLine", chip);
            line.anchorMin = new Vector2(0f, 0f); line.anchorMax = new Vector2(0f, 1f); line.pivot = new Vector2(0f, .5f);
            line.sizeDelta = new Vector2(3f, 0f); line.anchoredPosition = Vector2.zero;
            Img(line, fg);
            var trt = NewRect("Label", chip);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(12f, 0f); trt.offsetMax = new Vector2(-10f, 0f);
            label = trt.gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 15f; label.color = bg == UIPalette.Ink ? Color.white : UIPalette.Ink;
            label.alignment = TextAlignmentOptions.Midline; label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false;
            UIFonts.Apply(label, UITheme.TextRole.Body);
            return chip;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static Image Img(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return img;
        }
    }
}
