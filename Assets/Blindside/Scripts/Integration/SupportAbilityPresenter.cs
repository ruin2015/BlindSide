// The presentation half of the 'stim' / 'shield' / 'shock' cases of useAbility(u,target) in
// blindside_source/04_gameplay_combat_ai.js (~236-242). SupportAbilities/GadgetAbilities only change game state
// and raise OnHeal / OnShieldUp / OnShockPlaced; this turns them into the source's popup/fx/sfx/log, for the
// player's and the AI's use alike (same split as BreachAbilityPresenter):
//   stim:   popup(a,'+50 HP','#5dffa2'); fxRing(a.x,a.y,0xb98cff,600,1); sfx('ui');
//           log(`${u.kr} → ${a.kr}: 戦闘スティム (体力 +50, 移動力 +${add.toFixed(1)})`, mine)
//   shield: popup(u,'SHIELD','#9fd8ff'); sfx('ui'); log(`${u.kr}: シールド展開`, mine)
//   shock:  boom(d.x,d.y,0x4fb3ff,10) per destroyed drone; fxRing(x,y,0x4fb3ff,700,1.6); sfx('zap');
//           if(u.team===G.side) log(`${u.kr}: ショックワイヤー設置${n?` — 強化壁 ${n}個に通電`:''}`,'a')
// popup() itself (03_renderer_threejs.js:255/265) is WorldPopup below.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Blindside.Abilities;
using Blindside.Drones;
using Blindside.Movement;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Integration
{
    public class SupportAbilityPresenter
    {
        static readonly Color StimPurple = new Color32(0xb9, 0x8c, 0xff, 0xff);   // 0xb98cff
        static readonly Color HealGreen = new Color32(0x5d, 0xff, 0xa2, 0xff);    // #5dffa2
        static readonly Color ShieldBlue = new Color32(0x9f, 0xd8, 0xff, 0xff);   // #9fd8ff
        static readonly Color ZapBlue = new Color32(0x4f, 0xb3, 0xff, 0xff);      // 0x4fb3ff

        readonly AbilitySystem abilities;

        public SupportAbilityPresenter(AbilitySystem abilities)
        {
            this.abilities = abilities;
            abilities.OnHeal += OnHeal;
            abilities.OnShieldUp += OnShieldUp;
            abilities.OnShockPlaced += OnShock;
        }

        public void Unbind()
        {
            abilities.OnHeal -= OnHeal;
            abilities.OnShieldUp -= OnShieldUp;
            abilities.OnShockPlaced -= OnShock;
        }

        static string Kr(IAbilityUnit u) => u is IOperatorNamed n && !string.IsNullOrEmpty(n.Kr) ? n.Kr : u.Id;

        void OnHeal(IAbilityUnit u, IAbilityUnit a, int hpAdded, float moveAdded)
        {
            WorldPopup.Show(a.X, a.Y, "+50 HP", HealGreen);
            CombatFx.Ring(GridSpace.GridToWorld(a.X, a.Y), StimPurple, .6f, 1f);
            SfxNoise.Ui();
            HudLog.Add($"{Kr(u)} → {Kr(a)}: 戦闘スティム (体力 +50, 移動力 +{moveAdded.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)})",
                HudLog.TeamType(u.Team));
        }

        void OnShieldUp(IAbilityUnit u)
        {
            WorldPopup.Show(u.X, u.Y, "SHIELD", ShieldBlue);
            SfxNoise.Ui();
            HudLog.Add($"{Kr(u)}: シールド展開", HudLog.TeamType(u.Team));
        }

        void OnShock(IAbilityUnit u, ElectricField f, int panels, List<Drone> killed)
        {
            if (killed != null)
                foreach (var d in killed) CombatFx.Boom(GridSpace.GridToWorld(d.x, d.y), ZapBlue, 10);
            CombatFx.Ring(GridSpace.GridToWorld(f.x, f.y), ZapBlue, .7f, 1.6f);
            SfxNoise.Zap();
            if (u.Team == HudLog.Side)
                HudLog.Add($"{Kr(u)}: ショックワイヤー設置{(panels > 0 ? $" — 強化壁 {panels}個に通電" : "")}", HudLogType.Ally);
        }
    }

    /// <summary>
    /// Port of popup(o,text,col) + its syncFx tick: `.pop{font:700 18px var(--fd);transform:translate(-50%,-100%);
    /// text-shadow:0 2px 3px #000}` pinned at proj(x,1.3,y) with a random ±15px x offset, rising 40px over 1.1s,
    /// opacity 1-q².
    /// </summary>
    public class WorldPopup : MonoBehaviour
    {
        const float Life = 1.1f;
        static Canvas canvas;

        float t0, off, gx, gy;
        RectTransform rt;
        TMP_Text text, shadow;

        public static void Show(float gridX, float gridY, string msg, Color color)
        {
            EnsureCanvas();
            var go = new GameObject("Pop", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var p = go.AddComponent<WorldPopup>();
            p.rt = (RectTransform)go.transform;
            p.rt.anchorMin = p.rt.anchorMax = Vector2.zero;
            p.rt.pivot = new Vector2(.5f, 0f);
            p.rt.sizeDelta = new Vector2(200f, 26f);
            p.shadow = MakeText(p.rt, msg, new Color(0, 0, 0, .8f));
            p.shadow.rectTransform.anchoredPosition = new Vector2(0f, -2f);
            p.text = MakeText(p.rt, msg, color);
            p.gx = gridX; p.gy = gridY;
            p.t0 = Time.unscaledTime;
            p.off = (Random.value - .5f) * 30f;
            p.Place();
        }

        static TMP_Text MakeText(RectTransform parent, string msg, Color color)
        {
            var go = new GameObject("T", typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = msg; t.fontSize = 18; t.color = color;
            UIFonts.Auto(t, true);
            t.alignment = TextAlignmentOptions.Bottom; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        static void EnsureCanvas()
        {
            if (canvas != null) return;
            var go = new GameObject("WorldPopup_Canvas", typeof(RectTransform));
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4;   // under PlayerSelection_Canvas (menu / hint)
            UITheme.Current.ApplyTo(go.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Hud);
        }

        void LateUpdate()
        {
            float q = (Time.unscaledTime - t0) / Life;
            if (q >= 1f) { Destroy(gameObject); return; }
            Place();
            float a = 1f - q * q;
            text.alpha = a; shadow.alpha = .8f * a;
        }

        void Place()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var s = cam.WorldToScreenPoint(GridSpace.GridToWorld(gx, gy, 1.3f));
            bool on = s.z > 0f;
            if (text.enabled != on) { text.enabled = on; shadow.enabled = on; }
            float sf = canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
            float q = (Time.unscaledTime - t0) / Life;
            rt.anchoredPosition = new Vector2(s.x / sf + off, s.y / sf + q * 40f);
        }
    }
}
