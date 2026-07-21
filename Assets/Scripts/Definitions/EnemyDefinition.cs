using UnityEngine;

[CreateAssetMenu(fileName = "Enemy_", menuName = "Game/Enemy Definition")]
public class EnemyDefinition : ScriptableObject
{
    [Header("基础信息")]
    public EEnemyType Type;
    public string Name;

    [Header("属性")]
    public float BaseHP = 10f;
    public float MoveSpeed = 3f;
    public float ContactDamage = 1f;
    public float BaseXP = 3f;
    public float CollisionRadius = 0.5f;
    public float MeshScale = 1f;

    [Header("远程")]
    public bool bIsRanged;
    public float RangedAttackInterval = 2f;
    public float ProjectileDamage = 3f;
    public float ProjectileSpeed = 8f;

    [Header("冲刺")]
    public bool bCanDash;
    public float DashCooldown = 3f;
    public float DashSpeed = 12f;
    public float DashDuration = 0.3f;

    [Header("解锁")]
    public int MinWaveToSpawn;
}