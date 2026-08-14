using UnityEngine;

public class MagicBulletProjectile : MonoBehaviour
{
    public float Damage;
    public bool IsCrit;
    public PlayerController OwnerPlayer;
    public Vector3 Direction;
    public float Speed;
    public float Lifetime;

    private void Start()
    {
        // 注：寿命结束由 Update 统一管理（对象池复用后 Start 不会再次执行）

        // 投射物使用 Trigger 检测命中，避免推开玩家/敌人
        if (TryGetComponent<Collider>(out var col))
        {
            col.isTrigger = true;
            if (col is SphereCollider sphere)
            {
                sphere.radius = 1.0f;
            }
        }

        // 给子弹添加 Kinematic Rigidbody，让 Trigger 碰撞事件更可靠
        var body = gameObject.GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        CreateVisual();
    }

    private void CreateVisual()
    {
        // 主体：蓝色发光球
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "Core",
            Vector3.zero, Vector3.one * 0.25f, new Color(0.4f, 0.8f, 1f));

        // 内核：更亮的中心
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "Inner",
            Vector3.zero, Vector3.one * 0.15f, new Color(0.8f, 0.95f, 1f));

        // 十字光环
        Color ringColor = new Color(0.5f, 0.9f, 1f);
        VisualHelper.CreateVisual(PrimitiveType.Cube, transform, "RingX",
            Vector3.zero, new Vector3(0.45f, 0.05f, 0.05f), ringColor);
        VisualHelper.CreateVisual(PrimitiveType.Cube, transform, "RingY",
            Vector3.zero, new Vector3(0.05f, 0.45f, 0.05f), ringColor);
        VisualHelper.CreateVisual(PrimitiveType.Cube, transform, "RingZ",
            Vector3.zero, new Vector3(0.05f, 0.05f, 0.45f), ringColor);
    }

    private void Update()
    {
        transform.position += Direction * Speed * Time.deltaTime;

        // 寿命倒计时（替代 Start 的 Destroy 定时器，池化复用后依然有效）
        Lifetime -= Time.deltaTime;
        if (Lifetime <= 0f)
        {
            MagicBulletWeapon.Pool.Release(gameObject);
        }
    }

    public void Initialize(float damage, bool isCrit, PlayerController owner,
        Vector3 direction, float speed, float lifetime)
    {
        Damage = damage;
        IsCrit = isCrit;
        OwnerPlayer = owner;
        Direction = direction.normalized;
        Speed = speed;
        Lifetime = lifetime;

        // 立即设为 Trigger，避免 Start 调用延迟导致推开玩家
        if (TryGetComponent<Collider>(out var col))
        {
            col.isTrigger = true;
            // 加大命中体积，降低高速穿透/擦边未命中的概率
            if (col is SphereCollider sphere)
            {
                sphere.radius = 1.0f;
            }
        }

        // 给子弹添加 Kinematic Rigidbody，让 Trigger 碰撞事件更可靠
        var rb = gameObject.GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Enemy>(out var enemy))
        {
            enemy.ReceiveDamage(Damage, IsCrit);
            MagicBulletWeapon.Pool.Release(gameObject);
        }
    }
}