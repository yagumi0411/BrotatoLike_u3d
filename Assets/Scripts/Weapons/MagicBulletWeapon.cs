using UnityEngine;

public class MagicBulletWeapon : Weapon
{
    protected override void Fire(Enemy target)
    {
        Vector3 targetPos = target.transform.position;
        float damage = CalculateDamage();
        bool isCrit = RollCrit();

        // 生成投射物
        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "MagicBullet";
        // 与史莱姆等敌人中心高度对齐（敌人生成在 y=1）
        bullet.transform.position = transform.position + Vector3.up * 1.0f;
        bullet.transform.localScale = Vector3.one * 0.3f;

        // 设置投射物
        var projectile = bullet.AddComponent<MagicBulletProjectile>();
        projectile.Initialize(damage, isCrit, OwnerPlayer,
            (targetPos - bullet.transform.position).normalized,
            WeaponDef.ProjectileSpeed,
            WeaponDef.ProjectileLifetime);
    }
}
