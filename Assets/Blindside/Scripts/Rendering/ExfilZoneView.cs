// Draws every ExfilZone: the yellow hazard-striped floor (drawFloor()'s `t==='extract'` branch, 03_renderer_threejs.js:67/71)
// and the "EXFIL X" label (drawFloor()'s fillText('EXFIL',…), #ffd21f 700 26px, centred on the zone).
// The scene bakes one striped plane, Blindside_MapRoot/Floor/Floor_Extract (Mat_Extract, hazard_stripe, one stripe tile per
// cell); it is reused as the template — repositioned onto zone A and cloned for the others — so all zones read the one
// ExfilZone.All list. Grid → world is x, z=-y (GridSpace); a zone's plane sits at y .02 like the baked one.
// Labels are flat TextMeshPro text on the floor, rotated so that seen from the battle camera (south of the map, looking
// north and down) the letters read upright: text up = +z (grid −y). They sit at y .05, above the fog plane (.025), so they
// stay readable in unseen parts of the map.
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Blindside.Map;
using Blindside.UI;

namespace Blindside.Rendering
{
    public class ExfilZoneView : MonoBehaviour
    {
        const string FloorPath = "Blindside_MapRoot/Floor";
        const string TemplateName = "Floor_Extract";
        const float PlaneY = .02f, LabelY = .05f, PlaneSize = 10f;   // Unity's Plane primitive is 10×10
        static readonly Color LabelColor = new Color32(0xff, 0xd2, 0x1f, 0xff);   // #ffd21f

        readonly List<GameObject> planes = new List<GameObject>();
        readonly List<GameObject> labels = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<ExfilZoneView>(FindObjectsInactive.Include) != null) return;
            new GameObject("ExfilZones").AddComponent<ExfilZoneView>();
        }

        void Start() => Rebuild();

        public void Rebuild()
        {
            foreach (var g in planes) if (g != null && g.name != TemplateName && !g.name.StartsWith(TemplateName + "_A")) Destroy(g);
            foreach (var g in labels) if (g != null) Destroy(g);
            planes.Clear(); labels.Clear();

            var floor = GameObject.Find(FloorPath);
            var template = floor != null ? floor.transform.Find(TemplateName) ?? floor.transform.Find(TemplateName + "_A") : null;
            if (template == null) { Debug.LogWarning("[ExfilZoneView] Blindside_MapRoot/Floor/Floor_Extract not found — no exfil floor drawn."); return; }

            for (int i = 0; i < ExfilZone.All.Count; i++)
            {
                var z = ExfilZone.All[i];
                var go = i == 0 ? template.gameObject : Instantiate(template.gameObject, template.parent);
                go.name = TemplateName + "_" + z.Name;
                var c = z.Center;
                go.transform.position = GridSpace.GridToWorld(c.x, c.y, PlaneY);
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = new Vector3(z.Width / PlaneSize, 1f, z.Height / PlaneSize);
                SetStripeTiling(go, z.Width, z.Height);
                planes.Add(go);
                labels.Add(BuildLabel(z));
            }
        }

        // One stripe tile per cell (the baked plane: tiling 3×2 on a 3×2 zone). Only a zone of another size needs its own material.
        static void SetStripeTiling(GameObject go, int w, int h)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null || mr.sharedMaterial == null) return;
            var m = mr.sharedMaterial;
            if (m.mainTextureScale == new Vector2(w, h)) return;
            m = new Material(m) { name = m.name + " " + w + "x" + h };
            m.mainTextureScale = new Vector2(w, h);
            mr.sharedMaterial = m;
        }

        GameObject BuildLabel(ExfilZone z)
        {
            var go = new GameObject("ExfilLabel_" + z.Name);
            go.transform.SetParent(transform, false);
            var c = z.Center;
            go.transform.position = GridSpace.GridToWorld(c.x, c.y, LabelY);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // lies flat, face up, letter-up = +z
            var t = go.AddComponent<TextMeshPro>();
            t.text = z.Label;
            UIFonts.Auto(t, true);   // display font, no fake bold
            t.fontSize = 5.5f;   // "EXFIL A" spans ~2.3 cells, like the source's 26px label on 48px cells
            t.color = LabelColor;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(z.Width, z.Height);
            return go;
        }
    }
}
