using UnityEngine;

// 暗影法师 - 不移动，会远程攻击
public class ShadowMageEnemy : Enemy
{
    private float _attackCooldown;

    public override void Initialize(EnemyDefinition def, float statMultiplier)
    {
        base.Initialize(def, statMultiplier);
        _attackCooldown = 0f;
    }

    protected override void Update()
    {
        base.Update();

        if (EnemyDef.bIsRanged)
        {
            _attackCooldown -= Time.deltaTime;
            if (_attackCooldown <= 0)
            {
                TryRangedAttack();
                _attackCooldown = EnemyDef.RangedAttackInterval;
            }
        }
    }

    protected override void MoveTowardsPlayer()
    {
        // ShadowMage 不移动，保持原位攻击
    }

    protected override void CreateVisual()
    {
        float scale = EnemyDef.MeshScale;
        Color robeColor = new Color(0.2f, 0.05f, 0.35f);

        // 长袍身体
        VisualHelper.CreateVisual(PrimitiveType.Capsule, transform, "Robe",
            Vector3.zero,
            new Vector3(scale * 0.5f, scale * 1.1f, scale * 0.5f), robeColor);

        // 头罩
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "Hood",
            new Vector3(0f, scale * 0.55f, 0f),
            Vector3.one * scale * 0.35f, new Color(0.1f, 0.02f, 0.15f));

        // 法杖
        Color staffColor = new Color(0.4f, 0.25f, 0.15f);
        VisualHelper.CreateVisual(PrimitiveType.Cylinder, transform, "Staff",
            new Vector3(scale * 0.35f, scale * 0.1f, scale * 0.2f),
            new Vector3(scale * 0.06f, scale * 1f, scale * 0.06f), staffColor);

        // 法杖顶端宝石
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "Gem",
            new Vector3(scale * 0.35f, scale * 0.6f, scale * 0.2f),
            Vector3.one * scale * 0.12f, new Color(0.8f, 0.2f, 0.9f));
    }

    private void TryRangedAttack()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        // 创建敌人投射物
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "EnemyProjectile";
        projectile.transform.position = transform.position + Vector3.up * 0.5f;
        projectile.transform.localScale = Vector3.one * 0.25f;

        var proj = projectile.AddComponent<EnemyProjectile>();
        Vector3 dir = (player.transform.position - transform.position).normalized;
        proj.Initialize(EnemyDef.ProjectileDamage * StatMultiplier, dir, EnemyDef.ProjectileSpeed, 3f);
    }
}
