using UnityEngine;

[CreateAssetMenu(fileName = "Upgrade_", menuName = "Game/Upgrade Option")]
public class UpgradeOption : ScriptableObject
{
    public EUpgradeType Type;
    public string Name;
    [TextArea] public string Description;
    public float Value;

    [Header("武器专用")]
    public WeaponDefinition WeaponDef;

    [Header("解锁")]
    public int MinLevelToAppear;
}