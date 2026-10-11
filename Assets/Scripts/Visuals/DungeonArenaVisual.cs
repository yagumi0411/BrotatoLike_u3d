using System.Collections.Generic;
using UnityEngine;

public class DungeonArenaVisual : MonoBehaviour
{
    private Mesh _mesh;

    private void Start()
    {
        if (DungeonArt.Atlas == null) return;
        var ground = GameObject.Find("Ground");
        if (ground != null && ground.TryGetComponent<Renderer>(out var renderer)) renderer.enabled = false;
        float radius = GameManager.Instance?.MonsterSpawner?.ArenaRadius ?? 28f;
        BuildFloor(radius);
        // 装饰放在刷怪区域外，不新增碰撞或改变竞技场规则。
        for (int i = 0; i < 24; i++)
        {
            float angle = i * Mathf.PI * 2f / 24f;
            var column = new GameObject("DungeonColumn");
            column.transform.SetParent(transform, false);
            column.transform.position = new Vector3(Mathf.Cos(angle) * (radius + 1.8f), 0.05f,
                Mathf.Sin(angle) * (radius + 1.8f));
            var visual = new GameObject("ColumnSprite");
            visual.transform.SetParent(column.transform, false);
            var spriteRenderer = visual.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = DungeonArt.GetSprite("column");
            spriteRenderer.sharedMaterial = DungeonArt.SpriteMaterial;
            visual.transform.rotation = Camera.main != null ? Camera.main.transform.rotation : Quaternion.Euler(60f, 0f, 0f);
            spriteRenderer.sortingOrder = 1000 - Mathf.RoundToInt(column.transform.position.z * 20f);
        }
    }

    private void BuildFloor(float radius)
    {
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();
        const float tileSize = 1.5f;
        int extent = Mathf.CeilToInt((radius + 3f) / tileSize);
        var random = new System.Random(72); // 不消耗游戏随机数，保证美术不改变升级/刷怪结果。
        for (int z = -extent; z <= extent; z++)
        for (int x = -extent; x <= extent; x++)
        {
            float distance = new Vector2(x * tileSize, z * tileSize).magnitude;
            if (distance > radius + 2f) continue;
            string tile = distance > radius ? "wall_mid" : "floor_" + (random.Next(100) < 75 ? 1 : random.Next(2, 9));
            Rect rect = DungeonArt.GetSprite(tile).rect;
            float left = (rect.xMin + 0.05f) / DungeonArt.Atlas.width;
            float right = (rect.xMax - 0.05f) / DungeonArt.Atlas.width;
            float bottom = (rect.yMin + 0.05f) / DungeonArt.Atlas.height;
            float top = (rect.yMax - 0.05f) / DungeonArt.Atlas.height;
            int start = vertices.Count;
            float px = (x - 0.5f) * tileSize;
            float pz = (z - 0.5f) * tileSize;
            vertices.Add(new Vector3(px, 0.015f, pz));
            vertices.Add(new Vector3(px, 0.015f, pz + tileSize));
            vertices.Add(new Vector3(px + tileSize, 0.015f, pz + tileSize));
            vertices.Add(new Vector3(px + tileSize, 0.015f, pz));
            uvs.Add(new Vector2(left, bottom)); uvs.Add(new Vector2(left, top));
            uvs.Add(new Vector2(right, top)); uvs.Add(new Vector2(right, bottom));
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
        }
        var floor = new GameObject("DungeonFloor");
        floor.transform.SetParent(transform, false);
        _mesh = new Mesh { name = "DungeonFloorMesh" };
        _mesh.SetVertices(vertices); _mesh.SetUVs(0, uvs); _mesh.SetTriangles(indices, 0);
        _mesh.RecalculateBounds();
        floor.AddComponent<MeshFilter>().sharedMesh = _mesh;
        floor.AddComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("DungeonArt/FloorMaterial");
    }

    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
    }
}
