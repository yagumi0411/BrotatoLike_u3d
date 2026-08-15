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

    // === 玩家 ===
    /// <summary>当前局内的所有玩家（单机=1，同屏双人=2，联机=按连接数）</summary>
    public List<PlayerController> Players = new List<PlayerController>();

    /// <summary>主玩家：优先返回激活中的玩家（联机下为本地网络玩家），兼容 HUD/相机/结算等单玩家引用</summary>
    public PlayerController Player =>
        Players.Find(p => p != null && p.gameObject.activeInHierarchy)
        ?? (Players.Count > 0 ? Players[0] : null);

    /// <summary>新玩家注册事件（联机玩家生成后触发，HUD 等监听重绑）</summary>
    public event Action OnPlayerRegistered;

    public enum EGameMode
    {
        Single,      // 单人模式
        LocalCoop,   // 本地同屏双人
        Online       // 联机双人（阶段 2 实现）
    }

    [Header("游戏模式")]
    public EGameMode GameMode = EGameMode.LocalCoop;
    public Material Player2Material;

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

    private void Awake()
    {
        Instance = this;

        // 清空上一局残留的注册表/池（防编辑器热重载与跨局残留）
        EnemyRegistry.Clear();
        EnemyPool.Clear();

        // 初始化引用
        WaveManager = FindAnyObjectByType<WaveManager>();
        MonsterSpawner = FindAnyObjectByType<MonsterSpawner>();

        // 注册场景内已有玩家（重复注册幂等）
        RegisterPlayer(FindAnyObjectByType<PlayerController>());

        // 对 MainHUD 和 LevelUpUI 使用容错查找
        if (MainHUD == null)
            MainHUD = FindAnyObjectByType<MainHUD>();
        if (LevelUpUI == null)
            LevelUpUI = FindAnyObjectByType<LevelUpUI>();

        // 按当前模式生成玩家 2（仅本地双人模式）
        EnsureLocalPlayerTwo();
    }

    private void OnDestroy()
    {
        // 解除事件绑定，防止泄漏
        foreach (var player in Players)
        {
            if (player != null && player.StatsComponent != null)
                player.StatsComponent.OnLevelUp -= OnPlayerLevelUp;
        }
    }

    private void Start()
    {
        // 默认进入主菜单
        SetState(EGameState.MainMenu);
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

        // 结算/主菜单时强制关闭升级面板（双人模式下另一玩家可能正处于升级中，
        // 若其阵亡结算，升级 UI 会残留；EndLevelUp 同时恢复该玩家的操作状态）
        if (newState == EGameState.GameOver || newState == EGameState.MainMenu)
        {
            LevelUpUI?.EndLevelUp();
        }

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

        // 时间缩放（升级不再全局暂停：双人"升级不暂停"，升级者本人短暂无敌由 PlayerStatsComponent 管理）
        if (newState == EGameState.MainMenu || newState == EGameState.Paused || newState == EGameState.GameOver)
        {
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    // === 玩家注册与生成 ===

    /// <summary>注册玩家（幂等）：加入列表并绑定升级事件</summary>
    public void RegisterPlayer(PlayerController player)
    {
        if (player == null || Players.Contains(player)) return;

        Players.Add(player);
        if (player.StatsComponent != null)
            player.StatsComponent.OnLevelUp += OnPlayerLevelUp;

        OnPlayerRegistered?.Invoke();
    }

    /// <summary>取距离指定位置最近的玩家</summary>
    public PlayerController GetNearestPlayer(Vector3 position)
    {
        PlayerController best = null;
        float bestSqr = float.MaxValue;
        foreach (var player in Players)
        {
            // 跳过未激活玩家（单人模式下玩家 2 停用）；
            // 联机下只与本地控制玩家交互（远端玩家是同步幽灵，不参与本地索敌/磁吸）
            if (player == null || !player.gameObject.activeInHierarchy || !player.IsLocallyControlled) continue;
            float distSqr = (player.transform.position - position).sqrMagnitude;
            if (distSqr < bestSqr)
            {
                bestSqr = distSqr;
                best = player;
            }
        }
        return best;
    }

    /// <summary>切换游戏模式（主菜单模式按钮调用），即时调整玩家激活状态</summary>
    public void SetGameMode(EGameMode mode)
    {
        GameMode = mode;
        ApplyGameMode();
    }

    private void ApplyGameMode()
    {
        bool coop = GameMode == EGameMode.LocalCoop;
        if (coop)
        {
            EnsureLocalPlayerTwo();   // 需要时生成玩家 2（仅一次，之后复用）
        }

        // 玩家 1 恒激活；玩家 2 仅本地双人激活（联机模式阶段 2 按连接数生成）
        for (int i = 1; i < Players.Count; i++)
        {
            if (Players[i] != null)
                Players[i].gameObject.SetActive(coop);
        }
    }

    /// <summary>同屏双人：克隆玩家 1 生成玩家 2（方向键输入 + 头顶血条 + 材质区分）</summary>
    private void EnsureLocalPlayerTwo()
    {
        if (GameMode != EGameMode.LocalCoop || Players.Count == 0 || Players.Count >= 2) return;

        var p1 = Players[0];
        var p2 = Instantiate(p1.gameObject);
        p2.name = "Player2";

        var p2Controller = p2.GetComponent<PlayerController>();
        if (p2Controller == null)
        {
            Destroy(p2);
            return;
        }

        // 出生在玩家 1 右侧，避免重叠
        Vector3 spawnPos = p1.SpawnPosition + new Vector3(3f, 0f, 0f);
        p2.transform.position = spawnPos;
        p2Controller.SpawnPosition = spawnPos;
        p2Controller.SpawnRotation = p1.SpawnRotation;

        // 输入源换为方向键（克隆会复制玩家 1 的输入组件，覆盖引用即可）
        var arrows = p2.AddComponent<ArrowsInputProvider>();
        p2Controller.InputProvider = arrows;

        // 视觉区分（换玩家 2 材质）
        var visual = p2.transform.Find("PlayerVisual");
        if (visual != null && Player2Material != null)
            visual.GetComponent<Renderer>().material = Player2Material;

        // 玩家 2 无 HUD 面板，挂头顶血条
        p2.AddComponent<PlayerStatusBar>();

        // 注册（入列表 + 绑定升级事件）
        RegisterPlayer(p2Controller);
    }

    // === 公共 API ===

    /// <summary>
    /// 开始游戏（从主菜单调用）
    /// </summary>
    public void StartGame()
    {
        if (GameMode == EGameMode.Online)
        {
            StartOnlineGame();
            return;
        }

        // 先按模式调整玩家激活状态，再统一重置
        ApplyGameMode();
        ResetPlayer();
        ResetGameState();
        SetState(EGameState.Playing);
        MainHUD?.Rebind();
        WaveManager?.StartGame();
    }

    /// <summary>联机模式：隐藏本地场景玩家，弹出联机面板（连接成功后由网络玩家触发开战）</summary>
    private void StartOnlineGame()
    {
        foreach (var p in Players)
        {
            if (p != null)
                p.gameObject.SetActive(false);
        }
        MainMenuUI?.ShowOnlinePanel();
    }

    /// <summary>本地网络玩家就绪（Host/Client 各自触发）：注册、开战</summary>
    public void OnLocalPlayerReady(PlayerController player)
    {
        RegisterPlayer(player);
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
    /// 玩家升级时调用（绑定在 PlayerStatsComponent.OnLevelUp，双人模式下只影响升级者本人）
    /// </summary>
    public void OnPlayerLevelUp(PlayerStatsComponent stats)
    {
        var player = Players.Find(p => p != null && p.StatsComponent == stats);
        if (player == null) return;

        // 面板复用：若另一玩家正在升级，先结束其升级状态
        LevelUpUI?.EndLevelUp();

        int remainingSlots = player.GetRemainingWeaponSlots();
        var options = GenerateUpgradeOptions(player, 3, remainingSlots);

        if (options.Count > 0)
        {
            player.IsChoosingUpgrade = true;    // 暂停本人操作（移动/转向/武器）
            stats.BeginLevelUpState();          // 3 秒无敌（升级期间保命）
            LevelUpUI?.ShowOptions(player, options);
        }
        else
        {
            Debug.LogWarning("[GameManager] 没有可用的升级选项，跳过升级");
        }
    }

    /// <summary>
    /// 记录击杀
    /// </summary>
    public void AddKill()
    {
        TotalKills++;
    }

    /// <summary>
    /// 升级选项生成算法（联机阶段改由服务器生成后广播，此处保留单机逻辑）
    /// </summary>
    public List<UpgradeOption> GenerateUpgradeOptions(PlayerController player, int count, int remainingWeaponSlots)
    {
        count = Mathf.Max(count, 1);
        var result = new List<UpgradeOption>();

        // 1. 筛选可用的数值升级选项
        int playerLevel = player != null ? player.StatsComponent.CurrentLevel : 1;
        var statUpgrades = UpgradeOptionPool?
            .Where(o => o.Type != EUpgradeType.Weapon && o.MinLevelToAppear <= playerLevel)
            .ToList() ?? new List<UpgradeOption>();

        // 2. 筛选可用的武器升级选项
        int currentWave = WaveManager?.CurrentWave ?? 1;
        var equippedTypes = player?.EquippedWeapons
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
        var enemyProjectiles = FindObjectsByType<EnemyProjectile>();
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
        // 双人模式：所有玩家一同重置
        foreach (var player in Players)
        {
            ResetSinglePlayer(player);
        }
    }

    private void ResetSinglePlayer(PlayerController player)
    {
        if (player == null || !player.gameObject.activeInHierarchy) return;

        // 退出升级状态（若上一局结束时仍在升级）
        player.IsChoosingUpgrade = false;

        // 清除所有武器
        foreach (var weapon in player.EquippedWeapons)
        {
            if (weapon != null)
                Destroy(weapon.gameObject);
        }
        player.EquippedWeapons.Clear();

        // 重置位置
        player.transform.position = player.SpawnPosition;
        player.transform.eulerAngles = player.SpawnRotation;

        // 如果使用 CharacterController，需要手动重置
        var cc = player.CharacterController;
        if (cc != null)
        {
            cc.enabled = false;
            cc.transform.position = player.SpawnPosition;
            cc.enabled = true;
        }

        // 重置属性
        var stats = player.StatsComponent;
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
        player.SpawnStartingWeapon();
    }
}