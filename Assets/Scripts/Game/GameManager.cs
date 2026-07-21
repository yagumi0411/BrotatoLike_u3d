using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // === 游戏状态 ===
    public enum EGameState
    {
        MainMenu,
        Playing,
        Paused,
        LevelUp,
        GameOver
    }

    public EGameState CurrentState { get; private set; } = EGameState.MainMenu;

    // === 管理器引用 ===
    public WaveManager WaveManager { get; private set; }
    public MonsterSpawner MonsterSpawner { get; private set; }
    public PlayerController Player { get; private set; }

    // === UI 组件（由你在 Inspector 中拖拽） ===
    [Header("UI 组件")]
    public MainMenuUI MainMenuUI;
    public PauseUI PauseUI;
    public GameOverUI GameOverUI;
    public MainHUD MainHUD;
    public LevelUpUI LevelUpUI;

    // === 数据池 ===
    [Header("数据池")]
    public List<UpgradeOption> UpgradeOptionPool;
    public List<WeaponDefinition> WeaponDefinitions;

    // === 地图配置（预留接口） ===
    [Header("地图")]
    public string[] MapNames = new string[] { "竞技场" };

    // === 统计数据 ===
    [Header("统计数据（运行时）")]
    public int TotalKills;
    public float GamePlayTime;

    // === 内部 ===
    private int _selectedMapIndex;
    private Vector3 _playerStartPosition;
    private Vector3 _playerStartRotation;

    private void Awake()
    {
        Instance = this;

        // 初始化引用
        WaveManager = FindAnyObjectByType<WaveManager>();
        MonsterSpawner = FindAnyObjectByType<MonsterSpawner>();
        Player = FindAnyObjectByType<PlayerController>();

        // 记录玩家的初始位置（用于重新开始）
        if (Player != null)
        {
            _playerStartPosition = Player.transform.position;
            _playerStartRotation = Player.transform.eulerAngles;
        }

        // 对 MainHUD 和 LevelUpUI 使用容错查找
        if (MainHUD == null)
            MainHUD = FindAnyObjectByType<MainHUD>();
        if (LevelUpUI == null)
            LevelUpUI = FindAnyObjectByType<LevelUpUI>();
    }

    private void OnDestroy()
    {
        // 解除事件绑定，防止泄漏
        if (Player != null)
        {
            Player.StatsComponent.OnLevelUp -= OnPlayerLevelUp;
        }
    }

    private void Start()
    {
        // 默认进入主菜单
        SetState(EGameState.MainMenu);

        // 绑定升级事件
        if (Player != null)
        {
            Player.StatsComponent.OnLevelUp += OnPlayerLevelUp;
        }
    }

    private void Update()
    {
        // ESC 暂停（仅在 Playing 或 Paused 状态下生效）
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (CurrentState == EGameState.Playing)
            {
                PauseGame();
            }
            else if (CurrentState == EGameState.Paused)
            {
                ResumeGame();
            }
        }

        // 游戏进行中计时
        if (CurrentState == EGameState.Playing)
        {
            GamePlayTime += Time.deltaTime;
        }
    }

    // === 状态切换 ===

    private void SetState(EGameState newState)
    {
        CurrentState = newState;

        // 管理所有 UI 显隐
        if (MainMenuUI != null)
            MainMenuUI.gameObject.SetActive(newState == EGameState.MainMenu);

        if (MainHUD != null)
            MainHUD.gameObject.SetActive(newState == EGameState.Playing
                                          || newState == EGameState.Paused);

        if (PauseUI != null)
            PauseUI.gameObject.SetActive(newState == EGameState.Paused);

        // LevelUpUI 自己管理 Panel 显隐，不由 GameManager 控制其 gameObject

        if (GameOverUI != null)
            GameOverUI.gameObject.SetActive(newState == EGameState.GameOver);

        // 时间缩放
        if (newState == EGameState.MainMenu || newState == EGameState.Paused || newState == EGameState.LevelUp || newState == EGameState.GameOver)
        {
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    // === 公共 API ===

    /// <summary>
    /// 开始游戏（从主菜单调用）
    /// </summary>
    public void StartGame()
    {
        ResetPlayer();
        ResetGameState();
        SetState(EGameState.Playing);
        MainHUD?.Rebind();
        WaveManager?.StartGame();
    }

    /// <summary>
    /// 暂停游戏
    /// </summary>
    public void PauseGame()
    {
        if (CurrentState != EGameState.Playing) return;
        SetState(EGameState.Paused);
    }

    /// <summary>
    /// 恢复游戏
    /// </summary>
    public void ResumeGame()
    {
        if (CurrentState != EGameState.Paused && CurrentState != EGameState.LevelUp) return;
        SetState(EGameState.Playing);
    }

    /// <summary>
    /// 重新开始游戏
    /// </summary>
    public void RestartGame()
    {
        ResetWorld();
        ResetGameState();
        ResetPlayer();
        SetState(EGameState.Playing);
        MainHUD?.Rebind();
        WaveManager?.StartGame();
    }

    /// <summary>
    /// 返回主菜单
    /// </summary>
    public void ReturnToMainMenu()
    {
        ResetWorld();
        ResetGameState();
        ResetPlayer();
        SetState(EGameState.MainMenu);
    }

    /// <summary>
    /// 游戏结束（由 Player 死亡时调用）
    /// </summary>
    public void GameOver()
    {
        SetState(EGameState.GameOver);

        if (GameOverUI != null)
        {
            int level = Player != null ? Player.StatsComponent.CurrentLevel : 1;
            GameOverUI.ShowResult(WaveManager?.CurrentWave ?? 0, level, TotalKills, GamePlayTime);
        }
    }

    /// <summary>
    /// 玩家升级时调用（绑定在 PlayerStatsComponent.OnLevelUp）
    /// </summary>
    public void OnPlayerLevelUp()
    {
        Debug.Log($"[GameManager] OnPlayerLevelUp called, Level={Player?.StatsComponent.CurrentLevel}, LevelUpUI={(LevelUpUI != null ? "OK" : "NULL")}");

        if (LevelUpUI != null)
        {
            int remainingSlots = Player != null ? Player.GetRemainingWeaponSlots() : 0;
            var options = GenerateUpgradeOptions(3, remainingSlots);
            Debug.Log($"[GameManager] Generated {options.Count} options (UpgradeOptionPool has {UpgradeOptionPool?.Count ?? 0} items)");

            if (options.Count > 0)
            {
                LevelUpUI.ShowOptions(options);
                SetState(EGameState.LevelUp);
                Debug.Log($"[GameManager] SetState LevelUp, timeScale={Time.timeScale}");
            }
            else
            {
                Debug.LogWarning("[GameManager] 没有可用的升级选项，跳过升级");
            }
        }
    }

    /// <summary>
    /// 升级选项被选择后回调（由 LevelUpUI 调用，或由升级 UI 内部处理）
    /// </summary>
    public void OnUpgradeSelected()
    {
        ResumeGame();
    }

    /// <summary>
    /// 记录击杀
    /// </summary>
    public void AddKill()
    {
        TotalKills++;
    }

    /// <summary>
    /// 升级选项生成算法
    /// </summary>
    public List<UpgradeOption> GenerateUpgradeOptions(int count, int remainingWeaponSlots)
    {
        count = Mathf.Max(count, 1);
        var result = new List<UpgradeOption>();

        // 1. 筛选可用的数值升级选项
        int playerLevel = Player != null ? Player.StatsComponent.CurrentLevel : 1;
        var statUpgrades = UpgradeOptionPool?
            .Where(o => o.Type != EUpgradeType.Weapon && o.MinLevelToAppear <= playerLevel)
            .ToList() ?? new List<UpgradeOption>();

        // 2. 筛选可用的武器升级选项
        int currentWave = WaveManager?.CurrentWave ?? 1;
        var equippedTypes = Player?.EquippedWeapons
            .Select(w => w.WeaponDef.Type)
            .ToHashSet() ?? new HashSet<EWeaponType>();

        var weaponUpgrades = UpgradeOptionPool?
            .Where(o => o.Type == EUpgradeType.Weapon && o.WeaponDef != null)
            .Where(o => o.MinLevelToAppear <= playerLevel)
            .Where(o => o.WeaponDef.MinWaveToAppear <= currentWave)
            .Where(o => !equippedTypes.Contains(o.WeaponDef.Type))
            .ToList() ?? new List<UpgradeOption>();

        // 3. 决定武器选项数量
        int weaponCount = Mathf.Min(Random.Range(1, count + 1), remainingWeaponSlots, weaponUpgrades.Count);
        int statCount = count - weaponCount;

        // 4. 随机选取
        if (weaponCount > 0 && weaponUpgrades.Count > 0)
        {
            var selectedWeapons = weaponUpgrades.OrderBy(_ => Random.value).Take(weaponCount);
            result.AddRange(selectedWeapons);
        }
        else
        {
            statCount = count; // 没有可用武器时全用数值
        }

        if (statCount > 0 && statUpgrades.Count > 0)
        {
            var selectedStats = statUpgrades.OrderBy(_ => Random.value).Take(statCount);
            result.AddRange(selectedStats);
        }

        // 5. 还不够？从全部池子随机补
        if (result.Count < count && UpgradeOptionPool != null)
        {
            var fillers = UpgradeOptionPool
                .Where(o => o.MinLevelToAppear <= playerLevel)
                .OrderBy(_ => Random.value)
                .Take(count - result.Count);
            result.AddRange(fillers);
        }

        // 6. 打乱顺序
        result = result.OrderBy(_ => Random.value).ToList();

        return result;
    }

    // === 内部方法 ===

    private void ResetGameState()
    {
        TotalKills = 0;
        GamePlayTime = 0f;
    }

    private void ResetWorld()
    {
        // 清除所有敌人
        if (MonsterSpawner != null)
            MonsterSpawner.ClearAllEnemies();

        // 清除所有经验球
        XPOrb.ClearAllOrbs();

        // 清除所有敌人弹幕
        var enemyProjectiles = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
        foreach (var proj in enemyProjectiles)
        {
            Destroy(proj.gameObject);
        }

        // 重置波次
        if (WaveManager != null)
            WaveManager.ResetGame();
    }

    private void ResetPlayer()
    {
        if (Player == null) return;

        // 清除所有武器
        foreach (var weapon in Player.EquippedWeapons)
        {
            if (weapon != null)
                Destroy(weapon.gameObject);
        }
        Player.EquippedWeapons.Clear();

        // 重置位置
        Player.transform.position = _playerStartPosition;
        Player.transform.eulerAngles = _playerStartRotation;

        // 如果使用 CharacterController，需要手动重置
        var cc = Player.CharacterController;
        if (cc != null)
        {
            cc.enabled = false;
            cc.transform.position = _playerStartPosition;
            cc.enabled = true;
        }

        // 重置属性
        var stats = Player.StatsComponent;
        stats.FlatHPBonus = 0f;
        stats.PercentHPBonus = 0f;
        stats.GlobalDamageMultiplier = 0f;
        stats.GlobalAttackSpeedMultiplier = 0f;
        stats.FlatMoveSpeedBonus = 0f;
        stats.ExpGainMultiplier = 0f;
        stats.FlatPickupRangeBonus = 0f;
        stats.CritChanceBonus = 0f;
        stats.CritDamageMultiplierBonus = 0f;
        stats.CurrentXP = 0f;
        stats.CurrentLevel = 1;
        stats.CurrentHP = stats.GetEffectiveMaxHP();

        // 重新生成初始武器
        Player.SpawnStartingWeapon();
    }
}