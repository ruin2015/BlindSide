// The squad coming home: when an operation hands over to the HQ (HubEntrance.Pending), the scene opens from black on FLARE walking in
// from the south door up the lobby's line. The camera starts low in front of the figure and swings round behind it, ending where the
// HQ's own camera (HubCamera) sits, and a band reads 「本部帰還」 with the calendar. The player has no control meanwhile; a click or a key
// skips to the end. One unscaled clock (ITimedEffect.Evaluate(t)); nothing allocated per frame.
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Blindside.UI;

namespace Blindside.Hub
{
    public class HubEntrance : MonoBehaviour, ITimedEffect
    {
        /// <summary>The next HQ opens with the entrance.</summary>
        public static bool Pending
        {
            get => pending;
            set { pending = value; SceneFade.HoldForNextScene = value; }
        }
        static bool pending;

        public const float WalkTime = 3.6f, Total = 4.6f, FadeIn = .8f, BandFrom = .7f, BandIn = .35f, BandOut = 3.4f, BandEnd = 3.8f;
        public const float StartBehind = 7.5f, BandH = 96f;
        const float WalkSpeed = 2.2f;

        Transform player, cam;
        HubPlayer mover;
        HubCamera follow;
        CharacterController cc;
        Vector3 walkTo, walkFrom;
        Quaternion facing;
        float clock;
        bool running;

        RectTransform band;
        CanvasGroup bandGroup;
        Canvas overlay;

        public bool Playing => running;
        public float Duration => Total;

        /// <summary>Starts the walk in (`day`: the band's second line).</summary>
        public void Begin(Transform playerT, Camera view, string day)
        {
            player = playerT;
            cam = view != null ? view.transform : null;
            mover = player.GetComponent<HubPlayer>();
            cc = player.GetComponent<CharacterController>();
            follow = cam != null ? cam.GetComponent<HubCamera>() : null;
            walkTo = player.position;
            facing = player.rotation;
            walkFrom = walkTo - facing * Vector3.forward * StartBehind;
            if (mover != null) mover.enabled = false;
            if (cc != null) cc.enabled = false;
            if (follow != null) follow.enabled = false;

            var go = new GameObject("HubEntrance_Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            overlay = go.AddComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = 90;
            UITheme.Current.ApplyTo(go.AddComponent<CanvasScaler>(), UITheme.CanvasKind.Menu);
            BuildBand(go.transform, day, out band, out bandGroup);

            SceneFade.SetBlack();
            SceneFade.In(FadeIn);
            clock = 0f; running = true;
            Evaluate(0f);
        }

        /// <summary>The band (also built by Render Previews).</summary>
        public static void BuildBand(Transform parent, string day, out RectTransform rt, out CanvasGroup group)
        {
            var theme = UITheme.Current;
            rt = HudKit.NewRect("EntranceBand", parent);
            rt.anchorMin = new Vector2(0f, .5f); rt.anchorMax = new Vector2(1f, .5f); rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = new Vector2(0f, -210f); rt.sizeDelta = new Vector2(0f, BandH);
            group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            var bg = HudKit.NewRect("DecoBand", rt);
            HudKit.Stretch(bg);
            HudKit.AddImage(bg, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .86f));
            var line = HudKit.NewRect("DecoLine", rt);
            line.anchorMin = new Vector2(0f, 1f); line.anchorMax = Vector2.one; line.pivot = new Vector2(.5f, 1f); line.sizeDelta = new Vector2(0f, 2f);
            HudKit.AddImage(line, theme.blue);
            var en = HudKit.AddText(rt, "RETURN TO BASE", 13f, theme.blue, true, TextAlignmentOptions.Center, "En", UITheme.TextRole.Display);
            en.characterSpacing = 16f;
            en.rectTransform.anchorMin = new Vector2(0f, 1f); en.rectTransform.anchorMax = Vector2.one; en.rectTransform.pivot = new Vector2(.5f, 1f);
            en.rectTransform.anchoredPosition = new Vector2(0f, -10f); en.rectTransform.sizeDelta = new Vector2(0f, 18f);
            var ja = HudKit.AddText(rt, "本部帰還", 34f, Color.white, true, TextAlignmentOptions.Center, "Ja", UITheme.TextRole.Title);
            ja.rectTransform.anchorMin = new Vector2(0f, 1f); ja.rectTransform.anchorMax = Vector2.one; ja.rectTransform.pivot = new Vector2(.5f, 1f);
            ja.rectTransform.anchoredPosition = new Vector2(0f, -26f); ja.rectTransform.sizeDelta = new Vector2(0f, 44f);
            var d = HudKit.AddText(rt, day ?? "", 13f, Color.Lerp(theme.ink, Color.white, .7f), false, TextAlignmentOptions.Center, "Day", UITheme.TextRole.Body);
            d.rectTransform.anchorMin = Vector2.zero; d.rectTransform.anchorMax = new Vector2(1f, 0f); d.rectTransform.pivot = new Vector2(.5f, 0f);
            d.rectTransform.anchoredPosition = new Vector2(0f, 6f); d.rectTransform.sizeDelta = new Vector2(0f, 20f);
        }

        public void Evaluate(float t)
        {
            bool reduce = UIMotionSettings.reduceMotion;
            float walk = Mathf.Clamp01(t / WalkTime);
            var pos = Vector3.Lerp(walkFrom, walkTo, walk);
            player.SetPositionAndRotation(pos, facing);
            if (mover != null && mover.figure != null) mover.figure.Speed = walk < 1f ? WalkSpeed : 0f;

            if (cam != null && follow != null)
            {
                // from low in front of the figure round to the HQ camera's place behind it
                float k = UIEase.Tactical(Mathf.Clamp01(t / Total));
                float yaw = follow.yaw + Mathf.Lerp(150f, 0f, k), pitch = Mathf.Lerp(4f, follow.pitch, k), dist = Mathf.Lerp(3f, follow.distance, k);
                var rot = Quaternion.Euler(pitch, yaw, 0f);
                var focus = pos + Vector3.up * follow.height;
                cam.SetPositionAndRotation(focus + rot * Vector3.back * dist, rot);
            }

            if (band == null) return;
            float a = t < BandFrom ? 0f : t < BandOut ? (reduce ? 1f : UIEase.Tactical(Mathf.Clamp01((t - BandFrom) / BandIn))) : 1f - Mathf.Clamp01((t - BandOut) / (BandEnd - BandOut));
            if (bandGroup.alpha != a) bandGroup.alpha = a;
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            var kb = Keyboard.current; var mouse = Mouse.current;
            bool skip = clock > .3f && ((kb != null && kb.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame));
            if (skip) clock = Total;
            Evaluate(clock);
            if (clock < Total) return;
            running = false;
            if (mover != null && mover.figure != null) mover.figure.Speed = 0f;
            if (cc != null) cc.enabled = true;
            if (mover != null) mover.enabled = true;
            if (follow != null) follow.enabled = true;
            if (overlay != null) Destroy(overlay.gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { pending = false; }
    }
}
