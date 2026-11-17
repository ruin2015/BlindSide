// Plays a co-op event (Progress.CoopEvents) at the HQ, as a conversation scene (KitDialogue) with the rank-up card (KitCoopRankUp) over it:
//   the invitation and its two answers — turned down, the operator says so and nothing is spent (the event stays on offer);
//   taken, the scene's lines and choices play (an answer's extra points are kept), the RankUp step raises the co-op (Campaign.CoopRankUp)
//   and plays the card, and at the end the scene takes its half day (Campaign.CostTalk), the extra points are added and the game saved.
// The HQ's screen layer is hidden and the player and the camera hold still meanwhile (HubDirector checks Playing).
using System;
using System.Collections;
using UnityEngine;
using Blindside.Progress;
using Blindside.Save;
using Blindside.UI;

namespace Blindside.Hub
{
    public class CoopEventPlayer : MonoBehaviour
    {
        static CoopEventPlayer current;

        /// <summary>A co-op event is on screen.</summary>
        public static bool Playing => current != null;

        KitDialogue talk;
        KitCoopRankUp card;
        GameObject hud;

        /// <summary>Plays FLARE's due event with `key` (Campaign.CoopEventDue); `done(taken)` after it. False when there is none to play.</summary>
        public static bool Play(string key, Action<bool> done)
        {
            var d = SaveSystem.Current;
            if (current != null || d == null || !Campaign.CoopEventDue(d, key)) return false;
            var ev = CoopEvents.Of(key, Campaign.Rank(d, key));
            if (ev == null) return false;
            var go = new GameObject("CoopEvent_" + key);
            current = go.AddComponent<CoopEventPlayer>();
            current.StartCoroutine(current.Run(d, ev, done));
            return true;
        }

        IEnumerator Run(SaveData d, CoopEvent ev, Action<bool> done)
        {
            talk = KitDialogue.Build(transform);
            card = KitCoopRankUp.Build(transform);
            Hud(false);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            bool taken = false;
            try
            {
                yield return talk.RunAsk(ev.key, ev.invite, ev.inviteOptions);
                taken = talk.Choice == 0;
                yield return talk.Run(ev.key, taken ? ev.accept : ev.decline);
                if (taken)
                {
                    int bonus = 0;
                    foreach (var s in ev.steps)
                    {
                        switch (s.kind)
                        {
                            case CoopEvent.Kind.Line:
                                yield return talk.Run(s.speaker, s.text);
                                break;
                            case CoopEvent.Kind.Ask:
                                var labels = new string[s.answers.Length];
                                for (int i = 0; i < labels.Length; i++) labels[i] = s.answers[i].label;
                                yield return talk.RunAsk(s.speaker, s.text, labels);
                                var a = s.answers[Mathf.Clamp(talk.Choice, 0, s.answers.Length - 1)];
                                bonus += a.bonus;
                                if (a.reply != null) foreach (var line in a.reply) yield return talk.Run(s.speaker, line);
                                break;
                            case CoopEvent.Kind.RankUp:
                                int from = Campaign.Rank(d, ev.key);
                                int to = Campaign.CoopRankUp(d, ev.key);
                                SaveSystem.Write(false, notify: false);
                                talk.Close();   // (the card has the screen; the next line brings the box back in)
                                yield return card.Run(ev.key, from, to);
                                break;
                        }
                    }
                    Campaign.Spend(d, Campaign.CostTalk);
                    if (bonus > 0) Campaign.AddCoop(d, Campaign.Commander, ev.key, bonus);
                    SaveSystem.Write(false);
                }
            }
            finally
            {
                if (talk != null) talk.Close();
                Hud(true);
                current = null;
                Destroy(gameObject);
            }
            done?.Invoke(taken);
        }

        // the HQ's labels, minimap and hints wait (as the tour's)
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
    }
}
