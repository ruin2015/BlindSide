// Fits the scene's baked floor to a map: the outside plane (Blindside_MapRoot/Floor/Floor_Outside) covers the whole grid, the inside
// plane (Floor_Inside) the built-up area (MapData.BuiltBounds), each with one texture tile per cell — the same numbers the scene was
// baked with for the default map (26x20 and x3..22 × y5..17), so the default map is unchanged. The exfil zone floors and labels
// (ExfilZoneView) are rebuilt from the same map, so they follow it too.
// A built-up area that is not a full rectangle (a map of separate rooms with yard between them) can't be one plane: the inside floor
// is then a single mesh of one quad per non-outdoor cell (Floor_InsideCells, the same material, one tile per cell) and the plane is hidden.
// (The wall/crate blocks under Walls are synced by MapWallSync.)
using System.Collections.Generic;
using UnityEngine;
using Blindside.Map;

namespace Blindside.Rendering
{
    public static class MapFloorSync
    {
        const string FloorPath = "Blindside_MapRoot/Floor";
        const string CellsName = "Floor_InsideCells";
        const float PlaneSize = 10f;   // Unity's Plane primitive is 10×10

        /// <summary>Fits the floors and the exfil zones to `map`, which must be the current map (MapLibrary.Current).</summary>
        public static void Apply(MapData map)
        {
            var floor = GameObject.Find(FloorPath);
            if (floor != null)
            {
                Fit(floor.transform.Find("Floor_Outside"), 0, 0, map.width, map.height);
                var built = map.BuiltBounds();
                var inside = floor.transform.Find("Floor_Inside");
                bool rectangle = built.width > 0 && IsFullRectangle(map, built);
                if (inside != null) inside.gameObject.SetActive(rectangle);
                if (rectangle) Fit(inside, built.xMin, built.yMin, built.width, built.height);
                UpdateCells(floor.transform, inside, map, built.width > 0 && !rectangle);
            }
            var exfil = Object.FindAnyObjectByType<ExfilZoneView>(FindObjectsInactive.Include);
            if (exfil != null && ReferenceEquals(map, MapLibrary.Current)) exfil.Rebuild();
        }

        static bool IsFullRectangle(MapData map, RectInt b)
        {
            for (int y = b.yMin; y < b.yMax; y++)
                for (int x = b.xMin; x < b.xMax; x++)
                    if (map.TerrainAt(x, y) == MapTerrain.Out) return false;
            return true;
        }

        // The per-cell inside floor: built once per call for the maps that need it, hidden for the others.
        static void UpdateCells(Transform floorRoot, Transform insidePlane, MapData map, bool needed)
        {
            var existing = floorRoot.Find(CellsName);
            if (!needed) { if (existing != null) existing.gameObject.SetActive(false); return; }
            if (existing == null)
            {
                var go = new GameObject(CellsName);
                go.transform.SetParent(floorRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = new Mesh { name = CellsName };
                go.AddComponent<MeshRenderer>();
                existing = go.transform;
            }
            var mr = existing.GetComponent<MeshRenderer>();
            if (insidePlane != null)
            {
                var src = insidePlane.GetComponent<MeshRenderer>();
                mr.sharedMaterial = BaseMaterial(src.sharedMaterial);
                mr.shadowCastingMode = src.shadowCastingMode; mr.receiveShadows = src.receiveShadows;
            }
            float y = insidePlane != null ? insidePlane.position.y : .01f;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int cy = 0; cy < map.height; cy++)
                for (int cx = 0; cx < map.width; cx++)
                {
                    if (map.TerrainAt(cx, cy) == MapTerrain.Out) continue;
                    int b = v.Count;
                    v.Add(GridSpace.GridToWorld(cx - .5f, cy - .5f, y)); v.Add(GridSpace.GridToWorld(cx + .5f, cy - .5f, y));
                    v.Add(GridSpace.GridToWorld(cx + .5f, cy + .5f, y)); v.Add(GridSpace.GridToWorld(cx - .5f, cy + .5f, y));
                    uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, 0));
                    tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                }
            var mesh = existing.GetComponent<MeshFilter>().sharedMesh;
            mesh.Clear();
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            existing.gameObject.SetActive(true);
        }

        // The plane's material at one texture repeat per cell (the plane itself tiles it w×h times over its size).
        static readonly Dictionary<Material, Material> perCell = new Dictionary<Material, Material>();
        static Material BaseMaterial(Material src)
        {
            if (src == null) return null;
            if (perCell.TryGetValue(src, out var m) && m != null) return m;
            m = new Material(src) { name = src.name + " (per cell)" };
            m.mainTextureScale = Vector2.one;
            perCell[src] = m;
            return m;
        }

        // A plane covering cells x0..x0+w-1 × y0..y0+h-1 (cell centres on integers, so its edges sit half a cell outside them).
        static void Fit(Transform plane, int x0, int y0, int w, int h)
        {
            if (plane == null) return;
            plane.position = GridSpace.GridToWorld(x0 + (w - 1) / 2f, y0 + (h - 1) / 2f, plane.position.y);
            plane.localScale = new Vector3(w / PlaneSize, 1f, h / PlaneSize);
            var mr = plane.GetComponent<MeshRenderer>();
            if (mr == null || mr.sharedMaterial == null || mr.sharedMaterial.mainTextureScale == new Vector2(w, h)) return;
            var m = new Material(mr.sharedMaterial) { name = mr.sharedMaterial.name + " " + w + "x" + h };
            m.mainTextureScale = new Vector2(w, h);
            mr.sharedMaterial = m;
        }
    }
}
