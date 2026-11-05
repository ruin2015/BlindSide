// Defense GADGET (監視カメラ) for PlayerSelectionController — port of openCamPanel(u) in
// blindside_source/05_ui_input_intro.js (~211-219) and the #camPanel CSS in 00_markup_and_styles.html (~172-177):
//   G.ui={mode:'cams',unit:u}; #camPanel = .h "CAMERA FEED<span>監視カメラ</span>" + one button per living cam:
//     `CAM-${i+1:00} ${c.placed?'(設置)':'(固定)'}` · <span class="rec">● REC</span> (foe, blinking 1s steps(2))
//   click: panTo(c.x,c.y,400); sfx('ui'); fxRing(c.x,c.y,0xff6b8a,700,3.2);
//          every living attacker, not jamming, within 3.2 with losF(c→e,'def'): addMark('def',e,'drone'); e.revealT=now()+2000
//          log(`CAM-${i+1:00}: ${n?`敵 ${n}名を捕捉`:'異常なし'}`, n?'w':'')
//   setHint('確認する監視カメラを選択してください','カメラ周囲の円形視界 · 捕捉した敵はマップに表示',[{id:'cancel',label:'ESCで閉じる'}])
// #camPanel{right:14px;top:132px;width:230px;z-index:26;animation:slideInR .35s} — under the menu (30).
// selectUnit/clearUI/cancelMode hide it: here it is simply shown only while the mode is Cams.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.Abilities;
using Blindside.Data;
using Blindside.Rendering;
using Blindside.UI;
using Blindside.Vision;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        static readonly Color CamRingPink = new Color32(0xff, 0x6b, 0x8a, 0xff);   // 0xff6b8a

        RectTransform camPanel;
        readonly List<TMP_Text> camRecTexts = new List<TMP_Text>();
        float camPanelT0;

        int AliveCameraCount()
        {
            if (abilityCtx == null) return map != null ? map.FixedCameras.Count : 0;
            int n = 0;
            foreach (var c in abilityCtx.Cameras) if (c.alive) n++;
            return n;
        }

        void OpenCamPanel(SimUnit u)
        {
            if (abilityCtx == null) return;
            mode = Mode.Cams; uiUnit = u; panelUnit = u;
            var cams = new List<SurveillanceCamera>();
            foreach (var c in abilityCtx.Cameras) if (c.alive) cams.Add(c);
            BuildCamPanel(cams);
            SetHint("確認する監視カメラを選択してください", "カメラ周囲の円形視界 · 捕捉した敵はマップに表示", ("cancel", "ESCで閉じる", false));
        }

        void CamClicked(SurveillanceCamera c, int i)
        {
            if (!PlayerCanAct() || mode != Mode.Cams) return;
            if (BattlefieldCamera.Instance != null) BattlefieldCamera.Instance.PanTo(c.x, c.y, 400f);
            SfxNoise.Ui();
            CombatFx.Ring(GridSpace.GridToWorld(c.x, c.y), CamRingPink, .7f, 3.2f);
            int n = 0;
            float now = Time.unscaledTime;
            foreach (var e in units)
            {
                if (!e.Alive || e.Team != OperatorTeam.Attack || e.Ghost) continue;
                float dx = e.X - c.x, dy = e.Y - c.y;
                if (Mathf.Sqrt(dx * dx + dy * dy) > 3.2f) continue;
                if (vision == null || !vision.LineOfSight(c.x, c.y, e.X, e.Y, OperatorTeam.Defense)) continue;
                marks?.Add(OperatorTeam.Defense, e.X, e.Y, MarkKind.Spotted);
                e.RevealT = now + 2f;
                n++;
            }
            Log($"CAM-{i + 1:00}: {(n > 0 ? $"敵 {n}名を捕捉" : "異常なし")}", n > 0 ? "w" : "");
        }

        void BuildCamPanel(List<SurveillanceCamera> cams)
        {
            if (camPanel != null) Destroy(camPanel.gameObject);
            camRecTexts.Clear();
            camPanel = NewRect("CamPanel", canvas.transform);
            if (menuRoot != null) camPanel.SetSiblingIndex(menuRoot.GetSiblingIndex());   // z-index 26 < #cmdMenu 30
            camPanel.anchorMin = camPanel.anchorMax = new Vector2(1, 1);
            camPanel.pivot = new Vector2(1, 1);
            camPanel.anchoredPosition = new Vector2(-14f, -132f);
            AddImage(camPanel, Paper, raycast: true);
            var sh = camPanel.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, .35f); sh.effectDistance = new Vector2(0, -3);
            VLG(camPanel, 0, new RectOffset(0, 0, 0, 0));
            var fit = camPanel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            camPanel.sizeDelta = new Vector2(230f, 0f);

            // .h{background:var(--ink);color:#fff;padding:7px 10px;font:700 13px var(--fd);justify-content:space-between}
            var h = NewRect("H", camPanel);
            AddImage(h, Ink);
            HLG(h, 0, new RectOffset(10, 10, 7, 7));
            var ht = AddText(h, "CAMERA FEED", 13, Color.white, true, TextAlignmentOptions.MidlineLeft);
            ht.characterSpacing = 8;
            LE(ht, flexW: 1);
            AddText(h, "監視カメラ", 11, Color.white, false, TextAlignmentOptions.MidlineRight);

            for (int i = 0; i < cams.Count; i++)
            {
                var c = cams[i]; int idx = i;
                var b = NewRect("Cam_" + (i + 1), camPanel);
                var bg = AddImage(b, Paper, raycast: true);
                HLG(b, 0, new RectOffset(10, 10, 9, 9));
                var top = NewRect("Border", b);                 // border-top:1px solid rgba(0,0,0,.1)
                LE(top).ignoreLayout = true;
                top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.pivot = new Vector2(.5f, 1);
                top.sizeDelta = new Vector2(0, 1); top.anchoredPosition = Vector2.zero;
                AddImage(top, new Color(0, 0, 0, .1f));
                var lt = AddText(b, $"CAM-{i + 1:00} {(c.placed ? "(設置)" : "(固定)")}", 12, Ink, true, TextAlignmentOptions.MidlineLeft);
                LE(lt, flexW: 1);
                var rec = AddText(b, "● REC", 10, Foe, true, TextAlignmentOptions.MidlineRight);
                UseSymbolFont(rec);
                camRecTexts.Add(rec);
                var btn = b.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => CamClicked(c, idx));
                // #camPanel button:hover{background:var(--blu);color:#fff}
                var hover = b.gameObject.AddComponent<UIHoverSwap>();
                hover.Init(bg, lt, Paper, Blu, Ink, Color.white);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(camPanel);
            camPanelT0 = Time.unscaledTime;
        }

        /// <summary>Shown only in the Cams mode; slideInR .35s and the REC blink (1s, steps(2)).</summary>
        void SyncCamPanel()
        {
            if (camPanel == null) return;
            bool on = mode == Mode.Cams;
            if (camPanel.gameObject.activeSelf != on) camPanel.gameObject.SetActive(on);
            if (!on) return;
            float p = Mathf.Clamp01((Time.unscaledTime - camPanelT0) / .35f);
            float e = 1f - Mathf.Pow(1f - p, 3f);
            camPanel.anchoredPosition = new Vector2(-14f + 40f * (1f - e), -132f);
            bool recOn = (Time.unscaledTime % 1f) < .5f;
            foreach (var t in camRecTexts) if (t != null) t.enabled = recOn;
        }
    }

    /// <summary>A plain :hover background/text colour swap for a uGUI row.</summary>
    public class UIHoverSwap : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        Image bg; TMP_Text text; Color bg0, bg1, t0, t1;

        public void Init(Image bg, TMP_Text text, Color bgNormal, Color bgHover, Color textNormal, Color textHover)
        {
            this.bg = bg; this.text = text; bg0 = bgNormal; bg1 = bgHover; t0 = textNormal; t1 = textHover;
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData _) { bg.color = bg1; text.color = t1; }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData _) { bg.color = bg0; text.color = t0; }
        void OnDisable() { if (bg != null) { bg.color = bg0; text.color = t0; } }
    }
}
