// The sky dome: a big inside-out sphere (Art/Shaders/SkyDome.shader) centred on whichever camera is about to draw, so the
// sky never gets nearer or clips, whatever the camera does. Its sun follows the scene's directional light. Runs in the
// editor too (the Scene / Game views show the sky). No allocations per frame.
using UnityEngine;
using UnityEngine.Rendering;

namespace Blindside.Rendering
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class SkyDome : MonoBehaviour
    {
        [Tooltip("The light the sun sits behind. Empty: RenderSettings.sun.")]
        public Light sun;
        [Tooltip("Dome radius, metres (kept inside each camera's far plane).")]
        public float radius = 900f;

        static readonly int SunDirId = Shader.PropertyToID("_SunDir");
        MeshRenderer rend;
        MaterialPropertyBlock block;

        void OnEnable()
        {
            rend = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
            RenderPipelineManager.beginCameraRendering += Follow;
        }

        void OnDisable() => RenderPipelineManager.beginCameraRendering -= Follow;

        void Follow(ScriptableRenderContext context, Camera cam)
        {
            if (cam == null || rend == null) return;
            float r = Mathf.Min(radius, cam.farClipPlane * .9f);
            transform.SetPositionAndRotation(cam.transform.position, Quaternion.identity);
            transform.localScale = Vector3.one * (r * 2f);   // the primitive sphere is 1 unit across
            var light = sun != null ? sun : RenderSettings.sun;
            if (light == null) return;
            rend.GetPropertyBlock(block);
            block.SetVector(SunDirId, -light.transform.forward);
            rend.SetPropertyBlock(block);
        }
    }
}
