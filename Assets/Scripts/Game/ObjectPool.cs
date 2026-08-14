using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用对象池：按需创建 + SetActive 切换复用，
/// 消除运行时高频 Instantiate/Destroy 造成的 GC 分配与卡顿。
/// 注意：复用对象的所有运行时状态必须在 Get 后重新初始化（或用 OnEnable 重置）。
/// </summary>
public class ObjectPool
{
    private readonly Queue<GameObject> _available = new Queue<GameObject>();
    private readonly Func<GameObject> _factory;
    private readonly string _name;

    public ObjectPool(Func<GameObject> factory, string name = "Pool")
    {
        _factory = factory;
        _name = name;
    }

    /// <summary>
    /// 取出一个实例（池空时由工厂创建）。
    /// factory 参数允许调用方在池空时临时提供创建函数（如 EnemyPool 按类型懒创建）。
    /// </summary>
    public GameObject Get(Func<GameObject> factory = null)
    {
        while (_available.Count > 0)
        {
            var obj = _available.Dequeue();
            if (obj != null)
            {
                obj.SetActive(true);
                return obj;
            }
        }
        return (factory ?? _factory)();
    }

    /// <summary>回收实例（SetActive(false) 入池；已休眠对象重复回收会被忽略）</summary>
    public void Release(GameObject obj)
    {
        if (obj == null || !obj.activeSelf) return;
        obj.SetActive(false);
        _available.Enqueue(obj);
    }

    /// <summary>销毁池内所有休眠实例（游戏重开时清空残留）</summary>
    public void Clear()
    {
        while (_available.Count > 0)
        {
            var obj = _available.Dequeue();
            if (obj != null)
                UnityEngine.Object.Destroy(obj);
        }
    }
}
