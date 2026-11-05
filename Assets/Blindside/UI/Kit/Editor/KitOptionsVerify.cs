// Blindside > UI > Verify Options: edit-mode check that the motion options are saved to PlayerPrefs and read back.
//   1. defaults: with the keys removed, Load() gives reduceMotion off, screenFx on, level 中 (1)
//   2. save → load: sets reduceMotion on / screenFx off / level 強 (2) through the Set… methods, resets the statics, Load(), compares
//   3. level → scale: 弱 / 中 / 強 = 0.5 / 1.0 / 1.5
//   4. keys: the three "blindside.ui." keys exist after saving and are gone after DeleteSaved()
// The player's own saved values and the statics are put back afterwards.
using UnityEditor;
using UnityEngine;
using Blindside.UI;

namespace Blindside.UITools.EditorTools
{
    public static class KitOptionsVerify
    {
        [MenuItem("Blindside/UI/Verify Options")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[VerifyOptions] edit mode only."); return; }
            string[] keys = { UIMotionSettings.KeyReduce, UIMotionSettings.KeyScreenFx, UIMotionSettings.KeyLevel };
            var had = new bool[3]; var old = new int[3];
            for (int i = 0; i < 3; i++) { had[i] = PlayerPrefs.HasKey(keys[i]); old[i] = PlayerPrefs.GetInt(keys[i], 0); }
            bool r0 = UIMotionSettings.reduceMotion, f0 = UIMotionSettings.screenFx; int l0 = UIMotionSettings.effectLevel;
            int fail = 0;
            void Check(string name, bool ok) { Debug.Log($"[VerifyOptions] {(ok ? "PASS" : "FAIL")}  {name}"); if (!ok) fail++; }
            try
            {
                UIMotionSettings.DeleteSaved();
                UIMotionSettings.reduceMotion = true; UIMotionSettings.screenFx = false; UIMotionSettings.effectLevel = 2;     // dirty statics
                UIMotionSettings.Load();
                Check("defaults (reduce off, screenFx on, level 中)", !UIMotionSettings.reduceMotion && UIMotionSettings.screenFx && UIMotionSettings.effectLevel == 1);

                UIMotionSettings.SetReduceMotion(true);
                UIMotionSettings.SetScreenFx(false);
                UIMotionSettings.SetEffectLevel(2);
                Check("keys written", PlayerPrefs.HasKey(keys[0]) && PlayerPrefs.HasKey(keys[1]) && PlayerPrefs.HasKey(keys[2]));
                Check("key names start with blindside.ui.", keys[0].StartsWith("blindside.ui.") && keys[1].StartsWith("blindside.ui.") && keys[2].StartsWith("blindside.ui."));

                UIMotionSettings.reduceMotion = false; UIMotionSettings.screenFx = true; UIMotionSettings.effectLevel = 1;     // forget
                UIMotionSettings.Load();
                Check("save → load (reduce on, screenFx off, level 強)", UIMotionSettings.reduceMotion && !UIMotionSettings.screenFx && UIMotionSettings.effectLevel == 2);

                UIMotionSettings.effectLevel = 0; bool a = Mathf.Approximately(UIMotionSettings.EffectScale, .5f);
                UIMotionSettings.effectLevel = 1; bool b = Mathf.Approximately(UIMotionSettings.EffectScale, 1f);
                UIMotionSettings.effectLevel = 2; bool c = Mathf.Approximately(UIMotionSettings.EffectScale, 1.5f);
                Check("level → scale 0.5 / 1.0 / 1.5", a && b && c);

                UIMotionSettings.DeleteSaved();
                Check("keys deleted", !PlayerPrefs.HasKey(keys[0]) && !PlayerPrefs.HasKey(keys[1]) && !PlayerPrefs.HasKey(keys[2]));
            }
            finally
            {
                for (int i = 0; i < 3; i++) { if (had[i]) PlayerPrefs.SetInt(keys[i], old[i]); else PlayerPrefs.DeleteKey(keys[i]); }
                PlayerPrefs.Save();
                UIMotionSettings.reduceMotion = r0; UIMotionSettings.screenFx = f0; UIMotionSettings.effectLevel = l0;
            }
            Debug.Log(fail == 0 ? "[VerifyOptions] ALL PASS" : $"[VerifyOptions] {fail} FAILED");
        }
    }
}
