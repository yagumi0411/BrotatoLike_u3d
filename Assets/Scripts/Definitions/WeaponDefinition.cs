using UnityEngine;

[CreateAssetMenu(fileName = "Weapon_", menuName = "Game/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [Header("基础信息")]
    public EWeaponType Type;
    public string Name;
    [TextArea] public string Description;

    [Header("战斗属性")]
    public float BaseDamage = 5f;
    public float AttackInterval = 1f;
    public float Range = 15f;
    public ETargetMode TargetMode = ETargetMode.Nearest;

    [Header("投射物")]
    public float ProjectileSpeed = 8f;
    public float ProjectileLifetime = 2f;
    public int ProjectileCount = 1;

    [Header("扇形")]
    public float ConeHalfAngle = 30f;

    [Header("环绕")]
    public int OrbitCount = 3;
    public float OrbitRadius = 1.5f;
    public float OrbitSpeed = 180f;

    [Header("视觉特效")]
    public GameObject ProjectileVFXPrefab;

    [Header("解锁")]
    public int MinWaveToAppear;
}