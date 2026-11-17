// The HQ's ESC menu (1440 × 810 menu canvas), over the third-person view:
//   DecoDim rgba(3,5,8,.62), takes every click
//   "HQ MENU" 12 display (blue, spaced) + 「メニュー」 34 title, centred, 150 from the top; a 240 × 6 bar under it (blue 30% / grey)
//   entries (KitTextButton 22, 52 apart, centred from 300): 隊員情報 (the squad page: every operator's stats, ability, loadout — the
//   stat points are given there), コープ (HubCoopScreen: FLARE's co-op ranks and their stories), 設定 (KitOptionsPanel), 保存, タイトルへ, 再開
//   hint 「ESC  再開」 11 / 12 (dim) under the entries
//   Status bottom left (48, 48 from the bottom) 420 × 132 ink (slant 22, a blue band, an acc chip "STATUS // 現在の状態" 15): the money
//          "¥12,345" 40 display, under it the date · 午前/午後 · DAY n 15 and the rank 15 (right) — filled on Open from Data
// ESC closes it (a sub-panel open over it takes ESC first). The entrance is the dim's .2s fade (ITimedEffect.Evaluate); nothing per frame
// once settled.
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Blindside.UI
{
    public class HubMenu : MonoBehaviour, ITimedEffect
    {
        public const float HeadY = 150f, EntriesY = 300f, EntryStep = 52f, FadeTime = .2f;
        static readonly Color DimColor = new Color32(3, 5, 8, 158);

        Image dim;
        CanvasGroup group;
        KitOptionsPanel options;
        float clock; bool running;

        public bool IsOpen => gameObject.activeSelf;
        public bool SubOpen => options != null && options.IsOpen;
        /// <summary>True while a panel opened from the menu (the squad page, the growth panel) is over it: it takes ESC.</summary>
        public System.Func<bool> Covered;
        /// <summary>The save the status block reads (HubScreen.Data); null: nothing shown.</summary>
        public System.Func<Blindside.Save.SaveData> Data;
        /// <summary>A real session (false: placeholders).</summary>
        public System.Func<bool> Live;
        public const float StatusW = 420f, StatusH = 132f, StatusX = 48f, StatusBottom = 48f;
        TMP_Text statusMoney, statusDate, statusRank;

        public static HubMenu Build(Transform parent, System.Action squad, System.Action save, System.Action title, System.Action coop = null)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect("HubMenu", parent);
            HudKit.Stretch(root);
            var m = root.gameObject.AddComponent<HubMenu>();
            m.group = root.gameObject.AddComponent<CanvasGroup>();
            var dimRt = HudKit.NewRect("DecoDim", root);
            HudKit.Stretch(dimRt);
            m.dim = HudKit.AddImage(dimRt, DimColor, raycast: true);

            var en = HudKit.AddText(root, "HQ MENU", 12f, theme.blue, true, TextAlignmentOptions.Center, "En", UITheme.TextRole.Display);
            en.characterSpacing = 14f;
            Top(en.rectTransform, HeadY, 16f);
            var head = HudKit.AddText(root, "メニュー", 34f, Color.white, true, TextAlignmentOptions.Center, "Title", UITheme.TextRole.Title);
            Top(head.rectTransform, HeadY + 18f, 46f);
            var bar = HudKit.NewRect("DecoBar", root);
            bar.anchorMin = bar.anchorMax = new Vector2(.5f, 1f); bar.pivot = new Vector2(.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -(HeadY + 72f)); bar.sizeDelta = new Vector2(240f, 6f);
            var b1 = HudKit.NewRect("DecoBarBlue", bar);
            b1.anchorMin = Vector2.zero; b1.anchorMax = new Vector2(.3f, 1f); b1.offsetMin = b1.offsetMax = Vector2.zero;
            HudKit.AddImage(b1, theme.blue);
            var b2 = HudKit.NewRect("DecoBarGrey", bar);
            b2.anchorMin = new Vector2(.32f, 0f); b2.anchorMax = Vector2.one; b2.offsetMin = b2.offsetMax = Vector2.zero;
            HudKit.AddImage(b2, new Color32(0xC2, 0xC2, 0xC3, 0xff));

            m.Entry(0, "Squad", "隊員情報", () => squad?.Invoke());
            m.Entry(1, "Coop", "コープ", () => coop?.Invoke());
            m.Entry(2, "Settings", "設定", () => m.options.Open());
            m.Entry(3, "Save", "保存", () => save?.Invoke());
            m.Entry(4, "Title", "タイトルへ", () => title?.Invoke());
            m.Entry(5, "Resume", "再開", m.Close);

            var hint = HudKit.NewRect("Hint", root);
            Top(hint, EntriesY + 6f * EntryStep + 10f, 18f);
            var esc = HudKit.AddText(hint, "ESC", 11f, theme.dim, true, TextAlignmentOptions.MidlineRight, "Key", UITheme.TextRole.Display);
            esc.characterSpacing = 10f;
            esc.rectTransform.anchorMin = Vector2.zero; esc.rectTransform.anchorMax = new Vector2(.5f, 1f);
            esc.rectTransform.offsetMin = Vector2.zero; esc.rectTransform.offsetMax = new Vector2(-4f, 0f);
            var ht = HudKit.AddText(hint, "再開", 12f, theme.dim, false, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Body);
            ht.rectTransform.anchorMin = new Vector2(.5f, 0f); ht.rectTransform.anchorMax = Vector2.one;
            ht.rectTransform.offsetMin = new Vector2(4f, 0f); ht.rectTransform.offsetMax = Vector2.zero;

            m.BuildStatus(root);
            m.options = KitOptionsPanel.Build(root);
            root.gameObject.SetActive(false);
            return m;
        }

        // the current state: the money first, then the day and the rank
        void BuildStatus(RectTransform root)
        {
            var theme = UITheme.Current;
            var p = HudKit.NewRect("Status", root);
            p.anchorMin = p.anchorMax = p.pivot = Vector2.zero;
            p.anchoredPosition = new Vector2(StatusX, StatusBottom);
            p.sizeDelta = new Vector2(StatusW, StatusH);
            HudKit.AddImage(p, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .95f));
            p.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 22f);
            var band = HudKit.NewRect("DecoBand", p);
            UILayout.Place(band, 0f, 0f, 12f, StatusH);
            HudKit.AddImage(band, theme.blue);
            var chip = HudKit.NewRect("Chip", p);
            UILayout.Place(chip, 26f, 14f, 220f, 26f);
            HudKit.AddImage(chip, theme.acc);
            chip.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 8f);
            var cl = HudKit.AddText(chip, "STATUS // 現在の状態", 15f, theme.ink, true, TextAlignmentOptions.MidlineLeft, "Label", UITheme.TextRole.Body);
            cl.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(cl.rectTransform, 10f, 0f, 205f, 26f);
            var ml = HudKit.AddText(p, "所持金", 15f, Color.Lerp(theme.paper2, theme.ink, .2f), true, TextAlignmentOptions.MidlineLeft, "MoneyLabel", UITheme.TextRole.Body);
            UILayout.Place(ml.rectTransform, 26f, 46f, 70f, 46f);
            statusMoney = HudKit.AddText(p, "¥ —", 40f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Money", UITheme.TextRole.Display);
            statusMoney.textWrappingMode = TextWrappingModes.NoWrap;
            statusMoney.enableAutoSizing = true; statusMoney.fontSizeMin = 24f; statusMoney.fontSizeMax = 40f;
            UILayout.Place(statusMoney.rectTransform, 98f, 44f, StatusW - 130f, 50f);
            statusDate = HudKit.AddText(p, "", 15f, theme.acc, true, TextAlignmentOptions.MidlineLeft, "Date", UITheme.TextRole.Body);
            statusDate.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(statusDate.rectTransform, 26f, 96f, 230f, 24f);
            statusRank = HudKit.AddText(p, "", 15f, Color.white, true, TextAlignmentOptions.MidlineRight, "Rank", UITheme.TextRole.Body);
            statusRank.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(statusRank.rectTransform, 250f, 96f, 140f, 24f);
        }

        void RefreshStatus()
        {
            var d = Data != null ? Data() : null;
            if (d == null || (Live != null && !Live())) { statusMoney.SetText("¥ —"); statusDate.SetText(""); statusRank.SetText(""); return; }
            statusMoney.SetText("¥" + d.money.ToString("N0"));
            var dt = Blindside.Progress.Campaign.Date(d);
            statusDate.SetText(dt.Month + "/" + dt.Day + " " + Blindside.Progress.Campaign.WeekdayJp(d) + " · " + Blindside.Progress.Campaign.TimeName(d)
                               + " · DAY " + Blindside.Progress.Campaign.DayNumber(d));
            statusRank.SetText(Blindside.Progress.RankTable.Name(Blindside.Progress.RankTable.RankOf(d.rankPoints)));
        }

        static void Top(RectTransform rt, float y, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y); rt.sizeDelta = new Vector2(0f, h);
        }

        void Entry(int i, string name, string label, UnityEngine.Events.UnityAction onClick)
        {
            var b = KitTextButton.Create(transform, name, label, 22f, 44f, .5f, onClick);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -(EntriesY + i * EntryStep) - 22f);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RefreshStatus();
            clock = 0f;
            running = Application.isPlaying;
            Evaluate(running ? 0f : Duration);
        }

        public void Close()
        {
            running = false;
            if (options != null && options.IsOpen) options.Close();
            gameObject.SetActive(false);
        }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public float Duration => FadeTime;

        public void Evaluate(float t)
        {
            float a = Mathf.Clamp01(t / FadeTime);
            if (group.alpha != a) group.alpha = a;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !EscapeGate.Consumed && !SubOpen && (Covered == null || !Covered()))
            { EscapeGate.Consume(); Close(); return; }
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }
    }
}
