using UnityEngine;

// 幽灵 - 会冲刺
public class GhostEnemy : Enemy
{
    private bool _isDashing;
    private float _dashCooldown;
    private float _dashDuration;
    private Vector3 _dashDirection;

    public override void Initialize(EnemyDefinition def, float statMultiplier)
    {
        base.Initialize(def, statMultiplier);
        _dashCooldown = EnemyDef.DashCooldown;
    }

    protected override void Update()
    {
        base.Update();

        if (EnemyDef.bCanDash)
        {
            _dashCooldown -= Time.deltaTime;
            if (!_isDashing && _dashCooldown <= 0)
            {
                StartDash();
            }
            else if (_isDashing)
            {
                _dashDuration -= Time.deltaTime;
                transform.position += _dashDirection * EnemyDef.DashSpeed * StatMultiplier * Time.deltaTime;
                if (_dashDuration <= 0)
                {
                    _isDashing = false;
                    _dashCooldown = EnemyDef.DashCooldown;
                }
            }
        }
    }

    protected override void CreateVisual()
    {
        float scale = EnemyDef.MeshScale;
        Color ghostColor = new Color(0.75f, 0.85f, 0.95f);

        // 头部
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "Head",
            new Vector3(0f, scale * 0.25f, 0f),
            Vector3.one * scale * 0.45f, ghostColor);

        // 下摆（拉长球体模拟）
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "Tail",
            new Vector3(0f, -scale * 0.25f, 0f),
            new Vector3(scale * 0.4f, scale * 0.6f, scale * 0.4f), ghostColor);

        // 眼睛
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "EyeL",
            new Vector3(-scale * 0.12f, scale * 0.3f, scale * 0.25f),
            Vector3.one * scale * 0.08f, Color.black);
        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "EyeR",
            new Vector3(scale * 0.12f, scale * 0.3f, scale * 0.25f),
            Vector3.one * scale * 0.08f, Color.black);
    }

    private void StartDash()
    {
        _isDashing = true;
        _dashDuration = EnemyDef.DashDuration;
        _dashDirection = (GetTargetLocation() - transform.position).normalized;
    }
}
