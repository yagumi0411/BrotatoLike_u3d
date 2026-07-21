using UnityEngine;
using TMPro;

public static class DamagePopup
{
    private static Camera _mainCamera;

    public static void Spawn(Vector3 worldPosition, float damage, bool isCrit = false)
    {
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        // 创建伤害飘字 GameObject（世界空间 TextMeshPro，非 UI）
        GameObject obj = new GameObject("DamagePopup");
        obj.transform.position = worldPosition + Vector3.up * 2f;
        obj.transform.localScale = Vector3.one * 0.5f;

        var tmp = obj.AddComponent<TextMeshPro>();
        tmp.text = isCrit ? $"{(int)damage}!" : $"{(int)damage}";
        tmp.fontSize = isCrit ? 6f : 4f;
        tmp.color = isCrit ? new Color(1f, 0.3f, 0.1f) : Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSizeMin = 1f;
        tmp.fontSizeMax = 10f;
        tmp.enableAutoSizing = true;

        // 始终面向相机
        if (_mainCamera != null)
        {
            obj.transform.LookAt(_mainCamera.transform);
            obj.transform.Rotate(0, 180f, 0f);
        }

        var behavior = obj.AddComponent<DamagePopupBehavior>();
        behavior.Initialize(isCrit);
    }
}

public class DamagePopupBehavior : MonoBehaviour
{
    private TextMeshPro _tmp;
    private float _lifetime = 1.2f;
    private Vector3 _velocity;
    private bool _isCrit;

    public void Initialize(bool isCrit)
    {
        _isCrit = isCrit;
        _lifetime = isCrit ? 1.5f : 1.0f;
        _tmp = GetComponent<TextMeshPro>();
        _velocity = Vector3.up * (isCrit ? 3f : 2f) + Random.insideUnitSphere * 0.3f;
        _velocity.x *= 0.5f;
        _velocity.z *= 0.5f;
    }

    private void Update()
    {
        _lifetime -= Time.deltaTime;

        // 向上飘动并减速
        transform.position += _velocity * Time.deltaTime;
        _velocity.y *= 0.97f;

        // 面向相机
        var cam = Camera.main;
        if (cam != null)
        {
            transform.LookAt(cam.transform);
            transform.Rotate(0, 180f, 0f);
        }

        // 渐隐
        if (_lifetime < 0.3f && _tmp != null)
        {
            _tmp.alpha = _lifetime / 0.3f;
        }

        if (_lifetime <= 0f)
        {
            Destroy(gameObject);
        }
    }
}