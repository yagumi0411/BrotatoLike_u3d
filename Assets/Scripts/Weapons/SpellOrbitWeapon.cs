using UnityEngine;
using System.Collections.Generic;

public class SpellOrbitWeapon : Weapon
{
    private List<OrbitProjectile> _orbitProjectiles = new List<OrbitProjectile>();

    public override void Initialize(PlayerController owner, WeaponDefinition def)
    {
        base.Initialize(owner, def);
        SpawnOrbitProjectiles();
    }

    private void SpawnOrbitProjectiles()
    {
        bool useVFX = WeaponDef != null && WeaponDef.ProjectileVFXPrefab != null;

        for (int i = 0; i < WeaponDef.OrbitCount; i++)
        {
            GameObject orb;
            if (useVFX)
            {
                // 有 VFX 时创建空对象，避免缩放影响粒子
                orb = new GameObject($"OrbitOrb_{i}");
                orb.transform.SetParent(transform);
            }
            else
            {
                orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = $"OrbitOrb_{i}";
                orb.transform.SetParent(transform);
                orb.transform.localScale = Vector3.one * 0.2f;
            }

            var orbitProj = orb.AddComponent<OrbitProjectile>();
            orbitProj.Initialize(this, CalculateDamage(), RollCrit(), WeaponDef.OrbitRadius, WeaponDef.OrbitSpeed, i, WeaponDef.OrbitCount);
            _orbitProjectiles.Add(orbitProj);
        }
    }

    protected override void Fire(Enemy target)
    {
        // 环绕武器持续存在，不需要 Fire，实现为空
    }

    protected override bool CanAttack() => false;

    public float GetCurrentDamage() => CalculateDamage();
    public bool GetCurrentCrit() => RollCrit();
}