// .tb-right / #objective / #teamCount (HUD_Objective.prefab). CSS, 00_markup_and_styles.html:69-76:
//   .tb-right{padding:9px 14px 9px 14px;max-width:360px;box-shadow:var(--shadow);border-right:6px solid var(--ink)} (+ .brk, --hatch/--pp)
//   #objective{font-size:12px;line-height:1.5;color:var(--ink2)}
//   #objective b{display:block;font:700 10px var(--fm);color:#fff;background:var(--ink);padding:2px 6px;margin:0 0 4px;width:max-content}
//   #objective span{color:#a37200!important;font-weight:700}
//   #teamCount{display:flex;gap:16px;margin-top:7px;font:700 10px var(--fm);border-top:1px solid rgba(0,0,0,.15);padding-top:6px}
//   .pips{display:inline-flex;gap:3px;vertical-align:middle;margin-left:6px}
//   .pips i{width:7px;height:12px;display:block;transform:skewX(-18deg);background:currentColor}  .pips i.off{opacity:.18}
// Sized by hand (not by LayoutGroups) so HudTopbar can read Width/Height for its space-between layout.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Blindside.UI
{
    public class HudObjective : MonoBehaviour
    {
        public RectTransform titleBg;       // #objective b background (ink)
        public TMP_Text titleText;
        public TMP_Text bodyText;           // #objective body (rich text; the prep/deploy line is a <color> span)
        public RectTransform teamRule;      // #teamCount border-top
        public TMP_Text allyLabel, enemyLabel;
        public RectTransform allyPips, enemyPips;   // each holds pip Images (7x12 boxes, skewed sprite)

        const float PipW = 7f, PipGap = 3f, PipLeft = 6f, GroupGap = 16f;
        const float OffAlpha = .18f;

        public float Width { get; private set; }
        public float Height { get; private set; }

        RectTransform Rt => (RectTransform)transform;

        /// <summary>body: the #objective text after the title (lines joined with \n, rich text allowed).
        /// ally/enemy: one entry per pip, true = alive.</summary>
        public void Set(string body, IReadOnlyList<bool> ally, IReadOnlyList<bool> enemy)
        {
            bodyText.text = body;
            SetPips(allyPips, ally, UIPalette.Blue);
            SetPips(enemyPips, enemy, UIPalette.Foe);
            Relayout();
        }

        static void SetPips(RectTransform row, IReadOnlyList<bool> alive, Color c)
        {
            // Grow the row from its first pip if a side ever fields more than the prefab's five.
            while (row.childCount < alive.Count) Instantiate(row.GetChild(0).gameObject, row);
            for (int i = 0; i < row.childCount; i++)
            {
                var pip = (RectTransform)row.GetChild(i);
                bool used = i < alive.Count;
                pip.gameObject.SetActive(used);
                if (!used) continue;
                pip.anchoredPosition = new Vector2(i * (PipW + PipGap), 0f);
                var img = pip.GetComponentInChildren<Image>(true);
                c.a = alive[i] ? 1f : OffAlpha;
                img.color = c;
            }
        }

        static float PipsWidth(RectTransform row)
        {
            int n = 0;
            for (int i = 0; i < row.childCount; i++) if (row.GetChild(i).gameObject.activeSelf) n++;
            return n == 0 ? 0f : n * PipW + (n - 1) * PipGap;
        }

        /// <summary>The mockup's #tb .ob box at UITheme.hud.objective (360×58), padding 5px 34px 0 36px: the chip (11px) on the first row with
        /// the ALLY / ENEMY pips flush right beside it, then the body (12px/1.35, two lines).</summary>
        public void Relayout()
        {
            var size = UITheme.Current.hud.objective;
            const float PadL = 36f, PadR = 34f, PadT = 5f;
            float contentW = size.x - PadL - PadR;

            titleText.fontSize = 11f;
            float titleW = Mathf.Ceil(titleText.GetPreferredValues(titleText.text).x);
            Place(titleBg, PadL, -PadT, titleW + 16f, 16f);       // .chip{padding:1px 8px}

            bodyText.fontSize = 12f;
            UILayout.LineHeight(bodyText, 1.35f);
            Place((RectTransform)bodyText.transform, PadL, -(PadT + 16f + 1f), contentW, size.y - PadT - 17f);

            teamRule.gameObject.SetActive(false);                  // no rule in .ob
            const float rowH = 12f;
            allyLabel.fontSize = enemyLabel.fontSize = 11f;
            float allyLw = Mathf.Ceil(allyLabel.GetPreferredValues(allyLabel.text).x);
            float enemyLw = Mathf.Ceil(enemyLabel.GetPreferredValues(enemyLabel.text).x);
            float teamW = allyLw + PipLeft + PipsWidth(allyPips) + GroupGap + enemyLw + PipLeft + PipsWidth(enemyPips);
            float x = size.x - PadR - teamW, y = -(PadT + 2f);
            Place((RectTransform)allyLabel.transform, x, y, allyLw, rowH); x += allyLw + PipLeft;
            Place(allyPips, x, y, PipsWidth(allyPips), rowH); x += PipsWidth(allyPips) + GroupGap;
            Place((RectTransform)enemyLabel.transform, x, y, enemyLw, rowH); x += enemyLw + PipLeft;
            Place(enemyPips, x, y, PipsWidth(enemyPips), rowH);

            Width = size.x;
            Height = size.y;
            Rt.sizeDelta = new Vector2(Width, Height);
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
