// The HUD's command menu (#cmd, ui_spec "명령 메뉴"): a header chip ("KEY // COMMAND": ink, 12px display, a 4px band in the operator's
// colour on the left) over a column of TacticalButtons (226×40, gap 3). The menu knows no game types: Render takes CommandModels
// (id, Japanese name, English line, value, enabled) and calls onClick(id) — PlayerSelectionController fills it from Commands(u) and
// keeps DoCommand, the 1–6 keys, the position and Hide exactly as before. Hover / press / disabled are the TacticalButton's own
// (EventSystem pointer events), so UpdateHover is a no-op kept for the old call site.
//   Root        top-left anchored (pivot 0,1), 250 wide, 21 + n × 43 high
//     Ch        the header chip: DecoBg ink, DecoBand 4, Text 12 display (spacing .1em)
//     Row_<id>  slot at the row's place (never moves)
//       Enter   the entrance pose: x −26 → 0 and alpha 0 → 1 over .32s, row i starting i × 40ms (cmdIn)
//         Cmd_<id>   TacticalButton
// The entrance is ITimedEffect.Evaluate(t) on one menu clock; Update only advances it with Time.unscaledDeltaTime and stops at the
// end. No allocation per frame (rows and groups are cached at Render).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Command Menu")]
    public class KitCommandMenu : MonoBehaviour, ITimedEffect
    {
        public const float Width = 250f, HeaderH = 21f, RowW = 226f, RowH = 40f, Gap = 3f;
        public const float EnterTime = .32f, EnterStep = .04f, EnterShift = 26f;

        class Row
        {
            public RectTransform enter;
            public CanvasGroup group;
            public TacticalButton button;
        }
        readonly List<Row> rows = new List<Row>();
        float clock;
        bool running;

        public RectTransform Root => (RectTransform)transform;
        public bool Visible => gameObject.activeSelf;
        public int RowCount => rows.Count;
        public IReadOnlyList<TacticalButton> Buttons => buttonList;
        readonly List<TacticalButton> buttonList = new List<TacticalButton>();

        /// <summary>Anchors the root top-left (the controller positions it); the prefab holds only the root, like HUD_UnitPanel.</summary>
        public void Build()
        {
            var rt = Root;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(Width, HeaderH);
            gameObject.SetActive(false);
        }

        /// <summary>Instantiates the kit menu prefab under `parent`, built and hidden.</summary>
        public static KitCommandMenu Build(Transform parent, GameObject prefab)
        {
            var go = Instantiate(prefab, parent, false);
            go.name = "CmdMenu";
            var m = go.GetComponent<KitCommandMenu>();
            m.Build();
            return m;
        }

        /// <summary>Port of renderMenu(u)'s drawing: the header and one row per command; `fresh` plays the cmdIn entrance.</summary>
        public void Render(string header, Color accent, IList<CommandModel> list, bool fresh, System.Action<string> onClick)
        {
            var root = Root;
            running = false;
            HudKit.ClearChildren(root);
            rows.Clear();
            buttonList.Clear();

            // active before building: TMP measures itself in GetPreferredValues only on an active hierarchy
            if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);

            BuildHeader(header, accent);
            for (int i = 0; i < list.Count; i++) BuildRow(list[i], i, onClick);
            root.sizeDelta = new Vector2(Width, HeaderH + list.Count * (RowH + Gap));

            clock = 0f;
            if (fresh && Application.isPlaying) running = true;
            Evaluate(running ? 0f : Duration);
        }

        void BuildHeader(string text, Color accent)
        {
            // #cmd .h{font:700 12px display; letter-spacing:.1em; padding:3px 12px; border-left:4px solid var(--c)}
            var ch = HudKit.NewRect("Ch", Root);
            var bg = HudKit.NewRect("DecoBg", ch);
            HudKit.Stretch(bg);
            HudKit.AddImage(bg, UITheme.Current.ink, raycast: true);        // part of the menu, not the battlefield
            var band = HudKit.NewRect("DecoBand", ch);
            band.anchorMin = new Vector2(0f, 0f); band.anchorMax = new Vector2(0f, 1f); band.pivot = new Vector2(0f, .5f);
            band.anchoredPosition = Vector2.zero; band.sizeDelta = new Vector2(4f, 0f);
            HudKit.AddImage(band, accent);
            var t = HudKit.AddText(ch, text, 12f, Color.white, true, TextAlignmentOptions.MidlineLeft, "Text", UITheme.TextRole.Display);
            t.characterSpacing = 10f;
            HudKit.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(16f, 0f); t.rectTransform.offsetMax = new Vector2(-12f, 0f);
            float w = Mathf.Ceil(t.GetPreferredValues(text).x) + 16f + 12f;
            UILayout.Place(ch, 0f, 0f, w, HeaderH);
        }

        void BuildRow(CommandModel c, int index, System.Action<string> onClick)
        {
            var slot = HudKit.NewRect("Row_" + c.id, Root);
            UILayout.Place(slot, 0f, HeaderH + Gap + index * (RowH + Gap), RowW + TacticalButton.HoverShift, RowH);
            var enter = HudKit.NewRect("Enter", slot);
            UILayout.Place(enter, 0f, 0f, RowW + TacticalButton.HoverShift, RowH);
            var group = enter.gameObject.AddComponent<CanvasGroup>();

            // the value on the right: the distance ("7.0m"), the charges ("3回", "∞"), "展開" … ; empty → no chip
            var b = TacticalButton.Create(enter, "Cmd_" + c.id, (index + 1).ToString(), c.kr, c.en, c.sub, null, UITheme.TextRole.Body);
            UILayout.Place((RectTransform)b.transform, 0f, 0f, RowW, RowH);
            b.interactable = c.ok;
            if (!c.ok) b.Go(TacticalButton.Visual.Disabled, true);
            string id = c.id;
            b.onClick.AddListener(() => onClick?.Invoke(id));

            rows.Add(new Row { enter = enter, group = group, button = b });
            buttonList.Add(b);
        }

        /// <summary>The old call site polls the pointer; hover now comes from the buttons' pointer events.</summary>
        public void UpdateHover(Vector2 pos, float dt) { }

        public void Hide()
        {
            running = false;
            gameObject.SetActive(false);
            rows.Clear();
            buttonList.Clear();
        }

        // ---- ITimedEffect -----------------------------------------------------------------------------------------------------
        public float Duration => rows.Count == 0 ? 0f : EnterTime + (rows.Count - 1) * EnterStep;

        /// <summary>Poses the entrance `t` seconds in: row i slides in from the left (26) and fades in over .32s, starting at i × 40ms.</summary>
        public void Evaluate(float t)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                float p = UIMotionSettings.reduceMotion ? 1f : UIEase.Tactical(Mathf.Clamp01((t - i * EnterStep) / EnterTime));
                var r = rows[i];
                float x = -EnterShift * (1f - p);
                if (r.enter.anchoredPosition.x != x) r.enter.anchoredPosition = new Vector2(x, 0f);
                if (r.group.alpha != p) r.group.alpha = p;
            }
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }
    }
}
