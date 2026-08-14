using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public float WaveInterval = 30f;
    public float RestBetweenWaves = 3f;

    public int CurrentWave { get; private set; }
    public float WaveTimer { get; private set; }
    public bool IsWaveActive { get; private set; }
    public float RestTimer { get; private set; }

    public event System.Action<int> OnWaveChanged;

    public void StartGame()
    {
        CurrentWave = 0;
        StartNextWave();
    }

    private void Update()
    {
        if (IsWaveActive)
        {
            WaveTimer -= Time.deltaTime;
            if (WaveTimer <= 0)
            {
                EndWave();
            }
        }
        else
        {
            RestTimer -= Time.deltaTime;
            if (RestTimer <= 0)
            {
                StartNextWave();
            }
        }
    }

    public void StartNextWave()
    {
        CurrentWave++;
        WaveTimer = WaveInterval;
        IsWaveActive = true;
        OnWaveChanged?.Invoke(CurrentWave);
    }

    public void EndWave()
    {
        IsWaveActive = false;
        RestTimer = RestBetweenWaves;
    }

    public void ResetGame()
    {
        CurrentWave = 0;
        WaveTimer = 0f;
        IsWaveActive = false;
        RestTimer = 0f;
    }

    public float GetEnemyStatMultiplier() => Mathf.Pow(1.06f, CurrentWave - 1);
    public float GetXPWaveMultiplier() => 1f + (CurrentWave - 1) * 0.1f;
}