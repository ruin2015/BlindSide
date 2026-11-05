// ABILITY command for the non-panel kinds (stim = ally, shield = instant, shock = point) for PlayerSelectionController.
// Ports, from blindside_source:
//   05_ui_input_intro.js — doCommand's 'skill' branch (~173-178): instant → G.busy; useAbility(u).then(afterAction);
//                          ally with no sp.valid unit → log('対象の味方がいません','w'); selectUnit(u)
//                          onClick's ability branch (~323-328): point → sp.valid(g) ? [g] : sfx('click');
//                          ally → pickUnit(sx,sy) && sp.valid(a)
//   03_renderer_threejs.js — syncUIHelpers' ally highlight for stim (~239: the sel ring, recoloured 0xb98cff, on
//                          every sp.valid unit) and the point effect preview (~231-232: R.eff disc .18 + R.effRing
//                          at the hover, radius sp.radius, 0xffd21f when sp.valid else 0xff3b54);
//                          makeFigure's shield slab (~130: 0xa8d4ff opacity .7, .07×.78×.62 at (.38,.42,0) in
//                          the facing group) shown while u.shield (~139).
// Differences on purpose: the point kind highlights every in-range empty cell (no unit/hostage on it) whose centre
// passes sp.valid (the source only previews the hovered point and allows occupied spots) and a click places the
// field on that cell's centre. The overhead status chips
// (スティム / シールド展開 / 電波妨害) are this port's addition on top of showUnitPanel's .up-tags, so a stim on another ally and an
// enemy's raised shield are readable without opening the panel.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Blindside.Abilities;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        static readonly Color StimPurple = new Color32(0xb9, 0x8c, 0xff, 0xff);    // 0xb98cff
        static readonly Color InvalidRed = new Color32(0xff, 0x3b, 0x54, 0xff);    // 0xff3b54
        static readonly Color ShieldSlab = new Color32(0xa8, 0xd4, 0xff, 0xff);    // 0xa8d4ff

        readonly List<SimUnit> validAllies = new List<SimUnit>();
        readonly HashSet<int> validCells = new HashSet<int>();                     // y*Width+x
        bool allyRingsSuppressedSel;

        // Every non-breach ability: stim (ally) · shield/ghost (instant) · shock/gas/cam (point) · mirror (panel, .Ability.cs).
        static bool IsSupportAbility(string key) =>
            key == "stim" || key == "shield" || key == "shock" || key == "gas" || key == "ghost" || key == "mirror" || key == "cam";

        /// <summary>doCommand's 'skill' branch for kinds other than 'panel' (the caller already set mode/spec).</summary>
        bool StartSupportAbility(SimUnit u, AbilitySpec sp)
        {
            validAllies.Clear(); validCells.Clear();
            if (sp.Kind == AbilityTargetKind.Instant)
            {
                busy = true; HideHint();
                mode = Mode.Busy;
                StartCoroutine(AbilityRoutine(u, AbilityTarget.Instant));
                return true;
            }
            if (sp.Kind == AbilityTargetKind.Ally)
            {
                foreach (var a in units) if (sp.ValidAlly(a)) validAllies.Add(a);
                if (validAllies.Count == 0) { Log("対象の味方がいません", "w"); SelectUnit(u); return true; }
            }
            else if (sp.Kind == AbilityTargetKind.Point)
            {
                int r = Mathf.CeilToInt(sp.Range);
                bool needEmpty = u.AbilityKey != "gas";   // shock / cam: empty cells only; gas may land on the enemy
                for (int cy = Mathf.FloorToInt(u.Y) - r; cy <= Mathf.CeilToInt(u.Y) + r; cy++)
                    for (int cx = Mathf.FloorToInt(u.X) - r; cx <= Mathf.CeilToInt(u.X) + r; cx++)
                        if (cx >= 0 && cy >= 0 && cx < MapGrid.Width && cy < map.Height && sp.ValidPoint(cx, cy) && !(needEmpty && CellOccupied(cx, cy)))
                            validCells.Add(cy * MapGrid.Width + cx);
                if (validCells.Count == 0) { Log("設置可能な区域がありません", "w"); SelectUnit(u); return true; }
                pointCellsKey++;
            }
            SetHint(sp.Hint, sp.Sub, ("cancel", "ESC キャンセル", false));
            return true;
        }

        /// <summary>occupiedCell(x,y) without a `self`: a living unit or the hostage within .7 of the cell centre —
        /// the point kind only offers empty cells.</summary>
        bool CellOccupied(float x, float y)
        {
            foreach (var o in units)
                if (o.Alive && Mathf.Sqrt((o.X - x) * (o.X - x) + (o.Y - y) * (o.Y - y)) < .7f) return true;
            return hostage != null && hostage.Alive && Mathf.Sqrt((hostage.X - x) * (hostage.X - x) + (hostage.Y - y) * (hostage.Y - y)) < .7f;
        }

        /// <summary>onClick's ability branch for 'ally' / 'point'.</summary>
        bool OnClickSupportAbility(SimUnit u, Vector2 screenPos)
        {
            AbilityTarget target;
            if (abilitySpec.Kind == AbilityTargetKind.Ally)
            {
                var a = PickUnit(screenPos);
                if (a == null || !validAllies.Contains(a)) { SfxNoise.Click(); return true; }
                target = AbilityTarget.FromAlly(a);
            }
            else if (abilitySpec.Kind == AbilityTargetKind.Point)
            {
                if (!BattlefieldCamera.GroundAt(screenPos, out var g) || !validCells.Contains(g.y * MapGrid.Width + g.x)) { SfxNoise.Click(); return true; }
                target = AbilityTarget.FromPoint(g.x, g.y);
            }
            else return false;
            busy = true; HideHint();
            mode = Mode.Busy;
            StartCoroutine(AbilityRoutine(u, target));
            return true;
        }

        // ================================================================
        //  syncUIHelpers(): ally rings (stim) · point cells + effect preview (shock)
        // ================================================================
        Transform supportRoot;
        readonly List<GameObject> allyRingPool = new List<GameObject>();
        GameObject pointCellsGo, effDiscGo, effRingGo;
        Mesh pointCellsMesh;
        int pointCellsKey, builtPointCellsKey = -1;
        Material effDiscMat, effRingMat, pointCellsMat;

        void SyncSupportAbilityHelpers()
        {
            bool abilityMode = mode == Mode.Ability && uiUnit != null;
            bool allyOn = abilityMode && abilitySpec.Kind == AbilityTargetKind.Ally && validAllies.Count > 0;
            bool pointOn = abilityMode && abilitySpec.Kind == AbilityTargetKind.Point && validCells.Count > 0;
            if (!allyOn && !pointOn && supportRoot == null) return;
            EnsureSupportHelpers();

            // ---- ally rings: RingGeometry(.52,.6) at y .05, 0xb98cff; the selected unit's own ring pulses ----
            int n = 0;
            if (allyOn)
                foreach (var a in validAllies)
                {
                    if (!a.Alive || a.View == null) continue;
                    var ring = AllyRingAt(n++);
                    ring.transform.position = GridSpace.GridToWorld(a.RX, a.RY, .05f);
                    float s = a == uiUnit ? 1f + .07f * Mathf.Sin(Time.unscaledTime * 1000f / 160f) : 1f;
                    ring.transform.localScale = new Vector3(s, s, s);
                    ring.SetActive(true);
                }
            for (int i = n; i < allyRingPool.Count; i++) allyRingPool[i].SetActive(false);
            // m.sel.material.color → purple: the yellow selection ring gives way to the purple one while targeting.
            if (allyOn && uiUnit.View != null) { uiUnit.View.SetSelected(false); allyRingsSuppressedSel = true; }
            else if (allyRingsSuppressedSel) { allyRingsSuppressedSel = false; SyncRings(); }

            // ---- point: valid cells (built once per StartSupportAbility) + the hover preview ----
            if (pointOn && builtPointCellsKey != pointCellsKey) { builtPointCellsKey = pointCellsKey; BuildPointCellsMesh(); }
            pointCellsGo.SetActive(pointOn);
            bool hoverOn = false;
            if (pointOn)
            {
                bool blinkHi = Mathf.Sin(Time.unscaledTime * 1000f / 200f) > 0f;
                SetMatColor(pointCellsMat, new Color(HighlightYellow.r, HighlightYellow.g, HighlightYellow.b, blinkHi ? .26f : .16f));
                var mouse = Mouse.current;
                if (mouse != null && BattlefieldCamera.GroundAt(mouse.position.ReadValue(), out var h))
                {
                    hoverOn = true;
                    bool ok = validCells.Contains(h.y * MapGrid.Width + h.x);
                    var col = ok ? HighlightYellow : InvalidRed;
                    float r = abilitySpec.Radius;
                    effDiscGo.transform.position = GridSpace.GridToWorld(h.x, h.y, .05f);
                    effRingGo.transform.position = GridSpace.GridToWorld(h.x, h.y, .055f);
                    effDiscGo.transform.localScale = effRingGo.transform.localScale = new Vector3(r, r, r);
                    SetMatColor(effDiscMat, new Color(col.r, col.g, col.b, .18f));
                    SetMatColor(effRingMat, col);
                }
            }
            effDiscGo.SetActive(hoverOn);
            effRingGo.SetActive(hoverOn);
        }

        static void SetMatColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        void EnsureSupportHelpers()
        {
            if (supportRoot != null) return;
            supportRoot = new GameObject("SupportAbilityHelpers").transform;
            supportRoot.SetParent(transform, false);
            pointCellsMesh = new Mesh { name = "AbilityPointCells" };
            pointCellsGo = MakeMeshObject("PointCells", supportRoot, pointCellsMesh, HighlightYellow, 3003);
            pointCellsMat = pointCellsGo.GetComponent<MeshRenderer>().sharedMaterial;
            effDiscGo = MakeMeshObject("AbilityEff", supportRoot, ElectricFieldView.BuildDisc(64), HighlightYellow, 3004);
            effDiscMat = effDiscGo.GetComponent<MeshRenderer>().sharedMaterial;
            effRingGo = MakeMeshObject("AbilityEffRing", supportRoot, ElectricFieldView.BuildRing(.975f, 1f, 128), HighlightYellow, 3006);
            effRingMat = effRingGo.GetComponent<MeshRenderer>().sharedMaterial;
            pointCellsGo.SetActive(false); effDiscGo.SetActive(false); effRingGo.SetActive(false);
        }

        GameObject AllyRingAt(int i)
        {
            while (allyRingPool.Count <= i)
            {
                var go = MakeMeshObject("AllyRing", supportRoot, BuildRingMesh(.52f, .6f, 40), StimPurple, 3006);
                go.SetActive(false);
                allyRingPool.Add(go);
            }
            return allyRingPool[i];
        }

        void BuildPointCellsMesh()
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            foreach (int k in validCells)
            {
                int x = k % MapGrid.Width, y = k / MapGrid.Width, v = verts.Count;
                const float e = .46f;   // a hair inside the cell so neighbouring cells read as a grid
                verts.Add(GridSpace.GridToWorld(x - e, y - e, .04f));
                verts.Add(GridSpace.GridToWorld(x + e, y - e, .04f));
                verts.Add(GridSpace.GridToWorld(x + e, y + e, .04f));
                verts.Add(GridSpace.GridToWorld(x - e, y + e, .04f));
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
            }
            pointCellsMesh.Clear();
            pointCellsMesh.SetVertices(verts); pointCellsMesh.SetTriangles(tris, 0); pointCellsMesh.RecalculateBounds();
        }

        // ================================================================
        //  Status: the shield slab on the figure + overhead スティム / シールド展開 chips
        // ================================================================
        class StatusTag { public RectTransform root; public GameObject stim, shield, ghost; }
        readonly Dictionary<SimUnit, StatusTag> statusTags = new Dictionary<SimUnit, StatusTag>();
        readonly Dictionary<SimUnit, (GameObject go, float disp)> shieldSlabs = new Dictionary<SimUnit, (GameObject, float)>();
        Material shieldMat;

        float StatusTagLift(SimUnit u) =>
            statusTags.TryGetValue(u, out var t) && t != null && t.root.gameObject.activeSelf ? t.root.rect.height + 2f : 0f;

        void SyncAbilityStatus()
        {
            if (endedTagRoot == null) return;
            var cam = Cam;
            float sf = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
            if (units != null)
                foreach (var u in units)
                {
                    bool shown = u.Alive && u.View != null && Shown(u);
                    SyncShieldSlab(u, shown && u.Shield);

                    bool show = cam != null && shown && (u.Stim || u.Shield || u.Ghost);
                    statusTags.TryGetValue(u, out var tag);
                    Vector3 s = show ? cam.WorldToScreenPoint(u.View.LabelPoint) : default;
                    if (!show || s.z < 0f) { if (tag != null) tag.root.gameObject.SetActive(false); continue; }
                    if (tag == null) { tag = BuildStatusTag(); statusTags[u] = tag; }
                    if (tag.stim.activeSelf != u.Stim || tag.shield.activeSelf != u.Shield || tag.ghost.activeSelf != u.Ghost || !tag.root.gameObject.activeSelf)
                    {
                        tag.stim.SetActive(u.Stim); tag.shield.SetActive(u.Shield); tag.ghost.SetActive(u.Ghost);
                        tag.root.gameObject.SetActive(true);
                        LayoutRebuilder.ForceRebuildLayoutImmediate(tag.root);
                    }
                    tag.root.anchoredPosition = new Vector2(s.x / sf, s.y / sf + 4f + LockTagLift(u) + EndedTagLift(u));
                }
            foreach (var kv in statusTags)
                if (units == null || !ContainsUnit(kv.Key)) kv.Value.root.gameObject.SetActive(false);
            foreach (var kv in shieldSlabs)
                if ((units == null || !ContainsUnit(kv.Key)) && kv.Value.go != null) kv.Value.go.SetActive(false);
        }

        StatusTag BuildStatusTag()
        {
            var rt = NewRect("StatusTag", endedTagRoot);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(.5f, 0f);
            HLG(rt, 2, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            GameObject Chip(string text, Color bg, Color fg)
            {
                var c = NewRect("Chip", rt);
                AddImage(c, bg);
                HLG(c, 0, new RectOffset(6, 6, 1, 1), TextAnchor.MiddleCenter);
                AddText(c, text, 9, fg, true, TextAlignmentOptions.Center);
                return c.gameObject;
            }
            var st = new StatusTag { root = rt };
            st.stim = Chip("スティム", StimPurple, Ink);
            st.shield = Chip("シールド展開", Blu, Color.white);
            st.ghost = Chip("電波妨害", Blu, Color.white);
            return st;
        }

        void SyncShieldSlab(SimUnit u, bool on)
        {
            shieldSlabs.TryGetValue(u, out var e);
            if (!on) { if (e.go != null) e.go.SetActive(false); return; }
            if (e.go == null)
            {
                if (shieldMat == null)
                {
                    var probe = MakeMeshObject("Probe", transform, null, new Color(ShieldSlab.r, ShieldSlab.g, ShieldSlab.b, .7f), 3000);
                    shieldMat = probe.GetComponent<MeshRenderer>().sharedMaterial;
                    Destroy(probe);
                }
                var go = new GameObject("ShieldSlab");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = shieldMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                e = (go, u.Facing);
            }
            // m.dispA += angNorm(u.facing - m.dispA) * .3 — the figure's own smoothing, so the slab turns with it.
            float d = Mathf.DeltaAngle(e.disp * Mathf.Rad2Deg, u.Facing * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            e.disp += d * .3f;
            var dir = GridSpace.FacingToWorldDirection(e.disp);
            var basePos = u.View is Component vc && vc != null ? vc.transform.position : GridSpace.GridToWorld(u.RX, u.RY);
            e.go.transform.position = basePos + dir * .38f + Vector3.up * .42f;
            e.go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            e.go.transform.localScale = new Vector3(.62f, .78f, .07f);
            e.go.SetActive(true);
            shieldSlabs[u] = e;
        }
    }
}
