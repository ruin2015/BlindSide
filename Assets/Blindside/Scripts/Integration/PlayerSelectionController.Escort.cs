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
            if (hostage == null || !hostage.Alive) return;
            hostage.SetEscort(u);
            Log($"{u.Kr}: 人質を確保 — 救出地点(EXFIL)へ護送してください", "a");
            Sfx.Ui();
            WorldPopup.Show(hostage.X, hostage.Y, "SECURED", HostOrange);
            SelectUnit(u);               // re-renders the menu (SECURE is gone) and the panel's "人質護送" tag
            UnitsChanged?.Invoke();
        }

        // ================================================================
        //  hostage label: "人質" → "人質 · 護送中"
        // ================================================================
        RectTransform hostLabel;
        TMP_Text hostLabelText;
        bool? hostLabelEscorted;

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
            hostLabelEscorted = null;
            return rt;
        }

        void SyncHostageLabel()
        {
            if (endedTagRoot == null) return;
            var cam = Cam;
            bool show = cam != null && hostage != null && hostage.Alive && hostage.View is Component hv && hv != null && !TutorialGate.Active;   // (the tutorial has no hostage to show)
            if (!show) { if (hostLabel != null) hostLabel.gameObject.SetActive(false); return; }
            var s = cam.WorldToScreenPoint(hostage.View.LabelPoint);
            if (s.z < 0f) { if (hostLabel != null) hostLabel.gameObject.SetActive(false); return; }
            if (hostLabel == null || hostLabel.parent != endedTagRoot) hostLabel = BuildHostLabel();
            hostLabel.gameObject.SetActive(true);
            bool escorted = hostage.Escort != null;
            if (hostLabelEscorted != escorted) { hostLabelEscorted = escorted; hostLabelText.text = escorted ? "人質 · 護送中" : "人質"; }
            float sf = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
            hostLabel.anchoredPosition = new Vector2(s.x / sf, s.y / sf + 4f);
        }

        // ================================================================
        //  tether: orange dotted line escort ↔ hostage
        // ================================================================
        const float TetherSpacing = .2f, TetherDot = .075f, TetherHeight = .12f;
        const int TetherMaxDots = 16;
        Transform tetherRoot;
        readonly List<Transform> tetherDots = new List<Transform>();
        Material tetherMat;

        void SyncEscortTether()
        {
            var es = hostage != null ? hostage.Escort as SimUnit : null;
            bool show = es != null && hostage.Alive && es.Alive && es.View != null && Shown(es)
                        && endedTagRoot != null && endedTagRoot.gameObject.activeSelf;   // hidden during a battle cinematic
            if (!show) { if (tetherRoot != null && tetherRoot.gameObject.activeSelf) tetherRoot.gameObject.SetActive(false); return; }
            if (tetherRoot == null) BuildTether();
            tetherRoot.gameObject.SetActive(true);

            Vector2 a = new Vector2(hostage.RX, hostage.RY), b = new Vector2(es.RX, es.RY);
            float len = Vector2.Distance(a, b);
            int n = Mathf.Clamp(Mathf.FloorToInt(len / TetherSpacing), 0, TetherMaxDots);
            for (int i = 0; i < tetherDots.Count; i++)
            {
                bool on = i < n;
                if (tetherDots[i].gameObject.activeSelf != on) tetherDots[i].gameObject.SetActive(on);
                if (!on) continue;
                Vector2 p = Vector2.Lerp(a, b, (i + .5f) * TetherSpacing / Mathf.Max(len, .001f));
                tetherDots[i].position = GridSpace.GridToWorld(p.x, p.y, TetherHeight);
            }
        }

        void BuildTether()
        {
            tetherRoot = new GameObject("EscortTether").transform;
            tetherRoot.SetParent(transform, false);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            tetherMat = new Material(shader);
            if (tetherMat.HasProperty("_BaseColor")) tetherMat.SetColor("_BaseColor", HostOrange);
            if (tetherMat.HasProperty("_Color")) tetherMat.SetColor("_Color", HostOrange);
            for (int i = 0; i < TetherMaxDots; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(d.GetComponent<Collider>());
                d.name = "Dot";
                d.transform.SetParent(tetherRoot, false);
                d.transform.localScale = new Vector3(TetherDot, TetherDot * .5f, TetherDot);
                d.GetComponent<Renderer>().sharedMaterial = tetherMat;
                d.SetActive(false);
                tetherDots.Add(d.transform);
            }
        }

        void SyncEscortViews()
        {
            SyncHostageLabel();
            SyncEscortTether();
        }
    }
}
