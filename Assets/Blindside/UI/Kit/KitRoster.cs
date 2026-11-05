// The HUD's operator list (HUD_Roster.prefab, ui_spec "대원 카드"): SelectableCards stacked from UITheme.hud.rosterOrigin (x20 y76) with
// rosterGap (5) between them. Like the cards it knows no game types — the HUD fills each card through SelectableCard.SetData /
// SetState and listens to its onClick. Build(count) replaces the cards and, in Play, runs the entrance (.5s, index × 60ms).
// The root is 40 units wider than a card so a selected card (+18) stays inside it.
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Roster")]
    public class KitRoster : MonoBehaviour
    {
        [Tooltip("Container the cards are stacked in (this object when empty).")]
        public RectTransform cards;

        readonly List<SelectableCard> built = new List<SelectableCard>();

        public IReadOnlyList<SelectableCard> Cards => built;

        /// <summary>Removes the old cards and makes `count` new empty ones (fill them with SetData), top to bottom.</summary>
        public IReadOnlyList<SelectableCard> Build(int count)
        {
            foreach (var c in built) if (c != null) { c.gameObject.SetActive(false); HudKit.DestroySafe(c.gameObject); }
            built.Clear();

            var H = UITheme.Current.hud;
            float w = H.rosterCard.x, h = H.rosterCard.y, gap = H.rosterGap;
            UILayout.Place((RectTransform)transform, H.rosterOrigin.x, H.rosterOrigin.y, w + 40f, count * h + Mathf.Max(0, count - 1) * gap);
            var parent = cards != null ? cards : (RectTransform)transform;
            for (int i = 0; i < count; i++)
            {
                var card = SelectableCard.Create(parent, "Card_" + i);
                UILayout.Place((RectTransform)card.transform, 0f, i * (h + gap), w, h);
                built.Add(card);
            }
            return built;
        }

        /// <summary>The list's entrance: every card slides in from −46 and fades in over .5s, index × 60ms apart.</summary>
        public void PlayEnter()
        {
            for (int i = 0; i < built.Count; i++) if (built[i] != null) built[i].PlayEnter(i);
        }
    }
}
