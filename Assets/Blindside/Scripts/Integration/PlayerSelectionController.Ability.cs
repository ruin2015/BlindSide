// ABILITY command (panel-targeted breach abilities: hammer / launcher / thermite) for PlayerSelectionController.
// Ports, from blindside_source:
//   05_ui_input_intro.js — doCommand's 'skill' branch (~173-177), onClick's ability branch (~323-328),
//                          updateAim's `ui.hoverPanel=panelAt(h.x,h.y)` (~345)
//   03_renderer_threejs.js — syncUIHelpers' R.range ring (~230) and the panel highlight (~235-236):
//                          valid panels glow yellow, blinking (sin(t/200)>0 ? 0x4a3c00 : 0x2a2200), the hovered
//                          one steady and brighter (0x9a7d00)
// The effect itself (AbilitySystem.Use → BreachAbilities) and its fx/sfx/log (BreachAbilityPresenter) are shared
// with the AI. The glow is a translucent yellow shell over each valid wall cell rather than the wall material's
// emissive, since MapWallSync already owns the walls' property blocks (vision dimming, occluder fade).
// The ally / point / instant kinds (stim / shock / gas / cam / shield / ghost) branch off to
// PlayerSelectionController.SupportAbility.cs; mirror uses this panel flow like the breach abilities.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Blindside.Abilities;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        AbilitySystem abilities;
        AbilityContext abilityCtx;

        // ---- G.ui for mode 'ability' ----
        AbilitySpec abilitySpec;                               // ui.spec
        readonly HashSet<int> validPanels = new HashSet<int>(); // ui.validPanels
        int hoverPanel = -1;                                   // ui.hoverPanel

        static readonly Color HighlightYellow = new Color32(0xff, 0xd2, 0x1f, 0xff);   // 0xffd21f

        /// <summary>The match's ability dispatcher + its context (MatchBootstrap; shared with EnemyAI).</summary>
        public void BindAbilities(AbilitySystem abilities, AbilityContext ctx)
        {
            this.abilities = abilities; abilityCtx = ctx;
        }

        static bool IsBreachAbility(string key) => key == "hammer" || key == "launcher" || key == "thermite";

        /// <summary>
        /// doCommand's 'skill' branch for the panel abilities: spec = abilitySpec(u); validPanels = panels.filter(spec.valid);
        /// none → "選択可能な壁がありません" + back to the menu; else the ability mode with the spec's hint.
        /// </summary>
        /// <returns>false when this ability isn't connected (caller falls back to its log line).</returns>
        bool StartAbility(SimUnit u)
        {
            if (abilities == null || abilityCtx == null || map == null) return false;
            if (!IsBreachAbility(u.AbilityKey) && !IsSupportAbility(u.AbilityKey)) return false;
            abilityCtx.PrepPhase = turns.Phase == MatchPhase.Prep;
            var sp = abilities.GetSpec(u, abilityCtx);
            HideMenu();
            mode = Mode.Ability; uiUnit = u; panelUnit = u;
            abilitySpec = sp; hoverPanel = -1;
            validPanels.Clear();
            if (sp.Kind != AbilityTargetKind.Panel) return StartSupportAbility(u, sp);   // stim / shield / shock (.SupportAbility.cs)
            foreach (var p in map.Panels)
                if (sp.ValidPanel(p) && (!TutorialGate.Active || TutorialGate.PanelFilter == null || TutorialGate.PanelFilter(p.id))) validPanels.Add(p.id);
            NotifySelection();
            if (validPanels.Count == 0)
            {
                Log("選択可能な壁がありません — 壁にもっと近づいてください", "w");
                SelectUnit(u);
                return true;
            }
            SetHint(sp.Hint, sp.Sub, ("cancel", "ESC キャンセル", false));
            return true;
        }

        int PanelIdAt(int x, int y)
        {
            if (map == null || x < 0 || y < 0 || x >= MapGrid.Width || y >= map.Height) return -1;
            // an intact wall, or a launcher hole (still a panel: thermite / the launcher open it the rest of the way)
            var t = map.TileAt(x, y);
            if (t != TileType.Wall && t != TileType.Hole) return -1;
            int i = map.PanelIndexAt[y * MapGrid.Width + x];
            return i >= 0 ? map.Panels[i].id : -1;
        }

        /// <summary>onClick's ability branch: the clicked panel must be one of ui.validPanels, else sfx('click').</summary>
        /// <returns>true when the click was consumed by the ability mode.</returns>
        bool OnClickAbilityMode(Vector2 screenPos)
        {
            if (mode != Mode.Ability) return false;
            var u = uiUnit;
            if (abilitySpec.Kind != AbilityTargetKind.Panel) return OnClickSupportAbility(u, screenPos);
            int pid = BattlefieldCamera.GroundAt(screenPos, out var g) ? PanelIdAt(g.x, g.y) : -1;
            if (pid < 0 || !validPanels.Contains(pid)) { SfxNoise.Click(); return true; }
            busy = true; HideHint();
            mode = Mode.Busy;   // G.ui={mode:'busy',unit:u}
            StartCoroutine(AbilityRoutine(u, AbilityTarget.FromPanel(map.Panels[pid])));
            return true;
        }

        /// <summary>useAbility(u,target) (+ its spendAbility/updateHUD/checkWin), then G.busy=false; afterAction(u).</summary>
        IEnumerator AbilityRoutine(SimUnit u, AbilityTarget target)
        {
            abilityCtx.PrepPhase = turns.Phase == MatchPhase.Prep;
            yield return abilities.Use(u, target, abilityCtx);
            UnitsChanged?.Invoke();
            if (turns != null) turns.CheckWin();
            busy = false;
            if (turns == null || turns.Over) yield break;
            AfterAction(u);
        }

        // ================================================================
        //  syncUIHelpers(): R.range ring + the valid-panel highlight
        // ================================================================
        Transform abilityRoot;
        GameObject rangeGo;
        Material highlightMat;
        MaterialPropertyBlock highlightMpb;
        readonly List<Renderer> highlightPool = new List<Renderer>();
        readonly Dictionary<Vector2Int, Bounds> wallBounds = new Dictionary<Vector2Int, Bounds>();

        void SyncAbilityHelpers()
        {
            SyncSupportAbilityHelpers();
            SyncAbilityStatus();
            SyncCamPanel();
            bool reinforce = mode == Mode.Reinforce && uiUnit != null && validPanels.Count > 0;   // (.Reinforce.cs) same shells, no range ring
            bool on = reinforce || (mode == Mode.Ability && uiUnit != null && (validPanels.Count > 0 || abilitySpec.Kind != AbilityTargetKind.Panel));
            if (!on)
            {
                if (rangeGo != null) rangeGo.SetActive(false);
                foreach (var hl in highlightPool) hl.gameObject.SetActive(false);
                return;
            }
            EnsureAbilityHelpers();

            // updateAim: ui.hoverPanel = panelAt(G.hover)
            var mouse = Mouse.current;
            hoverPanel = mouse != null && BattlefieldCamera.GroundAt(mouse.position.ReadValue(), out var h) ? PanelIdAt(h.x, h.y) : -1;

            // R.range: RingGeometry(.975,1) scaled to spec.range at the unit, 0xffd21f opacity .7
            float r = reinforce ? 0f : abilitySpec.Range;
            rangeGo.SetActive(r > 0f);
            if (r > 0f)
            {
                rangeGo.transform.position = GridSpace.GridToWorld(uiUnit.X, uiUnit.Y, .05f);
                rangeGo.transform.localScale = new Vector3(r, r, r);
            }

            // e = hover ? 0x9a7d00 : (sin(t/200)>0 ? 0x4a3c00 : 0x2a2200) — as the shell's opacity.
            bool blinkHi = Mathf.Sin(Time.unscaledTime * 1000f / 200f) > 0f;
            int n = 0;
            foreach (int pid in validPanels)
            {
                float a = pid == hoverPanel ? .6f : blinkHi ? .34f : .2f;
                foreach (var c in map.Panels[pid].cells)
                {
                    var rend = HighlightAt(n++);
                    var b = WallBoundsAt(c);
                    rend.transform.position = b.center;
                    rend.transform.localScale = b.size + new Vector3(.04f, .04f, .04f);
                    highlightMpb.SetColor("_BaseColor", new Color(HighlightYellow.r, HighlightYellow.g, HighlightYellow.b, a));
                    highlightMpb.SetColor("_Color", new Color(HighlightYellow.r, HighlightYellow.g, HighlightYellow.b, a));
                    rend.SetPropertyBlock(highlightMpb);
                    rend.gameObject.SetActive(true);
                }
            }
            for (int i = n; i < highlightPool.Count; i++) highlightPool[i].gameObject.SetActive(false);
        }

        void EnsureAbilityHelpers()
        {
            if (abilityRoot != null) return;
            abilityRoot = new GameObject("AbilityHelpers").transform;
            abilityRoot.SetParent(transform, false);
            rangeGo = MakeMeshObject("AbilityRange", abilityRoot, BuildRingMesh(.975f, 1f, 128),
                new Color(HighlightYellow.r, HighlightYellow.g, HighlightYellow.b, .7f), 3006);
            // Same transparent unlit recipe as MakeMeshObject; colour comes per shell from the property block.
            var probe = MakeMeshObject("Probe", abilityRoot, null, HighlightYellow, 3005);
            highlightMat = probe.GetComponent<MeshRenderer>().sharedMaterial;
            Destroy(probe);
            highlightMpb = new MaterialPropertyBlock();
        }

        Renderer HighlightAt(int i)
        {
            while (highlightPool.Count <= i)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "PanelHighlight";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(abilityRoot, false);
                var mr = go.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.sharedMaterial = highlightMat;
                go.SetActive(false);
                highlightPool.Add(mr);
            }
            return highlightPool[i];
        }

        /// <summary>World bounds of the wall block drawn for a cell (Blindside_MapRoot/Walls/Wall_Wood_{x}_{y}),
        /// cached; a unit cube standing on the cell when the scene has no such block.</summary>
        Bounds WallBoundsAt(Vector2Int c)
        {
            if (wallBounds.TryGetValue(c, out var b)) return b;
            var go = GameObject.Find($"Blindside_MapRoot/Walls/Wall_Wood_{c.x}_{c.y}");
            var rs = go != null ? go.GetComponentsInChildren<Renderer>(true) : null;
            if (rs != null && rs.Length > 0)
            {
                b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            }
            else b = new Bounds(GridSpace.GridToWorld(c.x, c.y, .5f), Vector3.one);
            wallBounds[c] = b;
            return b;
        }
    }
}
