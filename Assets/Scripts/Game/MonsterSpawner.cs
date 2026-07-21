using System.Linq;
using System.Threading;
using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    public float ArenaRadius = 28f;
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
        1f / (2f + _waveManager.CurrentWave * 0.5f);

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

        GameObject enemyObj = new GameObject(def.Name);
        enemyObj.transform.position = pos;

        Enemy enemy = enemyObj.AddComponent(def.Type switch
        {
            EEnemyType.Slime => typeof(SlimeEnemy),
            EEnemyType.Skeleton => typeof(SkeletonEnemy),
            EEnemyType.Bat => typeof(BatEnemy),
            EEnemyType.ShadowMage => typeof(ShadowMageEnemy),
            EEnemyType.Ghost => typeof(GhostEnemy),
            _ => typeof(SlimeEnemy)
        }) as Enemy;

        enemy.Initialize(def, _waveManager.GetEnemyStatMultiplier());
    }

    public void ClearAllEnemies()
    {
        //var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        var enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude);
        foreach (var enemy in enemies)
        {
            Destroy(enemy.gameObject);
        }
    }

    public void ResetSpawner()
    {
        _spawnTimer = 0f;
    }

    private Vector3 GetSpawnPosition()
    {
        Vector2 random = Random.insideUnitCircle.normalized * ArenaRadius;
        return new Vector3(random.x, 1f, random.y);
    }
}