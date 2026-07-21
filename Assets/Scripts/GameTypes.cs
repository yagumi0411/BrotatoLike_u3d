using System;

public enum EUpgradeType
{
    Weapon,
    MaxHP_Add,
    MaxHP_Mul,
    Damage_All_Mul,
    AttackSpeed_All_Mul,
    MoveSpeed_Add,
    ExpRate_Mul,
    PickupRange_Add,
    CritChance_Add,
    CritDamage_Mul
}

public enum EWeaponType
{
    MagicBullet,
    FlameThrower,
    SpellOrbit
}

public enum EEnemyType
{
    Slime,
    Skeleton,
    Bat,
    ShadowMage,
    Ghost
}

public enum ETargetMode
{
    Nearest,
    Random
}