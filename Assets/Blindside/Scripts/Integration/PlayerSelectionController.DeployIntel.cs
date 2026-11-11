// The deployment's wall intel (the attack side): when the drop opens, every reinforced wall and every wall with current running
// through it is made known to the attack side (MapGrid seenByAtk / elecSeen — what discovery would set; ReinforcedWallViews and
// ElectricFieldView then draw them as such for the rest of the match), and while the deployment lasts they blink on the map
// (amber: reinforced, blue: electrified) with a legend card on the right and a log line.
// Presentation of the briefing: the defense AI never reads these flags (only the attack AI does, and the player is the attack side
// here). Per frame: the shells' colour through one MaterialPropertyBlock, no allocation.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.Data;
using Blindside.Map;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        static readonly Color IntelReinforced = UIPalette.Accent, IntelElectric = new Color32(0x4f, 0xb3, 0xff, 0xff);
        const float IntelBlink = 1.1f, IntelCardX = 20f, IntelCardY = 440f;
        /// <summary>The cap's height over the wall top and its share of the block's footprint (inside it: neighbours never overlap).</summary>
        const float CapH = .07f, CapInset = .86f;

        readonly List<Vector2Int> intelReinf = new List<Vector2Int>(), intelElec = new List<Vector2Int>();
        readonly List<Renderer> intelPool = new List<Renderer>();
        Transform intelRoot;
        RectTransform intelCard;
        TMP_Text intelReinfText, intelElecText;
        MaterialPropertyBlock intelMpb;
        bool intelOn;

        /// <summary>The drop opens: reveal the walls, start the blinking and the legend.</summary>
        void RevealWallIntel()
        {
            if (map == null || side != OperatorTeam.Attack || TutorialGate.Active) return;
            intelReinf.Clear(); intelElec.Clear();
            int nr = 0, ne = 0;
            for (int i = 0; i < map.Panels.Count; i++)
            {
                var p = map.Panels[i];
                bool standing = p.cells.TrueForAll(c => map.TileAt(c.x, c.y) == TileType.Wall);
                if (!standing) continue;
                // one cap per cell: blue where current runs (those are reinforced walls too), amber on the other reinforced ones
                if (p.reinforced) { map.SetPanelSeenByAtk(p.id); nr++; if (!p.electrified) foreach (var c in p.cells) intelReinf.Add(c); }
                if (p.electrified) { map.SetPanelElecSeen(p.id); ne++; foreach (var c in p.cells) intelElec.Add(c); }
            }
            Log($"偵察情報: 強化壁 {nr}か所、電気が流れる壁 {ne}か所を表示", "w");
            // (the WALL INTEL legend card is no longer shown; the caps on the walls still blink. BuildWallIntelCard stays for bringing it back)
            intelOn = true;
        }

        /// <summary>LateUpdate: the blinking shells and the legend, only while the deployment lasts.</summary>
        void SyncWallIntel()
        {
            bool on = intelOn && turns != null && turns.Phase == MatchPhase.Deploy && !turns.Over;
            if (!on)
            {
                if (intelOn && turns != null && turns.Phase != MatchPhase.Deploy) intelOn = false;
                if (intelCard != null && intelCard.gameObject.activeSelf) intelCard.gameObject.SetActive(false);
                for (int i = 0; i < intelPool.Count; i++) if (intelPool[i].gameObject.activeSelf) intelPool[i].gameObject.SetActive(false);
                return;
            }
            if (intelCard != null && !intelCard.gameObject.activeSelf) intelCard.gameObject.SetActive(true);
            float p = .5f + .5f * Mathf.Cos(Mathf.Repeat(Time.unscaledTime, IntelBlink) / IntelBlink * Mathf.PI * 2f);
            int n = 0;
            n = IntelShells(intelReinf, IntelReinforced, .45f + .5f * p, n);
            n = IntelShells(intelElec, IntelElectric, .45f + .5f * p, n);
            for (int i = n; i < intelPool.Count; i++) if (intelPool[i].gameObject.activeSelf) intelPool[i].gameObject.SetActive(false);
        }

        int IntelShells(List<Vector2Int> cells, Color col, float a, int n)
        {
            if (cells.Count == 0) return n;
            EnsureAbilityHelpers();   // (the shell material the ability mode's wall highlight uses)
            if (intelMpb == null) intelMpb = new MaterialPropertyBlock();
            var c = new Color(col.r, col.g, col.b, a);
            intelMpb.SetColor("_BaseColor", c); intelMpb.SetColor("_Color", c);
            for (int i = 0; i < cells.Count; i++)
            {
                if (map.TileAt(cells[i].x, cells[i].y) != TileType.Wall) continue;   // (broken since)
                // a flat glowing cap on the wall's top, inside its footprint (the old full shells overlapped the neighbours and
                // mixed into odd colours where two met)
                var rend = IntelShell(n++);
                var b = WallBoundsAt(cells[i]);
                rend.transform.position = new Vector3(b.center.x, b.max.y + CapH * .5f + .04f, b.center.z);   // (over the reinforced frame's top, .03 above the block)
                rend.transform.localScale = new Vector3(b.size.x * CapInset, CapH, b.size.z * CapInset);
                rend.SetPropertyBlock(intelMpb);
                if (!rend.gameObject.activeSelf) rend.gameObject.SetActive(true);
            }
            return n;
        }

        Renderer IntelShell(int i)
        {
            if (intelRoot == null) { intelRoot = new GameObject("WallIntel").transform; intelRoot.SetParent(transform, false); }
            while (intelPool.Count <= i)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "WallIntelShell";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(intelRoot, false);
                var mr = go.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.sharedMaterial = highlightMat;
                go.SetActive(false);
                intelPool.Add(mr);
            }
            return intelPool[i];
        }

        // the legend: right side, under where the illustration card sits
        void EnsureIntelCard()
        {
            if (intelCard != null || hudRoot == null) return;
            intelCard = BuildWallIntelCard(hudRoot, out intelReinfText, out intelElecText);
            intelCard.gameObject.SetActive(false);
        }

        /// <summary>The legend card (also built by Render Previews).</summary>
        public static RectTransform BuildWallIntelCard(RectTransform parent, out TMP_Text reinf, out TMP_Text elec)
        {
            // left, under the roster (5 cards: y76 … 406) — the right side is the illustration card's
            var r = HudKit.NewRect("WallIntelCard", parent);
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 1f);
            r.sizeDelta = new Vector2(300f, 118f); r.anchoredPosition = new Vector2(IntelCardX, -IntelCardY);
            HudKit.AddImage(r, new Color(UIPalette.Ink.r, UIPalette.Ink.g, UIPalette.Ink.b, .9f));
            var bar = HudKit.NewRect("DecoIntelBar", r);
            bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(0f, 1f); bar.pivot = new Vector2(0f, .5f);
            bar.sizeDelta = new Vector2(4f, 0f); bar.anchoredPosition = Vector2.zero;
            HudKit.AddImage(bar, UIPalette.Accent);
            var tag = HudKit.AddText(r, "WALL INTEL", 12, UIPalette.Accent, true, TextAlignmentOptions.TopLeft, "Tag", UITheme.TextRole.Display);
            Place(tag.rectTransform, 16f, 12f, 140f, 16f);
            var ja = HudKit.AddText(r, "偵察情報 · 配置中は点滅", 12, new Color(.62f, .64f, .68f), false, TextAlignmentOptions.TopRight, "TagJa", UITheme.TextRole.Body);
            Place(ja.rectTransform, 140f, 11f, 146f, 18f);
            reinf = IntelRow(r, 40f, IntelReinforced, "Reinforced");
            elec = IntelRow(r, 76f, IntelElectric, "Electric");
            return r;
        }

        static TMP_Text IntelRow(RectTransform card, float y, Color col, string name)
        {
            var sw = HudKit.NewRect("Swatch" + name, card); Place(sw, 16f, y + 5f, 22f, 22f);
            HudKit.AddImage(sw, col);
            var t = HudKit.AddText(card, "", 16, Color.white, false, TextAlignmentOptions.MidlineLeft, "Row" + name, UITheme.TextRole.Body);
            Place(t.rectTransform, 48f, y, 240f, 32f);
            return t;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }
    }
}
