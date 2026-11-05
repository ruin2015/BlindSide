// Port of caption(tag,main,sub) in blindside_source/05_ui_input_intro.js (~line 228): swaps the
// three text lines and re-triggers a quick show transition (`classList.remove('show'); void
// el.offsetWidth; classList.add('show')` -> here, a fast alpha snap-and-fade-in).
using System.Collections;
using UnityEngine;
using TMPro;

namespace Blindside.Cinematic
{
    public class IntroCaption : MonoBehaviour
    {
        public CanvasGroup group;
        public TMP_Text tagText, mainText, subText;
        const float ShowDuration = 0.18f;

        public void Show(string tag, string main, string sub)
        {
            if (tagText) tagText.text = tag;
            if (mainText) mainText.text = main;
            if (subText) subText.text = sub;
            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }

        public void Hide()
        {
            StopAllCoroutines();
            if (group) group.alpha = 0f;
        }

        IEnumerator FadeIn()
        {
            if (!group) yield break;
            group.alpha = 0f;
            float t = 0f;
            while (t < ShowDuration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(t / ShowDuration);
                yield return null;
            }
            group.alpha = 1f;
        }
    }
}
