using UnityEngine;

public class XPOrb : MonoBehaviour
{
    public float ExpValue;
    private bool _isMagnetizing;
    private const float MagnetSpeed = 6f;
    private Renderer _renderer;
    private Collider _collider;

    // 静态对象池：消除经验球高频生成/销毁的 GC 分配
    private static ObjectPool _pool;

    private static ObjectPool Pool
    {
        get
        {
            if (_pool == null)
                _pool = new ObjectPool(CreateOrb, "XPOrbPool");
            return _pool;
        }
    }

    private void OnEnable()
    {
        // 池化复用状态重置（SetActive(true) 时必然触发）
        _isMagnetizing = false;
    }

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();

        // 设为 Trigger，避免与玩家/怪物发生物理排斥
        if (_collider != null)
        {
            _collider.isTrigger = true;
        }

        // 添加 Kinematic Rigidbody 确保 Trigger 事件可靠，且不参与物理推挤
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void Update()
    {
        // 双人模式：磁吸离得最近的玩家
        var player = GameManager.Instance?.GetNearestPlayer(transform.position);
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        float pickupRadius = player.StatsComponent.GetEffectivePickupRadius();

        if (dist <= pickupRadius || _isMagnetizing)
        {
            _isMagnetizing = true;

            // 非常靠近玩家时直接吸收（保底，防止 Trigger 事件不可靠）
            if (dist < 0.3f)
            {
                Absorb(player);
                return;
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                player.transform.position,
                MagnetSpeed * Time.deltaTime);
        }
    }

    private void Absorb(PlayerController player)
    {
        player.StatsComponent.AddXP(ExpValue);
        Pool.Release(gameObject);   // 回池复用（替代 Destroy）
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerController>(out var player))
        {
            Absorb(player);
        }
    }

    public static void ClearAllOrbs()
    {
        // 仅在结算/重开时调用一次，低频扫描可接受（池内休眠实例不受影响）
        var orbs = FindObjectsByType<XPOrb>();
        foreach (var orb in orbs)
        {
            Destroy(orb.gameObject);
        }
    }

    public static void Spawn(Vector3 position, float xpValue)
    {
        GameObject orb = Pool.Get();
        orb.transform.position = position + Vector3.up * 0.5f;
        orb.GetComponent<XPOrb>().ExpValue = xpValue;
        // 磁吸状态由 OnEnable 重置
    }

    /// <summary>首次创建经验球模板（之后复用池中休眠实例）</summary>
    private static GameObject CreateOrb()
    {
        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "XPOrb";
        orb.transform.localScale = Vector3.one * 0.3f;
        orb.GetComponent<Renderer>().material.color = Color.blue;
        orb.AddComponent<XPOrb>();
        return orb;
    }
}
