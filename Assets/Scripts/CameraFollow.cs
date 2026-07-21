using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("留空则自动跟随 GameManager.Instance.Player")]
    public Transform Target;

    [Header("相对偏移")]
    public Vector3 Offset = new Vector3(0f, 15f, -10f);

    [Header("跟随平滑度")]
    [Range(0.01f, 1f)]
    public float Smoothness = 0.15f;

    private void LateUpdate()
    {
        if (Target == null)
        {
            Target = GameManager.Instance?.Player?.transform;
        }

        if (Target == null) return;

        Vector3 targetPosition = Target.position + Offset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Smoothness);
    }
}
