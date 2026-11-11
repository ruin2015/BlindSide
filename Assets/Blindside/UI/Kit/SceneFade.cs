// A plain fade to black across a scene change, on its own Screen Space Overlay canvas that survives the load (order 32000, under the
// wipe). Out(seconds, onBlack) darkens the screen and runs onBlack once it is black; the next scene lifts it with In(seconds). A scene
// that says nothing (HoldForNextScene off) is lifted on arrival, and so is a screen that didn't change scene at all, so it can never stay
// black. One unscaled clock (ITimedEffect.Evaluate(t)); nothing allocated per frame.
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Blindside.UI
{
    public class SceneFade : MonoBehaviour, ITimedEffect
    {
        public const float DefaultOut = .6f, DefaultIn = .7f;
        const int Order = 32000;

        static SceneFade instance;
        /// <summary>The next scene lifts the fade itself (its own entrance).</summary>
        public static bool HoldForNextScene;

        Image layer;
        Canvas canvas;
        float clock, duration, from, to;
        bool running;
        System.Action done;

        public float Duration => duration;
        public static bool Black => instance != null && instance.layer.color.a >= .999f;

        /// <summary>Darkens the screen over `seconds`; `onBlack` runs once it is black.</summary>
        public static void Out(float seconds, System.Action onBlack)
        {
            var f = Instance;
            f.done = () =>
            {
                onBlack?.Invoke();
                if (!Blindside.Flow.SceneFlow.Leaving) In(DefaultIn);   // (no scene change after all: nothing else would lift it)
            };
            f.Run(f.layer.color.a, 1f, seconds);
        }

        /// <summary>Lifts the fade over `seconds`.</summary>
        public static void In(float seconds)
        {
            if (instance == null) return;
            instance.done = null;
            instance.Run(instance.layer.color.a, 0f, seconds);
        }

        /// <summary>Black at once (a scene that opens from black).</summary>
        public static void SetBlack()
        {
            var f = Instance;
            f.done = null; f.running = false;
            f.from = f.to = 1f; f.Evaluate(0f);
        }

        static SceneFade Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("SceneFade", typeof(RectTransform));
                DontDestroyOnLoad(go);
                var f = go.AddComponent<SceneFade>();
                f.canvas = go.AddComponent<Canvas>();
                f.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                f.canvas.sortingOrder = Order;
                var rt = HudKit.NewRect("DecoFade", go.transform);
                HudKit.Stretch(rt);
                f.layer = HudKit.AddImage(rt, new Color(0f, 0f, 0f, 0f));
                f.canvas.enabled = false;
                SceneManager.sceneLoaded += f.OnLoaded;
                instance = f;
                return f;
            }
        }

        void OnLoaded(Scene s, LoadSceneMode m)
        {
            if (HoldForNextScene) { HoldForNextScene = false; return; }
            if (layer.color.a > 0f) In(DefaultIn);
        }

        void OnDestroy() { SceneManager.sceneLoaded -= OnLoaded; if (instance == this) instance = null; }

        void Run(float a, float b, float seconds)
        {
            from = a; to = b; duration = Mathf.Max(.01f, seconds); clock = 0f; running = true;
            Evaluate(0f);
        }

        public void Evaluate(float t)
        {
            float p = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            if (UIMotionSettings.reduceMotion) p = 1f;
            float a = Mathf.Lerp(from, to, p);
            var c = layer.color;
            if (c.a != a) { c.a = a; layer.color = c; }
            bool show = a > 0f;
            if (canvas.enabled != show) canvas.enabled = show;
        }

        void Update()
        {
            if (!running) return;
            clock += Time.unscaledDeltaTime;
            Evaluate(clock);
            if (clock < duration && !UIMotionSettings.reduceMotion) return;
            running = false;
            var d = done; done = null;
            d?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; HoldForNextScene = false; }
    }
}
