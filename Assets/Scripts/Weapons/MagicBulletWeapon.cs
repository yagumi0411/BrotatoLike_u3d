using UnityEngine;

public class MagicBulletWeapon : Weapon
{
    // 静态子弹池：所有魔法弹共享，消除每发子弹的 Instantiate/Destroy
    private static ObjectPool _bulletPool;

    public static ObjectPool Pool
    {
        get
        {
            if (_bulletPool == null)
                _bulletPool = new ObjectPool(CreateBullet, "MagicBulletPool");
            return _bulletPool;
        }
    }

    /// <summary>首次创建子弹模板（之后复用池中休眠实例）</summary>
    private static GameObject CreateBullet()
    {
        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "MagicBullet";
        bullet.transform.localScale = Vector3.one * 0.3f;
        bullet.AddComponent<MagicBulletProjectile>();
        return bullet;
    }

    protected override void Fire(Enemy target)
    {
        Vector3 targetPos = target.transform.position;
        float damage = CalculateDamage();
        bool isCrit = RollCrit();

        // 从池取子弹并重新初始化（视觉保留，状态全量重置）
        GameObject bullet = Pool.Get();
        bullet.transform.position = transform.position + Vector3.up * 1.0f;
        var projectile = bullet.GetComponent<MagicBulletProjectile>();
        projectile.Initialize(damage, isCrit, OwnerPlayer,
            (targetPos - bullet.transform.position).normalized,
            WeaponDef.ProjectileSpeed,
            WeaponDef.ProjectileLifetime);
    }
}
