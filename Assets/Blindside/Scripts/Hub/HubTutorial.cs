// CRUSHER shows the player round the HQ, the first time the squad comes home from an operation (a conversation scene, KitDialogue): the
// greeting with a choice, then the camera up over the HQ's middle and flown to each facility in turn, a last question — 「もう一度」 goes round
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
        // the tour's camera: over the middle first, then flown room to room
        const float OverviewTime = 1.4f, OverviewHold = .35f, OverviewHeight = 1.1f, OverviewMin = 6f, OverviewBack = .6f, FlyTime = 1.6f, Rise = 7f;

        /// <summary>Editor checks: the next HQ runs the tour whatever the save says.</summary>
        public static bool Force;

        /// <summary>This save still has the tour to see.</summary>
        public static bool Due(SaveData d) => Force || (d != null && d.tutorialDone && !d.hubTutorialDone && d.mainCleared >= 1);

        sealed class Stop
        {
            public string room, npcKey; public HubStation? station; public string[] lines;
        }

        static readonly Stop[] Stops =
        {
            new Stop { room = "医務室", npcKey = "SERUM", lines = new[] { "一つ目は医務室だ。", "ここでは医療品を買うことができる。\n作戦中に使ってみろ。" } },
            new Stop { room = "訓練所", npcKey = "BASTION", lines = new[] { "二つ目は訓練所だ。", "ここではお前のステータスを上げられる。\n円滑な作戦のため、積極的に活用しろ。", "また、これまでやった訓練を\nもう一度やることもできる。" } },
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
                yield return talk.Run(Speaker, "ともかく、中の施設を紹介する。");
                yield return ToCentre();
                foreach (var s in Stops)
                {
                    yield return CutTo(s);
                    foreach (var line in s.lines) yield return talk.Run(Speaker, line);
                }
                yield return talk.Run(Speaker, "以上、ここまでが説明だ。");
                yield return talk.RunAsk(Speaker, "他に聞きたいことはあるか？", "ありません", "もう一度聞かせてください");
                if (talk.Choice == 0) break;
            }
            yield return talk.Run(Speaker, "よし。では、この前できなかった\n訓練の続きをやる。ついて来い。");

            var d = SaveSystem.Current;
            if (d != null) { d.hubTutorialDone = true; SaveSystem.Write(true); }
            Force = false;
            TutorialDirector.Segment = TutorialSegment.Walls;
            SceneFade.Out(SceneFade.DefaultOut, SceneFlow.GoTutorial);
        }

        // the camera up over the HQ's middle, looking down on all of it (where the tour starts from)
        IEnumerator ToCentre()
        {
            if (cam == null) yield break;
            bool any = false;
            var b = new Bounds();
            foreach (var x in FindObjectsByType<HubArea>(FindObjectsInactive.Exclude))
            {
                var p = x.transform.position; p.y = 0f;
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
            var centre = any ? b.center : Vector3.zero;
            float h = Mathf.Max(b.extents.x, b.extents.z) * OverviewHeight + OverviewMin;
            var eye = centre + new Vector3(0f, h, -h * OverviewBack);
            yield return Fly(eye, Quaternion.LookRotation(centre - eye, Vector3.up), OverviewTime, false);
            yield return Hold(OverviewHold);
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
