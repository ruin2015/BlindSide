// The command menu (#cmdMenu / the mockup's #cmd), split out of PlayerSelectionController so it can be drawn from plain data
// (CommandModel) — by the game (commands(u)) and by the editor previews. Construction, rows, hover visuals are the controller's
// former BuildMenu / RenderMenu / BuildMenuRow / ApplyRowVisual / UpdateMenuHover bodies, unchanged; where the menu sits
// (positionMenu, which follows the unit on screen) stays with the controller.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    public struct CommandModel
    {
        public string id, kr, en, ic, sub;
        public bool ok;
    }

    public class CommandMenuView : MonoBehaviour
    {
        static readonly Color Paper = UIPalette.Paper, Ink = UIPalette.Ink, Blu = UIPalette.Blue, Dimp = UIPalette.Dim;
        public const float MenuWidth = 250f, MenuHeaderH = 21f;

        public RectTransform Root => (RectTransform)transform;
        public bool Visible => gameObject.activeSelf;
        public int RowCount => rows.Count;

        UIStaggerIn menuStagger;

        class MenuRow
        {
            public RectTransform face;
            public Image bg, subBg;
            public TMP_Text ic, t, sub;
            public string kr, en;
            public bool ok;
            public float hover; // 0..1, .12s transition
        }
        readonly List<MenuRow> rows = new List<MenuRow>();

        /// <summary>Instantiates HUD_CmdMenu under `parent` (hidden), its placeholder buttons removed.</summary>
        public static CommandMenuView Build(Transform parent, GameObject prefab)
        {
            var go = Instantiate(prefab, parent, false);
            go.SetActive(false);
            go.name = "CmdMenu";
            var v = go.AddComponent<CommandMenuView>();
            var menuRoot = v.Root;
            // Placeholder buttons (移動/射撃/能力使用/行動終了) are replaced by commands(u) on every render.
            HudKit.ClearChildren(menuRoot);

            // position:fixed with left/top from positionMenu() → anchor top-left of the screen.
            menuRoot.anchorMin = menuRoot.anchorMax = new Vector2(0, 1);
            menuRoot.pivot = new Vector2(0, 1);
            // #cmd{width:250px;gap:3px}: the column only stacks; each row is UITheme.hud.commandButton (226×40)
            UILayout.Column(menuRoot, UITheme.Current.hud.commandGap);

            v.menuStagger = UIUtil.GetOrAdd<UIStaggerIn>(go);
            // Rows are layout-driven slots with one inner "Body" each, so the stagger slides the Body
            // (UIStaggerIn's documented LayoutGroup setup) and the layout never fights the animation.
            v.menuStagger.animateInner = true;
            v.menuStagger.fromOffset = new Vector2(-18f, 0f);  // @keyframes cmdIn translateX(-18px)
            v.menuStagger.duration = .3f;
            v.menuStagger.step = .035f;                         // animation-delay: i*35ms
            return v;
        }

        /// <summary>Port of renderMenu(u)'s drawing: the header and one row per command; `fresh` plays the cmdIn stagger.</summary>
        public void Render(string header, IList<CommandModel> list, bool fresh, System.Action<string> onClick)
        {
            var menuRoot = Root;
            // Old rows are about to go; stop any stagger still sliding them in.
            menuStagger.StopAllCoroutines();
            HudKit.ClearChildren(menuRoot);
            rows.Clear();

            // Activate before building: TMP components added under an inactive parent skip Awake
            // and can't measure themselves (GetPreferredValues NREs). The empty menu's OnEnable
            // Play() is a no-op; the real stagger is started below once the rows exist.
            if (!menuRoot.gameObject.activeSelf) menuRoot.gameObject.SetActive(true);

            BuildMenuHeader(header);
            for (int i = 0; i < list.Count; i++) BuildMenuRow(list[i], i, onClick);

            if (fresh) menuStagger.Play();
            // not fresh: `animation:none` — rows appear in place (new rows start at alpha 1, base pos)

            var H = UITheme.Current.hud;
            menuRoot.sizeDelta = new Vector2(MenuWidth, MenuHeaderH + list.Count * (H.commandButton.y + H.commandGap));
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuRoot);
        }

        void BuildMenuHeader(string text)
        {
            // #cmdMenu .ch{font:700 10px;color:#fff;padding:3px 9px;background:var(--ink);
            //              align-self:flex-start;border-left:4px solid var(--blu)}
            var slot = HudKit.NewRect("Ch", Root);
            slot.pivot = new Vector2(.5f, .5f);
            var body = HudKit.NewRect("Body", slot);
            HudKit.Stretch(body);
            HudKit.AddImage(body, Ink, raycast: true);   // part of #cmdMenu, not the battlefield
            var border = HudKit.NewRect("Border", body);
            border.anchorMin = new Vector2(0, 0); border.anchorMax = new Vector2(0, 1); border.pivot = new Vector2(0, .5f);
            border.anchoredPosition = Vector2.zero; border.sizeDelta = new Vector2(4, 0);
            HudKit.AddImage(border, Blu);
            // #cmd .h{font:700 12px/1.25;padding:3px 12px;border-left:4px}
            var t = HudKit.AddText(body, text, 12, Color.white, true, TextAlignmentOptions.MidlineLeft);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(16, 0); t.rectTransform.offsetMax = new Vector2(-12, 0);
            var pref = t.GetPreferredValues(text);
            UILayout.Fixed(slot, Mathf.Ceil(pref.x) + 16 + 12, MenuHeaderH);
        }

        void BuildMenuRow(CommandModel c, int index, System.Action<string> onClick)
        {
            // Slot (layout) → Body (stagger target) → Face (hover translateX / background / button)
            // #cmd button{width:226px;height:40px} .k{width:34px;15px} .t{padding-left:12px;15px} small{11px} .s{margin-right:16px;12px;padding:1px 7px}
            var H = UITheme.Current.hud;
            float rw = H.commandButton.x, rh = H.commandButton.y;
            var slot = HudKit.NewRect("Cmd_" + c.id, Root);
            slot.pivot = new Vector2(.5f, .5f);
            UILayout.Fixed(slot, rw, rh);
            var body = HudKit.NewRect("Body", slot);
            HudKit.Stretch(body);
            var face = HudKit.NewRect("Face", body);
            HudKit.Stretch(face);
            var bg = HudKit.AddImage(face, Paper, raycast: true);
            var sh = face.gameObject.AddComponent<Shadow>();          // box-shadow:0 3px 10px rgba(0,0,0,.35)
            sh.effectColor = new Color(0, 0, 0, .35f); sh.effectDistance = new Vector2(0, -3);
            var btn = face.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.interactable = c.ok;
            string cid = c.id;
            btn.onClick.AddListener(() => onClick?.Invoke(cid));
            // .k — number key
            const float KeyW = 34f, IcW = 16f;
            var k = HudKit.NewRect("K", face);
            HudKit.AddImage(k, Ink);
            UILayout.Place(k, 0, 0, KeyW, rh);
            var kt = HudKit.AddText(k, (index + 1).ToString(), 15, Color.white, true, TextAlignmentOptions.Center);
            HudKit.Stretch(kt.rectTransform);
            kt.rectTransform.offsetMin = new Vector2(5f, 0f);          // padding-left:5px

            // .ic
            var ic = HudKit.AddText(face, c.ic, 14, Blu, false, TextAlignmentOptions.Center, "Ic");
            HudKit.UseSymbolFont(ic);
            UILayout.Place(ic.rectTransform, KeyW + 8, 0, IcW, rh);

            // .sub (flush right, 16px in)
            var subWrap = HudKit.NewRect("Sub", face);
            var subBg = HudKit.AddImage(subWrap, new Color(0, 0, 0, .08f));
            var sub = HudKit.AddText(subWrap, c.sub ?? "", 12, Ink, true, TextAlignmentOptions.Center);
            HudKit.UseSymbolFont(sub); // ∞
            float subW = string.IsNullOrEmpty(c.sub) ? 0f : Mathf.Ceil(sub.GetPreferredValues(sub.text).x) + 14f;
            UILayout.Place(subWrap, rw - 16f - subW, (rh - 17f) / 2f, subW, 17f);
            HudKit.Stretch(sub.rectTransform);
            subWrap.gameObject.SetActive(!string.IsNullOrEmpty(c.sub));

            // .t + .en
            float tx = KeyW + 8 + IcW + 6;
            var t = HudKit.AddText(face, "", 15, Ink, true, TextAlignmentOptions.MidlineLeft, "T", role: UITheme.TextRole.Title);
            UILayout.LineHeight(t, 1f);
            UILayout.Place(t.rectTransform, tx, 0, rw - tx - 16f - subW - (subW > 0 ? 6f : 0f), rh);

            var row = new MenuRow { face = face, bg = bg, subBg = subBg, ic = ic, t = t, sub = sub, kr = c.kr, en = c.en, ok = c.ok };
            rows.Add(row);
            ApplyRowVisual(row);

            // .cmd:disabled{opacity:.42;filter:grayscale(1)}
            if (!c.ok)
            {
                var cg = face.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = .42f;
                ic.color = HudKit.Grayscale(Blu);
            }
        }

        // .cmd:hover:not(:disabled){background:var(--blu);color:#fff;transform:translateX(6px)} (.12s)
        void ApplyRowVisual(MenuRow r)
        {
            float h = r.ok ? r.hover : 0f;
            r.bg.color = Color.Lerp(Paper, Blu, h);
            Color txt = Color.Lerp(Ink, Color.white, h);
            Color en = Color.Lerp(Dimp, Color.white, h);
            r.t.text = $"{r.kr}\n<size=11><color=#{ColorUtility.ToHtmlStringRGB(en)}>{r.en}</color></size>";
            r.t.color = txt;
            if (r.ok) r.ic.color = Color.Lerp(Blu, Color.white, h);
            r.sub.color = txt;
            r.subBg.color = Color.Lerp(new Color(0, 0, 0, .08f), new Color(1, 1, 1, .25f), h);
            r.face.anchoredPosition = new Vector2(6f * UIEase.Tactical(h), 0f);
        }

        /// <summary>Moves each enabled row's hover toward the pointer state (`pos` in screen pixels) at the .12s transition rate.</summary>
        public void UpdateHover(Vector2 pos, float dt)
        {
            if (!Visible || rows.Count == 0) return;
            float stepAmt = dt / .12f;
            foreach (var r in rows)
            {
                bool over = r.ok && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)r.face.parent, pos, null);
                float target = over ? 1f : 0f;
                if (Mathf.Approximately(r.hover, target)) continue;
                r.hover = Mathf.MoveTowards(r.hover, target, stepAmt);
                ApplyRowVisual(r);
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            rows.Clear();
        }
    }
}
