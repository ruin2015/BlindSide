// SECURE (人質確保) command + the hostage's overhead label / escort tether for PlayerSelectionController.
// Ports, from blindside_source:
//   05_ui_input_intro.js  — doCommand's escort branch (~184): setEscort(u); log; sfx('ui'); popup(G.hostage,'SECURED','#ffb13b'); selectUnit(u)
//   03_renderer_threejs.js — syncLabels' hostage label (~270-277): `.ul.host .ul-n` = "人質" / "人質 · 護送中"
//   00_markup_and_styles.html — .ul-n / .ul.host .ul-n (~301-303)
// The command menu entry itself (commands(u)'s `escort`, shown within 1.6m + segFree, hidden once someone escorts)
// lives in PlayerSelectionController.Commands. SECURE spends no move and no action, like the source.
//
// Not in the source: the orange dotted tether between the escort and the hostage (requested) — small flat dots laid
// along the two drawn (rx/ry) positions, grid space with z = -y applied only by GridSpace.GridToWorld.
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Blindside.Combat;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        static readonly Color HostOrange = new Color32(0xff, 0xb1, 0x3b, 0xff);   // '#ffb13b' — SECURED popup / tether
        static readonly Color HostBorder = new Color32(0xff, 0xab, 0x3b, 0xff);   // .ul.host .ul-n border-color
        static readonly Color HostInk = new Color32(0xff, 0xd0, 0x8f, 0xff);      // .ul.host .ul-n color

        /// <summary>doCommand's `escort` branch — the hostage follows this unit from now on.</summary>
        void DoEscort(SimUnit u)
        {
            var h = FreeHostageNear(u);
            if (h == null) return;
            h.SetEscort(u);
            Log($"{u.Kr}: 人質を確保 — 救出地点(EXFIL)へ護送してください", "a");
            Sfx.Ui();
            WorldPopup.Show(h.X, h.Y, "SECURED", HostOrange);
            SelectUnit(u);               // re-renders the menu (SECURE is gone) and the panel's "人質護送" tag
            UnitsChanged?.Invoke();
        }

        // ================================================================
        //  hostage label: "人質" → "人質 · 護送中"
        // ================================================================
        // one label per hostage (index = hostages index); state: -1 unknown, 0 free, 1 escorted
        readonly List<RectTransform> hostLabels = new List<RectTransform>();
        readonly List<TMP_Text> hostLabelTexts = new List<TMP_Text>();
        readonly List<int> hostLabelStates = new List<int>();
        TMP_Text hostLabelText;

        RectTransform BuildHostLabel()
        {
            var rt = NewRect("HostageLabel", endedTagRoot);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(.5f, 0f);
            AddImage(rt, new Color32(14, 16, 20, (byte)(.88f * 255)));
            HLG(rt, 0, new RectOffset(5 + 3, 6, 1, 1), TextAnchor.MiddleCenter);
            var fit = rt.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var b = NewRect("Border", rt);
            LE(b).ignoreLayout = true;
            b.anchorMin = new Vector2(0, 0); b.anchorMax = new Vector2(0, 1); b.pivot = new Vector2(0, .5f);
            b.anchoredPosition = Vector2.zero; b.sizeDelta = new Vector2(3, 0);
            AddImage(b, HostBorder);
            hostLabelText = AddText(rt, "人質", 10, HostInk, true, TextAlignmentOptions.Center);
            hostLabelText.characterSpacing = 10;
            return rt;
        }

        void SyncHostageLabel()
        {
            if (endedTagRoot == null) return;
            var cam = Cam;
            int n = Mathf.Max(hostages.Count, hostLabels.Count);
            for (int i = 0; i < n; i++)
            {
                while (hostLabels.Count <= i) { hostLabels.Add(null); hostLabelTexts.Add(null); hostLabelStates.Add(-1); }
                var lbl = hostLabels[i];
                if (lbl != null && lbl.parent != endedTagRoot) lbl = null;
                var h = i < hostages.Count ? hostages[i] : null;
                bool show = cam != null && h != null && h.Alive && !h.Extracted && h.View is Component hv && hv != null && !TutorialGate.Active;   // (the tutorial has no hostage to show)
                var s = show ? cam.WorldToScreenPoint(h.View.LabelPoint) : Vector3.zero;
                if (!show || s.z < 0f) { if (lbl != null) lbl.gameObject.SetActive(false); continue; }
                if (lbl == null)
                {
                    lbl = BuildHostLabel();
                    hostLabels[i] = lbl; hostLabelTexts[i] = hostLabelText; hostLabelStates[i] = -1;
                }
                lbl.gameObject.SetActive(true);
                int state = h.Escort != null ? 1 : 0;
                if (hostLabelStates[i] != state)
                {
                    hostLabelStates[i] = state;
                    string name = hostages.Count > 1 ? "人質" + (i + 1) : "人質";
                    hostLabelTexts[i].text = state == 1 ? name + " · 護送中" : name;
                }
                float sf = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
                lbl.anchoredPosition = new Vector2(s.x / sf, s.y / sf + 4f);
            }
        }

        // ================================================================
        //  tether: orange dotted line escort ↔ hostage
        // ================================================================
        const float TetherSpacing = .2f, TetherDot = .075f, TetherHeight = .12f;
        const int TetherMaxDots = 16;
        // one tether per hostage (index = hostages index): its root and its dots
        readonly List<Transform> tetherRoots = new List<Transform>();
        readonly List<List<Transform>> tetherDotSets = new List<List<Transform>>();
        Material tetherMat;

        void SyncEscortTether()
        {
            int count = Mathf.Max(hostages.Count, tetherRoots.Count);
            for (int k = 0; k < count; k++)
            {
                var h = k < hostages.Count ? hostages[k] : null;
                var es = h != null ? h.Escort as SimUnit : null;
                bool show = es != null && h.Alive && !h.Extracted && es.Alive && es.View != null && Shown(es)
                            && endedTagRoot != null && endedTagRoot.gameObject.activeSelf;   // hidden during a battle cinematic
                var root = k < tetherRoots.Count ? tetherRoots[k] : null;
                if (!show) { if (root != null && root.gameObject.activeSelf) root.gameObject.SetActive(false); continue; }
                if (root == null) root = BuildTether(k);
                root.gameObject.SetActive(true);
                var dots = tetherDotSets[k];

                Vector2 a = new Vector2(h.RX, h.RY), b = new Vector2(es.RX, es.RY);
                float len = Vector2.Distance(a, b);
                int n = Mathf.Clamp(Mathf.FloorToInt(len / TetherSpacing), 0, TetherMaxDots);
                for (int i = 0; i < dots.Count; i++)
                {
                    bool on = i < n;
                    if (dots[i].gameObject.activeSelf != on) dots[i].gameObject.SetActive(on);
                    if (!on) continue;
                    Vector2 p = Vector2.Lerp(a, b, (i + .5f) * TetherSpacing / Mathf.Max(len, .001f));
                    dots[i].position = GridSpace.GridToWorld(p.x, p.y, TetherHeight);
                }
            }
        }

        Transform BuildTether(int k)
        {
            while (tetherRoots.Count <= k) { tetherRoots.Add(null); tetherDotSets.Add(new List<Transform>()); }
            var root = new GameObject("EscortTether_" + (k + 1)).transform;
            root.SetParent(transform, false);
            tetherRoots[k] = root;
            if (tetherMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                tetherMat = new Material(shader);
                if (tetherMat.HasProperty("_BaseColor")) tetherMat.SetColor("_BaseColor", HostOrange);
                if (tetherMat.HasProperty("_Color")) tetherMat.SetColor("_Color", HostOrange);
            }
            var dots = tetherDotSets[k];
            dots.Clear();
            for (int i = 0; i < TetherMaxDots; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(d.GetComponent<Collider>());
                d.name = "Dot";
                d.transform.SetParent(root, false);
                d.transform.localScale = new Vector3(TetherDot, TetherDot * .5f, TetherDot);
                d.GetComponent<Renderer>().sharedMaterial = tetherMat;
                d.SetActive(false);
                dots.Add(d.transform);
            }
            return root;
        }

        void SyncEscortViews()
        {
            SyncHostageLabel();
            SyncEscortTether();
        }
    }
}
