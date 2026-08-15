using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家 2 输入源：方向键移动，无鼠标可用，朝向 = 移动方向（Brotato 双人标准做法）。
/// </summary>
public class ArrowsInputProvider : MonoBehaviour, IInputProvider
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 AimDirection { get; private set; }

    private void Update()
    {
        Vector2 input = Vector2.zero;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.leftArrowKey.isPressed) input.x -= 1f;
        }
        MoveInput = input.normalized;
        AimDirection = MoveInput;
    }
}
