using System.Collections.Generic;
using UnityEngine;

// 0x72 原始图集使用左上角坐标；Unity Sprite 使用左下角坐标。
public static class DungeonArt
{
    private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Sprite[]> Animations = new Dictionary<string, Sprite[]>();
    private static Texture2D _atlas;
    private static Material _spriteMaterial;
    public static Texture2D Atlas => _atlas != null ? _atlas : LoadAtlas();
    public static Material SpriteMaterial => _spriteMaterial != null ? _spriteMaterial :
        (_spriteMaterial = Resources.Load<Material>("DungeonArt/SpriteMaterial"));

    private static Texture2D LoadAtlas()
    {
        _atlas = Resources.Load<Texture2D>("DungeonArt/DungeonTileset");
        var tiles = Resources.Load<TextAsset>("DungeonArt/Tiles");
        if (_atlas == null || tiles == null) return null;
        _atlas.filterMode = FilterMode.Point;
        foreach (string line in tiles.text.Split('\n'))
        {
            string[] fields = line.Trim().Split(' ');
            if (fields.Length != 5) continue;
            int x = int.Parse(fields[1]);
            int y = int.Parse(fields[2]);
            int width = int.Parse(fields[3]);
            int height = int.Parse(fields[4]);
            var sprite = Sprite.Create(_atlas, new Rect(x, _atlas.height - y - height, width, height),
                new Vector2(0.5f, 0f), 16f, 0, SpriteMeshType.FullRect);
            sprite.name = fields[0];
            Sprites[fields[0]] = sprite;
        }
        return _atlas;
    }

    public static Sprite GetSprite(string name)
    {
        if (Atlas == null) return null;
        return Sprites.TryGetValue(name, out var sprite) ? sprite : null;
    }

    public static Sprite[] GetAnimation(string prefix)
    {
        if (Animations.TryGetValue(prefix, out var frames)) return frames;
        var list = new List<Sprite>();
        for (int i = 0; i < 4; i++)
        {
            var sprite = GetSprite(prefix + i);
            if (sprite != null) list.Add(sprite);
        }
        frames = list.ToArray();
        Animations[prefix] = frames;
        return frames;
    }

    public static bool CreateActor(Transform parent, string idle, string run, float scale, Color tint,
        float footOffset = -0.9f)
    {
        if (GetAnimation(idle).Length == 0) return false;
        var visual = parent.Find("DungeonVisual");
        if (visual == null)
        {
            var obj = new GameObject("DungeonVisual");
            obj.transform.SetParent(parent, false);
            visual = obj.transform;
            obj.AddComponent<DungeonSpriteVisual>();
        }
        visual.GetComponent<DungeonSpriteVisual>().Configure(idle, run, scale, tint, footOffset);
        return true;
    }

    public static bool CreateEnemy(Enemy enemy)
    {
        float scale = Mathf.Max(0.8f, enemy.EnemyDef.MeshScale);
        switch (enemy.EnemyDef.Type)
        {
            case EEnemyType.Slime:
                return CreateActor(enemy.transform, "tiny_slug_anim_f", "tiny_slug_anim_f", scale * 1.25f, Color.white);
            case EEnemyType.Skeleton:
                return CreateActor(enemy.transform, "skelet_idle_anim_f", "skelet_run_anim_f", scale * 1.5f, Color.white);
            case EEnemyType.Bat:
                return CreateActor(enemy.transform, "imp_idle_anim_f", "imp_run_anim_f", scale * 1.1f,
                    new Color(0.75f, 0.65f, 1f), -0.5f);
            case EEnemyType.ShadowMage:
                return CreateActor(enemy.transform, "necromancer_anim_f", "necromancer_anim_f", scale * 1.2f, Color.white);
            case EEnemyType.Ghost:
                return CreateActor(enemy.transform, "angel_idle_anim_f", "angel_run_anim_f", scale * 1.4f,
                    new Color(0.65f, 0.9f, 1f, 0.8f), -0.6f);
            default: return false;
        }
    }
}
