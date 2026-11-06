// The HQ's minimap, top right (menu canvas): the whole floor plan, north up, drawn once from the scene's walls —
//   Map      a 300 × 300 ink plate (.82), 24 from the top and the right; a "MAP" tag 10 display (dim) in its corner
//   Walls    every wall's footprint (XZ) as a paper2 bar; area names (HubArea) 10 body, dim, along each room's bottom edge
//   Icons    a 22 dot per station NPC in its station colour with its glyph (BSTactical: ▣ 作戦室, ⌖ 訓練所, ✚ 医務室, ⬢ 装備購入,
//            ✦ IGNIS) — display text only (the room's name below says whose it is)
//   Player   a blue ➜ (BSTactical) turned with the player's heading
// Built once (allocations then); per frame only the player marker moves (anchoredPosition + rotation). The scale fits the walls'
// bounds into the plate with a 12 margin.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Blindside.Hub;

namespace Blindside.UI
{
    public class HubMinimap : MonoBehaviour
    {
        public const float Size = 300f, Margin = 12f, Top = 24f, Right = 24f, IconSize = 22f;

        RectTransform plate, marker;
        Vector2 min; float scale = 1f;
        Transform player;

        public static HubMinimap Build(Transform parent, IList<Bounds> walls, IList<HubNpc> npcs, IList<HubArea> areas, Transform player)
        {
            var theme = UITheme.Current;
            var rt = HudKit.NewRect("Minimap", parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-Right, -Top);
            rt.sizeDelta = new Vector2(Size, Size);
            var m = rt.gameObject.AddComponent<HubMinimap>();
            m.plate = rt;
            m.player = player;
            HudKit.AddImage(rt, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .82f));
            rt.gameObject.AddComponent<RectMask2D>();
            var tag = HudKit.AddText(rt, "MAP", 10f, theme.dim, true, TextAlignmentOptions.TopLeft, "Tag", UITheme.TextRole.Display);
            tag.characterSpacing = 8f;
            UILayout.Place(tag.rectTransform, 8f, 6f, 60f, 14f);

            // fit the floor plan
            bool any = false; Bounds all = default;
            foreach (var b in walls) { if (!any) { all = b; any = true; } else all.Encapsulate(b); }
            if (!any) all = new Bounds(Vector3.zero, new Vector3(30f, 1f, 30f));
            m.min = new Vector2(all.min.x, all.min.z);
            float span = Mathf.Max(all.size.x, all.size.z, 1f);
            m.scale = (Size - Margin * 2f) / span;
            // centre the shorter side
            var pad = new Vector2((span - all.size.x) * .5f, (span - all.size.z) * .5f);
            m.min -= pad;

            var wallsRoot = HudKit.NewRect("Walls", rt);
            HudKit.Stretch(wallsRoot);
            var wallColor = Color.Lerp(theme.paper2, theme.ink, .15f);
            foreach (var b in walls)
            {
                var w = HudKit.NewRect("Wall", wallsRoot);
                w.anchorMin = w.anchorMax = Vector2.zero; w.pivot = new Vector2(.5f, .5f);
                w.anchoredPosition = m.ToMap(b.center);
                w.sizeDelta = new Vector2(Mathf.Max(2f, b.size.x * m.scale), Mathf.Max(2f, b.size.z * m.scale));
                HudKit.AddImage(w, wallColor);
            }
            if (areas != null)
                foreach (var a in areas)
                {
                    if (a == null || string.IsNullOrEmpty(a.displayName)) continue;
                    var t = HudKit.AddText(rt, a.displayName, 10f, Color.Lerp(theme.dim, Color.white, .25f), false, TextAlignmentOptions.Center, "Area", UITheme.TextRole.Body);
                    t.textWrappingMode = TextWrappingModes.NoWrap;
                    var tr = t.rectTransform;
                    tr.anchorMin = tr.anchorMax = Vector2.zero; tr.pivot = new Vector2(.5f, .5f);
                    // along the room's bottom edge (the station icons stand inside, higher up)
                    tr.anchoredPosition = m.ToMap(a.transform.position) - new Vector2(0f, a.size.y * m.scale * .5f - 10f);
                    tr.sizeDelta = new Vector2(Mathf.Ceil(t.GetPreferredValues(a.displayName).x) + 4f, 14f);
                }
            foreach (var n in npcs)
            {
                if (n == null) continue;
                StationLook(n.station, n.operatorKey, out var glyph, out var col);
                var icon = HudKit.NewRect("Icon_" + n.name, rt);
                icon.anchorMin = icon.anchorMax = Vector2.zero; icon.pivot = new Vector2(.5f, .5f);
                icon.anchoredPosition = m.ToMap(n.transform.position);
                icon.sizeDelta = new Vector2(IconSize, IconSize);
                HudKit.AddImage(icon, col);
                icon.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.Custom, SlantedGraphic.Direction.Right, 4f);
                var g = HudKit.AddText(icon, glyph, 13f, theme.ink, true, TextAlignmentOptions.Center, "Glyph", UITheme.TextRole.Display);
                HudKit.UseSymbolFont(g);
                HudKit.Stretch(g.rectTransform);
            }

            m.marker = HudKit.NewRect("Player", rt);
            m.marker.anchorMin = m.marker.anchorMax = Vector2.zero; m.marker.pivot = new Vector2(.5f, .5f);
            m.marker.sizeDelta = new Vector2(26f, 26f);
            var arrow = HudKit.AddText(m.marker, "➜", 16f, theme.blue, true, TextAlignmentOptions.Center, "Arrow", UITheme.TextRole.Display);
            HudKit.UseSymbolFont(arrow);
            HudKit.Stretch(arrow.rectTransform);
            m.Tick();
            return m;
        }

        /// <summary>A station's glyph and colour on the map.</summary>
        public static void StationLook(HubStation st, string op, out string glyph, out Color col)
        {
            var theme = UITheme.Current;
            switch (st)
            {
                case HubStation.Operations: glyph = "▣"; col = theme.blue; break;
                case HubStation.Training: glyph = "⌖"; col = theme.foe; break;
                case HubStation.Medical: glyph = "✚"; col = new Color(.4f, .85f, .55f); break;
                case HubStation.Shop: glyph = "⬢"; col = theme.acc; break;
                case HubStation.Management: case HubStation.Loadout: case HubStation.Growth: glyph = "✦"; col = new Color(.95f, .5f, .2f); break;
                default: glyph = "◎"; col = theme.paper2; break;
            }
        }

        Vector2 ToMap(Vector3 w) => new Vector2((w.x - min.x) * scale + Margin, (w.z - min.y) * scale + Margin);

        void LateUpdate() => Tick();

        void Tick()
        {
            if (player == null || marker == null) return;
            var p = ToMap(player.position);
            if (marker.anchoredPosition != p) marker.anchoredPosition = p;
            // ➜ points right (+x): heading 0 (north, +z) is up → rotate by 90 − yaw
            float z = 90f - player.eulerAngles.y;
            var r = Quaternion.Euler(0f, 0f, z);
            if (marker.localRotation != r) marker.localRotation = r;
        }
    }
}
