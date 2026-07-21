using UnityEngine;

// 史莱姆 - 慢速肉盾
public class SlimeEnemy : Enemy
{
    protected override void CreateVisual()
    {
        float scale = EnemyDef.MeshScale;
        Color bodyColor = new Color(0.2f, 0.8f, 0.3f);

        // 主体：压扁的球体
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "Body",
            Vector3.zero, new Vector3(scale, scale * 0.6f, scale), bodyColor);

        // 眼睛
        float eyeSize = scale * 0.12f;
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "EyeL",
            new Vector3(-scale * 0.2f, scale * 0.15f, scale * 0.35f),
            Vector3.one * eyeSize, Color.black);
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "EyeR",
            new Vector3(scale * 0.2f, scale * 0.15f, scale * 0.35f),
            Vector3.one * eyeSize, Color.black);
    }
}
