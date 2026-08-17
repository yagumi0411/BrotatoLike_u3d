using UnityEngine;

/// <summary>
/// 世界空间 UI 朝向相机（billboard）：每帧将物体旋转对齐到主相机，
/// 用于玩家头顶血条等需要始终朝向屏幕的 UI。
/// </summary>
public class FaceCamera : MonoBehaviour
{
    private Camera _camera;

    private void Start()
    {
        _camera = Camera.main ?? FindAnyObjectByType<Camera>();
    }

    private void LateUpdate()
    {
        if (_camera == null) return;

        // 正面朝向相机（保留竖直，避免血条躺平）
        transform.rotation = Quaternion.LookRotation(
            transform.position - _camera.transform.position,
            Vector3.up);
    }
}
