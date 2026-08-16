using UnityEngine;

/// <summary>
/// 全局手感反馈管理器（静态）：Hitstop（命中顿帧）、屏幕震动、命中特效池。
/// - Hitstop 通过 Time.timeScale 实现，自身计时用 unscaledDeltaTime（timeScale 变化不影响计时）
/// - 顿帧结束时回查 GameManager 状态恢复 timeScale，避免覆盖暂停/结算的 0 倍速
/// - 屏幕震动由 CameraFollow 每帧拉取偏移（trauma 平方曲线，随机方向）
/// - 命中特效走对象池，消除高频 Instantiate/Destroy
/// </summary>
public static class GameFeel
{
    // === Hitstop（命中顿帧） ===
    private static float _hitStopRemaining;
    private static float _hitStopTimeScale = 1f;

    public const float KillHitStopDuration = 0.06f;  // 击杀顿帧时长
    public const float KillHitStopTimeScale = 0.2f;  // 顿帧期间 timeScale
    public const float HurtHitStopDuration = 0.08f;  // 玩家受击顿帧时长
    public const float HurtHitStopTimeScale = 0.15f;

    /// <summary>触发命中顿帧（可叠加：取剩余时间较长者）</summary>
    public static void HitStop(float duration, float timeScale)
    {
        _hitStopRemaining = Mathf.Max(_hitStopRemaining, duration);
        _hitStopTimeScale = timeScale;
        Time.timeScale = timeScale;
    }

    /// <summary>由 GameManager.Update 每帧驱动（Update 不受 timeScale 影响）</summary>
    public static void Tick()
    {
        if (_hitStopRemaining > 0f)
        {
            _hitStopRemaining -= Time.unscaledDeltaTime;
            if (_hitStopRemaining <= 0f)
            {
                _hitStopRemaining = 0f;
                Time.timeScale = GetTargetTimeScale();
            }
        }

        if (_trauma > 0f)
        {
            _trauma = Mathf.Max(0f, _trauma - ShakeDecay * Time.unscaledDeltaTime);
        }
    }

    /// <summary>当前游戏状态对应的正常 timeScale（Playing/LevelUp=1，其余=0）</summary>
    private static float GetTargetTimeScale()
    {
        var gm = GameManager.Instance;
        if (gm == null) return 1f;
        return gm.CurrentState == GameManager.EGameState.Playing
            || gm.CurrentState == GameManager.EGameState.LevelUp ? 1f : 0f;
    }

    // === 屏幕震动 ===
    private static float _trauma;
    public const float ShakeMaxOffset = 0.3f;  // 最大偏移（米）
    public const float ShakeDecay = 3f;        // trauma 每秒衰减

    /// <summary>叠加震动强度（0~1，每次受击约 +0.5）</summary>
    public static void AddShake(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

    /// <summary>相机每帧拉取震动偏移（trauma² 曲线 + 随机方向，无平滑直接叠加）</summary>
    public static Vector3 GetShakeOffset()
    {
        if (_trauma <= 0f) return Vector3.zero;
        float intensity = _trauma * _trauma * ShakeMaxOffset;
        return new Vector3(
            (Random.value * 2f - 1f) * intensity,
            0f,
            (Random.value * 2f - 1f) * intensity);
    }

    // === 命中特效池 ===
    private static ObjectPool _hitEffectPool;

    private static ObjectPool HitEffectPool =>
        _hitEffectPool ??= new ObjectPool(CreateHitEffect, "HitEffectPool");

    /// <summary>命中点生成闪光特效（暴击更大更橙）</summary>
    public static void SpawnHitEffect(Vector3 position, bool isCrit)
    {
        var obj = HitEffectPool.Get();
        obj.transform.position = position + Vector3.up * 0.5f;
        var behavior = obj.GetComponent<HitEffectBehavior>();
        behavior.Initialize(isCrit);
    }

    /// <summary>首次创建命中特效模板（球体无碰撞体，之后复用池中休眠实例）</summary>
    /// <summary>回收入池（HitEffectBehavior 寿命结束时调用）</summary>
    public static void ReleaseHitEffect(GameObject effect) => HitEffectPool.Release(effect);

    private static GameObject CreateHitEffect()
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = "HitEffect";
        if (obj.TryGetComponent<Collider>(out var col))
            Object.Destroy(col);
        obj.AddComponent<HitEffectBehavior>();
        return obj;
    }

    /// <summary>重置全部手感状态（游戏重开时调用，防跨局残留）</summary>
    public static void Reset()
    {
        _hitStopRemaining = 0f;
        _trauma = 0f;
        _hitEffectPool?.Clear();
    }
}

/// <summary>
/// 命中特效行为：膨胀-收缩 + 变暗消失，寿命结束回池（替代 Destroy）。
/// </summary>
public class HitEffectBehavior : MonoBehaviour
{
    private const float NormalLifetime = 0.15f;
    private const float CritLifetime = 0.22f;

    private Renderer _renderer;
    private Color _baseColor;
    private Vector3 _baseScale;
    private float _totalLifetime;
    private float _lifetime;

    public void Initialize(bool isCrit)
    {
        _totalLifetime = isCrit ? CritLifetime : NormalLifetime;
        _lifetime = _totalLifetime;
        _baseScale = isCrit ? Vector3.one * 0.45f : Vector3.one * 0.22f;
        _baseColor = isCrit ? new Color(1f, 0.5f, 0.1f) : Color.white;

        if (_renderer == null)
        {
            _renderer = GetComponent<Renderer>();
            _renderer.material.color = _baseColor; // 首次实例化材质，之后复用不产生分配
        }
        else
        {
            _renderer.material.color = _baseColor;
        }

        transform.localScale = _baseScale;
    }

    private void Update()
    {
        _lifetime -= Time.deltaTime;
        if (_lifetime <= 0f)
        {
            GameFeel.ReleaseHitEffect(gameObject);
            return;
        }

        // 0→0.2 膨胀至 1.4 倍，之后收缩至 0
        float t = 1f - _lifetime / _totalLifetime;
        float scaleMul = t < 0.2f ? 1f + t * 2f : 1.4f - (t - 0.2f) * 1.75f;
        transform.localScale = _baseScale * Mathf.Max(0f, scaleMul);

        // 逐渐变暗
        _renderer.material.color = Color.Lerp(_baseColor, Color.black, t);
    }
}
