// The intro of a map without the house (MapData.useHouseIntro false). The house intro is a Timeline built around the default
// map's house — its orbit camera, the House_Exterior shell, the helicopter path to its garage — so a map that has no house
// gets this short one instead, about 7 seconds and skippable (any key or a click):
//   letterbox in → a glitch title with the date and "OPERATION : {map name}" → the camera comes down from high above the
//   middle of the map to the game's own height → the helicopter enters from off-screen, heads for the middle of the map and
//   stops there (where the deploy phase picks it up: HeliDeployRig.Begin) → fade out, the match starts, fade in.
// It drives the scene's existing pieces (letterbox bars, the OperationTitle glitch text, the Fade overlay, the HeliRig, the
// battle camera) directly and never plays the Timeline, so House_Exterior stays hidden. The world under it is bound to the
// map first (floor size, wall blocks), since normally that happens only when the match starts.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Blindside.Map;
using Blindside.Rendering;
using Blindside.UI;

namespace Blindside.Cinematic
{
    public class SimpleIntro : MonoBehaviour
    {
        public const float Total = 7f;               // seconds, skipping excluded
        const float FadeInEnd = .8f, TitleAt = .5f, TitleOutAt = 3.1f, HeliAt = 3.2f, HeliEnd = 6.2f, CamEnd = 6.2f, FadeOutAt = 6.4f;
        const float HighH = 78f;                     // the camera's height at the start of the shot (StartH = the game's own)
        const float HeliHoverZ = 6f, HeliStartZ = 17f;

        public bool Playing { get; private set; }
        bool skip;

        /// <summary>Plays the intro for the current map and calls `startMatch` once the screen is black (the match's own
        /// set-up runs behind it); the intro then fades back in over the running match.</summary>
        public static SimpleIntro Play(Action startMatch)
        {
            var go = new GameObject("SimpleIntro");
            var si = go.AddComponent<SimpleIntro>();
            si.StartCoroutine(si.Run(startMatch));
            return si;
        }

        /// <summary>Jump to the fade-out.</summary>
        public void Skip() => skip = true;

        static float Smooth(float p) { p = Mathf.Clamp01(p); return p * p * (3f - 2f * p); }

        IEnumerator Run(Action startMatch)
        {
            Playing = true;
            var data = MapLibrary.Current;
            var seq = FindFirstObjectByType<IntroSequence>(FindObjectsInactive.Include);
            var letterbox = FindFirstObjectByType<LetterboxBars>(FindObjectsInactive.Include);
            var rigs = FindFirstObjectByType<IntroCameraRig>(FindObjectsInactive.Include);
            var fade = seq != null ? seq.fadeGroup : null;
            var titleText = seq != null ? seq.titleText : null;
            var glitch = seq != null ? seq.titleGlitch : null;
            var titleGroup = seq != null ? seq.titleGroup : null;
            Transform heli = rigs != null ? rigs.heliTarget : null;
            var heliRig = rigs != null ? rigs.heliRig : null;

            // The world the shot flies over: this map's floor and walls (the house shell is never enabled here).
            MapFloorSync.Apply(data);
            MapWallSync.Bind(MapGrid.CreateDefault());
            var shell = GameObject.Find("House_Exterior");
            if (shell != null) shell.SetActive(false);

            var center = data.MapCenter;
            if (fade != null) fade.alpha = 1f;
            var cam = BattlefieldCamera.Activate(center.x, center.y);
            if (cam != null) cam.h = HighH;
            BattlefieldCamera.InputLocked = true;
            if (heli != null) heli.gameObject.SetActive(false);

            // the title: date line + operation line, in the scene's glitch text (restored afterwards).
            string oldText = titleText != null ? titleText.text : null;
            var titleRt = titleText != null ? titleText.rectTransform : null;
            Vector2 oldSize = titleRt != null ? titleRt.sizeDelta : Vector2.zero;
            if (titleText != null)
            {
                var d = DateTime.Now;
                titleText.text = $"{d:yyyy.MM.dd} {d.DayOfWeek.ToString().Substring(0, 3).ToUpperInvariant()}\nOPERATION : {data.name}";
                titleRt.sizeDelta = new Vector2(oldSize.x, Mathf.Max(oldSize.y, 150f));
            }
            if (titleGroup != null) titleGroup.alpha = 0f;

            // the helicopter comes in along the same bearing HeliDeployRig.Begin assumes, so the hand-over doesn't turn it.
            float pAt = 4200f / 11500f, e0 = pAt * pAt * (3f - 2f * pAt), bearing = 1f + e0 * .7f;
            float reach = Mathf.Max(data.width, data.height) * .9f + 12f;
            Vector2 dir = new Vector2(Mathf.Cos(bearing), Mathf.Sin(bearing));
            float hd = Mathf.Atan2(-dir.y, -dir.x);

            Vector3 glitchPos = glitch != null ? glitch.transform.localPosition : default, glitchScale = glitch != null ? glitch.transform.localScale : default;
            Quaternion glitchRot = glitch != null ? glitch.transform.localRotation : default;
            Coroutine titleRoutine = null;
            float t = 0f; bool titleIn = false, titleOut = false;
            float skipArmed = .3f;   // the click that pressed START must not count
            while (t < FadeOutAt && !skip)
            {
                t += Time.unscaledDeltaTime;
                if (t > skipArmed && SkipPressed()) { skip = true; break; }

                if (fade != null) fade.alpha = 1f - Smooth(t / FadeInEnd);
                if (letterbox != null) letterbox.openAmount = Smooth(t / .7f);
                if (cam != null) cam.h = Mathf.Lerp(HighH, BattlefieldCamera.StartH, Smooth(t / CamEnd));

                if (!titleIn && t >= TitleAt && glitch != null) { titleIn = true; titleRoutine = StartCoroutine(glitch.PlayIn(.9f)); }
                if (titleIn && !titleOut && t >= TitleOutAt && glitch != null) { titleOut = true; if (titleRoutine != null) StopCoroutine(titleRoutine); titleRoutine = StartCoroutine(glitch.PlayOut(.7f)); }

                if (heli != null && t >= HeliAt)
                {
                    if (!heli.gameObject.activeSelf) heli.gameObject.SetActive(true);
                    float q0 = Mathf.Clamp01((t - HeliAt) / (HeliEnd - HeliAt)), q = 1f - Mathf.Pow(1f - q0, 2.2f);
                    var pos = center + dir * reach * (1f - q);
                    heli.position = GridSpace.GridToWorld(pos.x, pos.y, Mathf.Lerp(HeliStartZ, HeliHoverZ, q));
                    heli.rotation = Quaternion.LookRotation(GridSpace.FacingToWorldDirection(hd), Vector3.up);
                    if (heliRig != null) heliRig.tilt = -.32f * Mathf.Pow(1f - q, 1.2f);
                }
                yield return null;
            }

            // out: to black (and the letterbox away), start the match behind it, then in again.
            float from = fade != null ? fade.alpha : 0f, ft = 0f, dur = skip ? .35f : (Total - FadeOutAt);
            float lbFrom = letterbox != null ? letterbox.openAmount : 0f;
            while (ft < dur)
            {
                ft += Time.unscaledDeltaTime;
                float k = Smooth(ft / dur);
                if (fade != null) fade.alpha = Mathf.Lerp(from, 1f, k);
                if (letterbox != null) letterbox.openAmount = Mathf.Lerp(lbFrom, 0f, k);
                yield return null;
            }
            if (fade != null) fade.alpha = 1f;
            if (letterbox != null) letterbox.openAmount = 0f;
            if (titleRoutine != null) StopCoroutine(titleRoutine);
            if (glitch != null) { glitch.transform.localPosition = glitchPos; glitch.transform.localScale = glitchScale; glitch.transform.localRotation = glitchRot; }
            if (titleGroup != null) titleGroup.alpha = 0f;
            if (titleText != null) { titleText.text = oldText; titleRt.sizeDelta = oldSize; }
            BattlefieldCamera.InputLocked = false;

            startMatch?.Invoke();
            yield return null;   // the match's set-up (RunOneMatch) runs its first steps behind the black
            yield return new WaitForSecondsRealtime(.15f);
            float it = 0f;
            while (it < .6f)
            {
                it += Time.unscaledDeltaTime;
                if (fade != null) fade.alpha = 1f - Smooth(it / .6f);
                yield return null;
            }
            if (fade != null) fade.alpha = 0f;
            Playing = false;
            Destroy(gameObject);
        }

        static bool SkipPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame && !kb.escapeKey.wasPressedThisFrame) return true;   // (ESC is the game menu's)
            var mouse = Mouse.current;
            return mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
        }
    }
}
