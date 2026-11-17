// CRUSHER shows the player round the HQ, the first time the squad comes home from an operation (a conversation scene, KitDialogue): the
// greeting with a choice; then the overview — the camera straight up through the roof (the ceilings taken off for the tour), the whole
// HQ from above with a card over every facility (HubTourLabels) and a line on what each one is for — then the camera flown to each
// facility in turn for the details (「長押しでスキップ」 top right skips to the end at any time), a last question — 「もう一度」 goes round
// again, 「ありません」 marks it done and takes the player to the wall training the alarm interrupted (TutorialDirector.Segment = Walls).
// The player and the HQ's own camera and labels wait meanwhile.
using System.Collections;
using UnityEngine;
using Blindside.Flow;
using Blindside.Save;
using Blindside.Tutorial;
using Blindside.UI;

namespace Blindside.Hub
{
    public class HubTutorial : MonoBehaviour
    {
        const string Speaker = "CRUSHER";
        const float InsideDoor = .35f, EyeHeight = 2.3f, LookHeight = 1.15f;
        // the tour's camera between rooms: flown up and over (the roof is off for the whole tour)
        const float FlyTime = 1.6f, Rise = 7f;

        /// <summary>Editor checks: the next HQ runs the tour whatever the save says.</summary>
        public static bool Force;

        /// <summary>This save still has the tour to see.</summary>
        public static bool Due(SaveData d) => Force || (d != null && d.tutorialDone && !d.hubTutorialDone && d.mainCleared >= 1);

        sealed class Stop
        {
            public string room, npcKey; public HubStation? station; public string[] lines;
        }

        // ---- the overview: the camera straight up through the roof (the ceilings and lamps taken away), the whole HQ seen from above,
        // a card over every facility, and CRUSHER says in a line what each one is (its card lit) — then the rooms one by one (Stops) ----
        // (each facility's profile card, as the rescue intro's M.I.A cards: who runs it, its English name, what can be done there)
        sealed class Area
        {
            public string room, en, host, hostLabel, line, joinedLine; public string[] actions, joinedActions; public Color32 accent;
        }

        static readonly Area[] Areas =
        {
            new Area { room = "作戦室", en = "OPERATIONS", host = "CRUSHER", hostLabel = "CRUSHER (隊長)", accent = new Color32(0xea, 0xaf, 0x20, 0xff),
                       actions = new[] { "メイン作戦に出撃する", "サブクエストを受ける (1日)", "出撃メンバーを編成する" },
                       line = "ここが作戦室だ。\n次の任務やサブ任務は、ここで受ける。" },
            new Area { room = "医務室", en = "MEDICAL", host = "SERUM", hostLabel = "SERUM (衛生兵)", accent = new Color32(0x55, 0xca, 0x8a, 0xff),
                       actions = new[] { "医療キットを買う", "アドロピン注射器・覚醒薬を買う", "作戦中の回復手段を整える" },
                       line = "医務室。\n作戦で使う医療品が手に入る。" },
            new Area { room = "装備購入", en = "SUPPLY", host = "SILENCE", hostLabel = "SILENCE (補給係)", accent = new Color32(0xff, 0xc8, 0x1e, 0xff),
                       actions = new[] { "武器・アタッチメントを買う", "ガジェットを買う", "隊員に贈り物をする" },
                       line = "装備購入。\nパーツやガジェットを買える。" },
            new Area { room = "隊員・装備管理", en = "LOADOUT", host = "IGNIS", hostLabel = "IGNIS (装備担当)", accent = new Color32(0x00, 0xce, 0xf9, 0xff),
                       actions = new[] { "銃にパーツを取り付ける", "ガジェットを持たせる", "仲間のロードアウトを変える" },
                       line = "隊員・装備管理室。\n銃のカスタムや、仲間のロードアウトを整える。" },
            new Area { room = "訓練所", en = "TRAINING", host = "BASTION", hostLabel = "BASTION (教官)", accent = new Color32(0x9e, 0x6a, 0xbf, 0xff),
                       actions = new[] { "ステータスを鍛える", "BASTIONとトレーニング (½日)" },
                       line = "訓練所。\nここでステータスを鍛えられる。" },
            new Area { room = "炊事場", en = "KITCHEN", host = "GHOST", hostLabel = "GHOST (炊事係)", accent = new Color32(0xef, 0x82, 0x02, 0xff),
                       actions = new[] { "隊員と炊事支援をする (½日)", "次の出撃で最大HP +10", "一緒に作った隊員とのコープ" },
                       line = "炊事場。\n飯を作れば、次の出撃で効いてくる。" },
            new Area { room = "電子戦室", en = "E-WAR", hostLabel = "不在", accent = new Color32(0xb0, 0x5c, 0xff, 0xff),
                       actions = new[] { "現在は使われていない" },
                       line = "電子戦室。……今は空き部屋だが、\nいずれ使う奴らが来る。",
                       joinedActions = new[] { "GHOST・SILENCEの部屋", "交流・褒賞休暇" },
                       joinedLine = "電子戦室。\nGHOSTとSILENCEの部屋だ。\n銃が要るならSILENCEに頼め。" },
        };
        // up through the roof, then settled straight over the whole HQ (OverviewPitch: almost vertical — the 8.5 m walls hide no room;
        // OverviewMargin m round the facilities' middles reaches their outer walls)
        const float RoofRise = 7f, RiseTime = 1.1f, SettleTime = 1.6f, OverviewPitch = 88f, OverviewMargin = 6f;
        // (KitDialogue: the art over x 70 … 510 and the box under y 584; the profile card at the right from x 1040 — of 1440 × 810: the
        // HQ framed in the gap between)
        const float FreeX = .535f, FreeY = .63f, FreeW = .36f, FreeH = .66f;

        static readonly Stop[] Stops =
        {
            new Stop { room = "医務室", npcKey = "SERUM", lines = new[] { "一つ目は医務室だ。", "ここでは医療品を買うことができる。\n作戦中に使ってみろ。" } },
            new Stop { room = "訓練所", npcKey = "BASTION", lines = new[] { "二つ目は訓練所だ。", "ここではお前のステータスを上げられる。\n円滑な作戦のため、積極的に活用しろ。" } },
            new Stop { room = "隊員・装備管理", npcKey = "IGNIS", lines = new[] { "三つ目は隊員・装備管理室だ。", "ここではお前の銃に装備を取り付けたり、\n仲間のロードアウトを変更できる。" } },
            new Stop { room = "炊事場", station = HubStation.Kitchen, lines = new[] { "四つ目は炊事場だ。", "ここでは飯を作って食べられる。\n飯を食った後の作戦では、\nいいことが起きるかもしれないな。" } },
            new Stop { room = "作戦室", npcKey = "CRUSHER", lines = new[] { "最後に作戦室だ。", "ここで次の任務や、\nサブ任務を受けることができる。", "任務までの残り時間は、他の隊員と交流したり、\n訓練をしたりして過ごすといいだろう。" } },
        };

        Transform player, cam;
        HubPlayer mover;
        HubCamera follow;
        KitDialogue talk;

        public bool Playing { get; private set; }

        public void Begin(Transform playerT, Camera view)
        {
            player = playerT;
            cam = view != null ? view.transform : null;
            mover = player.GetComponent<HubPlayer>();
            follow = cam != null ? cam.GetComponent<HubCamera>() : null;
            if (mover != null) { mover.enabled = false; if (mover.figure != null) mover.figure.Speed = 0f; }
            if (follow != null) follow.enabled = false;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            talk = KitDialogue.Build(transform);
            Hud(false);   // (the topbar, the minimap and the hints wait: the tour's cards and lines have the screen)
            // SKIP (held down, as the battle tutorial's): top right, clear of the conversation box
            var talkCanvas = talk.GetComponentInParent<Canvas>();
            if (talkCanvas != null)
            {
                skip = Blindside.Tutorial.TutorialSkipButton.Build(talkCanvas.transform);
                var rt = (RectTransform)skip.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-Blindside.Tutorial.TutorialSkipButton.Right, -SkipTop);
                skip.OnSkip = Skip;
            }
            Playing = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            yield return talk.Run(Speaker, "任務ご苦労だった、新入り。");
            yield return talk.RunAsk(Speaker, "ここが俺たちの本部だ。", "すごく広いですね……", "これで全部ですか？");
            if (talk.Choice == 0) yield return talk.Run(Speaker, "そうか？ 長くいすぎて、\nよく分からなくなったな。");
            else yield return talk.Run(Speaker, "本部に来た記念に、\nしごかれたいのか？");

            while (true)
            {
                yield return talk.Run(Speaker, "ともかく、中の施設を紹介する。\nまずは上から、全体を見てみろ。");
                yield return Overview();
                yield return talk.Run(Speaker, "では、一つずつ詳しく見ていくぞ。");
                foreach (var s in Stops)
                {
                    if (s.station == HubStation.Kitchen && !Joined()) continue;
                    yield return CutTo(s);
                    foreach (var line in s.lines) yield return talk.Run(Speaker, line);
                }
                yield return talk.Run(Speaker, "以上、ここまでが説明だ。");
                yield return talk.RunAsk(Speaker, "他に聞きたいことはあるか？", "ありません", "もう一度聞かせてください");
                if (talk.Choice == 0) break;
            }
            Roof(true);
            Hud(true);
            if (skip != null) skip.gameObject.SetActive(false);
            yield return talk.Run(Speaker, "よし。次の任務までの時間は、\n仲間と過ごすなり、訓練するなり、\n好きに使え。");
            Finish();
        }

        // ---- the end (the last line said, or SKIP held): the tour marked seen, the player free at the HQ (the training room is no longer
        // part of the story: the first operation teaches as it is played) ----
        Blindside.Tutorial.TutorialSkipButton skip;
        const float SkipTop = 20f;
        bool finished;

        void Finish()
        {
            if (finished) return;
            finished = true;
            Roof(true);
            Hud(true);
            if (labels != null) { Destroy(labels.transform.parent.gameObject); labels = null; }
            if (skip != null) skip.gameObject.SetActive(false);
            var d = SaveSystem.Current;
            if (d != null) { d.hubTutorialDone = true; SaveSystem.Write(true); }
            Force = false;
            if (talk != null) talk.Close();
            if (mover != null) mover.enabled = true;
            if (follow != null) follow.enabled = true;
            Playing = false;
        }

        /// <summary>SKIP held: the tour stops where it is and goes straight to its end.</summary>
        void Skip()
        {
            if (finished) return;
            StopAllCoroutines();
            if (talk != null) talk.Close();
            Finish();
        }

        // ---- the overview ----------------------------------------------------------------------------------------------------------
        HubTourLabels labels;
        readonly System.Collections.Generic.List<Renderer> roof = new System.Collections.Generic.List<Renderer>();

        /// <summary>The ceilings and the lamps hanging from them, off (the tour looks in from above) or back on.</summary>
        void Roof(bool on)
        {
            if (!on && roof.Count == 0)
                foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if ((r.name == "Ceiling" || r.name == "DecoLamp") && r.enabled) roof.Add(r);
            foreach (var r in roof) if (r != null) r.enabled = on;
            if (on) roof.Clear();
        }

        void OnDestroy() { Roof(true); Hud(true); }

        GameObject hud;

        /// <summary>The HQ's own overlay (HubScreen's root: topbar, minimap, prompts, hints) shown or hidden.</summary>
        void Hud(bool on)
        {
            if (hud == null)
            {
                var hs = FindAnyObjectByType<HubScreen>(FindObjectsInactive.Include);
                var r = hs != null ? hs.transform.Find("HubScreen") : null;
                hud = r != null ? r.gameObject : null;
            }
            if (hud != null) hud.SetActive(on);
        }

        IEnumerator Overview()
        {
            if (cam == null) yield break;
            var areas = FindObjectsByType<HubArea>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            // straight up from where the camera is, tipping down as it goes; the roof gone as it rises through it
            Roof(false);
            var up = cam.position + Vector3.up * RoofRise;
            var fwd = cam.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            var tipped = Quaternion.LookRotation(Quaternion.AngleAxis(50f, Vector3.Cross(Vector3.up, fwd.normalized)) * fwd.normalized, Vector3.up);
            yield return Fly(up, tipped, RiseTime, false);
            // over the whole HQ: every facility in view
            OverviewPose(areas, out var eye, out var rot);
            yield return Fly(eye, rot, SettleTime, false);
            // a card over each facility, then a line on each (its card lit)
            if (labels == null) labels = HubTourLabels.Build(transform);
            var spoken = new System.Collections.Generic.List<Area>();
            foreach (var a in Areas)
            {
                HubArea at = null;
                foreach (var x in areas) if (x.displayName == a.room) at = x;
                if (at == null) continue;
                if (!Joined() && (a.room == "装備購入" || a.room == "炊事場")) continue;   // (no one there before GHOST and SILENCE join)
                bool joined = a.joinedLine != null && Joined();
                labels.Add(new HubTourLabels.Room
                {
                    name = a.room, en = a.en, accent = a.accent, world = at.transform.position,
                    host = joined ? "GHOST" : a.host, hostLabel = joined ? "GHOST · SILENCE" : a.hostLabel,
                    actions = joined && a.joinedActions != null ? a.joinedActions : a.actions,
                });
                spoken.Add(a);
            }
            labels.Show(cam.GetComponent<Camera>());
            yield return Hold(labels.Duration + .2f);
            for (int i = 0; i < spoken.Count; i++)
            {
                labels.Highlight(i);
                var a = spoken[i];
                yield return talk.Run(Speaker, a.joinedLine != null && Joined() ? a.joinedLine : a.line);
            }
            labels.Highlight(-1);
            labels.Hide();
            Destroy(labels.transform.parent.gameObject);   // (a new set next time round: "もう一度")
            labels = null;
        }

        // GHOST and SILENCE have joined the squad (the rescue operation won)
        static bool Joined()
        {
            var d = SaveSystem.Current;
            return d != null && d.unlockedOperators != null && d.unlockedOperators.Contains("GHOST");
        }

        // high over the HQ's middle, tipped down OverviewPitch, far enough back for every facility (and OverviewMargin m round them)
        void OverviewPose(HubArea[] areas, out Vector3 eye, out Quaternion rot)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var x in areas)
            {
                var p = x.transform.position; p.y = 0f;
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
            var centre = any ? b.center : Vector3.zero;
            var view = cam.GetComponent<Camera>();
            float vfov = (view != null ? view.fieldOfView : 50f) * .5f * Mathf.Deg2Rad;
            float aspect = view != null ? view.aspect : 16f / 9f;
            float hfov = Mathf.Atan(Mathf.Tan(vfov) * aspect);
            float halfX = b.extents.x + OverviewMargin, halfZ = b.extents.z + OverviewMargin;
            // the HQ fits the part of the picture the conversation leaves free (right of CRUSHER, over the line box): FreeW × FreeH
            // of the view, its middle at FreeX, FreeY (fractions; y from the bottom)
            float dist = Mathf.Max(halfX / (Mathf.Tan(hfov) * FreeW), halfZ * Mathf.Sin(OverviewPitch * Mathf.Deg2Rad) / (Mathf.Tan(vfov) * FreeH)) * 1.05f;
            var dir = Quaternion.Euler(OverviewPitch, 0f, 0f) * Vector3.forward;   // looking north, tipped down
            rot = Quaternion.LookRotation(dir, Vector3.up);
            eye = centre - dir * dist;
            // slid so the HQ's middle lands at (FreeX, FreeY) of the picture
            eye -= rot * Vector3.right * ((FreeX - .5f) * 2f * dist * Mathf.Tan(hfov));
            eye -= rot * Vector3.up * ((FreeY - .5f) * 2f * dist * Mathf.Tan(vfov));
        }

        // the camera into the room, flying up and over the walls: just inside its door, looking at who runs it (or the room's middle)
        IEnumerator CutTo(Stop s)
        {
            if (cam == null) yield break;
            Pose(s, out var eye, out var rot);
            yield return Fly(eye, rot, FlyTime, true);
        }

        // one move of the camera (unscaled, eased): a Bézier from where it is to `eye` — over the top (Rise above the higher end) when `arc`
        IEnumerator Fly(Vector3 eye, Quaternion rot, float time, bool arc)
        {
            Vector3 p0 = cam.position; Quaternion r0 = cam.rotation;
            float top = Mathf.Max(p0.y, eye.y) + (arc ? Rise : 0f);
            var c1 = arc ? new Vector3(p0.x, top, p0.z) : Vector3.Lerp(p0, eye, .33f);
            var c2 = arc ? new Vector3(eye.x, top, eye.z) : Vector3.Lerp(p0, eye, .66f);
            if (UIMotionSettings.reduceMotion) time = 0f;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                float k = HubArmoryStage.EaseOut(t / time), u = 1f - k;   // fast at first, slowing into place
                var p = u * u * u * p0 + 3f * u * u * k * c1 + 3f * u * k * k * c2 + k * k * k * eye;
                cam.SetPositionAndRotation(p, Quaternion.Slerp(r0, rot, k));
                yield return null;
            }
            cam.SetPositionAndRotation(eye, rot);
        }

        static IEnumerator Hold(float s) { for (float t = 0f; t < s; t += Time.unscaledDeltaTime) yield return null; }

        void Pose(Stop s, out Vector3 eye, out Quaternion rot)
        {
            HubSign sign = null; HubArea area = null; HubNpc npc = null;
            foreach (var x in FindObjectsByType<HubSign>(FindObjectsInactive.Exclude)) if (x.displayName == s.room) sign = x;
            foreach (var x in FindObjectsByType<HubArea>(FindObjectsInactive.Exclude)) if (x.displayName == s.room) area = x;
            foreach (var x in FindObjectsByType<HubNpc>(FindObjectsInactive.Exclude))
                if ((s.npcKey != null && x.operatorKey == s.npcKey) || (s.station.HasValue && x.station == s.station.Value)) npc = x;
            var focus = npc != null ? npc.transform.position : area != null ? area.transform.position : Vector3.zero;
            var door = sign != null ? sign.transform.position : focus + Vector3.back * 3f;
            door.y = 0f; focus.y = 0f;
            eye = Vector3.Lerp(door, focus, InsideDoor) + Vector3.up * EyeHeight;
            var look = focus + Vector3.up * LookHeight;
            rot = Quaternion.LookRotation(look - eye, Vector3.up);
        }
    }
}
