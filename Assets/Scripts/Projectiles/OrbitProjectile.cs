using UnityEngine;

public class OrbitProjectile : MonoBehaviour
{
    private Weapon _ownerWeapon;
    private float _damage;
    private bool _isCrit;
    private float _radius;
    private float _speed;
    private int _index;
    private int _total;
    private float _currentAngle;

    private float _attackCooldown;

    public void Initialize(Weapon owner, float damage, bool isCrit, float radius, float speed, int index, int total)
    {
        _ownerWeapon = owner;
        _damage = damage;
        _isCrit = isCrit;
        _radius = radius;
        _speed = speed;
        _index = index;
        _total = total;
        _currentAngle = 360f * index / total;

        SetupCollider();
        CreateVisual();
    }

    private void SetupCollider()
    {
        // 确保有碰撞体用于 Trigger 检测
        if (!TryGetComponent<Collider>(out var col))
            col = gameObject.AddComponent<SphereCollider>();

        col.isTrigger = true;
        if (col is SphereCollider sphere)
            sphere.radius = 0.8f;
    }

    private void CreateVisual()
    {
        // 检查武器定义中是否有 VFX 预制体
        GameObject vfxPrefab = _ownerWeapon != null && _ownerWeapon.WeaponDef != null
            ? _ownerWeapon.WeaponDef.ProjectileVFXPrefab
            : null;

        if (vfxPrefab != null)
        {
            var vfx = Instantiate(vfxPrefab, transform);
            vfx.transform.localPosition = Vector3.zero;
            vfx.transform.localRotation = Quaternion.identity;
            return;
        }

        // fallback: 飞弹主体：蓝色发光球
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "Core",
            Vector3.zero, Vector3.one * 0.15f, new Color(0.3f, 0.7f, 1f));

        // 内核：更亮的中心
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "Inner",
            Vector3.zero, Vector3.one * 0.08f, new Color(0.8f, 0.95f, 1f));

        // 飞弹尾迹：拉伸的立方体模拟运动方向光晕
        VisualHelper.CreateVisual(PrimitiveType.Cube, transform, "Glow",
            Vector3.zero, new Vector3(0.4f, 0.04f, 0.04f),
            new Color(0.5f, 0.85f, 1f, 0.6f));
    }

    private void Update()
    {
        // 更新角度
        _currentAngle += _speed * Time.deltaTime;
        if (_currentAngle >= 360f) _currentAngle -= 360f;

        // 计算轨道位置
        float rad = _currentAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad) * _radius, 0, Mathf.Sin(rad) * _radius);
        transform.position = _ownerWeapon.transform.position + Vector3.up * 0.5f + offset;

        // 朝运动方向旋转
        Vector3 dir = new Vector3(-Mathf.Sin(rad), 0, Mathf.Cos(rad));
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);

        // 冷却递减
        if (_attackCooldown > 0f)
            _attackCooldown -= Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_attackCooldown > 0f) return;

        if (other.TryGetComponent<Enemy>(out var enemy))
        {
            enemy.ReceiveDamage(_damage, _isCrit);
            _attackCooldown = 0.5f; // 0.5s 冷却，防止同一敌人被高频命中
        }
    }
}