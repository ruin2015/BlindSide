// What the hostage looks like (Resources/HostageLook.asset): the model, its sitting and walking clips (played by HostageAnimator),
// and the profile that sizes the model — its clip slots stay empty, so no UnitAnimator is built for the hostage.
using UnityEngine;

namespace Blindside.Rendering
{
    [CreateAssetMenu(fileName = "HostageLook", menuName = "Blindside/Hostage Look")]
    public class HostageLook : ScriptableObject
    {
        public GameObject model;
        public AnimationClip sit, walk;
        [Tooltip("Sizes the model (modelHeight); its clip slots stay empty.")]
        public UnitAnimationProfile profile;
        [Min(.05f)] public float walkClipSpeed = 1.1f;
        [Range(0f, 1f), Tooltip("Where in the walk cycle the feet pass each other (the standing pose while escorted).")]
        public float standPose = .19f;   // (HostageWalking: both feet down and side by side here, measured from the clip)

        const string ResourceName = "HostageLook";
        static HostageLook loaded;
        static bool tried;
        /// <summary>Resources/HostageLook.asset, or null (the hostage keeps the unit prefab's look).</summary>
        public static HostageLook Current
        {
            get
            {
                if (!tried) { loaded = Resources.Load<HostageLook>(ResourceName); tried = true; }
                return loaded;
            }
        }
    }
}
