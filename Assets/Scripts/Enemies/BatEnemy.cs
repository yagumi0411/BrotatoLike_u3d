using UnityEngine;

// 蝙蝠 - 快速低血敌人
public class BatEnemy : Enemy
{
    protected override void CreateVisual()
    {
        float scale = EnemyDef.MeshScale;
        Color bodyColor = new Color(0.35f, 0.3f, 0.4f);

        // 身体
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "Body",
            Vector3.zero,
            new Vector3(scale * 0.6f, scale * 0.4f, scale * 0.5f), bodyColor);

        // 翅膀
        Color wingColor = new Color(0.5f, 0.45f, 0.55f);
        GameObject wingL = VisualHelper.CreateVisual(PrimitiveType.Cube, transform, "WingL",
            new Vector3(-scale * 0.5f, scale * 0.1f, 0f),
            new Vector3(scale * 0.7f, scale * 0.05f, scale * 0.35f), wingColor);
        wingL.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);

        GameObject wingR = VisualHelper.CreateVisual(PrimitiveType.Cube, transform, "WingR",
            new Vector3(scale * 0.5f, scale * 0.1f, 0f),
            new Vector3(scale * 0.7f, scale * 0.05f, scale * 0.35f), wingColor);
        wingR.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);

        // 耳朵
        VisualHelper.CreateVisual(PrimitiveType.Cube, transform, "EarL",
            new Vector3(-scale * 0.15f, scale * 0.3f, 0f),
            new Vector3(scale * 0.08f, scale * 0.2f, scale * 0.08f), bodyColor);
        VisualHelper.CreateVisual(PrimitiveType.Cube, transform, "EarR",
            new Vector3(scale * 0.15f, scale * 0.3f, 0f),
            new Vector3(scale * 0.08f, scale * 0.2f, scale * 0.08f), bodyColor);
    }
}
