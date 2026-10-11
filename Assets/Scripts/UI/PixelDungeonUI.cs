using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Kenney UI Pack - Pixel Adventure (CC0). Keep the pixel corners intact when resizing panels.
public static class PixelDungeonUI
{
    private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

    public static Image Surface(GameObject target, string name, Color tint)
    {
        var image = target.GetComponent<Image>();
        if (image == null) image = target.AddComponent<Image>();
        if (!Sprites.TryGetValue(name, out var sprite))
        {
            var texture = Resources.Load<Texture2D>("PixelDungeonUI/" + name);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Point;
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
                Sprites[name] = sprite;
            }
        }
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 0.5f;
        image.color = tint;
        // These two source tiles are hollow frames. Give them an opaque inset so
        // the arena and the neighboring item's text cannot show through.
        if (name == "Panel" || name == "Slot")
        {
            var fill = target.transform.Find("SurfaceFill") as RectTransform;
            if (fill == null)
            {
                var obj = new GameObject("SurfaceFill", typeof(RectTransform), typeof(Image));
                obj.transform.SetParent(target.transform, false);
                fill = obj.GetComponent<RectTransform>();
                fill.SetAsFirstSibling();
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = Vector2.one;
                fill.offsetMin = new Vector2(12, 12);
                fill.offsetMax = new Vector2(-12, -12);
            }
            var background = fill.GetComponent<Image>();
            background.color = name == "Panel" ? new Color(0.13f, 0.115f, 0.1f) : new Color(0.065f, 0.055f, 0.045f);
            background.raycastTarget = false;
        }
        return image;
    }
}
