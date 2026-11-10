// The presentation half of the 'hammer' / 'launcher' / 'thermite' cases of useAbility(u,target) and of
// breakPanel(p,st) in blindside_source/04_gameplay_combat_ai.js (~213, 233-235). AbilitySystem/BreachAbilities
// only change game state and raise OnBreachSwing (once per beat) / OnBreachComplete (after the panel broke);
// this turns them into the source's fx/sfx/addMark/log, for the player's and the AI's use alike:
//   hammer   (×3): G.shake=.15; sfx('hit'); fxRing(p.cx,p.cy,0xffd21f,250,.6)
//   launcher (×1): fxArc(u,p.cx,p.cy,0xff9d1c,600); sfx('shot')
//   thermite (×5): fxRing(p.cx,p.cy,0xff9d1c,300,.8); sfx('hit')
//   breakPanel:    boom(c.x,c.y,0xffb13b,16) per cell; sfx('boom')
//   then:          addMark(other(u.team),p.cx,p.cy,'breach'); log(…, u.team===G.side?'a':'f')
using UnityEngine;
using Blindside.Abilities;
using Blindside.Map;
using Blindside.Movement;
using Blindside.Rendering;
using Blindside.UI;
using Blindside.Vision;

namespace Blindside.Integration
{
    public class BreachAbilityPresenter
    {
        static readonly Color Yellow = new Color32(0xff, 0xd2, 0x1f, 0xff);   // 0xffd21f
        static readonly Color Orange = new Color32(0xff, 0x9d, 0x1c, 0xff);   // 0xff9d1c
        static readonly Color BoomCol = new Color32(0xff, 0xb1, 0x3b, 0xff);  // 0xffb13b
        /// <summary>The broken wall's dust and chips (plaster / concrete).</summary>
        public static readonly Color FxWall = new Color(.62f, .57f, .5f);

        readonly AbilitySystem abilities;
        readonly MarkBoard marks;

        public BreachAbilityPresenter(AbilitySystem abilities, MarkBoard marks)
        {
            this.abilities = abilities; this.marks = marks;
            abilities.OnBreachSwing += OnSwing;
            abilities.OnBreachComplete += OnComplete;
        }

        public void Unbind()
        {
            abilities.OnBreachSwing -= OnSwing;
            abilities.OnBreachComplete -= OnComplete;
        }

        static string Kr(IAbilityUnit u) => u is IOperatorNamed n && !string.IsNullOrEmpty(n.Kr) ? n.Kr : u.Id;

        void OnSwing(IAbilityUnit u, WallPanel p)
        {
            var at = GridSpace.GridToWorld(p.centerX, p.centerY);
            switch (u.AbilityKey)
            {
                case "hammer":
                    if (BattlefieldCamera.Instance != null) BattlefieldCamera.Instance.Shake(.15f);
                    SfxNoise.Hit();
                    CombatFx.Ring(at, Yellow, .25f, .6f);
                    break;
                case "launcher":
                    FxArc.Spawn(u.X, u.Y, p.centerX, p.centerY, Orange, .6f);
                    SfxNoise.Shot();
                    break;
                case "thermite":
                    CombatFx.Ring(at, Orange, .3f, .8f);
                    SfxNoise.Hit();
                    break;
            }
        }

        void OnComplete(IAbilityUnit u, WallPanel p, TileType state, bool wasReinforced)
        {
            // particles: dust and chips of the wall; the launcher and the thermite also blow it (flash, sparks, a little fire)
            bool blast = u.AbilityKey == "launcher" || u.AbilityKey == "thermite";
            foreach (var c in p.cells) FxBlast.WallBreak(GridSpace.GridToWorld(c.x, c.y, .9f), FxWall, blast);
            SfxNoise.Boom();
            marks?.Add(MarkBoard.Other(u.Team), p.centerX, p.centerY, MarkKind.Breach);

            string wall = wasReinforced ? "強化壁" : "壁";
            string msg = u.AbilityKey switch
            {
                "hammer" => $"{Kr(u)}: ハンマーで壁を破壊しました",
                "launcher" => $"{Kr(u)}: {wall}に穴を開けました (通行不可)",
                "thermite" => $"{Kr(u)}: 発熱爆薬で{wall}を開放しました",
                _ => null,
            };
            if (msg != null) HudLog.Add(msg, HudLog.TeamType(u.Team));
        }
    }
}
