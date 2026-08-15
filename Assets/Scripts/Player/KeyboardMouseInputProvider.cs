using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家 1 输入源：WASD 移动 + 鼠标瞄准（原 PlayerController 内置逻辑，抽离为独立组件）。
/// </summary>
public class KeyboardMouseInputProvider : MonoBehaviour, IInputProvider
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 AimDirection { get; private set; }

    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main ?? FindAnyObjectByType<Camera>();
    }

    private void Update()
    {
        Vector2 input = Vector2.zero;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
        }
        MoveInput = input.normalized;

        // 鼠标瞄准（射线打到 Ground 层）
        AimDirection = Vector2.zero;
        var mouse = Mouse.current;
        if (mouse != null && _mainCamera != null)
        {
            Ray ray = _mainCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore))
            {
                Vector3 targetPoint = hit.point;
                targetPoint.y = transform.position.y;
                Vector3 dir = targetPoint - transform.position;
                AimDirection = new Vector2(dir.x, dir.z).normalized;
            }
        }
    }
}
