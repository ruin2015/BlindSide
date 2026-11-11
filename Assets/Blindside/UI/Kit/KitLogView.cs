// The HUD's battle log (#log, ui_spec "안내 바 / 로그"): lines 24 high and 300 wide stacked upwards from 20 above the bottom, right of
// the hint bar (by request: lower and shorter than the source's 400 × 7 above the bar), gap 3, the newest 3 lines; Tab shows all
// of them (KitLogHistory). Each line is a parallelogram (slant 12, ink at .86)
// with a coloured slanted marker on the left (ally = blue, foe = foe red, warning = acc, plain = dim) and 12px body text, one line:
// a text that is too wide first gets letter spacing −0.02em, and if that is not enough it wraps to two lines and the line grows.
// Motion (all one clock, ITimedEffect.Evaluate(t); Update only advances it with unscaled time and stops when nothing moves):
// a new line slides in 30 from the right and fades in over .35s, older lines glide up to make room, and the line that falls out of
// the 7 is pushed up one slot while it fades out. Lines are pooled (7 + spare): nothing is created or destroyed after the first Add.
// HudLog stays the sink (Add / Remove / Clear events), exactly as HudLogView used it.
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Log View")]
    public class KitLogView : MonoBehaviour, ITimedEffect
    {
        // low on the screen: beside the hint bar (right of it, on the bottom line), only the newest VisibleLines; the whole match's log
        // is the Tab window (KitLogHistory)
        public const float LineH = 24f, Width = 280f, Gap = 3f, Slant = 12f, PadL = 30f, PadR = 20f, PadT = 4f, PadB = 5f;
        public const float TextSize = 12f, TextLine = 15f, Spacing = -2f;                       // −0.02em in TMP's 1/100 em
        public const float MarkX = 10f, MarkW = 4f, MarkSkew = 20f;                              // ::before skewX(−20deg)
        public const float EnterTime = .35f, EnterShift = 30f, BaseBottom = 20f, FromCentre = 135f, BesideHint = 8f;   // (280 + 8 leaves a gap before END TURN)
        public const int VisibleLines = 3;
        const int Spare = 3;

        static readonly Color BgColor = new Color32(14, 16, 20, 219);                            // rgba(14,16,20,.86)
        static readonly Color TextColor = new Color32(0xE2, 0xE7, 0xEC, 0xFF);

        class Entry
        {
            public RectTransform root, inner, mark;
            public CanvasGroup group;
            public Image bg, markImage;
            public SlantedGraphic markShape;
            public TMP_Text text;
            public string str;
            public float height, born, moveT0, fromY, toY, dieT0;
            public bool alive, dying;
        }

        Entry[] pool;
        readonly System.Collections.Generic.List<Entry> live = new System.Collections.Generic.List<Entry>(VisibleLines + 1);
        float clock;
        bool animating;
        float lineBase = -1f;

        public float Duration => EnterTime;
        public int LineCount => live.Count;

        // ---- layout -----------------------------------------------------------------------------------------------------------
        void ApplyLayout()
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(-FromCentre + KitHintBar.Width + BesideHint, BaseBottom);
            rt.sizeDelta = new Vector2(Width, rt.sizeDelta.y);
        }

        void OnHintHeight(float h) { ApplyLayout(); }

        void EnsurePool()
        {
            if (pool != null) return;
            pool = new Entry[VisibleLines + Spare];
            for (int i = 0; i < pool.Length; i++) pool[i] = CreateEntry();
        }

        Entry CreateEntry()
        {
            var e = new Entry();
            e.root = HudKit.NewRect("Entry", transform);
            e.root.anchorMin = e.root.anchorMax = Vector2.zero; e.root.pivot = Vector2.zero;
            e.inner = HudKit.NewRect("Inner", e.root);
            HudKit.Stretch(e.inner);
            e.group = e.inner.gameObject.AddComponent<CanvasGroup>();
            e.group.blocksRaycasts = false;

            var bg = HudKit.NewRect("DecoBg", e.inner);
            HudKit.Stretch(bg);
            e.bg = HudKit.AddImage(bg, BgColor);
            bg.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, Slant);

            e.mark = HudKit.NewRect("DecoMark", e.inner);
            e.mark.anchorMin = new Vector2(0f, 0f); e.mark.anchorMax = new Vector2(0f, 1f); e.mark.pivot = new Vector2(0f, .5f);
            e.markImage = HudKit.AddImage(e.mark, Color.white);
            e.markShape = e.mark.gameObject.AddComponent<SlantedGraphic>();

            e.text = HudKit.AddText(e.inner, "", TextSize, TextColor, false, TextAlignmentOptions.TopLeft, "Text", UITheme.TextRole.Body);
            UILayout.LineHeight(e.text, 1.25f);
            var trt = e.text.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(PadL, 0f); trt.offsetMax = new Vector2(-PadR, -PadT);

            e.root.gameObject.SetActive(false);
            return e;
        }

        // ---- HudLog -----------------------------------------------------------------------------------------------------------
        void OnEnable()
        {
            ApplyLayout();
            if (Application.isPlaying) KitLogHistory.Ensure(transform);       // the Tab window with the whole log
            KitHintBar.HeightChanged += OnHintHeight;
            HudLog.OnAdd += OnAdd;
            HudLog.OnCleared += ClearAll;
            HudLog.OnRemoved += RemoveLine;
            ClearAll();
            var recent = HudLog.Recent;
            for (int i = Mathf.Max(0, recent.Count - VisibleLines); i < recent.Count; i++) AddLine(recent[i].text, recent[i].type, true);
        }

        void OnDisable()
        {
            KitHintBar.HeightChanged -= OnHintHeight;
            HudLog.OnAdd -= OnAdd;
            HudLog.OnCleared -= ClearAll;
            HudLog.OnRemoved -= RemoveLine;
        }

        /// <summary>While on, new lines aren't drawn here (the Tab window still has them): the deployment outside the first main quest.</summary>
        public bool Muted { get; set; }

        void OnAdd(string text, HudLogType type) { if (!Muted) AddLine(text, type, false); }

        /// <summary>Editor previews: lays the log out and adds a line directly, settled (no HudLog subscription, no slide-in).</summary>
        public void PreviewLine(string text, HudLogType type)
        {
            ApplyLayout();
            AddLine(text, type, true);
        }

        void ClearAll()
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++) Release(pool[i]);
            live.Clear();
            SetStackHeight();
        }

        void RemoveLine(string text)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (live[i].str != text) continue;
                Release(live[i]);
                live.RemoveAt(i);
                Relayout(false, null);
                Evaluate(clock);
                return;
            }
        }

        static void Release(Entry e)
        {
            e.alive = e.dying = false;
            if (e.root.gameObject.activeSelf) e.root.gameObject.SetActive(false);
        }

        Entry Take()
        {
            Entry oldestDying = null;
            for (int i = 0; i < pool.Length; i++)
            {
                var e = pool[i];
                if (!e.alive && !e.dying) return e;
                if (e.dying && (oldestDying == null || e.dieT0 < oldestDying.dieT0)) oldestDying = e;
            }
            Release(oldestDying);       // a burst of lines: the line that was fading out goes at once
            return oldestDying;
        }

        void AddLine(string text, HudLogType type, bool instant)
        {
            EnsurePool();
            if (!Application.isPlaying) instant = true;

            var e = Take();
            Configure(e, text, type);
            e.alive = true; e.dying = false;
            e.born = instant ? -100f : clock;
            e.fromY = e.toY = 0f; e.moveT0 = -100f;
            e.root.gameObject.SetActive(true);
            live.Add(e);

            Entry leaving = null;
            if (live.Count > VisibleLines)
            {
                leaving = live[0];
                live.RemoveAt(0);
                if (instant) Release(leaving);
                else
                {
                    leaving.alive = false; leaving.dying = true; leaving.dieT0 = clock;
                    leaving.fromY = CurY(leaving, clock);
                    leaving.toY = leaving.fromY + e.height + Gap;       // pushed up one line
                    leaving.moveT0 = clock;
                }
            }
            Relayout(instant, e);
            if (!instant) animating = true;
            Evaluate(clock);
        }

        void Configure(Entry e, string s, HudLogType type)
        {
            e.str = s;
            var t = e.text;
            e.root.gameObject.SetActive(true);                 // TMP measures only on an active hierarchy
            if (lineBase < 0f) lineBase = t.GetPreferredValues("あ", 100000f, 100000f).y;
            int lines = KitTextFit.Fit(t, s, Width - PadL - PadR, TextLine, Spacing);
            if (lines > 1) t.characterSpacing = Spacing;     // wrapped: keep the narrowed spacing
            e.height = lines <= 1 ? LineH : Mathf.Max(LineH, PadT + lines * TextLine + PadB);
            e.root.sizeDelta = new Vector2(Width, e.height);

            var th = UITheme.Current;
            e.markImage.color = type == HudLogType.Ally ? th.blue : type == HudLogType.Foe ? th.foe : type == HudLogType.Warn ? th.acc : th.dim;
            float skew = e.height * Mathf.Tan(MarkSkew * Mathf.Deg2Rad);
            e.mark.anchoredPosition = new Vector2(MarkX - skew * .5f, 0f);
            e.mark.sizeDelta = new Vector2(MarkW + skew, 0f);
            e.markShape.Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, skew);
            e.inner.anchoredPosition = Vector2.zero;
        }

        // newest at the bottom; older lines above. Entries already in the log glide from where they are to their new place.
        void Relayout(bool instant, Entry fresh)
        {
            float y = 0f;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var e = live[i];
                if (e != fresh)
                {
                    if (instant) { e.fromY = e.toY = y; e.moveT0 = -100f; }
                    else if (!Mathf.Approximately(e.toY, y))
                    {
                        e.fromY = CurY(e, clock); e.toY = y; e.moveT0 = clock;
                    }
                }
                else e.fromY = e.toY = y;
                y += e.height + Gap;
            }
            SetStackHeight();
        }

        void SetStackHeight()
        {
            float h = 0f;
            for (int i = 0; i < live.Count; i++) h += live[i].height + (i > 0 ? Gap : 0f);
            var rt = (RectTransform)transform;
            if (!Mathf.Approximately(rt.sizeDelta.y, h)) rt.sizeDelta = new Vector2(Width, h);
        }

        static float CurY(Entry e, float t)
        {
            if (UIMotionSettings.reduceMotion) return e.toY;
            return Mathf.Lerp(e.fromY, e.toY, UIEase.Tactical((t - e.moveT0) / EnterTime));
        }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        /// <summary>Poses every line at clock time `t` (a line's own times are relative to the same clock).</summary>
        public void Evaluate(float t)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++)
            {
                var e = pool[i];
                if (e.alive)
                {
                    float p = UIMotionSettings.reduceMotion ? 1f : UIEase.Tactical((t - e.born) / EnterTime);
                    Pose(e, CurY(e, t), EnterShift * (1f - p), p);
                }
                else if (e.dying)
                {
                    float q = Mathf.Clamp01((t - e.dieT0) / EnterTime);
                    if (UIMotionSettings.reduceMotion || q >= 1f) { Release(e); continue; }
                    Pose(e, CurY(e, t), 0f, 1f - q);
                }
            }
        }

        static void Pose(Entry e, float y, float x, float alpha)
        {
            var pos = new Vector2(0f, y);
            if (e.root.anchoredPosition != pos) e.root.anchoredPosition = pos;
            var ip = new Vector2(x, 0f);
            if (e.inner.anchoredPosition != ip) e.inner.anchoredPosition = ip;
            if (e.group.alpha != alpha) e.group.alpha = alpha;
        }

        void Update()
        {
            if (!animating) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            bool busy = false;
            for (int i = 0; i < pool.Length && !busy; i++)
            {
                var e = pool[i];
                if (e.dying) busy = true;
                else if (e.alive && (clock - e.born < EnterTime || clock - e.moveT0 < EnterTime)) busy = true;
            }
            if (busy) return;
            // settled: rebase the clock so it never grows large
            animating = false;
            clock = 0f;
            for (int i = 0; i < pool.Length; i++) if (pool[i].alive) { pool[i].born = pool[i].moveT0 = -100f; pool[i].fromY = pool[i].toY; }
        }
    }
}
