// The HQ stations' panel (ショップ / 訓練所 / 作戦室 / 装備設定 / 隊員強化's squad list): one frame, its content from an IHubContent.
// Built at runtime under a full-screen parent, the look of the growth panel (KitGrowthPanel):
//   DecoDim (rgba(3,5,8,.5), takes every click) + Pop 880 × 560, centred: Box paper, 26 cut top right / bottom left, DecoShadow
//     Head     ink 46: blue band 12, the title 18 body, the English 11 display (right)
//     Tabs     (when the content has any) 38: slanted chips 150 × 30 (the open one blue), under the head
//     List     rows 66 high in a ScrollRect: [face 54] name 16 / note 12 (dim) · value 13 display (right) · button 124 × 36
//              (blue when it can be pressed, paper2 when not)
//     Footer   52: the status line (money, or what just happened; 13) on the left + 「閉じる」 (paper2) on the right
// Rows are rebuilt on a click only (never per frame). ESC closes (EscapeGate). Entrance .2s dim + .32s pop (ITimedEffect.Evaluate).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Blindside.UI
{
    /// <summary>One row of a station's list.</summary>
    public sealed class HubRow
    {
        public string name = "", note = "", value = "", button = "";
        public Sprite face;
        public Color band = Color.clear;
        public bool enabled = true;
        /// <summary>Drawn as the current choice (a blue edge, the value in blue).</summary>
        public bool current;
    }

    /// <summary>What a station shows in the list panel.</summary>
    public interface IHubContent
    {
        string Title { get; }
        string En { get; }
        int TabCount { get; }
        string Tab(int i);
        /// <summary>The rows of tab `tab` into `into` (cleared first).</summary>
        void Fill(int tab, List<HubRow> into);
        /// <summary>Row `row`'s button on tab `tab`; returns the status line to show (null: the default).</summary>
        string Act(int tab, int row);
        /// <summary>The footer's status line when nothing just happened.</summary>
        string Status();
    }

    public class HubListPanel : MonoBehaviour, ITimedEffect
    {
        public const float Width = 880f, Height = 560f, HeadH = 46f, TabsH = 38f, RowH = 66f, FootH = 52f, PadX = 28f, Cut = 26f, BandW = 12f;
        public const float BtnW = 124f, BtnH = 36f, FaceS = 54f, TabW = 150f, TabH = 30f;
        public const float DimTime = .2f, PopTime = .32f;
        static readonly Color DimColor = new Color32(3, 5, 8, 128);

        RectTransform pop, box, tabsRow, content, viewport;
        CanvasGroup popGroup;
        Image dim;
        TMP_Text title, en, status;
        ScrollRect scroll;
        float clock; bool running;

        IHubContent source;
        int tab;
        readonly List<HubRow> rows = new List<HubRow>(16);
        readonly List<GameObject> built = new List<GameObject>(32);
        string flash;

        public bool IsOpen => gameObject.activeSelf;
        public IHubContent Content => source;
        /// <summary>After every close (the HQ unfreezes the player).</summary>
        public System.Action Closed;
        /// <summary>True while something over this panel (the growth panel) takes ESC.</summary>
        public System.Func<bool> EscBlocked;

        public static HubListPanel Build(Transform parent)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect("HubPanel", parent);
            HudKit.Stretch(root);
            var p = root.gameObject.AddComponent<HubListPanel>();

            var dimRt = HudKit.NewRect("DecoDim", root);
            HudKit.Stretch(dimRt);
            p.dim = HudKit.AddImage(dimRt, DimColor, raycast: true);

            p.pop = HudKit.NewRect("Pop", root);
            p.pop.anchorMin = p.pop.anchorMax = p.pop.pivot = new Vector2(.5f, .5f);
            p.pop.sizeDelta = new Vector2(Width, Height);
            p.popGroup = p.pop.gameObject.AddComponent<CanvasGroup>();
            var boxPanel = TacticalPanel.Create(p.pop, "Box", KitColor.Paper, SlantedGraphic.Size.Custom, 0f);
            HudKit.Stretch(boxPanel.rectTransform);
            boxPanel.raycastTarget = true;
            boxPanel.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.Mockup);
            HudKit.Stretch(KitShadow.Behind(boxPanel.rectTransform, new Color(.03f, .04f, .06f), .6f, 14f, 30f).rectTransform);
            p.box = boxPanel.rectTransform;

            // head
            var head = HudKit.NewRect("Head", p.box);
            UILayout.Place(head, 0f, 0f, Width, HeadH);
            HudKit.AddImage(head, theme.ink);
            head.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.TopRight);
            var hb = HudKit.NewRect("DecoBand", head);
            hb.anchorMin = new Vector2(0f, 0f); hb.anchorMax = new Vector2(0f, 1f); hb.pivot = new Vector2(0f, .5f);
            hb.anchoredPosition = Vector2.zero; hb.sizeDelta = new Vector2(BandW, 0f);
            HudKit.AddImage(hb, theme.blue);
            p.title = HudKit.AddText(head, "", 18f, Color.white, false, TextAlignmentOptions.MidlineLeft, "Title", UITheme.TextRole.Body);
            UILayout.Place(p.title.rectTransform, BandW + PadX, 0f, 360f, HeadH);
            p.en = HudKit.AddText(head, "", 11f, Color.Lerp(theme.ink, Color.white, .65f), true, TextAlignmentOptions.MidlineRight, "En", UITheme.TextRole.Display);
            p.en.characterSpacing = 10f;
            UILayout.Place(p.en.rectTransform, Width - 52f - 260f, 0f, 260f, HeadH);

            // tabs
            p.tabsRow = HudKit.NewRect("Tabs", p.box);
            UILayout.Place(p.tabsRow, PadX, HeadH + 10f, Width - PadX * 2f, TabH);

            // list
            p.viewport = HudKit.NewRect("List", p.box);
            p.viewport.gameObject.AddComponent<RectMask2D>();
            HudKit.AddImage(p.viewport, new Color(0f, 0f, 0f, 0f), raycast: true);
            p.content = HudKit.NewRect("Rows", p.viewport);
            p.content.anchorMin = new Vector2(0f, 1f); p.content.anchorMax = new Vector2(1f, 1f); p.content.pivot = new Vector2(.5f, 1f);
            p.content.anchoredPosition = Vector2.zero; p.content.sizeDelta = Vector2.zero;
            // rows past the viewport are scrolled into view (masked by List): not off-screen by mistake
            var ig = p.content.gameObject.AddComponent<LintIgnoreOverlap>();
            ig.bounds = true; ig.reason = "scroll content: rows below the viewport are masked until scrolled to";
            p.scroll = p.viewport.gameObject.AddComponent<ScrollRect>();
            p.scroll.content = p.content; p.scroll.viewport = p.viewport;
            p.scroll.horizontal = false; p.scroll.vertical = true;
            p.scroll.movementType = ScrollRect.MovementType.Clamped;
            p.scroll.scrollSensitivity = 30f;

            // footer
            float fy = Height - FootH;
            var foot = HudKit.NewRect("Footer", p.box);
            UILayout.Place(foot, 0f, fy, Width, FootH);
            HudKit.AddImage(foot, Color.Lerp(theme.paper, theme.paper2, .5f));
            foot.gameObject.AddComponent<CutCornerGraphic>().Set(Cut, CutCornerGraphic.Corners.BottomLeft);
            p.status = HudKit.AddText(foot, "", 13f, theme.ink, true, TextAlignmentOptions.MidlineLeft, "Status", UITheme.TextRole.Body);
            p.status.textWrappingMode = TextWrappingModes.NoWrap; p.status.overflowMode = TextOverflowModes.Ellipsis;
            UILayout.Place(p.status.rectTransform, PadX + 10f, 0f, Width - 260f - PadX, FootH);
            var close = HudKit.NewRect("Close", foot);
            UILayout.Place(close, Width - 220f, 0f, 220f, FootH);
            var ci = HudKit.AddImage(close, theme.ink, raycast: true);
            close.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 18f);
            var cb = close.gameObject.AddComponent<Button>();
            cb.transition = Selectable.Transition.None; cb.targetGraphic = ci;
            cb.onClick.AddListener(p.Close);
            close.gameObject.AddComponent<KitBrighten>().image = ci;
            var ct = HudKit.AddText(close, "閉じる", 16f, Color.white, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Title);
            HudKit.Stretch(ct.rectTransform);

            root.gameObject.SetActive(false);
            return p;
        }

        // ---- open / close ------------------------------------------------------------------------------------------------------
        public void Open(IHubContent content, int startTab = 0)
        {
            source = content;
            tab = Mathf.Clamp(startTab, 0, Mathf.Max(0, content.TabCount - 1));
            title.SetText(content.Title);
            en.SetText(content.En);
            flash = null;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            BuildTabs();
            Rebuild();
            scroll.verticalNormalizedPosition = 1f;
            clock = 0f;
            running = Application.isPlaying;
            Evaluate(running ? 0f : Duration);
        }

        public void Close()
        {
            running = false;
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        /// <summary>Shows tab `i` (previews).</summary>
        public void ShowTab(int i)
        {
            if (source == null) return;
            tab = Mathf.Clamp(i, 0, Mathf.Max(0, source.TabCount - 1));
            flash = null;
            BuildTabs();
            Rebuild();
        }

        /// <summary>Redraws the rows (after something else changed the save, e.g. the growth panel).</summary>
        public void Refresh() { if (source != null && IsOpen) Rebuild(); }

        // ---- build the dynamic parts (on open / a click) -----------------------------------------------------------------------
        void BuildTabs()
        {
            HudKit.ClearChildren(tabsRow);
            var theme = UITheme.Current;
            int n = source.TabCount;
            tabsRow.gameObject.SetActive(n > 0);
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                bool on = i == tab;
                var t = HudKit.NewRect("Tab" + i, tabsRow);
                UILayout.Place(t, i * (TabW + 6f), 0f, TabW, TabH);
                var img = HudKit.AddImage(t, on ? theme.blue : theme.paper2, raycast: true);
                t.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
                var b = t.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None; b.targetGraphic = img;
                b.onClick.AddListener(() => ShowTab(idx));
                t.gameObject.AddComponent<KitBrighten>().image = img;
                var tx = HudKit.AddText(t, source.Tab(i), 13f, on ? Color.white : theme.ink, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Body);
                tx.textWrappingMode = TextWrappingModes.NoWrap; tx.overflowMode = TextOverflowModes.Ellipsis;
                HudKit.Stretch(tx.rectTransform);
            }
            float top = HeadH + (n > 0 ? 10f + TabH + 10f : 14f);
            UILayout.Place(viewport, PadX, top, Width - PadX * 2f, Height - FootH - 10f - top);
        }

        void Rebuild()
        {
            foreach (var g in built) if (g != null) HudKit.DestroySafe(g);
            built.Clear();
            source.Fill(tab, rows);
            var theme = UITheme.Current;
            float w = Width - PadX * 2f;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                int idx = i;
                var row = HudKit.NewRect("Row" + i, content);
                row.anchorMin = row.anchorMax = new Vector2(0f, 1f); row.pivot = new Vector2(0f, 1f);
                row.anchoredPosition = new Vector2(0f, -i * RowH); row.sizeDelta = new Vector2(w, RowH - 6f);
                built.Add(row.gameObject);
                HudKit.AddImage(row, r.current ? Color.Lerp(theme.paper, theme.blue, .12f) : Color.Lerp(theme.paper, theme.paper2, .45f));
                var edge = HudKit.NewRect("DecoEdge", row);
                UILayout.Place(edge, 0f, 0f, 4f, RowH - 6f);
                HudKit.AddImage(edge, r.current ? theme.blue : (r.band.a > 0f ? r.band : Color.Lerp(theme.paper2, theme.ink, .2f)));

                float x = 16f;
                if (r.face != null)
                {
                    var f = HudKit.NewRect("Face", row);
                    UILayout.Place(f, x, (RowH - 6f - FaceS) * .5f, FaceS, FaceS);
                    HudKit.AddImage(f, Color.Lerp(theme.paper2, theme.ink, .1f));
                    f.gameObject.AddComponent<RectMask2D>();
                    var ph = HudKit.NewRect("Photo", f);
                    HudKit.Stretch(ph);
                    var im = ph.gameObject.AddComponent<Image>();
                    im.sprite = r.face; im.preserveAspect = true; im.raycastTarget = false;
                    x += FaceS + 14f;
                }
                float valueW = 190f, right = w - BtnW - 16f;
                var nm = HudKit.AddText(row, r.name, 16f, theme.ink, true, TextAlignmentOptions.TopLeft, "Name", UITheme.TextRole.Body);
                nm.textWrappingMode = TextWrappingModes.NoWrap; nm.overflowMode = TextOverflowModes.Ellipsis;
                UILayout.Place(nm.rectTransform, x, 9f, right - valueW - x - 10f, 22f);
                var note = HudKit.AddText(row, r.note, 12f, theme.dim, false, TextAlignmentOptions.TopLeft, "Note", UITheme.TextRole.Body);
                note.textWrappingMode = TextWrappingModes.NoWrap; note.overflowMode = TextOverflowModes.Ellipsis;
                UILayout.Place(note.rectTransform, x, 34f, right - x - 10f, 18f);
                var val = HudKit.AddText(row, r.value, 13f, r.current ? theme.blue : theme.ink, true, TextAlignmentOptions.MidlineRight, "Value", UITheme.TextRole.Body);
                val.textWrappingMode = TextWrappingModes.NoWrap; val.overflowMode = TextOverflowModes.Ellipsis;
                UILayout.Place(val.rectTransform, right - valueW - 14f, 6f, valueW, 24f);

                if (!string.IsNullOrEmpty(r.button))
                {
                    var b = HudKit.NewRect("Button", row);
                    UILayout.Place(b, w - BtnW - 14f, (RowH - 6f - BtnH) * .5f, BtnW, BtnH);
                    var bi = HudKit.AddImage(b, r.enabled ? theme.blue : Color.Lerp(theme.paper2, theme.ink, .12f), raycast: true);
                    b.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 10f);
                    var btn = b.gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None; btn.targetGraphic = bi;
                    btn.interactable = r.enabled;
                    btn.onClick.AddListener(() => Press(idx));
                    if (r.enabled) b.gameObject.AddComponent<KitBrighten>().image = bi;
                    var bt = HudKit.AddText(b, r.button, 14f, r.enabled ? Color.white : theme.dim, true, TextAlignmentOptions.Center, "Text", UITheme.TextRole.Title);
                    bt.textWrappingMode = TextWrappingModes.NoWrap;
                    HudKit.Stretch(bt.rectTransform);
                }
            }
            content.sizeDelta = new Vector2(0f, rows.Count * RowH);
            status.SetText(string.IsNullOrEmpty(flash) ? source.Status() : flash);
        }

        void Press(int row)
        {
            if (source == null) return;
            flash = source.Act(tab, row);
            if (IsOpen && source != null) Rebuild();
        }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public float Duration => Mathf.Max(DimTime, PopTime);

        public void Evaluate(float t)
        {
            var c = DimColor; c.a *= Mathf.Clamp01(t / DimTime);
            if (dim.color != c) dim.color = c;
            float p = UIEase.Tactical(Mathf.Clamp01(t / PopTime));
            float s = UIMotionSettings.reduceMotion ? 1f : Mathf.Lerp(.9f, 1f, p);
            if (pop.localScale.x != s) pop.localScale = new Vector3(s, s, 1f);
            if (popGroup.alpha != p) popGroup.alpha = p;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed && (EscBlocked == null || !EscBlocked()))
            { EscapeGate.Consume(); Close(); return; }
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }
    }
}
