using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class TalentRankSave
{
    public string Id;
    public int Rank;
}

[Serializable]
public class MetaProgressionSave
{
    public int Version = 1;
    public long TotalXP;
    public long UnspentXP;
    public int EarnedPoints;
    public string LastSettledRunId;
    public List<string> SettledRunIds = new List<string>();
    public List<TalentRankSave> Ranks = new List<TalentRankSave>();
}

public class RunProgressionReward
{
    public int XP;
    public int Points;
}

// One profile owns both rewards and allocations; every mutation is saved immediately.
public class MetaProgression
{
    private MetaProgressionSave _save = new MetaProgressionSave();
    private readonly Dictionary<string, int> _ranks = new Dictionary<string, int>();
    private readonly HashSet<string> _settledRuns = new HashSet<string>();
    private readonly TalentEffectRegistry _effects;
    private bool _readOnly;
    public TalentGraph Graph { get; }
    public string SavePath { get; }
    public string StorageError { get; private set; }
    public bool IsReadOnly => _readOnly;
    public long TotalXP => _save.TotalXP;
    public long UnspentXP => _save.UnspentXP;
    public int EarnedPoints => _save.EarnedPoints;
    public int SpentPoints
    {
        get { int total = 0; foreach (int rank in _ranks.Values) total += rank; return total; }
    }
    public int AvailablePoints => Math.Max(0, EarnedPoints - SpentPoints);
    public int NextPointXP => Graph.Rules.GetPointCost(EarnedPoints);
    public bool AllPointsEarned => EarnedPoints >= Graph.TotalRanks;
    public RunProgressionReward LastReward { get; private set; }
    public event Action OnChanged;

    public MetaProgression(TalentGraphDefinition definition, string savePath, TalentEffectRegistry effects = null)
    {
        _effects = effects ?? new TalentEffectRegistry();
        Graph = new TalentGraph(definition, _effects);
        SavePath = savePath;
        Load();
    }

    public static MetaProgression CreateDefault()
    {
        var data = Resources.Load<TextAsset>("Progression/MageTalentGraph");
        if (data == null) throw new InvalidOperationException("Missing MageTalentGraph configuration.");
        return new MetaProgression(JsonUtility.FromJson<TalentGraphDefinition>(data.text),
            Path.Combine(Application.persistentDataPath, "meta_progression_v1.json"));
    }

    public int GetRank(string id) => id != null && _ranks.TryGetValue(id, out int rank) ? rank : 0;

    public bool IsConnected(string id)
    {
        if (Graph.GetNode(id) == null) return false;
        if (id == Graph.StartNodeId || GetRank(id) > 0) return true;
        foreach (string neighbor in Graph.GetNeighbors(id))
            if (neighbor == Graph.StartNodeId || GetRank(neighbor) > 0) return true;
        return false;
    }

    public bool CanAllocate(string id)
    {
        var node = Graph.GetNode(id);
        return !_readOnly && node != null && id != Graph.StartNodeId && AvailablePoints > 0 &&
            GetRank(id) < node.MaxRank && IsConnected(id);
    }

    public bool TryAllocate(string id)
    {
        if (!CanAllocate(id)) return false;
        _ranks[id] = GetRank(id) + 1;
        Persist(); OnChanged?.Invoke();
        return true;
    }

    public bool ResetTalents()
    {
        if (_readOnly || _ranks.Count == 0) return false;
        _ranks.Clear(); Persist(); OnChanged?.Invoke();
        return true;
    }

    public TalentBonuses GetBonuses()
    {
        var bonuses = new TalentBonuses();
        foreach (var node in Graph.Nodes)
            if (node.Id != Graph.StartNodeId) _effects.Apply(node, GetRank(node.Id), bonuses);
        return bonuses;
    }

    public RunProgressionReward SettleRun(string runId, int kills, float combatSeconds, int completedWaves)
    {
        if (_readOnly || string.IsNullOrWhiteSpace(runId) || _settledRuns.Contains(runId)) return null;
        int reward = Graph.Rules.GetRunXP(kills, combatSeconds, completedWaves);
        _save.LastSettledRunId = runId;
        _settledRuns.Add(runId);
        _save.SettledRunIds.Add(runId);
        _save.TotalXP += reward;
        _save.UnspentXP += reward;
        int before = EarnedPoints;
        ConvertXPToPoints();
        LastReward = new RunProgressionReward { XP = reward, Points = EarnedPoints - before };
        Persist(); OnChanged?.Invoke();
        return LastReward;
    }

    // Content must use unique IDs and registered effect handlers; file loading is separate.
    public bool TryRegisterContent(TalentGraphDefinition content, out string error)
    {
        if (_readOnly) { error = "Progression save is read-only."; return false; }
        if (!Graph.TryAppend(content, out error)) return false;
        ConvertXPToPoints(); Persist(); OnChanged?.Invoke();
        return true;
    }

    private void ConvertXPToPoints()
    {
        while (!AllPointsEarned && UnspentXP >= NextPointXP)
        { _save.UnspentXP -= NextPointXP; _save.EarnedPoints++; }
    }

    private void Load()
    {
        if (!File.Exists(SavePath)) return;
        try
        {
            var loaded = JsonUtility.FromJson<MetaProgressionSave>(File.ReadAllText(SavePath));
            if (loaded == null || loaded.Version != 1 || loaded.TotalXP < 0 || loaded.UnspentXP < 0 ||
                loaded.UnspentXP > loaded.TotalXP || loaded.EarnedPoints < 0 || loaded.EarnedPoints > 100000)
                throw new InvalidDataException("Unsupported or invalid progression save.");
            _save = loaded;
            if (_save.Ranks == null) _save.Ranks = new List<TalentRankSave>();
            if (_save.SettledRunIds == null) _save.SettledRunIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(_save.LastSettledRunId)) _settledRuns.Add(_save.LastSettledRunId);
            foreach (string id in _save.SettledRunIds)
                if (!string.IsNullOrWhiteSpace(id)) _settledRuns.Add(id);
            if (_save.Ranks != null)
                foreach (var entry in _save.Ranks)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || entry.Rank <= 0) continue;
                    var node = Graph.GetNode(entry.Id);
                    if (entry.Id == Graph.StartNodeId) continue;
                    int rank = Math.Min(entry.Rank, node != null ? node.MaxRank : 100);
                    _ranks[entry.Id] = Math.Max(GetRank(entry.Id), rank);
                }
            if (SpentPoints > EarnedPoints) _ranks.Clear();
            // Retain missing content IDs, but refund known nodes disconnected by content changes.
            var reached = new HashSet<string> { Graph.StartNodeId };
            var queue = new Queue<string>(); queue.Enqueue(Graph.StartNodeId);
            while (queue.Count > 0)
                foreach (string id in Graph.GetNeighbors(queue.Dequeue()))
                    if (GetRank(id) > 0 && reached.Add(id)) queue.Enqueue(id);
            foreach (var node in Graph.Nodes)
                if (!reached.Contains(node.Id)) _ranks.Remove(node.Id);
            ConvertXPToPoints();
        }
        catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is ArgumentException || exception is UnauthorizedAccessException)
        {
            _readOnly = true;
            _save = new MetaProgressionSave(); _ranks.Clear(); _settledRuns.Clear();
            StorageError = "局外存档无法读取，已保留原文件，请检查备份。";
            Debug.LogWarning("[MetaProgression] " + StorageError + " " + exception.Message);
        }
    }

    public bool Persist()
    {
        if (_readOnly) return false;
        try
        {
            _save.Ranks.Clear();
            foreach (var pair in _ranks) _save.Ranks.Add(new TalentRankSave { Id = pair.Key, Rank = pair.Value });
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            string temporary = SavePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(_save, true));
            if (File.Exists(SavePath))
            {
                File.Copy(SavePath, SavePath + ".bak", true);
                File.Replace(temporary, SavePath, null);
            }
            else File.Move(temporary, SavePath);
            StorageError = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            StorageError = "局外进度暂未保存，关闭游戏前请检查存储权限。";
            Debug.LogWarning("[MetaProgression] " + StorageError + " " + exception.Message);
            return false;
        }
    }
}
