using UnityEngine;

// 骷髅 - 中速普通敌人
public class SkeletonEnemy : Enemy
{
    protected override void CreateVisual()
    {
        float scale = EnemyDef.MeshScale;
        Color boneColor = new Color(0.9f, 0.9f, 0.85f);

        // 头
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "Head",
            new Vector3(0f, scale * 0.7f, 0f),
            Vector3.one * scale * 0.35f, boneColor);

        // 身体
        VisualHelper.CreateVisual(PrimitiveType.Capsule, transform, "Body",
            Vector3.zero,
            new Vector3(scale * 0.35f, scale * 0.9f, scale * 0.35f), boneColor);

        // 长矛
        VisualHelper.CreateVisual(PrimitiveType.Cylinder, transform, "Spear",
            new Vector3(scale * 0.3f, scale * 0.2f, scale * 0.3f),
            new Vector3(scale * 0.08f, scale * 0.8f, scale * 0.08f), new Color(0.5f, 0.35f, 0.2f));

        // 矛头
        GameObject spearTip = VisualHelper.CreateVisual(PrimitiveType.Cube, transform, "SpearTip",
            new Vector3(scale * 0.3f, scale * 0.65f, scale * 0.3f),
            new Vector3(scale * 0.08f, scale * 0.2f, scale * 0.08f), new Color(0.7f, 0.7f, 0.75f));
        spearTip.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
    }
}
