using UnityEngine;

// 表现层朝向相机；父对象仍使用原有三维移动、朝向和碰撞。
public class DungeonSpriteVisual : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private Sprite[] _idle;
    private Sprite[] _run;
    private Transform _owner;
    private Rigidbody _body;
    private Camera _camera;
    private Vector3 _lastPosition;
    private Color _tint;
    private float _clock;
    private bool _moving;
    private float _motionGrace;
    private float _flash;
    private float _footOffset;
    private float _scale;
    private SpriteRenderer _staff;

    public void Configure(string idle, string run, float scale, Color tint, float footOffset)
    {
        _owner = transform.parent;
        _body = _owner.GetComponent<Rigidbody>();
        _camera = Camera.main;
        _renderer = GetComponent<SpriteRenderer>();
        if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
        _renderer.sharedMaterial = DungeonArt.SpriteMaterial;
        _idle = DungeonArt.GetAnimation(idle);
        _run = DungeonArt.GetAnimation(run);
        _scale = scale;
        _tint = tint;
        _footOffset = footOffset;
        _renderer.sprite = _idle[0];
        _renderer.color = tint;
        _renderer.flipX = false;
        _lastPosition = _owner.position;
        if (_owner.GetComponent<PlayerController>() != null && _staff == null)
        {
            var staff = new GameObject("DungeonStaff");
            staff.transform.SetParent(transform, false);
            staff.transform.localScale = Vector3.one * 0.8f;
            _staff = staff.AddComponent<SpriteRenderer>();
            _staff.sprite = DungeonArt.GetSprite("weapon_red_magic_staff");
            _staff.sharedMaterial = DungeonArt.SpriteMaterial;
        }
        _clock = 0f;
        _moving = false;
        _motionGrace = 0f;
        _flash = 0f;
        UpdateTransform();
    }

    public void Flash() => _flash = 0.12f;

    private void LateUpdate()
    {
        if (_owner == null || _idle == null) return;
        if (_camera == null) _camera = Camera.main;
        Vector3 delta = _owner.position - _lastPosition;
        if (Time.deltaTime <= 0f || (GameManager.Instance != null &&
            GameManager.Instance.CurrentState != GameManager.EGameState.Playing))
        {
            UpdateTransform();
            _lastPosition = _owner.position;
            return;
        }
        // 敌人的物理更新与渲染帧率不同，不能用单帧位移在待机/跑动之间切换。
        Vector3 velocity = _body != null ? _body.linearVelocity : delta / Time.deltaTime;
        velocity.y = 0f;
        bool hasMotion = velocity.sqrMagnitude > 0.01f;
        _motionGrace = hasMotion ? 0.12f : Mathf.Max(0f, _motionGrace - Time.deltaTime);
        bool moving = _motionGrace > 0f;
        if (moving != _moving)
        {
            _moving = moving;
            _clock = 0f;
        }
        if (hasMotion && _camera != null)
        {
            float horizontal = Vector3.Dot(velocity, _camera.transform.right);
            if (Mathf.Abs(horizontal) > 0.15f) _renderer.flipX = horizontal < 0f;
        }
        _clock += Time.deltaTime;
        var frames = moving && _run.Length > 0 ? _run : _idle;
        _renderer.sprite = frames[(int)(_clock * (moving ? 8f : 5f)) % frames.Length];
        _flash = Mathf.Max(0f, _flash - Time.deltaTime);
        _renderer.color = _flash > 0f ? new Color(1f, 0.45f, 0.45f, _tint.a) : _tint;
        UpdateTransform();
        _lastPosition = _owner.position;
    }

    private void UpdateTransform()
    {
        transform.position = _owner.position + Vector3.up * _footOffset;
        if (_camera != null) transform.rotation = _camera.transform.rotation;
        // 子对象忽略父对象缩放，防止经验球和投射物的物理尺寸影响精灵尺寸。
        Vector3 parentScale = _owner.lossyScale;
        transform.localScale = new Vector3(_scale / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
            _scale / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)),
            _scale / Mathf.Max(0.001f, Mathf.Abs(parentScale.z)));
        _renderer.sortingOrder = 1000 - Mathf.RoundToInt(_owner.position.z * 20f);
        if (_staff != null)
        {
            _staff.transform.localPosition = new Vector3(_renderer.flipX ? -0.45f : 0.45f, 0.15f, -0.01f);
            _staff.flipX = _renderer.flipX;
            _staff.sortingOrder = _renderer.sortingOrder + 1;
        }
    }
}
