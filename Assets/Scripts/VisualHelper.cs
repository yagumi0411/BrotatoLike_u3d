using UnityEngine;

public static class VisualHelper
{
    /// <summary>
    /// 创建一个 Primitive 作为视觉模型，并自动移除其碰撞体、设置颜色和父级。
    /// </summary>
    public static GameObject CreateVisual(PrimitiveType primitiveType, Transform parent, string name,
        Vector3 localPosition, Vector3 localScale, Color color, Quaternion? localRotation = null)
    {
        GameObject obj = GameObject.CreatePrimitive(primitiveType);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localScale = localScale;
        obj.transform.localRotation = localRotation ?? Quaternion.identity;

        if (obj.TryGetComponent<Renderer>(out var renderer))
        {
            renderer.material.color = color;
        }

        if (obj.TryGetComponent<Collider>(out var collider))
        {
            Object.Destroy(collider);
        }

        return obj;
    }
}
