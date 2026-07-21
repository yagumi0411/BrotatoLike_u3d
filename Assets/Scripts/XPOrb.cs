using UnityEngine;

public class XPOrb : MonoBehaviour
{
    public float ExpValue;
    private bool _isMagnetizing;
    private const float MagnetSpeed = 6f;
    private Renderer _renderer;
    private Collider _collider;

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
        var player = GameManager.Instance?.Player;
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
        Destroy(gameObject);
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
        var orbs = FindObjectsByType<XPOrb>(FindObjectsSortMode.None);
        foreach (var orb in orbs)
        {
            Destroy(orb.gameObject);
        }
    }

    public static void Spawn(Vector3 position, float xpValue)
    {
        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "XPOrb";
        orb.transform.position = position + Vector3.up * 0.5f;
        orb.transform.localScale = Vector3.one * 0.3f;
        orb.GetComponent<Renderer>().material.color = Color.blue;

        var orbComp = orb.AddComponent<XPOrb>();
        orbComp.ExpValue = xpValue;
    }
}