// The hostage situation on screen (presentation only — no rule, no AI and no random stream is touched): on a civilian map played on
// the attack side, an enemy standing next to the hostage (within Reach cells) is its captor. The captor holds its gun up (the aim pose)
// at the hostage until it falls or the hostage is taken; the first time the squad's sight finds the hostage or the captor, the standoff
// plays once — black bars close in top and bottom, the camera pans onto the two of them, the captor shouts, the hostage calls out, a
// caption and the log line say what the squad is looking at — and opens again after Hold seconds.
// Unscaled time; ITimedEffect.Evaluate(t) poses the bars and the caption; nothing is allocated per frame after it is built.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Blindside.Data;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Integration
{
    public class HostageStandoff : MonoBehaviour, ITimedEffect
    {
        public const float Reach = 1.6f;
        public const float BarH = 74f, InTime = .35f, Hold = 3.2f, OutTime = .45f;
        static readonly Color Foe = new Color32(0xe5, 0x36, 0x4d, 0xff);
        static readonly Color Civ = new Color32(0xff, 0xb1, 0x3b, 0xff);

        readonly List<SimUnit> captors = new List<SimUnit>(2);
        SimHostage hostage;
        VisibilityController sight;
        bool played;
        float clock = -1f;
        Canvas canvas;
        RectTransform top, bottom;
        CanvasGroup captionG;
        TMP_Text caption;

        public float Duration => InTime + Hold + OutTime;

        /// <summary>The standoff for this match (null: no hostage held at gunpoint here).</summary>
        public static HostageStandoff Bind(GameObject host, List<SimUnit> units, List<SimHostage> hostages, VisibilityController sight, bool attackSide)
        {
            var s = host.GetComponent<HostageStandoff>();
            if (s != null) s.Unbind();
            if (!attackSide || hostages == null || hostages.Count == 0 || units == null) return null;
            if (s == null) s = host.AddComponent<HostageStandoff>();
            s.hostage = hostages[0];
            s.sight = sight;
            foreach (var u in units)
                if (u.Team == OperatorTeam.Defense && u.Alive && Mathf.Abs(u.X - s.hostage.X) + Mathf.Abs(u.Y - s.hostage.Y) <= Reach) s.captors.Add(u);
            if (s.captors.Count == 0) { s.enabled = false; return null; }
            s.played = false; s.clock = -1f;
            s.enabled = true;
            return s;
        }

        public void Unbind()
        {
            captors.Clear(); hostage = null; played = false; clock = -1f;
            if (canvas != null) canvas.gameObject.SetActive(false);
            enabled = false;
        }

        /// <summary>`u` holds the hostage at gunpoint right now (the match keeps its aim pose up).</summary>
        public bool Aims(SimUnit u)
        {
            if (hostage == null || !hostage.Alive || hostage.Escort != null || hostage.Extracted || !u.Alive) return false;
            for (int i = 0; i < captors.Count; i++) if (captors[i] == u) return true;
            return false;
        }

        void Update()
        {
            if (hostage == null) return;
            if (!played && sight != null && Found())
            {
                played = true;
                Play();
            }
            if (clock >= 0f)
            {
                clock += Time.unscaledDeltaTime;
                Evaluate(clock);
                if (clock >= Duration) { clock = -1f; if (canvas != null) canvas.gameObject.SetActive(false); }
            }
        }

        bool Found()
        {
            if (!hostage.Alive || hostage.Escort != null) return false;
            if (sight.CellSeen(hostage.X, hostage.Y)) return true;
            foreach (var c in captors) if (c.Alive && c.Placed && sight.CellSeen(c.X, c.Y)) return true;
            return false;
        }

        void Play()
        {
            SimUnit captor = null;
            foreach (var c in captors) if (c.Alive) { captor = c; break; }
            float mx = hostage.X, my = hostage.Y;
            if (captor != null) { mx = (mx + captor.X) * .5f; my = (my + captor.Y) * .5f; }
            if (BattlefieldCamera.Instance != null) BattlefieldCamera.Instance.PanTo(mx, my, 700f);
            if (captor != null) WorldPopup.Show(captor.X, captor.Y, "来るな！ 人質がどうなってもいいのか！", Foe);
            WorldPopup.Show(hostage.X, hostage.Y, "助けて……！", Civ);
            HudLog.Add("人質に銃を突きつけている敵を確認 — 人質を巻き込まずに制圧せよ", HudLogType.Warn);
            SfxNoise.Ui();
            EnsureUi();
            canvas.gameObject.SetActive(true);
            clock = UIMotionSettings.reduceMotion ? InTime : 0f;
            Evaluate(clock);
        }

        void EnsureUi()
        {
            if (canvas != null) return;
            var go = new GameObject("HostageStandoffUI", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 810f);
            scaler.matchWidthOrHeight = .5f;
            var root = (RectTransform)go.transform;
            top = Bar("DecoLetterboxTop", root, 1f);
            bottom = Bar("DecoLetterboxBottom", root, 0f);
            var theme = UITheme.Current;
            var cap = HudKit.NewRect("Caption", root);
            cap.anchorMin = new Vector2(0f, 0f); cap.anchorMax = new Vector2(1f, 0f); cap.pivot = new Vector2(.5f, 0f);
            cap.anchoredPosition = new Vector2(0f, 12f); cap.sizeDelta = new Vector2(0f, 50f);
            captionG = cap.gameObject.AddComponent<CanvasGroup>();
            var en = HudKit.AddText(cap, "HOSTAGE SITUATION", 12f, Foe, true, TextAlignmentOptions.Top, "En", UITheme.TextRole.Display);
            en.characterSpacing = 10f;
            en.rectTransform.anchorMin = new Vector2(0f, 1f); en.rectTransform.anchorMax = new Vector2(1f, 1f); en.rectTransform.pivot = new Vector2(.5f, 1f);
            en.rectTransform.anchoredPosition = Vector2.zero; en.rectTransform.sizeDelta = new Vector2(0f, 16f);
            caption = HudKit.AddText(cap, "人質確認 — 敵が人質に銃を向けている", 18f, Color.white, true, TextAlignmentOptions.Bottom, "Text", UITheme.TextRole.Title);
            caption.rectTransform.anchorMin = Vector2.zero; caption.rectTransform.anchorMax = new Vector2(1f, 0f); caption.rectTransform.pivot = new Vector2(.5f, 0f);
            caption.rectTransform.anchoredPosition = Vector2.zero; caption.rectTransform.sizeDelta = new Vector2(0f, 28f);
            canvas.gameObject.SetActive(false);
        }

        static RectTransform Bar(string name, RectTransform root, float y)
        {
            var r = HudKit.NewRect(name, root);
            r.anchorMin = new Vector2(0f, y); r.anchorMax = new Vector2(1f, y); r.pivot = new Vector2(.5f, y);
            r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(0f, BarH);
            HudKit.AddImage(r, new Color(0f, 0f, 0f, .92f), raycast: false);
            return r;
        }

        /// <summary>`t` s into the standoff: the bars slide in (InTime), hold, slide out (OutTime); the caption fades with them.</summary>
        public void Evaluate(float t)
        {
            if (top == null) return;
            float k = t < InTime ? UIEase.Tactical(Mathf.Clamp01(t / InTime))
                    : t < InTime + Hold ? 1f
                    : 1f - UIEase.Tactical(Mathf.Clamp01((t - InTime - Hold) / OutTime));
            var tp = new Vector2(0f, BarH * (1f - k));
            if (top.anchoredPosition != tp) top.anchoredPosition = tp;
            var bp = new Vector2(0f, -BarH * (1f - k));
            if (bottom.anchoredPosition != bp) bottom.anchoredPosition = bp;
            if (captionG.alpha != k) captionG.alpha = k;
        }
    }
}
