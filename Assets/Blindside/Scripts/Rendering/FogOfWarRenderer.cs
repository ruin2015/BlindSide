// Port of the fog-of-war half of syncVision() in blindside_source/03_renderer_threejs.js
// (~lines 162-167): a 26x20 RGBA texture where each grid cell is either fully clear (visible)
// or a dark, semi-opaque navy (`fogData[i]=3,5,9,175`). The source recomputes visibility with
// teamVision(team).all every time G.ver changes; here that "2-stage" union (unit FOV + gadget
// vision) is supplied pre-built by TeamVisionBuilder and simply painted into the texture.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Blindside.Rendering
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FogOfWarRenderer : MonoBehaviour
    {
        static readonly Color32 FogColor = new Color32(3, 5, 9, 175);
        static readonly Color32 ClearColor = new Color32(3, 5, 9, 0);

        // fm.position.y=.02 — just above the floor (lifted to .025: Floor_Extract's plane already sits at
        // .02 here and would z-fight). renderOrder=1 in the source draws it after the opaque
        // map but, through the depth test, never over a figure; UnitView's figures are transparent with
        // ZWrite off here, so the fog goes one step before the Transparent queue instead (still after
        // the opaque walls, which depth-occlude it, and before the reach overlay/vision polygons).
        const float Height = 0.025f;
        const int Queue = (int)RenderQueue.Transparent - 10;

        Texture2D tex;
        Color32[] pixels;
        int width, height;

        /// <summary>Allocates the WxH fog texture and the world-space quad it's painted onto.</summary>
        public void Build(int mapWidth, int mapHeight)
        {
            width = mapWidth; height = mapHeight;
            // fogTex.magFilter=minFilter=THREE.LinearFilter — cell edges fade across the neighbour.
            tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = FogColor;
            tex.SetPixels32(pixels);
            tex.Apply();

            GetComponent<MeshFilter>().sharedMesh = BuildQuadMesh(width, height);
            var mr = GetComponent<MeshRenderer>();
            var mat = MakeUnlitTransparent();
            mat.mainTexture = tex;
            mr.material = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>
        /// Port of the fog write in syncVision(): every cell in `visibleCells` (a grid index,
        /// y*Width+x, as produced by TeamVisionBuilder.BuildVisibleCells) goes fully clear;
        /// everything else is dark fog.
        /// </summary>
        public void Apply(HashSet<int> visibleCells)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int k = y * width + x;
                    pixels[k] = visibleCells.Contains(k) ? ClearColor : FogColor;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
        }

        // Cell (x,y)'s center sits at GridSpace.GridToWorld(x,y) to match cell-instance placement
        // elsewhere in the project, so the quad spans grid [-0.5, W-0.5] x [-0.5, H-0.5].
        static Mesh BuildQuadMesh(int w, int h)
        {
            var mesh = new Mesh { name = "FogOfWarQuad" };
            mesh.vertices = new[]
            {
                GridSpace.GridToWorld(-0.5f, -0.5f, Height),
                GridSpace.GridToWorld(w - 0.5f, -0.5f, Height),
                GridSpace.GridToWorld(-0.5f, h - 0.5f, Height),
                GridSpace.GridToWorld(w - 0.5f, h - 0.5f, Height),
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            // GridToWorld mirrors Z, which flips this quad's winding (a reflection always inverts
            // orientation) — reversed here (0,1,2 / 2,1,3 instead of 0,2,1 / 2,3,1) to keep it facing up.
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Material MakeUnlitTransparent()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            mat.renderQueue = Queue;
            return mat;
        }
    }
}
