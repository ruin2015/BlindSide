// The autosave notice: after every autosave (SaveSystem.Saved(true)) a small ink chip shows at the bottom right for 2s, over every
// scene (its own DontDestroyOnLoad Screen Space Overlay canvas, menu reference 1440×810, sorting order 32000 — under UIScreenFX /
// KitWipe, no raycaster: it never takes a click).
//   Chip   220×40, ink, slant 10 (small), a 4px blue band on the left; x right 20, y bottom 96 (above the END TURN button and the
//          title's SET / チュートリアル corner)
//     Icon  a 9×9 blue diamond, turning while the chip shows
//     Tag   AUTO SAVE 11 display (blue), Label 保存しました 13 body (white)
// Motion (ITimedEffect.Evaluate): in .25s (fade + 24 from the right, tactical ease), held, out .3s from 1.7s (fade). Update advances an
// unscaled clock only while it plays. With UIMotionSettings.reduceMotion it only fades. No allocation per frame.
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Blindside.Save;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Kit Save Indicator")]
    public class KitSaveIndicator : MonoBehaviour, ITimedEffect
    {
        public const float W = 220f, H = 40f, Right = 20f, Bottom = 96f, InTime = .25f, OutAt = 1.7f, OutTime = .3f, Shift = 24f;
        public const string TagText = "AUTO SAVE", LabelText = "保存しました";

        public static KitSaveIndicator Instance { get; private set; }

        public RectTransform chip, icon;
        public CanvasGroup group;
        float clock; bool running;

        public float Duration => OutAt + OutTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SaveSystem.Saved -= OnSaved;
            SaveSystem.Saved += OnSaved;
        }

        static void OnSaved(bool auto)
        {
            if (!auto || !Application.isPlaying) return;
            if (Instance == null) Install();
            Instance.Play();
        }

        static void Install()
        {
            var go = new GameObject("BlindsideSaveIndicator", typeof(RectTransform));
            DontDestroyOnLoad(go);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 32000;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<UIThemeCanvas>().kind = UITheme.CanvasKind.Menu;
            Instance = BuildInto(go.transform);
        }

        /// <summary>Builds the (hidden) chip under `parent` (a menu-reference canvas); previews pose it with Evaluate.</summary>
        public static KitSaveIndicator BuildInto(Transform parent)
        {
            var theme = UITheme.Current;
            var root = HudKit.NewRect("SaveIndicator", parent);
            HudKit.Stretch(root);
            var v = root.gameObject.AddComponent<KitSaveIndicator>();

            v.chip = HudKit.NewRect("Chip", root);
            v.chip.anchorMin = v.chip.anchorMax = v.chip.pivot = new Vector2(1f, 0f);
            v.chip.anchoredPosition = new Vector2(-Right, Bottom);
            v.chip.sizeDelta = new Vector2(W, H);
            v.group = v.chip.gameObject.AddComponent<CanvasGroup>();
            v.group.blocksRaycasts = false; v.group.interactable = false;
            HudKit.AddImage(v.chip, theme.ink);
            v.chip.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.SmallMin);
            v.chip.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            // the band follows the chip's left edge (the same slant: top [10, 14], bottom [0, 4])
            var band = HudKit.NewRect("DecoBand", v.chip);
            UILayout.Place(band, 0f, 0f, 14f, H);
            HudKit.AddImage(band, theme.blue);
            band.gameObject.AddComponent<SlantedGraphic>().Set(SlantedGraphic.Size.SmallMin);

            // a small blue diamond that turns while the chip shows
            v.icon = HudKit.NewRect("Icon", v.chip);
            v.icon.anchorMin = v.icon.anchorMax = new Vector2(0f, .5f);
            v.icon.pivot = new Vector2(.5f, .5f);
            v.icon.anchoredPosition = new Vector2(28f, 0f);
            v.icon.sizeDelta = new Vector2(9f, 9f);
            HudKit.AddImage(v.icon, theme.blue);

            var tag = HudKit.AddText(v.chip, TagText, 11f, theme.blue, true, TextAlignmentOptions.MidlineLeft, "Tag", UITheme.TextRole.Display);
            tag.characterSpacing = 10f;
            tag.textWrappingMode = TextWrappingModes.NoWrap;
            float tw = Mathf.Ceil(tag.GetPreferredValues(TagText).x) + 1f;
            UILayout.Place(tag.rectTransform, 44f, 0f, tw, H);
            var label = HudKit.AddText(v.chip, LabelText, 13f, Color.white, false, TextAlignmentOptions.MidlineLeft, "Label", UITheme.TextRole.Body);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UILayout.Place(label.rectTransform, 44f + tw + 10f, 0f, W - (44f + tw + 10f) - 14f, H);

            v.Evaluate(v.Duration);            // hidden (the settled end)
            return v;
        }

        /// <summary>Shows the chip from the start (an autosave just happened).</summary>
        public void Play()
        {
            clock = 0f; running = true;
            Evaluate(0f);
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            if (clock >= Duration) { running = false; clock = Duration; }
            Evaluate(clock);
        }

        public void Evaluate(float t)
        {
            if (chip == null) return;
            bool reduce = UIMotionSettings.reduceMotion;
            float e = t <= 0f ? 0f : t >= InTime ? 1f : reduce ? t / InTime : UIEase.Tactical(t / InTime);
            float a = t < OutAt ? e : Mathf.Clamp01(1f - (t - OutAt) / OutTime);
            if (group.alpha != a) group.alpha = a;
            bool show = a > 0f;
            if (chip.gameObject.activeSelf != show) chip.gameObject.SetActive(show);
            var pos = new Vector2(-Right + (reduce ? 0f : Shift * (1f - e)), Bottom);
            if (chip.anchoredPosition != pos) chip.anchoredPosition = pos;
            var rot = Quaternion.Euler(0f, 0f, 45f + (reduce ? 0f : -t * 240f));
            if (icon.localRotation != rot) icon.localRotation = rot;
        }
    }
}
