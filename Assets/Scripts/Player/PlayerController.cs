using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("组件")]
    public CharacterController CharacterController;
    public PlayerStatsComponent StatsComponent;
    public Transform CameraTransform;

    [Header("武器")]
    public List<Weapon> EquippedWeapons = new List<Weapon>();
    public const int MaxWeaponSlots = 6;

    [Header("初始武器")]
    public WeaponDefinition StartingWeaponPrefab;

    [Header("视觉")]
    [Tooltip("是否在启动时自动生成一个胶囊体作为玩家模型")]
    public bool AutoCreateVisual = true;
    public Material PlayerMaterial;
    public Vector3 VisualScale = new Vector3(0.8f, 0.9f, 0.8f);

    [Header("输入")]
    [Tooltip("输入源：键盘鼠标 / 方向键 / 网络输入。为空时自动挂载键盘鼠标")]
    public IInputProvider InputProvider;

    /// <summary>升级中：暂停自身输入与武器攻击（双人"升级不暂停"玩法）</summary>
    public bool IsChoosingUpgrade { get; set; }

    /// <summary>本局出生点（重新开始时回到这里）</summary>
    public Vector3 SpawnPosition;
    public Vector3 SpawnRotation;

    private Vector2 _moveInput;
    private Vector3 _aimDirection;
    private Camera _mainCamera;
    private Vector3 _targetMoveDir;
    private Vector3 _lastMoveDir;

    private void Awake()
    {
        CharacterController = GetComponent<CharacterController>();
        StatsComponent = GetComponent<PlayerStatsComponent>();
        SpawnPosition = transform.position;
        SpawnRotation = transform.eulerAngles;
    }

    private void Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            _mainCamera = FindAnyObjectByType<Camera>();
        }

        if (_mainCamera != null)
        {
            CameraTransform = _mainCamera.transform;
        }
        else
        {
            Debug.LogWarning("PlayerController: 未找到相机，移动将使用世界坐标方向");
        }

        if (CharacterController == null)
        {
            Debug.LogError("PlayerController: 未找到 CharacterController，角色无法移动");
        }

        // 无输入源时默认键盘鼠标（场景玩家零配置）
        if (InputProvider == null)
            InputProvider = gameObject.AddComponent<KeyboardMouseInputProvider>();

        // 注册到 GameManager（多玩家支持，幂等）
        GameManager.Instance?.RegisterPlayer(this);

        SpawnStartingWeapon();
        CreateSimpleVisual();
        StatsComponent.OnDeath += OnPlayerDeath;
    }

    private void Update()
    {
        // 升级中：暂停操作（不移动、不转向），武器由 Weapon 自行停火
        if (IsChoosingUpgrade) return;

        HandleInput();
        UpdateMovementVector();
        ApplyMovement();
    }

    private void HandleInput()
    {
        if (InputProvider == null) return;

        _moveInput = InputProvider.MoveInput;

        // 输入源提供 XZ 平面瞄准方向（鼠标或移动方向），转为世界朝向
        Vector2 aim2D = InputProvider.AimDirection;
        _aimDirection = aim2D.sqrMagnitude > 0.01f
            ? new Vector3(aim2D.x, 0f, aim2D.y).normalized
            : Vector3.zero;
    }

    private void UpdateMovementVector()
    {
        if (_moveInput.sqrMagnitude <= 0.01f)
        {
            _targetMoveDir = Vector3.zero;
            return;
        }

        Vector3 forward;
        Vector3 right;

        if (CameraTransform != null)
        {
            forward = CameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();
            right = CameraTransform.right;
            right.y = 0f;
            right.Normalize();
        }
        else
        {
            forward = Vector3.forward;
            right = Vector3.right;
        }

        _targetMoveDir = (forward * _moveInput.y + right * _moveInput.x).normalized;

        // 检测方向突变（调试抽搐）
        if (_lastMoveDir.sqrMagnitude > 0.01f && Vector3.Dot(_lastMoveDir, _targetMoveDir) < 0.3f)
        {
            Debug.LogWarning($"[PlayerController] Direction JUMP at {Time.time:F2}: {_targetMoveDir} (was {_lastMoveDir})");
        }
        _lastMoveDir = _targetMoveDir;
    }

    private void ApplyMovement()
    {
        float speed = StatsComponent != null ? StatsComponent.GetEffectiveMoveSpeed() : 6f;

        if (_targetMoveDir.sqrMagnitude > 0.01f)
        {
            // 使用 Time.smoothDeltaTime 代替 Time.deltaTime，
            // 消除帧率波动导致的移动步长忽大忽小（抽搐感）
            float dt = CharacterController != null ? Time.smoothDeltaTime : Time.deltaTime;
            Vector3 motion = _targetMoveDir * speed * dt;

            if (CharacterController != null)
            {
                CharacterController.Move(motion);
            }
            else
            {
                transform.position += motion;
            }
        }

        // 更新朝向
        if (_aimDirection.sqrMagnitude > 0.01f)
        {
            transform.forward = _aimDirection;
        }
    }

    public void SpawnStartingWeapon()
    {
        if (StartingWeaponPrefab != null)
        {
            AddWeapon(StartingWeaponPrefab);
        }
    }

    private void CreateSimpleVisual()
    {
        if (!AutoCreateVisual) return;

        if (transform.Find("PlayerVisual") != null) return;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "PlayerVisual";
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = VisualScale;

        Destroy(visual.GetComponent<Collider>());

        if (PlayerMaterial != null)
        {
            visual.GetComponent<Renderer>().material = PlayerMaterial;
        }
    }

    public bool AddWeapon(WeaponDefinition weaponDef)
    {
        if (IsWeaponSlotFull()) return false;

        var weaponObj = new GameObject(weaponDef.Name);
        weaponObj.transform.SetParent(transform);
        weaponObj.transform.localPosition = Vector3.zero;

        Weapon weapon = weaponObj.AddComponent(weaponDef.Type switch
        {
            EWeaponType.MagicBullet => typeof(MagicBulletWeapon),
            EWeaponType.FlameThrower => typeof(FlameThrowerWeapon),
            EWeaponType.SpellOrbit => typeof(SpellOrbitWeapon),
            _ => typeof(MagicBulletWeapon)
        }) as Weapon;

        weapon.Initialize(this, weaponDef);
        EquippedWeapons.Add(weapon);
        return true;
    }

    public bool IsWeaponSlotFull() => EquippedWeapons.Count >= MaxWeaponSlots;
    public int GetRemainingWeaponSlots() => MaxWeaponSlots - EquippedWeapons.Count;

    public void ApplyUpgrade(UpgradeOption option)
    {
        if (option.Type == EUpgradeType.Weapon && option.WeaponDef != null)
        {
            AddWeapon(option.WeaponDef);
        }
        else
        {
            StatsComponent.ApplyUpgrade(option);
        }
    }

    public void OnPlayerDeath()
    {
        Debug.Log($"游戏结束! 存活波次: {GameManager.Instance?.WaveManager.CurrentWave}, 等级: {StatsComponent.CurrentLevel}");
        GameManager.Instance?.GameOver();
    }
}