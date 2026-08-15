using UnityEngine;

/// <summary>
/// 输入源抽象：同屏双人（键盘/方向键）与网络输入共用同一接口。
/// MoveInput 为归一化移动向量；AimDirection 为 XZ 平面瞄准方向（零向量表示无瞄准）。
/// </summary>
public interface IInputProvider
{
    Vector2 MoveInput { get; }
    Vector2 AimDirection { get; }
}
