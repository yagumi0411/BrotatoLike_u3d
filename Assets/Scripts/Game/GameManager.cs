using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

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
        GameOver,
        Shop
    }

    public EGameState CurrentState { get; private set; } = EGameState.MainMenu;

    // === 管理器引用 ===
    public WaveManager WaveManager { get; private set; }
    public MonsterSpawner MonsterSpawner { get; private set; }
    public PlayerController Player { get; private set; }
    public DifficultyManager DifficultyManager { get; private set; }
    public WaveShopUI ShopUI { get; private set; }
    public MetaProgression Progression { get; private set; }
    public TalentGraphUI TalentUI { get; private set; }

    [Header("金币与商店")]
    [Min(1)] public int GoldPerKill = 1;
    [Min(1)] public int HealingCost = 8;
    [Min(1f)] public float HealingAmount = 4f;
    public int Gold { get; private set; }
    public bool HealingPurchased { get; private set; }
    public event System.Action<int> OnGoldChanged;

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

    [Header("难度模式")]
    [Tooltip("Fixed 用于对照实验；Adaptive 根据玩家近期表现调节敌人压力")]
    public EDifficultyMode DifficultyMode = EDifficultyMode.Adaptive;

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
    private int _pendingLevelUps;
    private string _runId;
    private int _completedWaves;

    private void Awake()
    {
        Instance = this;
        Progression = MetaProgression.CreateDefault();

        // 清空上一局残留的注册表/池（防编辑器热重载与跨局残留）
        EnemyRegistry.Clear();
        EnemyPool.Clear();

        // 初始化引用
        WaveManager = FindAnyObjectByType<WaveManager>();
        MonsterSpawner = FindAnyObjectByType<MonsterSpawner>();
        Player = FindAnyObjectByType<PlayerController>();
        DifficultyManager = GetComponent<DifficultyManager>();
        if (DifficultyManager == null)
            DifficultyManager = gameObject.AddComponent<DifficultyManager>();
        DifficultyManager.Mode = DifficultyMode;

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
        ShopUI = GetComponent<WaveShopUI>();
        if (ShopUI == null) ShopUI = gameObject.AddComponent<WaveShopUI>();
        TalentUI = GetComponent<TalentGraphUI>();
        if (TalentUI == null) TalentUI = gameObject.AddComponent<TalentGraphUI>();
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
        DifficultyManager?.Initialize(Player);
    }

    private void OnApplicationQuit() => Progression?.Persist();

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
        ShopUI?.SetVisible(newState == EGameState.Shop);
        if (newState != EGameState.MainMenu) TalentUI?.SetVisible(false);

        // 时间缩放
        if (newState == EGameState.MainMenu || newState == EGameState.Paused || newState == EGameState.LevelUp || newState == EGameState.GameOver || newState == EGameState.Shop)
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
        if (CurrentState != EGameState.MainMenu) return;
        ResetPlayer();
        ResetGameState();
        MonsterSpawner?.ResetSpawner();
        BeginRun();
        DifficultyManager?.ResetSession();
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
        if (_pendingLevelUps > 0)
        {
            TryShowNextLevelUp();
            return;
        }
        SetState(EGameState.Playing);
    }

    /// <summary>
    /// 重新开始游戏
    /// </summary>
    public void RestartGame()
    {
        if (CurrentState == EGameState.MainMenu) return;
        SettleRun();
        ResetWorld();
        ResetGameState();
        ResetPlayer();
        DifficultyManager?.ResetSession();
        BeginRun();
        SetState(EGameState.Playing);
        MainHUD?.Rebind();
        WaveManager?.StartGame();
    }

    /// <summary>
    /// 返回主菜单
    /// </summary>
    public void ReturnToMainMenu()
    {
        SettleRun();
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
        if (CurrentState == EGameState.MainMenu || CurrentState == EGameState.GameOver) return;
        SettleRun();
        _pendingLevelUps = 0;
        LevelUpUI?.Hide();
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
        if (CurrentState == EGameState.MainMenu || CurrentState == EGameState.GameOver) return;
        _pendingLevelUps++;
        if (CurrentState != EGameState.Playing) return;
        TryShowNextLevelUp();
    }

    private void TryShowNextLevelUp()
    {
        while (_pendingLevelUps > 0)
        {
            if (LevelUpUI == null)
            {
                _pendingLevelUps = 0;
                return;
            }

            int remainingSlots = Player != null ? Player.GetRemainingWeaponSlots() : 0;
            List<UpgradeOption> options = GenerateUpgradeOptions(3, remainingSlots);

            if (options.Count > 0)
            {
                LevelUpUI.ShowOptions(options);
                SetState(EGameState.LevelUp);
                return;
            }

            _pendingLevelUps--;
            Debug.LogWarning("[GameManager] 没有可用的升级选项，跳过本次升级");
        }

        ResumeGame();
    }

    /// <summary>
    /// 升级选项被选择后回调（由 LevelUpUI 调用，或由升级 UI 内部处理）
    /// </summary>
    public void OnUpgradeSelected()
    {
        if (CurrentState != EGameState.LevelUp || _pendingLevelUps <= 0) return;
        if (_pendingLevelUps > 0)
            _pendingLevelUps--;
        TryShowNextLevelUp();
    }

    /// <summary>
    /// 记录击杀
    /// </summary>
    public void AddKill()
    {
        TotalKills++;
        DifficultyManager?.RecordKill();
    }

    /// <summary>
    /// 升级选项生成算法
    /// </summary>
    public List<UpgradeOption> GenerateUpgradeOptions(int count, int remainingWeaponSlots)
    {
        count = Mathf.Max(count, 1);
        var candidates = new List<UpgradeOption>();
        int playerLevel = Player != null ? Player.StatsComponent.CurrentLevel : 1;
        int currentWave = WaveManager?.CurrentWave ?? 1;
        var equippedTypes = new HashSet<EWeaponType>();
        if (Player != null)
        {
            foreach (Weapon weapon in Player.EquippedWeapons)
            {
                if (weapon != null && weapon.WeaponDef != null)
                    equippedTypes.Add(weapon.WeaponDef.Type);
            }
        }

        if (UpgradeOptionPool != null)
        {
            foreach (UpgradeOption option in UpgradeOptionPool)
            {
                if (option == null || option.MinLevelToAppear > playerLevel) continue;
                if (option.Type == EUpgradeType.Weapon)
                {
                    if (remainingWeaponSlots <= 0 || option.WeaponDef == null) continue;
                    if (option.WeaponDef.MinWaveToAppear > currentWave) continue;
                    if (equippedTypes.Contains(option.WeaponDef.Type)) continue;
                }
                candidates.Add(option);
            }
        }

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int optionCount = Mathf.Min(count, candidates.Count);
        return candidates.GetRange(0, optionCount);
    }

    // === 内部方法 ===

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        Gold += amount;
        OnGoldChanged?.Invoke(Gold);
    }

    public void OpenWaveShop()
    {
        if (CurrentState != EGameState.Playing) return;
        _completedWaves++;
        HealingPurchased = false;
        SetState(EGameState.Shop);
    }

    public bool BuyHealing()
    {
        var stats = Player?.StatsComponent;
        int price = Mathf.Max(1, HealingCost);
        if (CurrentState != EGameState.Shop || HealingPurchased || Gold < price || stats == null ||
            stats.CurrentHP <= 0f || stats.CurrentHP >= stats.GetEffectiveMaxHP()) return false;
        Gold -= price;
        HealingPurchased = true;
        stats.Heal(Mathf.Max(1f, HealingAmount));
        OnGoldChanged?.Invoke(Gold);
        return true;
    }

    public void ContinueFromShop()
    {
        if (CurrentState != EGameState.Shop) return;
        SetState(EGameState.Playing);
        WaveManager?.StartNextWave();
        if (_pendingLevelUps > 0) TryShowNextLevelUp();
    }

    private void ResetGameState()
    {
        TotalKills = 0;
        GamePlayTime = 0f;
        _completedWaves = 0;
        _pendingLevelUps = 0;
        LevelUpUI?.Hide();
        Gold = 0;
        HealingPurchased = false;
        OnGoldChanged?.Invoke(Gold);
    }

    private void ResetWorld()
    {
        // 清除所有敌人
        if (MonsterSpawner != null)
        {
            MonsterSpawner.ClearAllEnemies();
            MonsterSpawner.ResetSpawner();
        }
        EnemyRegistry.Clear();

        // 清除所有经验球
        XPOrb.ClearAllOrbs();

        // 清除所有敌人弹幕
        var enemyProjectiles = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
        foreach (var proj in enemyProjectiles)
        {
            proj.gameObject.SetActive(false);
            Destroy(proj.gameObject);
        }

        // 重开/返回菜单时回收上一局的魔法弹，并清除火焰粒子。
        foreach (var proj in FindObjectsByType<MagicBulletProjectile>())
            MagicBulletWeapon.Pool.Release(proj.gameObject);
        foreach (var proj in FindObjectsByType<FlameProjectile>())
        {
            proj.gameObject.SetActive(false);
            Destroy(proj.gameObject);
        }
        foreach (var popup in FindObjectsByType<DamagePopupBehavior>())
        {
            popup.gameObject.SetActive(false);
            Destroy(popup.gameObject);
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
            {
                weapon.gameObject.SetActive(false);
                Destroy(weapon.gameObject);
            }
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
        stats.SetTalentBonuses(Progression.GetBonuses());
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

    private void BeginRun() => _runId = System.Guid.NewGuid().ToString("N");

    private void SettleRun()
    {
        if (_runId == null) return;
        Progression.SettleRun(_runId, TotalKills, GamePlayTime, _completedWaves);
        _runId = null;
    }
}
