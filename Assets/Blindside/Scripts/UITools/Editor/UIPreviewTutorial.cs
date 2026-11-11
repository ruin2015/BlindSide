// Render Previews states for the tutorial's screen UI: CRUSHER's radio (sliding in, typing, the whole line with the advance mark) and
// the guide (the frame round a command button and the destination chip), built as the tutorial builds them on a HUD stage canvas and
// posed with ITimedEffect.Evaluate(t). The guide's targets are fixed places here (there is no game camera on a stage).
using UnityEngine;
using UnityEngine.UI;
using Blindside.Tutorial;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    internal static class UIPreviewTutorial
    {
        public const string RadioOpen = "tutorial_radio_open", RadioTyping = "tutorial_radio_typing", Radio = "tutorial_radio", Guide = "tutorial_guide";
        public const string DroneScan = "tutorial_drone_scan", Drone = "tutorial_drone", Siren = "tutorial_siren", RadioHq = "tutorial_radio_hq";

        /// <summary>The drone card `t` s into its entrance, with CRUSHER's line about it on the radio.</summary>
        public static void BuildDrone(UIPreview.Stage s, float t)
        {
            var canvas = s.NewCanvas("TutorialDrone_Stage", UITheme.CanvasKind.Hud, 0);
            var r = TutorialRadio.Build(canvas.transform, canvas);
            r.enabled = false;
            r.Say(TutorialDirector.LDrone1, true);
            r.Evaluate(6f);
            var c = TutorialDroneCard.Build(canvas.transform);
            c.enabled = false;
            c.Play();
            c.Evaluate(t);
        }

        /// <summary>The alarm at its first flash, with the HQ radio talking.</summary>
        public static void BuildSiren(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("TutorialSiren_Stage", UITheme.CanvasKind.Hud, 0);
            var r = TutorialRadio.Build(canvas.transform, canvas);
            r.enabled = false;
            r.SetSpeaker(true);
            r.Say(string.Format(TutorialDirector.LRadio, 5), true);
            r.Evaluate(6f);
            var sr = TutorialSiren.Build(canvas.transform);
            sr.enabled = false;
            sr.Play();
            sr.Evaluate(.15f);
        }

        public const string Skip = "tutorial_skip", SkipHold = "tutorial_skip_hold";

        /// <summary>The SKIP chip `t` s into a hold (0 = untouched), with the radio as it sits during a lesson.</summary>
        public static void BuildSkip(UIPreview.Stage s, float t)
        {
            var canvas = s.NewCanvas("TutorialSkip_Stage", UITheme.CanvasKind.Hud, 0);
            var r = TutorialRadio.Build(canvas.transform, canvas);
            r.enabled = false;
            r.Say(TutorialDirector.LMove1, false);
            r.Evaluate(4f);
            var b = TutorialSkipButton.Build(canvas.transform);
            b.enabled = false;
            b.Evaluate(t);
        }

        public const string OperatorCard = "tutorial_operator_card", DemoClick = "tutorial_demo_click";

        /// <summary>A demonstration pressing MOVE: the frame on the (stand-in) button and the pointer on it, .15 s into its click.</summary>
        public static void BuildDemoClick(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("TutorialDemo_Stage", UITheme.CanvasKind.Hud, 0);
            var btn = HudKit.NewRect("StandInMoveButton", canvas.transform);
            btn.anchorMin = btn.anchorMax = Vector2.zero; btn.pivot = Vector2.zero;
            btn.anchoredPosition = new Vector2(900f, 520f); btn.sizeDelta = new Vector2(226f, 40f);
            HudKit.AddImage(btn, UIPalette.Paper);
            var label = HudKit.AddText(btn, "移動", 16, UIPalette.Ink, true, TMPro.TextAlignmentOptions.MidlineLeft, "Label", UITheme.TextRole.Body);
            HudKit.Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(44f, 0f);
            var g = TutorialGuide.Build(canvas.transform, canvas);
            g.enabled = false;
            g.FrameLocal(new Vector2(900f, 520f), new Vector2(1126f, 560f), TutorialDirector.GMove);
            g.Evaluate(0f);
            g.CursorLocal(new Vector2(1013f, 540f));
            g.Click();
            g.Evaluate(.15f);
        }

        /// <summary>The deployment briefing: IGNIS's card on the right, CRUSHER introducing him on the radio.</summary>
        public static void BuildOperatorCard(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("TutorialOperatorCard_Stage", UITheme.CanvasKind.Hud, 0);
            var r = TutorialRadio.Build(canvas.transform, canvas);
            r.enabled = false;
            r.Say("この隊員はイグニス。\n強化壁を一度で破壊できる。", true);
            r.Evaluate(6f);
            var op = UnityEditor.AssetDatabase.LoadAssetAtPath<Blindside.Data.OperatorData>("Assets/Blindside/Data/Operators/Operator_IGNIS.asset");
            var c = TutorialOperatorCard.Build(canvas.transform);
            c.enabled = false;
            c.Show(op);
            c.Evaluate(1f);
        }

        public static void BuildRadioHq(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("TutorialRadioHq_Stage", UITheme.CanvasKind.Hud, 0);
            var r = TutorialRadio.Build(canvas.transform, canvas);
            r.enabled = false;
            r.SetSpeaker(true);
            r.Say(string.Format(TutorialDirector.LRadio, 5), true);
            r.Evaluate(6f);
        }

        public static void BuildRadio(UIPreview.Stage s, float t)
        {
            var canvas = s.NewCanvas("TutorialRadio_Stage", UITheme.CanvasKind.Hud, 0);
            var r = TutorialRadio.Build(canvas.transform, canvas);
            r.enabled = false;
            r.Say(TutorialDirector.LHideDone, true);   // (the longest three-line line: the box must hold it)
            r.Evaluate(t);
        }

        public const string Objective = "tutorial_objective", ObjectiveFire = "tutorial_objective_fire";

        /// <summary>A lesson as it runs: the radio (bottom left), the objective card with its controls (right) and, for FIRE, the chip
        /// beside the pointer.</summary>
        public static void BuildObjective(UIPreview.Stage s, bool fire)
        {
            var canvas = s.NewCanvas(fire ? "TutorialObjectiveFire_Stage" : "TutorialObjective_Stage", UITheme.CanvasKind.Hud, 0);
            var r = TutorialRadio.Build(canvas.transform, canvas);
            r.enabled = false;
            r.Say(fire ? TutorialDirector.LCam : TutorialDirector.LMove1, false);
            r.Evaluate(4f);
            var o = TutorialObjective.Build(canvas.transform);
            o.enabled = false;
            if (fire) o.Set(TutorialDirector.OCam, TutorialDirector.CFire); else o.Set(TutorialDirector.OMove1, TutorialDirector.CMove1);
            o.Evaluate(TutorialObjective.InTime + .5f);
            if (!fire) return;
            var g = TutorialGuide.Build(canvas.transform, canvas);
            g.enabled = false;
            g.MouseTip(TutorialDirector.TFire);
            g.Evaluate(.2f);
            g.mouseChip.anchoredPosition = new Vector2(1000f, 460f);
        }

        public const string Forecast = "engage_forecast", ForecastAllyDown = "engage_forecast_ally_down", ForecastPursuit = "engage_forecast_pursuit";

        public const string SupportBadge = "support_fire_badge";

        /// <summary>The battle stage's 支援射撃 badge, CRUSHER joining in, settled.</summary>
        public static void BuildSupportBadge(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("SupportBadge_Stage", UITheme.CanvasKind.Hud, 0);
            var b = KitSupportBadge.Build(canvas.transform, canvas);
            b.enabled = false;
            b.PoseAt("CRUSHER", "CRUSHER", "クラッシャー", TutorialRadio.CrusherColor, KitSupportBadge.InTime + .2f);
        }

        public const string ForecastTour = "tutorial_forecast_tour";

        /// <summary>The walk round the attack forecast, on the HP step.</summary>
        public static void BuildForecastTour(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("EngageForecastTour_Stage", UITheme.CanvasKind.Hud, 0);
            var p = EngagePreview.Build(canvas.transform, canvas);
            p.enabled = false;
            var a = new EngagePreview.Side { key = "FLARE", en = "FLARE", kr = "フレア", weapon = "M24", color = UIPalette.Hex("#F8594F"), ally = true, hp = 100, maxHp = 100, after = 64,
                                             dmg = "50", hit = "94%", hs = "12%", shots = "×1", result = "1/1", resultTotal = 50 };
            var b = new EngagePreview.Side { key = "CRUSHER", en = "CRUSHER", kr = "クラッシャー", weapon = "AR15", color = TutorialRadio.CrusherColor, ally = false, hp = 120, maxHp = 120, after = 70,
                                             dmg = "18", hit = "76%", hs = "8%", shots = "×3", result = "2/3", resultTotal = 36 };
            p.OpenAim(a, b, false);
            p.Evaluate(EngagePreview.XTo + .2f);
            var tour = TutorialForecastTour.Build(canvas.transform, canvas);
            tour.enabled = false;
            tour.Pose(p, 2);
        }

        /// <summary>The forecast with two allies joining in with 支援射撃: CRUSHER's round a headshot, SERUM's a miss.</summary>
        public static void BuildForecastPursuit(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("EngageForecastPursuit_Stage", UITheme.CanvasKind.Hud, 0);
            var p = EngagePreview.Build(canvas.transform, canvas);
            p.enabled = false;
            var a = new EngagePreview.Side { key = "FLARE", en = "FLARE", kr = "フレア", weapon = "M24", color = UIPalette.Hex("#F8594F"), ally = true, hp = 100, maxHp = 100, after = 100,
                                             dmg = "38", hit = "78%", hs = "32%", shots = "×1", preempt = true, result = "1/1", resultTotal = 38,
                                             pursuers = new[] { new EngagePreview.Pursuer { key = "CRUSHER", hit = true, crit = true, damage = 48 }, new EngagePreview.Pursuer { key = "SERUM" } } };
            var b = new EngagePreview.Side { key = "VOLT", en = "UNKNOWN", kr = "テロリスト", weapon = "M870", color = UIPalette.Hex("#EF8202"), ally = false, hp = 120, maxHp = 120, after = 34 };
            p.Open(a, b, null, null, false);
            p.Evaluate(EngagePreview.XTo + .2f);
        }

        /// <summary>The preemptive attack's forecast: FLARE on VOLT — VOLT expected to fall (grey, X) or, `allyDown`, FLARE the one
        /// expected to fall to the return fire. Posed at the end of its entrance.</summary>
        public static void BuildForecast(UIPreview.Stage s, bool allyDown)
        {
            var canvas = s.NewCanvas(allyDown ? "EngageForecastAlly_Stage" : "EngageForecast_Stage", UITheme.CanvasKind.Hud, 0);
            var p = EngagePreview.Build(canvas.transform, canvas);
            p.enabled = false;
            var a = new EngagePreview.Side { key = "FLARE", en = "FLARE", kr = "フレア", weapon = "M24", color = UIPalette.Hex("#F8594F"), ally = true, hp = 100, maxHp = 100, after = allyDown ? 0 : 64,
                                             dmg = "98", hit = "78%", hs = "32%", shots = "×1", preempt = true,
                                             result = allyDown ? "0/1" : "1/1", crits = allyDown ? 0 : 1, resultTotal = allyDown ? 0 : 196 };
            var b = new EngagePreview.Side { key = "VOLT", en = "UNKNOWN", kr = "テロリスト", weapon = "M870", color = UIPalette.Hex("#EF8202"), ally = false, hp = 120, maxHp = 120, after = allyDown ? 120 : 0 };
            if (allyDown) { b.dmg = "28"; b.hit = "66%"; b.hs = "12%"; b.shots = "×4"; b.result = "4/4"; b.crits = 1; b.resultTotal = 140; }
            p.Open(a, b, null, null, false);
            p.Evaluate(EngagePreview.XTo + .2f);
        }

        public const string WallCard3 = "tutorial_wall_card", WallCard2 = "tutorial_wall_card_shot";

        /// <summary>The wall sheet: the three kinds (stage 4) or the two of the shooting lesson (stage 2), with CRUSHER's line.</summary>
        public static void BuildWallCard(UIPreview.Stage s, bool three)
        {
            var canvas = s.NewCanvas(three ? "WallCard3_Stage" : "WallCard2_Stage", UITheme.CanvasKind.Hud, 0);
            var r = TutorialRadio.Build(canvas.transform, canvas);
            r.enabled = false;
            r.Say(three ? TutorialDirector.LBreach2 : TutorialDirector.LWallsTip, true);
            r.Evaluate(6f);
            var c = TutorialWallCard.Build(canvas.transform);
            c.enabled = false;
            if (three) c.Show(TutorialDirector.HWallKinds, TutorialWallCard.Kind.Reinforced, TutorialWallCard.Kind.Solid, TutorialWallCard.Kind.Breakable);
            else c.Show(TutorialDirector.HWallsShot, TutorialWallCard.Kind.Breakable, TutorialWallCard.Kind.Solid);
            c.Evaluate(c.Duration + .1f);
        }

        public const string GadgetCard = "tutorial_gadget_card";

        /// <summary>FLARE's launcher card, settled (glow mid-pulse), with CRUSHER's line about her ability.</summary>
        public static void BuildGadgetCard(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("GadgetCard_Stage", UITheme.CanvasKind.Hud, 0);
            var r = TutorialRadio.Build(canvas.transform, canvas);
            r.enabled = false;
            r.Say(TutorialDirector.LBreach5, true);
            r.Evaluate(6f);
            var op = UnityEditor.AssetDatabase.LoadAssetAtPath<Blindside.Data.OperatorData>("Assets/Blindside/Data/Operators/Operator_FLARE.asset");
            var c = TutorialGadgetCard.Build(canvas.transform, op != null ? op.abilityName : null, op != null ? op.charges : 4);
            c.enabled = false;
            c.Play();
            c.Evaluate(TutorialGadgetCard.ScanTo + TutorialGadgetCard.GlowPeriod * .25f);
        }

        public const string WallIntel = "deploy_wall_intel";

        /// <summary>The deployment's wall intel legend (right, under the illustration card's place).</summary>
        public static void BuildWallIntel(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("WallIntel_Stage", UITheme.CanvasKind.Hud, 0);
            var root = HudKit.NewRect("HudRoot", canvas.transform); HudKit.Stretch(root);
            Blindside.Integration.PlayerSelectionController.BuildWallIntelCard(root, out var reinf, out var elec);
            reinf.text = "強化壁  <color=#FFC81E><b>6</b></color>か所";
            elec.text = "電気が流れる壁  <color=#4FB3FF><b>4</b></color>か所";
        }

        public const string HintUndrop = "hud_hint_undrop";

        /// <summary>The deployment's hint with the long 「…の降下取り消し」 chip beside ESC: the chips go on their own row under the text.</summary>
        public static void BuildHintUndrop(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("HintUndrop_Stage", UITheme.CanvasKind.Hud, 0);
            var hud = HudKit.NewRect("MoveHud", canvas.transform); HudKit.Stretch(hud);
            var hint = KitHintBar.Build(hud);
            hint.Set("フレア — 降下する位置をクリックしてください",
                     "建物の外の明るく表示された区域ならどこでも可 · 他の隊員はクリックまたはQで選択",
                     new System.Collections.Generic.List<HintButton> { new HintButton("undrop", "クラッシャーの降下取り消し", false), new HintButton("cancel", "ESC 選択解除", false) }, null);
            hint.Evaluate(KitHintBar.EnterTime + .1f);
        }

        public const string ForecastAim = "engage_aim";

        /// <summary>FIRE aiming at an enemy: the forecast (no buttons) over a stand-in of the unit panel (bottom left, 430×190 with the
        /// face 30 above it) — the two must not overlap.</summary>
        public static void BuildForecastAim(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("EngageAim_Stage", UITheme.CanvasKind.Hud, 0);
            var panelStandIn = HudKit.NewRect("StandInUnitPanel", canvas.transform);
            panelStandIn.anchorMin = panelStandIn.anchorMax = Vector2.zero; panelStandIn.pivot = Vector2.zero;
            panelStandIn.anchoredPosition = new Vector2(20f, 20f); panelStandIn.sizeDelta = new Vector2(430f, 220f);
            HudKit.AddImage(panelStandIn, new Color(UIPalette.Paper.r, UIPalette.Paper.g, UIPalette.Paper.b, .5f));
            var p = EngagePreview.Build(canvas.transform, canvas);
            p.enabled = false;
            var a = new EngagePreview.Side { key = "CRUSHER", en = "CRUSHER", kr = "クラッシャー", weapon = "AR15", color = TutorialRadio.CrusherColor, ally = true, hp = 120, maxHp = 120, after = 96,
                                             dmg = "25", hit = "82%", hs = "28%", shots = "×3", preempt = true, result = "2/3", crits = 1, resultTotal = 75 };
            var b = new EngagePreview.Side { key = "VOLT", en = "UNKNOWN", kr = "テロリスト", weapon = "M870", color = UIPalette.Hex("#EF8202"), ally = false, hp = 120, maxHp = 120, after = 45,
                                             dmg = "30", hit = "52%", hs = "6%", shots = "×1", result = "0/1" };
            p.OpenAim(a, b, false);
            p.Evaluate(EngagePreview.XTo + .2f);
        }

        public const string SquadCall = "squad_callout";

        /// <summary>A match's squad radio call: FLARE's drone has found VOLT (the two-line call), fully typed.</summary>
        public static void BuildSquadCall(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("SquadCall_Stage", UITheme.CanvasKind.Hud, 0);
            var r = TutorialRadio.Build(canvas.transform, canvas);
            r.enabled = false;
            r.SetOperator("FLARE", "FLARE", "フレア", UIPalette.Hex("#F8594F"));
            r.Say(string.Format(Blindside.Integration.PlayerSelectionController.CDrone, "テロリスト"), false);
            r.Evaluate(4f);
        }

        public static void BuildGuide(UIPreview.Stage s)
        {
            var canvas = s.NewCanvas("TutorialGuide_Stage", UITheme.CanvasKind.Hud, 0);
            var r = TutorialRadio.Build(canvas.transform, canvas);
            r.enabled = false;
            r.Say(TutorialDirector.LMove1, false);
            r.Evaluate(4f);

            // a stand-in for the menu's MOVE button (226×40, like KitCommandMenu's rows)
            var btn = HudKit.NewRect("StandInMoveButton", canvas.transform);
            btn.anchorMin = btn.anchorMax = Vector2.zero; btn.pivot = Vector2.zero;
            btn.anchoredPosition = new Vector2(900f, 520f); btn.sizeDelta = new Vector2(226f, 40f);
            HudKit.AddImage(btn, UIPalette.Paper);
            var label = HudKit.AddText(btn, "移動", 16, UIPalette.Ink, true, TMPro.TextAlignmentOptions.MidlineLeft, "Label", UITheme.TextRole.Body);
            HudKit.Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(44f, 0f);

            var g = TutorialGuide.Build(canvas.transform, canvas);
            g.enabled = false;
            g.FrameLocal(new Vector2(900f, 520f), new Vector2(1126f, 560f), TutorialDirector.GMove);
            g.TargetLocal(new Vector2(1400f, 640f), TutorialDirector.GTarget);
            g.Evaluate(.2f);
        }
    }
}
