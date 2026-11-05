// #roster (HUD_Roster.prefab). CSS, 00_markup_and_styles.html:79-80:
//   #roster{position:fixed;left:14px;top:100px;display:flex;flex-direction:column;gap:5px;z-index:15;width:216px}
//   .rcard{animation:slideInL .45s cubic-bezier(.2,.9,.2,1) both}  buildRoster(): d.style.animationDelay=(i*60)+'ms'
//   @keyframes slideInL{from{transform:translateX(-40px);opacity:0}to{transform:none;opacity:1}}
// Cards are stacked by hand; the staggered slideInL is UIStaggerIn, added at runtime (never saved into
// the prefab — see GameplayUIBridge's header on the Missing-Script serialization problem with the
// classes that used to live in BlindsideUIAnim.cs).
using System.Collections.Generic;
using UnityEngine;

namespace Blindside.UI
{
    public class HudRoster : MonoBehaviour
    {
        public RectTransform cards;          // container the cards are stacked in
        public HudRosterCard template;       // inactive
        public float gap = 5f;

        readonly List<HudRosterCard> built = new List<HudRosterCard>();
        UIStaggerIn stagger;

        public IReadOnlyList<HudRosterCard> Cards => built;

        /// <summary>buildRoster(): `el.innerHTML=''` then one card per player unit, each sliding in 60ms after the last.</summary>
        public IReadOnlyList<HudRosterCard> Build(int count)
        {
            // Deactivate first: Destroy is deferred to end of frame and UIStaggerIn walks active children.
            foreach (var c in built) if (c != null) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
            built.Clear();

            // #roster{left:20px;top:76px;gap:5px} — UITheme.hud.rosterOrigin / rosterGap / rosterCard
            var H = UITheme.Current.hud;
            gap = H.rosterGap;
            float h = H.rosterCard.y;
            UILayout.Place((RectTransform)transform, H.rosterOrigin.x, H.rosterOrigin.y, H.rosterCard.x + 40f, count * h + Mathf.Max(0, count - 1) * gap);
            for (int i = 0; i < count; i++)
            {
                var card = Instantiate(template, cards);
                card.name = "Card_" + i;
                card.ApplyLayout();
                var rt = (RectTransform)card.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(0f, -i * (h + gap));
                card.gameObject.SetActive(true);
                built.Add(card);
            }

            if (stagger == null)
            {
                stagger = cards.gameObject.AddComponent<UIStaggerIn>();
                stagger.fromOffset = new Vector2(-40f, 0f);
                stagger.duration = .45f;
                stagger.step = .06f;
                stagger.animateInner = true;   // slide Card/Inner; Card itself keeps its stacked slot
            }
            stagger.Play();
            return built;
        }
    }
}
