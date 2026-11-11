// A unit's light in a night operation (MissionAtmosphere puts one on every unit's view during the match): the player's own operators
// carry a weapon light — a white spot aimed where they face — and a soft glow round them, so where each one stands reads at a glance;
// an enemy, only while it is shown, a faint red glow. Both go out with the unit (dead, or hidden: UnitView.Hidden). The lights are
// switched, not rebuilt, so nothing is allocated per frame.
using UnityEngine;

namespace Blindside.Rendering
{
    public class UnitNightLight : MonoBehaviour
    {
        static readonly Color AllyGlow = new Color(.75f, .88f, 1f), FoeGlow = new Color(1f, .25f, .2f), Torch = new Color(1f, .97f, .9f);

        UnitView view;
        System.Func<bool> alive;
        Light glow, torch;
        bool ally;

        public void Setup(UnitView v, bool isAlly, System.Func<bool> isAlive)
        {
            view = v; ally = isAlly; alive = isAlive;
            if (glow == null)
            {
                glow = NewLight("NightGlow", LightType.Point, new Vector3(0f, 1.6f, 0f), Quaternion.identity);
                glow.range = ally ? 3.2f : 2.2f;
                glow.intensity = ally ? .9f : .7f;
                glow.color = ally ? AllyGlow : FoeGlow;
            }
            if (ally && torch == null)
            {
                torch = NewLight("WeaponLight", LightType.Spot, new Vector3(0f, 1.2f, .3f), Quaternion.Euler(14f, 0f, 0f));
                torch.range = 6.5f; torch.spotAngle = 52f; torch.innerSpotAngle = 20f;
                torch.intensity = 2.5f;   // (soft: enough to find the operator, not to flood the room)
                torch.color = Torch;
            }
        }

        Light NewLight(string name, LightType type, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(view.ModelRoot != null ? view.ModelRoot : view.transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var l = go.AddComponent<Light>();
            l.type = type;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            return l;
        }

        void LateUpdate()
        {
            if (view != null && glow == null) Setup(view, ally, alive);   // (the model was swapped and took the lights with it)
            bool on = view != null && !view.Hidden && (alive == null || alive());
            if (glow != null && glow.enabled != on) glow.enabled = on;
            if (torch != null && torch.enabled != on) torch.enabled = on;
        }

        void OnDestroy()
        {
            if (glow != null) Destroy(glow.gameObject);
            if (torch != null) Destroy(torch.gameObject);
        }
    }
}
