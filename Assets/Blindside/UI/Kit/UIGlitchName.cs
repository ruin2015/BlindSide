// The name text's part of the card glitch (spec "이름 글자"), as ITimedEffect.Evaluate(t) on the same 0.8s clock as UIGlitchImage's
// CardEnter: hidden (x−30) up to 14%, in from the left at 14%, x+8 with the RGB split ±3 at 18%… until 30%, x−4 with the opposite
// split until 46%, then normal. The red / blue copies are UIGlitchText's; this component poses only their split
// (UIGlitchText.SetSplit) and switches that component's own random loop off (driven). Steps only — no interpolation.
using TMPro;
using UnityEngine;

namespace Blindside.UI
{
    [AddComponentMenu("Blindside/UI/Glitch Name")]
    [RequireComponent(typeof(TMP_Text))]
    public class UIGlitchName : MonoBehaviour, ITimedEffect
    {
        public const float CardDuration = 0.8f;

        struct Key
        {
            public float t, x, split, alpha;
            public Key(float t, float x, float split, float alpha) { this.t = t; this.x = x; this.split = split; this.alpha = alpha; }
        }

        static readonly Key[] Keys =
        {
            new Key(0f, -30f, 0f, 0f),      // hidden
            new Key(.14f, -30f, 0f, 1f),    // in from the left
            new Key(.18f, 8f, 3f, 1f),      // x+8, red −3 / blue +3
            new Key(.30f, -4f, -3f, 1f),    // x−4, the other way round
            new Key(.46f, 0f, 0f, 1f),      // normal
        };

        public UIGlitchText glitch;
        CanvasGroup group;
        RectTransform rt;
        Vector2 baseAnchored;
        bool inited;
        float clock;
        bool running;

        public float Duration => CardDuration;

        void Init()
        {
            if (inited) return;
            inited = true;
            rt = (RectTransform)transform;
            baseAnchored = rt.anchoredPosition;
            if (glitch == null) glitch = UIUtil.GetOrAdd<UIGlitchText>(gameObject);
            glitch.loop = false; glitch.driven = true;
            group = UIUtil.GetOrAdd<CanvasGroup>(gameObject);
        }

        /// <summary>Poses the name `t` seconds into the card glitch.</summary>
        public void Evaluate(float t)
        {
            Init();
            float x = 0f, split = 0f, alpha = 1f;
            if (t < Duration)
            {
                float p = t / CardDuration;
                int k = 0;
                for (int i = Keys.Length - 1; i >= 0; i--) if (p >= Keys[i].t) { k = i; break; }
                x = Keys[k].x; split = Keys[k].split; alpha = Keys[k].alpha;
                if (UIMotionSettings.reduceMotion) { x = 0f; split = 0f; }
                else { x *= UIMotionSettings.EffectScale; split *= UIMotionSettings.EffectScale; }
            }
            rt.anchoredPosition = baseAnchored + new Vector2(x, 0f);
            group.alpha = alpha;
            glitch.SetSplit(split);
        }

        public void Play()
        {
            Init();
            clock = 0f; running = true;
            Evaluate(0f);
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock >= Duration) running = false;
        }
    }
}
