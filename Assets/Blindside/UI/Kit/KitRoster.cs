// The HUD's operator list (HUD_Roster.prefab), at the top left of the HUD canvas (x20 y14, drawn at Scale):
//   Header   「隊員情報」: an ink parallelogram (slant 14) with a slanted accent stripe at its left, the title in white (Title font,
//            italic), 300×40
//   Card_N   TacticalRosterCards (300×68; the old SelectableCard is UI/Legacy) packed tight under it, Gap apart
// A selected card's face stands over its top edge (TacticalRosterCard.PopOffset, 0 … PopRoom, on the selection's own tween): the list
// opens that much room above it every frame, so the cards below slide down with it and nothing overlaps; deselected, they close up
// again. Like the cards it knows no game types — the HUD fills each card through TacticalRosterCard.SetData / SetWeapon / SetState and
// listens to its onClick. Build(count) replaces the cards and, in Play, runs the entrance (.5s, index × 60ms). The root is 40 units
// wider than a card so a selected card (+18) stays inside it, and PopRoom taller for the one opened slot. Relayout allocates nothing.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Roster")]
    public class KitRoster : MonoBehaviour
    {
        public const float Gap = 6f, X = 20f, Top = 14f, HeaderH = 40f, HeaderGap = 10f, HeaderStripe = 6f;
        public const string HeaderTitle = "隊員情報";
        /// <summary>The whole list drawn a little smaller than its 300-wide design (scaled about its top-left corner).</summary>
        public const float Scale = .85f;
        static float CardsTop => HeaderH + HeaderGap;

        [Tooltip("Container the cards are stacked in (this object when empty).")]
        public RectTransform cards;

        readonly List<TacticalRosterCard> built = new List<TacticalRosterCard>();
        RectTransform header;

        public IReadOnlyList<TacticalRosterCard> Cards => built;

        /// <summary>Removes the old cards and makes `count` new empty ones (fill them with SetData), top to bottom.</summary>
        public IReadOnlyList<TacticalRosterCard> Build(int count)
        {
            foreach (var c in built) if (c != null) { c.gameObject.SetActive(false); HudKit.DestroySafe(c.gameObject); }
            built.Clear();

            float w = TacticalRosterCard.Width, h = TacticalRosterCard.Height;
            UILayout.Place((RectTransform)transform, X, Top, w + 40f,
                CardsTop + count * h + Mathf.Max(0, count - 1) * Gap + TacticalRosterCard.PopRoom);
            transform.localScale = new Vector3(Scale, Scale, 1f);
            var parent = cards != null ? cards : (RectTransform)transform;
            if (header == null) header = BuildHeader(parent);
            for (int i = 0; i < count; i++)
            {
                var card = TacticalRosterCard.Create(parent, "Card_" + i);
                UILayout.Place((RectTransform)card.transform, 0f, CardsTop + i * (h + Gap), w, h);
                built.Add(card);
            }
            return built;
        }

        /// <summary>「隊員情報」 over the cards.</summary>
        static RectTransform BuildHeader(Transform parent)
        {
            var theme = UITheme.Current;
            const float slant = TacticalRosterCard.Slant, w = TacticalRosterCard.Width;
            var rt = HudKit.NewRect("Header", parent);
            UILayout.Place(rt, 0f, 0f, w, HeaderH);
            var bg = HudKit.NewRect("DecoBg", rt);
            HudKit.Stretch(bg);
            HudKit.AddImage(bg, theme.ink);
            bg.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, slant);
            var st = HudKit.NewRect("DecoStripe", rt);
            UILayout.Place(st, 4f, 0f, HeaderStripe + slant, HeaderH);
            HudKit.AddImage(st, UIPalette.Accent);
            st.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, slant);
            var t = HudKit.AddText(rt, HeaderTitle, 22f, Color.white, true, TextAlignmentOptions.Center, "Title", UITheme.TextRole.Title);
            t.fontStyle |= FontStyles.Italic;                                  // the design's slanted heading (the card faces lean too)
            t.characterSpacing = 8f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(t.rectTransform, slant + HeaderStripe + 8f, 0f, w - slant * 2f - HeaderStripe - 8f, HeaderH);
            return rt;
        }

        /// <summary>The list's entrance: every card slides in from −46 and fades in over .5s, index × 60ms apart.</summary>
        public void PlayEnter()
        {
            for (int i = 0; i < built.Count; i++) if (built[i] != null) built[i].PlayEnter(i);
        }

        /// <summary>Stacks the cards under the header: each one PopOffset below where the tight list would put it (plus the ones above).</summary>
        public void Relayout()
        {
            float y = CardsTop, h = TacticalRosterCard.Height;
            for (int i = 0; i < built.Count; i++)
            {
                var c = built[i];
                if (c == null) continue;
                y += c.PopOffset;
                var rt = (RectTransform)c.transform;
                var p = new Vector2(0f, -y);
                if (rt.anchoredPosition != p) rt.anchoredPosition = p;
                y += h + Gap;
            }
        }

        // after the cards' Update has posed this frame's selection tween
        void LateUpdate() => Relayout();
    }
}
