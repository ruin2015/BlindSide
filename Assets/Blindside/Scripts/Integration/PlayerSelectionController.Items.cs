// The HQ's items in the match, the player's operators only (SimUnit.Items, from the save). One menu command per kind carried; each use
// spends the unit's action and one from the squad's stock. No GameRng draw is made here.
//   破片手榴弾 / スモーク弾 / 閃光弾   thrown (Mode.Throw, .Grenade.cs); what happens where they land is here for smoke and flash
//     smoke    a screen of SmokeR: sight stops at it, shots through it are SmokePenalty harder, until the thrower's side's next phase
//     flash    everyone in FlashR with a clear line, friend or foe: hit chance −FlashAcc and every hit on them a headshot — enemies
//              through their next two phases' start, the thrower's side until its next phase
//   粘着爆薬    set on an adjacent plain wall (Mode.Ability's wall picking): after a short fuse the wall is gone
//   クレイモア   set in front of the unit; an enemy coming close sets it off (MapGadgetRuntime), an enemy's shot destroys it
//   医療キット   HP back at once; アドロピン注射器 / 覚醒薬: buffs for their turns (SimUnit.AddEffect)
//   除細動器    no command: it goes off by itself (SimUnit.Revive)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blindside.Abilities;
using Blindside.Hub;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.Turns;
using Blindside.UI;

namespace Blindside.Integration
{
    public partial class PlayerSelectionController
    {
        public const string SmokeKey = "smoke", FlashKey = "flash", BreachKey = "breach", ClaymoreKey = "claymore";
        public const float SmokeR = 2.2f, FlashR = 3.5f, FlashAcc = 45f, ClaymoreBlastR = 2.4f, ClaymoreNear = 80f, ClaymoreEdge = 30f, BreachFuse = .8f;
        const string ItemPrefix = "item:";
        static readonly Color ItemHeal = new Color32(0x5d, 0xff, 0xa2, 0xff);

        MapGadgetRuntime itemGadgets;
        bool breachItemMode;

        /// <summary>The match's placed gadgets, and the items' per-phase upkeep.</summary>
        public void BindItems(MapGadgetRuntime gadgets)
        {
            itemGadgets = gadgets;
            if (gadgets != null) gadgets.ClaymoreBlast = OnClaymoreBlast;
            if (turns != null) turns.OnPhaseBegin += OnItemPhase;
        }

        static int ItemStock(string key) => ItemEffects.Stock(key);

        static string ItemName(string key) { var g = HubCatalog.GadgetOf(key); return g != null ? g.name : key; }

        static bool IsThrown(string key) => key == FragKey || key == SmokeKey || key == FlashKey;

        static string ItemIcon(string key)
        {
            switch (key)
            {
                case FragKey: return "✹";
                case SmokeKey: return "◎";
                case FlashKey: return "✦";
                case BreachKey: return "▣";
                case ClaymoreKey: return "⬢";
                default: return "✚";
            }
        }

        static string ItemEn(string key)
        {
            switch (key)
            {
                case FragKey: return "FRAG";
                case SmokeKey: return "SMOKE";
                case FlashKey: return "FLASH";
                case BreachKey: return "BREACH";
                case ClaymoreKey: return "CLAYMORE";
                default: return "MEDICAL";
            }
        }

        // ---- the commands ------------------------------------------------------------------------------------------------------
        void AddItemCommand(SimUnit u, List<Command> L, bool prep)
        {
            if (u.Items.Count == 0 || turns == null || prep || turns.Phase == MatchPhase.Deploy) return;
            for (int i = 0; i < u.Items.Count; i++)
            {
                string key = u.Items[i];
                if (u.Items.IndexOf(key) != i) continue;   // (two of a kind: one command)
                var g = HubCatalog.GadgetOf(key);
                if (g == null) continue;
                int n = 0;
                foreach (var k in u.Items) if (k == key) n++;
                bool usable = !u.Acted && !u.Shield && ItemStock(key) > 0 && !(g.heal > 0 && u.HP >= u.MaxHP);
                L.Add(new Command { id = ItemPrefix + key, kr = g.name, en = ItemEn(key), ic = ItemIcon(key), ok = usable, sub = n + "個" });
            }
        }

        /// <summary>doCommand for an item: false when `c` isn't one.</summary>
        bool DoItemCommand(SimUnit u, string c)
        {
            if (!c.StartsWith(ItemPrefix)) return false;
            string key = c.Substring(ItemPrefix.Length);
            if (IsThrown(key)) { HideMenu(); StartThrow(u, key); }
            else if (key == BreachKey) StartBreachItem(u);
            else if (key == ClaymoreKey) SetClaymore(u);
            else UseMedical(u, key);
            return true;
        }

        /// <summary>The action and one item are spent.</summary>
        void SpendItem(SimUnit u, string key)
        {
            u.Acted = true;
            u.Items.Remove(key);
            ItemEffects.Spend(key);
        }

        // ---- medical -----------------------------------------------------------------------------------------------------------
        void UseMedical(SimUnit u, string key)
        {
            var g = HubCatalog.GadgetOf(key);
            if (g == null) return;
            HideMenu();
            SpendItem(u, key);
            if (g.heal > 0)
            {
                int before = u.HP;
                u.HP = Mathf.Min(u.MaxHP, u.HP + g.heal);
                WorldPopup.Show(u.X, u.Y, "+" + (u.HP - before), ItemHeal);
            }
            if (g.turns > 0)
            {
                float rangeBefore = Blindside.Combat.CombatModifiers.MoveRange(u.Mv, u.Weapon, u);
                u.AddEffect(new SimUnit.TimedEffect { turns = g.turns, bonusHp = g.bonusHp, acc = g.accPct, headshot = g.headshotPct, mobilityPct = g.mobilityPct });
                if (u.MoveLeft > 0f) u.MoveLeft += Blindside.Combat.CombatModifiers.MoveRange(u.Mv, u.Weapon, u) - rangeBefore;   // (the extra reach counts this turn)
                WorldPopup.Show(u.X, u.Y, "BOOST", ItemHeal);
            }
            CombatFx.Ring(GridSpace.GridToWorld(u.X, u.Y), ItemHeal, .6f, 1.4f);
            Sfx.Ui();
            Log($"{KrOf(u)}: {g.name}を使用", "a");
            AfterAction(u);
        }

        // ---- smoke and flash, where they land ----------------------------------------------------------------------------------
        void DropSmoke(SimUnit u, Vector2 at)
        {
            if (abilityCtx == null) return;
            abilityCtx.Smokes.Add(new SmokeCloud { x = at.x, y = at.y, r = SmokeR, team = u.Team });
            RefreshSmokeCells();
            CombatFx.Ring(GridSpace.GridToWorld(at.x, at.y, .06f), new Color(.75f, .78f, .82f), .6f, SmokeR);
            SfxNoise.Ui();
            Log("スモーク弾が煙幕を張った", "a");
        }

        // the cells each screen reaches: inside its radius with no wall between them and its centre
        void RefreshSmokeCells()
        {
            map.SmokeCells.Clear();
            foreach (var s in abilityCtx.Smokes)
            {
                int r = Mathf.CeilToInt(s.r);
                int cx = Mathf.FloorToInt(s.x + .5f), cy = Mathf.FloorToInt(s.y + .5f);
                for (int y = cy - r; y <= cy + r; y++)
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        if (x < 0 || y < 0 || x >= MapGrid.Width || y >= map.Height) continue;
                        if ((x - s.x) * (x - s.x) + (y - s.y) * (y - s.y) > s.r * s.r) continue;
                        var t = map.TileAt(x, y);
                        if (t == TileType.Wall || t == TileType.Hard) continue;
                        var path = Blindside.Combat.CombatSystem.TraceLine(map, s.x, s.y, x, y);
                        if (!path.Blocked && path.Walls == 0) map.SmokeCells.Add(y * MapGrid.Width + x);
                    }
            }
            map.BumpMap();
        }

        void DetonateFlash(SimUnit thrower, Vector2 at)
        {
            var white = new Color(1f, 1f, .92f);
            CombatFx.Boom(GridSpace.GridToWorld(at.x, at.y, .3f), white, 8);
            CombatFx.Ring(GridSpace.GridToWorld(at.x, at.y, .06f), white, .5f, FlashR);
            SfxNoise.Boom();
            if (BattlefieldCamera.Instance != null) BattlefieldCamera.Instance.Shake(.15f);
            Log("閃光弾が炸裂した", "a");
            if (units == null) return;
            foreach (var t in units)
            {
                if (!t.Alive || !t.Placed || t.Captive) continue;
                float dx = t.X - at.x, dy = t.Y - at.y;
                if (dx * dx + dy * dy > FlashR * FlashR) continue;
                if (vision != null && !vision.LineOfSight(at.x, at.y, t.X, t.Y, thrower.Team)) continue;
                t.AddEffect(new SimUnit.TimedEffect { turns = t.Team == thrower.Team ? 1 : 2, acc = -FlashAcc, flash = true });
                WorldPopup.Show(t.X, t.Y, "BLIND", white);
                Log(KrOf(t) + " の視界が閃光で奪われた", t.Team == side ? "f" : "a");
            }
        }

        // ---- 粘着爆薬 --------------------------------------------------------------------------------------------------------------
        void StartBreachItem(SimUnit u)
        {
            HideMenu();
            mode = Mode.Ability; uiUnit = u; panelUnit = u;
            breachItemMode = true;
            abilitySpec = new AbilitySpec(AbilityTargetKind.Panel, "爆薬を設置する壁を選択してください", "隣接する通常の壁のみ可能",
                validPanel: p => map.TileAt(p.cells[0].x, p.cells[0].y) == TileType.Wall && !p.reinforced && AbilitySystem.PanelNear(p, u));
            hoverPanel = -1;
            validPanels.Clear();
            foreach (var p in map.Panels)
                if (abilitySpec.ValidPanel(p) && (!TutorialGate.Active || TutorialGate.PanelFilter == null || TutorialGate.PanelFilter(p.id))) validPanels.Add(p.id);
            NotifySelection();
            if (validPanels.Count == 0)
            {
                breachItemMode = false;
                Log("選択可能な壁がありません — 壁にもっと近づいてください", "w");
                SelectUnit(u);
                return;
            }
            SetHint(abilitySpec.Hint, abilitySpec.Sub, ("cancel", "ESC キャンセル", false));
        }

        IEnumerator BreachItemRoutine(SimUnit u, WallPanel p)
        {
            breachItemMode = false;
            SpendItem(u, BreachKey);
            u.Facing = Mathf.Atan2(p.centerY - u.Y, p.centerX - u.X);
            u.View?.PlayAbility("frag");
            Log($"{KrOf(u)}: 粘着爆薬を設置", "a");
            UnitsChanged?.Invoke();

            var at = GridSpace.GridToWorld(p.centerX, p.centerY, .9f);
            float t = 0f;
            while (t < BreachFuse)
            {
                t += Time.unscaledDeltaTime;
                if (Mathf.Repeat(t, .2f) < Time.unscaledDeltaTime) CombatFx.Spark(at, new Color(1f, .3f, .2f), .16f, .08f);
                yield return null;
            }
            FxBlast.Explosion(at);
            SfxNoise.Boom();
            if (BattlefieldCamera.Instance != null) BattlefieldCamera.Instance.Shake(.3f);
            BreakPanel(p.id, TileType.Rubble, true);
            if (vision != null) vision.MapVersion++;
            marks?.Add(Blindside.Vision.MarkBoard.Other(u.Team), p.centerX, p.centerY, Blindside.Vision.MarkKind.Breach);
            Log("粘着爆薬で壁を破壊した", "a");
            UnitsChanged?.Invoke();
            turns?.CheckWin();
            busy = false;
            if (turns == null || turns.Over) yield break;
            AfterAction(u);
        }

        // ---- クレイモア ---------------------------------------------------------------------------------------------------------------
        void SetClaymore(SimUnit u)
        {
            if (itemGadgets == null) return;
            HideMenu();
            float x = u.X + Mathf.Cos(u.Facing) * .7f, y = u.Y + Mathf.Sin(u.Facing) * .7f;
            var tile = map.TileAt(Mathf.FloorToInt(x + .5f), Mathf.FloorToInt(y + .5f));
            if (tile == TileType.Wall || tile == TileType.Hard) { x = u.X; y = u.Y; }   // (facing a wall: at its feet)
            SpendItem(u, ClaymoreKey);
            itemGadgets.PlaceClaymore(u, x, y);
            Sfx.Ui();
            Log($"{KrOf(u)}: クレイモアを設置", "a");
            AfterAction(u);
        }

        void OnClaymoreBlast(SimUnit owner, float x, float y)
        {
            var at = new Vector2(x, y);
            FxBlast.Explosion(GridSpace.GridToWorld(x, y, .15f));
            CombatFx.Ring(GridSpace.GridToWorld(x, y, .06f), FragOrange, .6f, ClaymoreBlastR);
            SfxNoise.Boom();
            if (BattlefieldCamera.Instance != null) BattlefieldCamera.Instance.Shake(.3f);
            Log("クレイモアが爆発した", "a");
            HurtEnemies(owner, at, ClaymoreBlastR, 0f, ClaymoreNear, ClaymoreEdge, "クレイモア");
            UnitsChanged?.Invoke();
            turns?.CheckWin();
        }

        /// <summary>The enemies of `by` within `radius` with a clear line from the blast: dead inside `killR`, else `near` → `edge` by distance.</summary>
        void HurtEnemies(SimUnit by, Vector2 at, float radius, float killR, float near, float edge, string what)
        {
            if (units == null) return;
            foreach (var t in units)
            {
                if (!t.Alive || !t.Placed || t.Team == by.Team) continue;
                float dx = t.X - at.x, dy = t.Y - at.y, dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > radius) continue;
                if (vision != null && !vision.LineOfSight(at.x, at.y, t.X, t.Y, by.Team)) continue;
                int dmg = dist <= killR ? t.HP
                    : Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(near, edge, (dist - killR) / (radius - killR)) * t.DamageTakenScale));
                t.HP = Mathf.Max(0, t.HP - dmg);
                WorldPopup.Show(t.X, t.Y, "-" + dmg, FragOrange);
                if (t.HP <= 0)
                {
                    t.View?.PlayDeath();
                    WorldPopup.Show(t.X, t.Y - .4f, "K.I.A", new Color(1f, .23f, .33f));
                    if (combat != null) combat.ReportKill(t, by);
                    else t.Alive = false;
                    Log(KrOf(t) + " を" + what + "で倒した", "a");
                }
                else t.View?.PlayHit(false);
            }
        }

        // ---- upkeep ---------------------------------------------------------------------------------------------------------------
        void OnItemPhase(MatchPhase phase)
        {
            if (units == null) return;
            var acting = TurnManager.ActingTeam(phase);
            foreach (var u in units)
            {
                if (u.Team != acting) continue;
                u.TickEffects();
                if (u.Alive) u.MoveLeft = Mathf.Min(u.MoveLeft, Blindside.Combat.CombatModifiers.MoveRange(u.Mv, u.Weapon, u));
            }
            if (abilityCtx != null && abilityCtx.Smokes.RemoveAll(s => s.team == acting) > 0)
            {
                RefreshSmokeCells();
                Log("煙幕が晴れた", "");
            }
        }
    }
}
