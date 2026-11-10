// The battle's full log (Tab): the HUD log shows only its newest few lines (KitLogView), this window lists the whole match from
// HudLog.History, grouped by round (配置 before the first round), newest at the bottom, the same marker colours as the log lines
// (ally blue, foe red, warning acc, plain dim). It opens scrolled to the newest line; the wheel scrolls; Tab or ESC closes
// (ESC is used: EscapeGate, so the game menu does not open with it). A dim backdrop swallows clicks while it is open.
// The text is rebuilt only when it opens or a line arrives while it is open (never per frame). Motion: .2s fade + scale .96 → 1,
// ITimedEffect.Evaluate(t) on an unscaled clock that stops when settled.
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Log History")]
    public class KitLogHistory : MonoBehaviour, ITimedEffect
    {
        public const float W = 820f, H = 660f, HeaderH = 74f, Pad = 28f, PadBottom = 22f, StripeW = 8f;
        public const float TitleSize = 26f, SubSize = 15f, CloseSize = 14f, BodySize = 17f, RoundSize = 17f;
        public const float EnterTime = .2f, EnterScale = .96f, WheelStep = 72f, BackdropAlpha = .55f;
        public const int ShowMax = 200;                                     // newest lines drawn (keeps the text mesh small)

        public static KitLogHistory Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.open;

        RectTransform root, window, content;
        CanvasGroup group;
        TMP_Text body, empty;
        bool open, running, dirty;
        float clock, offset, maxOffset;
        readonly StringBuilder sb = new StringBuilder(8192);

        public float Duration => EnterTime;

        /// <summary>Makes the window once on the HUD's root canvas (KitLogView calls this at runtime).</summary>
        public static KitLogHistory Ensure(Transform near)
        {
            if (Instance != null) return Instance;
            var canvas = near.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            var go = new GameObject("LogHistory", typeof(RectTransform));
            go.transform.SetParent(canvas.rootCanvas.transform, false);
            var h = go.AddComponent<KitLogHistory>();
            h.Build();
            return h;
        }

        void Awake() { if (Instance == null) Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; HudLog.OnAdd -= OnAdd; HudLog.OnCleared -= OnCleared; }

        // ---- build ------------------------------------------------------------------------------------------------------------
        public void Build()
        {
            if (root != null) return;
            var th = UITheme.Current;
            root = (RectTransform)transform;
            HudKit.Stretch(root);
            group = gameObject.AddComponent<CanvasGroup>();

            var dim = HudKit.NewRect("DecoBackdrop", root);
            HudKit.Stretch(dim);
            HudKit.AddImage(dim, new Color(0f, 0f, 0f, BackdropAlpha), true);   // swallows clicks on the battlefield

            window = HudKit.NewRect("Window", root);
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f, .5f);
            window.sizeDelta = new Vector2(W, H);
            var bg = th.ink; bg.a = .96f;
            HudKit.AddImage(window, bg, true);

            var stripe = HudKit.NewRect("DecoStripe", window);
            stripe.anchorMin = new Vector2(0f, 0f); stripe.anchorMax = new Vector2(0f, 1f); stripe.pivot = new Vector2(0f, .5f);
            stripe.sizeDelta = new Vector2(StripeW, 0f);
            HudKit.AddImage(stripe, th.acc);

            var rule = HudKit.NewRect("DecoRule", window);
            rule.anchorMin = new Vector2(0f, 1f); rule.anchorMax = new Vector2(1f, 1f); rule.pivot = new Vector2(.5f, 1f);
            rule.offsetMin = new Vector2(Pad, -HeaderH - 1f); rule.offsetMax = new Vector2(-Pad, -HeaderH);
            HudKit.AddImage(rule, th.ink3);

            var title = HudKit.AddText(window, "COMBAT LOG", TitleSize, th.paper, true, TextAlignmentOptions.BottomLeft, "Title", UITheme.TextRole.Display);
            Place(title.rectTransform, Pad, 14f, 300f, 34f);
            var sub = HudKit.AddText(window, "戦闘ログ", SubSize, th.paper2, false, TextAlignmentOptions.TopLeft, "Sub", UITheme.TextRole.Body);
            Place(sub.rectTransform, Pad, 48f, 300f, 20f);
            var close = HudKit.AddText(window, "Tab / ESC  閉じる", CloseSize, th.paper2, false, TextAlignmentOptions.MidlineRight, "Close", UITheme.TextRole.Body);
            var crt = close.rectTransform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-Pad, -24f); crt.sizeDelta = new Vector2(260f, 26f);

            var view = HudKit.NewRect("Viewport", window);
            view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one;
            view.offsetMin = new Vector2(Pad, PadBottom); view.offsetMax = new Vector2(-Pad, -HeaderH - 12f);
            view.gameObject.AddComponent<RectMask2D>();

            content = HudKit.NewRect("Content", view);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(.5f, 1f);
            content.sizeDelta = new Vector2(0f, 10f);
            body = HudKit.AddText(content, "", BodySize, th.paper, false, TextAlignmentOptions.TopLeft, "Lines", UITheme.TextRole.Body);
            HudKit.Stretch(body.rectTransform);
            body.richText = true;
            UILayout.LineHeight(body, 1.45f);

            empty = HudKit.AddText(view, "まだ記録はありません", BodySize, th.dim, false, TextAlignmentOptions.Center, "Empty", UITheme.TextRole.Body);
            HudKit.Stretch(empty.rectTransform);

            if (Application.isPlaying) { HudLog.OnAdd += OnAdd; HudLog.OnCleared += OnCleared; }   // (previews: no OnDestroy in edit mode)
            root.gameObject.SetActive(true);
            group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
            SetChildren(false);
        }

        static void Place(RectTransform rt, float x, float top, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -top); rt.sizeDelta = new Vector2(w, h);
        }

        void SetChildren(bool on)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i).gameObject;
                if (c.activeSelf != on) c.SetActive(on);
            }
        }

        // ---- text -------------------------------------------------------------------------------------------------------------
        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        void Rebuild(bool toBottom)
        {
            dirty = false;
            var th = UITheme.Current;
            var hist = HudLog.History;
            sb.Length = 0;
            int from = Mathf.Max(0, hist.Count - ShowMax);
            if (from > 0) sb.Append("<color=#").Append(Hex(th.dim)).Append(">（古い記録は省略）</color>\n");
            int round = int.MinValue;
            string acc = Hex(th.acc), ally = Hex(th.blue), foe = Hex(th.foe), plain = Hex(th.dim);
            for (int i = from; i < hist.Count; i++)
            {
                var (text, type, r) = hist[i];
                if (r != round)
                {
                    round = r;
                    if (i > from) sb.Append('\n');
                    sb.Append("<size=").Append(RoundSize).Append("><b><color=#").Append(acc).Append('>');
                    if (r <= 0) sb.Append("配置"); else sb.Append("ラウンド ").Append(r);
                    sb.Append("</color></b></size>\n");
                }
                string mark = type == HudLogType.Ally ? ally : type == HudLogType.Foe ? foe : type == HudLogType.Warn ? acc : plain;
                sb.Append("<color=#").Append(mark).Append(">■</color>  ").Append(text).Append('\n');
            }
            body.SetText(sb);
            empty.gameObject.SetActive(hist.Count == 0);

            float wv = ((RectTransform)content.parent).rect.width;
            float hv = ((RectTransform)content.parent).rect.height;
            float hc = hist.Count == 0 ? 0f : body.GetPreferredValues(sb.ToString(), wv, 100000f).y + 8f;
            content.sizeDelta = new Vector2(0f, hc);
            maxOffset = Mathf.Max(0f, hc - hv);
            if (toBottom || offset > maxOffset) offset = toBottom ? maxOffset : Mathf.Min(offset, maxOffset);
            content.anchoredPosition = new Vector2(0f, offset);
        }

        void OnAdd(string text, HudLogType type)
        {
            if (!open) return;
            bool atBottom = offset >= maxOffset - 1f;
            dirty = true;
            if (atBottom) Rebuild(true);
        }

        void OnCleared() { if (open) Rebuild(true); }

        // ---- open / close -----------------------------------------------------------------------------------------------------
        public void Open()
        {
            if (open) return;
            open = true;
            SetChildren(true);
            group.blocksRaycasts = true; group.interactable = true;
            transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            Rebuild(true);
            clock = 0f; running = true;
            Evaluate(0f);
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            group.blocksRaycasts = false; group.interactable = false;
            group.alpha = 0f;
            SetChildren(false);
            running = false;
        }

        public void Toggle() { if (open) Close(); else Open(); }

        /// <summary>Editor previews: open and settled, showing whatever HudLog.History holds.</summary>
        public void PoseOpen()
        {
            Build();
            open = true;
            SetChildren(true);
            group.blocksRaycasts = true;
            Rebuild(true);
            Evaluate(Duration);
        }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public void Evaluate(float t)
        {
            float p = UIMotionSettings.reduceMotion ? 1f : UIEase.Tactical(t / EnterTime);
            if (group.alpha != p) group.alpha = p;
            float s = Mathf.LerpUnclamped(EnterScale, 1f, p);
            var sc = new Vector3(s, s, 1f);
            if (window.localScale != sc) window.localScale = sc;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.tabKey.wasPressedThisFrame && !Blindside.Rendering.BattlefieldCamera.InputLocked) Toggle();
                else if (open && kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed) { EscapeGate.Consume(); Close(); }
            }
            if (!open) return;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float y = mouse.scroll.ReadValue().y;
                if (y != 0f)
                {
                    offset = Mathf.Clamp(offset - Mathf.Sign(y) * WheelStep, 0f, maxOffset);
                    content.anchoredPosition = new Vector2(0f, offset);
                    if (dirty && offset >= maxOffset - 1f) Rebuild(true);
                }
            }
            if (running)
            {
                clock += Time.unscaledDeltaTime;
                Evaluate(clock);
                if (clock >= EnterTime) running = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }
    }
}
