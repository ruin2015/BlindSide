// Lint v2 — the design-size table (editor only). Matches by COMPONENT TYPE, not by object name: for each kit component the table lists the
// rects it owns and the size they should have, read from UITheme (hud / title sizes; ui_spec.md) at lint time. UILint (v2 part) compares
// the real rect sizes with it and reports a "size" issue at 5% or more. A component type that is not in the table is listed under
// "unmatched" at the end of the report (listing only).
//   SelectableCard 250×62 · KitCommandMenu button (TacticalButton under it) 226×40 · KitUnitPanel 430×190 · KitArtCard 300×500
//   KitEndButton 300×56 · KitTopbar logo 280×46 / phase 290×46 / round 160×46 / objective 480×58 · KitHintBar 400×48 (taller allowed:
//   two lines) · KitLogView line 24 high (a wrapped two-line entry is taller by design) · DialogView 420 wide · KitTitleTop side card
//   343×176, map card 520×74, editor button 166×74, win condition / deploy 343×106 · KitTitleSquad row 696×60 · KitSpotlight 474×330
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class LintSpec
    {
        public struct Target
        {
            public RectTransform rt;
            public Vector2 size;
            public string what;
            public bool heightMin;       // the height may be larger (two-line hint bar), not smaller
            public bool ignoreHeight;    // only the width is specified
        }

        static readonly Dictionary<Type, Action<Component, List<Target>>> table = Build();

        public static bool Has(Type t) => table.ContainsKey(t);

        public static void Collect(Component c, List<Target> into)
        {
            if (table.TryGetValue(c.GetType(), out var f)) f(c, into);
        }

        static void Add(List<Target> l, Transform t, Vector2 size, string what, bool heightMin = false, bool ignoreHeight = false)
        {
            if (t is RectTransform rt && t.gameObject.activeInHierarchy)
                l.Add(new Target { rt = rt, size = size, what = what, heightMin = heightMin, ignoreHeight = ignoreHeight });
        }

        static Dictionary<Type, Action<Component, List<Target>>> Build()
        {
            var d = new Dictionary<Type, Action<Component, List<Target>>>();

            d[typeof(SelectableCard)] = (c, l) => Add(l, c.transform, UITheme.Current.hud.rosterCard, "SelectableCard");
            d[typeof(TacticalButton)] = (c, l) =>
            {
                if (c.GetComponentInParent<KitCommandMenu>() != null) Add(l, c.transform, UITheme.Current.hud.commandButton, "KitCommandMenu button");
            };
            d[typeof(KitUnitPanel)] = (c, l) => Add(l, c.transform, UITheme.Current.hud.unitPanel, "KitUnitPanel");
            d[typeof(KitArtCard)] = (c, l) => Add(l, c.transform, UITheme.Current.hud.artCard, "KitArtCard");
            d[typeof(KitEndButton)] = (c, l) => Add(l, c.transform, UITheme.Current.hud.endTurn, "END TURN button");

            d[typeof(KitTopbar)] = (c, l) =>
            {
                var tb = (KitTopbar)c;
                var h = UITheme.Current.hud;
                Add(l, tb.logo, new Vector2(h.logoWidth, h.topbarHeight), "KitTopbar logo");
                // the phase container is badge + round box (436 in all); the badge itself is the 290×46 block
                if (tb.phase != null) Add(l, tb.phase.Find("Badge"), new Vector2(h.phaseWidth, h.topbarHeight), "KitTopbar phase badge");
                // round and objective: ui_spec.md (160 / 480×58) — UITheme's roundWidth / objective are checked against the spec separately
                if (tb.phase != null) Add(l, tb.phase.Find("RoundBox"), new Vector2(KitTopbar.RoundW, h.topbarHeight), "KitTopbar round (ui_spec 160)");
                Add(l, tb.objective, new Vector2(KitTopbar.ObjW, KitTopbar.ObjH), "KitTopbar objective (ui_spec 480×58)");
            };

            d[typeof(KitHintBar)] = (c, l) => Add(l, c.transform, UITheme.Current.hud.hint, "KitHintBar", heightMin: true);

            d[typeof(KitLogView)] = (c, l) =>
            {
                var h = UITheme.Current.hud;
                foreach (Transform e in c.transform)
                {
                    if (!e.gameObject.activeSelf || e.name != "Entry") continue;
                    var t = e.GetComponentInChildren<TMP_Text>();
                    bool one = t == null || t.textInfo == null || t.textInfo.lineCount <= 1;
                    Add(l, e, new Vector2(h.logWidth, h.logLineHeight), one ? "log line" : "log line (wrapped: width only)", false, !one);
                }
            };

            d[typeof(DialogView)] = (c, l) => Add(l, c.transform.Find("Pop"), new Vector2(UITheme.Current.hud.dialogWidth, 0f), "DialogView box (width)", false, true);

            d[typeof(KitTitleTop)] = (c, l) =>
            {
                var tt = (KitTitleTop)c;
                var T = UITheme.Current.title;
                Vector2[] want = { T.sideCard, T.sideCard, T.mapCard, T.editorButton, T.winCondition, T.startButton };
                string[] names = { "side card A", "side card B", "map card", "editor button", "win condition", "deploy" };
                for (int i = 0; i < want.Length && i < tt.blocks.Length; i++)
                    if (tt.blocks[i] != null) Add(l, tt.blocks[i], want[i], "KitTitleTop " + names[i]);
            };

            d[typeof(KitTitleSquad)] = (c, l) =>
            {
                var ts = (KitTitleSquad)c;
                foreach (var row in ts.rows)
                    if (row != null && row.block != null) Add(l, row.block, UITheme.Current.title.squadRow, "KitTitleSquad row");
            };

            d[typeof(KitSpotlight)] = (c, l) => Add(l, ((KitSpotlight)c).body, UITheme.Current.title.spotlight, "KitSpotlight");

            return d;
        }
    }
}
