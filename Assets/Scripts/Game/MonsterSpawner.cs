using System.Linq;
using System.Threading;
using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    public float ArenaRadius = 28f;
    [Tooltip("生成环内半径下限的兜底值：实际下限取 max(该值, 屏幕四角视线到玩家的最远距离)")]
    public float MinSpawnDistance = 13f;
    [Tooltip("怪物刷出点相对屏幕边缘的额外距离（世界单位）：各方向下限 = 该方向穿出视野的距离 + 该值")]
    public float SpawnMargin = 3f;
    public System.Collections.Generic.List<EnemyDefinition> EnemyPrefabs;

    private WaveManager _waveManager;
    private float _spawnTimer;

    private void Awake()
    {
        _waveManager = GetComponentInChildren<WaveManager>();
    }

    private void Update()
    {
        if (_waveManager == null || !_waveManager.IsWaveActive)
            return;

        float spawnInterval = GetSpawnInterval();
        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0)
        {
            SpawnEnemy();
            _spawnTimer = spawnInterval;
        }
    }

    private float GetSpawnInterval() =>
        1f / (2f + _waveManager.CurrentWave * 0.4f);

    private void SpawnEnemy()
    {
        var available = EnemyPrefabs
            .Where(e => e.MinWaveToSpawn <= _waveManager.CurrentWave)
            .ToList();

        //Debug.Log($"[MonsterSpawner] SpawnEnemy called. Pool={EnemyPrefabs.Count}, available={available.Count}, wave={_waveManager.CurrentWave}");

        if (available.Count == 0) return;

        var def = available[Random.Range(0, available.Count)];
        Vector3 pos = GetSpawnPosition();

        //Debug.Log($"[MonsterSpawner] Spawning {def.Name} at {pos}");

        // 从对象池获取实例（池空时首次创建），消除每次生成的 Instantiate 开销
        Enemy enemy = EnemyPool.Get(def.Type, () => CreateEnemyObject(def));
        enemy.transform.position = pos;
        enemy.Initialize(def, _waveManager.GetEnemyStatMultiplier());
    }

    /// <summary>首次创建裸敌人实例（之后复用池中休眠实例）</summary>
    private Enemy CreateEnemyObject(EnemyDefinition def)
    {
        GameObject enemyObj = new GameObject(def.Name);
        return enemyObj.AddComponent(def.Type switch
        {
            EEnemyType.Slime => typeof(SlimeEnemy),
            EEnemyType.Skeleton => typeof(SkeletonEnemy),
            EEnemyType.Bat => typeof(BatEnemy),
            EEnemyType.ShadowMage => typeof(ShadowMageEnemy),
            EEnemyType.Ghost => typeof(GhostEnemy),
            _ => typeof(SlimeEnemy)
        }) as Enemy;
    }

    public void ClearAllEnemies()
    {
        // 注册表遍历替代 FindObjectsByType 全场景扫描
        var enemies = EnemyRegistry.All;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (enemies[i] != null)
                Destroy(enemies[i].gameObject);
        }
    }

    public void ResetSpawner()
    {
        _spawnTimer = 0f;
    }

    /// <summary>
    /// 以玩家为中心的环形带生成：方向均匀随机（360° 均匀来袭），
    /// 各方向下限 = 该方向穿出屏幕边缘的距离 + SpawnMargin（保证屏幕外刷出、且各方向均衡），
    /// 上限 = 该方向到地图圆边界的距离（地图内约束）。可视四边形随相机实时计算，环带自动跟随玩家。
    /// </summary>
    private Vector3 GetSpawnPosition()
    {
        Vector3 playerPos = GameManager.Instance?.Player?.transform?.position ?? Vector3.zero;
        Vector2 playerPos2D = new Vector2(playerPos.x, playerPos.z);
        Vector2[] viewQuad = GetViewQuad(playerPos);

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;   // 防御：极小概率的零点向量

            float minRadius = GetViewDistance(playerPos2D, dir, viewQuad);
            float maxRadius = GetMaxSpawnRadius(playerPos, dir);
            if (maxRadius <= minRadius) continue;   // 该方向空间不足（如玩家贴边朝地图外），换个方向

            float radius = Random.Range(minRadius, maxRadius);
            return new Vector3(playerPos.x + dir.x * radius, 1f, playerPos.z + dir.y * radius);
        }

        // 兜底：无可用环形带（相机异常、或玩家贴边过紧）。
        // 玩家在圆内：沿随机方向取可达最远点（地图内优先，尽量远离玩家，位置随机）。
        // 玩家在圆外（异常场景配置）：以地图中心为锚点在圆边缘随机生成，避免生成在玩家脚下。
        if (playerPos2D.sqrMagnitude < ArenaRadius * ArenaRadius)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;

                float maxRadius = GetMaxSpawnRadius(playerPos, dir);
                if (maxRadius <= 0f) continue;

                return new Vector3(playerPos.x + dir.x * maxRadius, 1f, playerPos.z + dir.y * maxRadius);
            }
        }

        Vector2 centerDir = Random.insideUnitCircle.normalized;
        if (centerDir.sqrMagnitude < 0.001f) centerDir = Vector2.up;
        return new Vector3(centerDir.x * ArenaRadius, 1f, centerDir.y * ArenaRadius);
    }

    /// <summary>
    /// 屏幕四角视线与玩家水平面的交点（可视地面四边形，2D 化）。
    /// 透视投影保持直线性，屏幕边缘在玩家平面上映射为直线段，四边形即可视区域。
    /// 相机缺失或视线朝上时返回 null，调用方回退到 MinSpawnDistance。
    /// </summary>
    private Vector2[] GetViewQuad(Vector3 playerPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return null;

        // 环形顺序：左下→右下→右上→左上（保证相邻索引构成四边形边）
        Vector2[] viewportCorners =
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f)
        };

        Vector2[] quad = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            Ray ray = cam.ViewportPointToRay(viewportCorners[i]);
            if (ray.direction.y >= 0f) return null;   // 防御：视线不朝地面
            float t = (playerPos.y - ray.origin.y) / ray.direction.y;
            Vector3 hit = ray.GetPoint(t);
            quad[i] = new Vector2(hit.x, hit.z);
        }
        return quad;
    }

    /// <summary>
    /// 方向 dir 从玩家出发穿出可视四边形的距离 + SpawnMargin（该方向的环带下限）。
    /// 玩家在四边形内部，任意方向必与一条边相交，取射线-线段最近交点即可。
    /// </summary>
    private float GetViewDistance(Vector2 playerPos, Vector2 dir, Vector2[] quad)
    {
        if (quad == null) return MinSpawnDistance;

        float best = float.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            Vector2 e = quad[(i + 1) % 4] - quad[i];
            Vector2 w = quad[i] - playerPos;
            float denom = e.x * dir.y - e.y * dir.x;
            if (Mathf.Abs(denom) < 1e-6f) continue;        // 射线与边平行
            float s = (dir.x * w.y - dir.y * w.x) / denom; // 边上的参数
            float t = (w.y * e.x - w.x * e.y) / denom;     // 射线参数
            if (s < 0f || s > 1f || t <= 0f) continue;     // 交点不在边内或射线反向
            best = Mathf.Min(best, t);
        }
        return best < float.MaxValue ? best + SpawnMargin : MinSpawnDistance;
    }

    /// <summary>
    /// 玩家沿 dir 方向到地图圆（半径 ArenaRadius）边界的距离；玩家在圆外时返回 0。
    /// 解方程 t² + 2(p·d)t + (|p|² - R²) = 0 取正根。
    /// </summary>
    private float GetMaxSpawnRadius(Vector3 playerPos, Vector2 dir)
    {
        Vector2 p = new Vector2(playerPos.x, playerPos.z);
        float dot = Vector2.Dot(p, dir);
        float disc = dot * dot - (p.sqrMagnitude - ArenaRadius * ArenaRadius);
        if (disc <= 0f) return 0f;
        return Mathf.Max(0f, -dot + Mathf.Sqrt(disc));
    }
}