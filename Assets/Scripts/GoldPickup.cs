using System.Collections.Generic;
using UnityEngine;

public class GoldPickup : MonoBehaviour
{
    private static readonly List<GoldPickup> Active = new List<GoldPickup>();
    private static ObjectPool _pool;
    private int _value;
    private bool _magnetizing;

    public static void Spawn(Vector3 position, int value)
    {
        if (value <= 0) return;
        if (_pool == null) _pool = new ObjectPool(Create, "GoldPickupPool");
        var pickup = _pool.Get().GetComponent<GoldPickup>();
        pickup.transform.position = position + Vector3.up * 0.25f;
        pickup._value = value;
        pickup._magnetizing = false;
        Active.Add(pickup);
    }

    private static GameObject Create()
    {
        var obj = new GameObject("GoldPickup");
        obj.AddComponent<GoldPickup>();
        DungeonArt.CreateActor(obj.transform, "coin_anim_f", "coin_anim_f", 1.2f, Color.white, -0.1f);
        return obj;
    }

    private void Update()
    {
        var manager = GameManager.Instance;
        if (manager == null || manager.CurrentState != GameManager.EGameState.Playing || manager.Player == null) return;
        Vector3 target = manager.Player.transform.position;
        // 金币在地面移动，拾取距离只计算水平距离。
        target.y = transform.position.y;
        float distance = (transform.position - target).sqrMagnitude;
        float radius = manager.Player.StatsComponent.GetEffectivePickupRadius();
        if (distance <= radius * radius) _magnetizing = true;
        if (!_magnetizing) return;
        transform.position = Vector3.MoveTowards(transform.position, target, 8f * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude < 0.09f) Collect();
    }

    private void Collect()
    {
        if (_value <= 0) return;
        GameManager.Instance?.AddGold(_value);
        _value = 0;
        Active.Remove(this);
        _pool.Release(gameObject);
    }

    public static void CollectAll()
    {
        for (int i = Active.Count - 1; i >= 0; i--)
            if (Active[i] != null) Active[i].Collect();
        Active.Clear();
    }

    public static void ClearAll()
    {
        foreach (var pickup in Active)
            if (pickup != null)
            {
                pickup._value = 0;
                pickup.gameObject.SetActive(false);
                Destroy(pickup.gameObject);
            }
        Active.Clear();
        _pool?.Clear();
        _pool = null;
    }
}
