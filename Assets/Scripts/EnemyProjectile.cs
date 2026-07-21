using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public float Damage;
    public Vector3 Direction;
    public float Speed;
    public float Lifetime;

    private void Start()
    {
        Destroy(gameObject, Lifetime);

        // 敌人投射物也使用 Trigger，避免推开玩家
        if (TryGetComponent<Collider>(out var col))
        {
            col.isTrigger = true;
            if (col is SphereCollider sphere)
            {
                sphere.radius = 1.0f;
            }
        }

        // 添加 Kinematic Rigidbody，让 Trigger 碰撞事件更可靠
        var body = gameObject.GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        CreateVisual();
    }

    private void CreateVisual()
    {
        // 核心：暗红色
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "Core",
            Vector3.zero, Vector3.one * 0.2f, new Color(0.8f, 0.1f, 0.1f));

        // 内核：亮红色
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "Inner",
            Vector3.zero, Vector3.one * 0.12f, new Color(1f, 0.4f, 0.4f));
    }

    private void Update()
    {
        transform.position += Direction * Speed * Time.deltaTime;
    }

    public void Initialize(float damage, Vector3 direction, float speed, float lifetime)
    {
        Damage = damage;
        Direction = direction.normalized;
        Speed = speed;
        Lifetime = lifetime;

        // 立即设为 Trigger，避免 Start 调用延迟导致推开玩家
        if (TryGetComponent<Collider>(out var col))
        {
            col.isTrigger = true;
            if (col is SphereCollider sphere)
            {
                sphere.radius = 1.0f;
            }
        }

        // 添加 Kinematic Rigidbody，让 Trigger 碰撞事件更可靠
        var body = gameObject.GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerController>(out var player))
        {
            player.StatsComponent.TakeDamage(Damage);
            Destroy(gameObject);
        }
    }
}