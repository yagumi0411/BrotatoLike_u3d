using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人对象池：按 EEnemyType 分池复用敌人实例。
/// 生成时由 MonsterSpawner 调用 Get，死亡时由 Enemy.OnDeath 调用 Release。
/// 回收时同步从 EnemyRegistry 注销（休眠对象不可被索敌）。
/// </summary>
public static class EnemyPool
{
    private static readonly Dictionary<EEnemyType, ObjectPool> _pools =
        new Dictionary<EEnemyType, ObjectPool>();

    /// <summary>取出敌人实例（池空时调用 factory 首次创建）</summary>
    public static Enemy Get(EEnemyType type, Func<Enemy> factory)
    {
        var pool = GetOrCreatePool(type);
        var obj = pool.Get(() => factory().gameObject);
        return obj.GetComponent<Enemy>();
    }

    /// <summary>回收敌人：注销注册表 + SetActive(false) 入池</summary>
    public static void Release(Enemy enemy)
    {
        if (enemy == null) return;
        EnemyRegistry.Unregister(enemy);
        var pool = GetOrCreatePool(enemy.EnemyDef.Type);
        pool.Release(enemy.gameObject);
    }

    /// <summary>清空所有敌人池（游戏重开时调用，防止跨局残留）</summary>
    public static void Clear()
    {
        foreach (var pool in _pools.Values)
            pool.Clear();
        _pools.Clear();
    }

    private static ObjectPool GetOrCreatePool(EEnemyType type)
    {
        if (!_pools.TryGetValue(type, out var pool))
        {
            pool = new ObjectPool(null, $"EnemyPool_{type}");
            _pools[type] = pool;
        }
        return pool;
    }
}
