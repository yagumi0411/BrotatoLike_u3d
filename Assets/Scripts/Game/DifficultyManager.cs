using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public enum EDifficultyMode
{
    Fixed,
    Adaptive
}

/// <summary>
/// 根据玩家近期表现，对基础波次难度进行小范围修正。
/// 固定模式始终返回 1，不改变原有玩法。
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    [Header("模式")]
    public EDifficultyMode Mode = EDifficultyMode.Adaptive;

    [Header("评估参数")]
    [Min(1f)] public float EvaluationInterval = 10f;
    [Range(0.01f, 0.20f)] public float AdjustmentStep = 0.05f;
    [Range(0.50f, 1f)] public float MinMultiplier = 0.80f;
    [Range(1f, 1.50f)] public float MaxMultiplier = 1.20f;
    [Range(0f, 1f)] public float ReducePressureThreshold = 0.65f;
    [Range(0f, 1f)] public float IncreasePressureThreshold = 0.35f;

    [Header("波次目标值（后续按试玩日志校准）")]
    [Min(0.01f)] public float BaseKillsPerSecondTarget = 0.7f;
    [Min(0.01f)] public float BaseXPPerSecondTarget = 1.5f;
    [Min(0f)] public float TargetGrowthPerWave = 0.04f;

    public float CurrentMultiplier { get; private set; } = 1f;
    public float LastPressureScore { get; private set; } = 0.5f;

    private PlayerStatsComponent _playerStats;
    private float _elapsedInWindow;
    private float _damageTakenInWindow;
    private float _xpGainedInWindow;
    private int _killsInWindow;
    private string _sessionId;
    private string _logPath;

    public void Initialize(PlayerController player)
    {
        if (_playerStats != null)
        {
            _playerStats.OnDamageTaken -= RecordDamage;
            _playerStats.OnXPGained -= RecordXP;
        }

        _playerStats = player != null ? player.StatsComponent : null;
        if (_playerStats != null)
        {
            _playerStats.OnDamageTaken += RecordDamage;
            _playerStats.OnXPGained += RecordXP;
        }
    }

    private void OnDestroy()
    {
        if (_playerStats != null)
        {
            _playerStats.OnDamageTaken -= RecordDamage;
            _playerStats.OnXPGained -= RecordXP;
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.EGameState.Playing)
            return;

        _elapsedInWindow += Time.deltaTime;
        if (_elapsedInWindow >= EvaluationInterval)
            Evaluate();
    }

    public void ResetSession()
    {
        CurrentMultiplier = 1f;
        LastPressureScore = 0.5f;
        _elapsedInWindow = 0f;
        _damageTakenInWindow = 0f;
        _xpGainedInWindow = 0f;
        _killsInWindow = 0;
        CreateSessionLog();
    }

    public void RecordKill()
    {
        _killsInWindow++;
    }

    public float GetSpawnRateMultiplier() => Mode == EDifficultyMode.Adaptive ? CurrentMultiplier : 1f;

    public float GetEnemyHealthMultiplier()
    {
        if (Mode == EDifficultyMode.Fixed) return 1f;
        return Mathf.Lerp(0.90f, 1.10f, NormalizeMultiplier());
    }

    public float GetEnemyDamageMultiplier()
    {
        if (Mode == EDifficultyMode.Fixed) return 1f;
        return Mathf.Lerp(0.95f, 1.05f, NormalizeMultiplier());
    }

    public EnemyDefinition SelectEnemy(IReadOnlyList<EnemyDefinition> available)
    {
        if (available == null || available.Count == 0) return null;
        if (Mode == EDifficultyMode.Fixed) return available[Random.Range(0, available.Count)];

        float totalWeight = 0f;
        for (int i = 0; i < available.Count; i++)
            totalWeight += GetEnemyWeight(available[i]);

        float roll = Random.value * totalWeight;
        for (int i = 0; i < available.Count; i++)
        {
            roll -= GetEnemyWeight(available[i]);
            if (roll <= 0f) return available[i];
        }

        return available[available.Count - 1];
    }

    private void RecordDamage(float damage)
    {
        _damageTakenInWindow += Mathf.Max(0f, damage);
    }

    private void RecordXP(float amount)
    {
        _xpGainedInWindow += Mathf.Max(0f, amount);
    }

    private void Evaluate()
    {
        float interval = Mathf.Max(_elapsedInWindow, 0.01f);
        int wave = GameManager.Instance.WaveManager != null ? GameManager.Instance.WaveManager.CurrentWave : 1;
        float hpRatio = 0f;
        if (Mode == EDifficultyMode.Adaptive && _playerStats != null)
        {
            hpRatio = _playerStats.GetEffectiveMaxHP() > 0f
                ? Mathf.Clamp01(_playerStats.CurrentHP / _playerStats.GetEffectiveMaxHP())
                : 0f;
            float damageRisk = Mathf.Clamp01(_damageTakenInWindow / _playerStats.GetEffectiveMaxHP());
            float killTarget = BaseKillsPerSecondTarget * (1f + (wave - 1) * TargetGrowthPerWave);
            float xpTarget = BaseXPPerSecondTarget * (1f + (wave - 1) * TargetGrowthPerWave);
            float killStrength = Mathf.Clamp01(_killsInWindow / interval / killTarget);
            float xpStrength = Mathf.Clamp01(_xpGainedInWindow / interval / xpTarget);

            LastPressureScore = Mathf.Clamp01(
                0.35f * (1f - hpRatio) + 0.30f * damageRisk -
                0.20f * killStrength - 0.15f * xpStrength + 0.35f);

            if (LastPressureScore > ReducePressureThreshold)
                CurrentMultiplier = Mathf.Max(MinMultiplier, CurrentMultiplier - AdjustmentStep);
            else if (LastPressureScore < IncreasePressureThreshold)
                CurrentMultiplier = Mathf.Min(MaxMultiplier, CurrentMultiplier + AdjustmentStep);
        }

        if (_playerStats != null && Mode == EDifficultyMode.Fixed)
        {
            hpRatio = _playerStats.GetEffectiveMaxHP() > 0f
                ? Mathf.Clamp01(_playerStats.CurrentHP / _playerStats.GetEffectiveMaxHP())
                : 0f;
        }

        WriteWindowLog(wave, hpRatio);

        _elapsedInWindow = 0f;
        _damageTakenInWindow = 0f;
        _xpGainedInWindow = 0f;
        _killsInWindow = 0;
    }

    private float NormalizeMultiplier()
    {
        return Mathf.InverseLerp(MinMultiplier, MaxMultiplier, CurrentMultiplier);
    }

    private float GetEnemyWeight(EnemyDefinition definition)
    {
        if (definition == null) return 0f;

        bool highPressure = definition.Type == EEnemyType.Bat ||
                            definition.Type == EEnemyType.Ghost ||
                            definition.Type == EEnemyType.ShadowMage;
        if (CurrentMultiplier < 0.95f) return highPressure ? 0.5f : 1.5f;
        if (CurrentMultiplier > 1.05f) return highPressure ? 1.5f : 0.75f;
        return 1f;
    }

    private void CreateSessionLog()
    {
        _sessionId = System.Guid.NewGuid().ToString("N");
        string logDirectory = Path.Combine(Application.persistentDataPath, "DifficultyLogs");
        Directory.CreateDirectory(logDirectory);
        _logPath = Path.Combine(logDirectory, $"difficulty_{System.DateTime.Now:yyyyMMdd_HHmmss}_{_sessionId}.csv");
        File.WriteAllText(_logPath,
            "session_id,mode,elapsed_seconds,wave,hp_ratio,damage_taken,kills_in_window,xp_gained,active_enemy_count,pressure_score,difficulty_multiplier,spawn_interval\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private void WriteWindowLog(int wave, float hpRatio)
    {
        if (string.IsNullOrEmpty(_logPath))
            CreateSessionLog();

        float elapsedSeconds = GameManager.Instance != null ? GameManager.Instance.GamePlayTime : 0f;
        float spawnInterval = GameManager.Instance?.MonsterSpawner?.GetCurrentSpawnInterval() ?? 0f;
        string line = string.Format(CultureInfo.InvariantCulture,
            "{0},{1},{2:F3},{3},{4:F4},{5:F3},{6},{7:F3},{8},{9:F4},{10:F4},{11:F4}\n",
            _sessionId,
            Mode,
            elapsedSeconds,
            wave,
            hpRatio,
            _damageTakenInWindow,
            _killsInWindow,
            _xpGainedInWindow,
            EnemyRegistry.All.Count,
            LastPressureScore,
            CurrentMultiplier,
            spawnInterval);
        File.AppendAllText(_logPath, line, Encoding.UTF8);
    }
}
