using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人注册表：事件驱动维护的活敌人列表。
/// 敌人 Initialize 时注册、回收/销毁时注销，替代 FindObjectsByType 全场景扫描。
/// 查询零分配：手写循环遍历 List，不产生任何 GC 分配。
/// </summary>
public static class EnemyRegistry
{
    private static readonly List<Enemy> _enemies = new List<Enemy>(128);

    /// <summary>当前活着的敌人数量</summary>
    public static int Count => _enemies.Count;

    /// <summary>活敌人列表（外部只读遍历，不要在遍历过程中增删）</summary>
    public static List<Enemy> All => _enemies;

    public static void Register(Enemy enemy)
    {
        if (enemy != null)
            _enemies.Add(enemy);
    }

    /// <summary>注销敌人（swap-remove：O(1)，改变顺序但不影响索敌语义）</summary>
    public static void Unregister(Enemy enemy)
    {
        int index = _enemies.IndexOf(enemy);
        if (index >= 0)
        {
            int last = _enemies.Count - 1;
            _enemies[index] = _enemies[last];
            _enemies.RemoveAt(last);
        }
    }

    /// <summary>清空注册表（游戏重开时调用，防止编辑器热重载/跨局残留）</summary>
    public static void Clear()
    {
        _enemies.Clear();
    }
}
