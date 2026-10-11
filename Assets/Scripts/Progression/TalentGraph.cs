using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TalentEffectDefinition
{
    public string Id;
    public float Value;
}

[Serializable]
public class TalentNodeDefinition
{
    public string Id;
    public string Name;
    public string Description;
    public string Branch;
    public Vector2 Position;
    public int MaxRank = 1;
    public bool Notable;
    public TalentEffectDefinition[] Effects;
}

[Serializable]
public class TalentLinkDefinition
{
    public string From;
    public string To;
}

[Serializable]
public class TalentProgressionRules
{
    public int FirstPointXP = 10;
    public int PointCostLinear = 4;
    public int PointCostQuadratic = 1;
    public int MaxPointXP = 250;
    public int MinimumRunXP = 10;
    public float MinimumCombatSeconds = 15f;
    public int KillsPerXP = 5;
    public float SecondsPerXP = 15f;
    public int XPPerCompletedWave = 3;

    public int GetPointCost(int earnedPoints)
    {
        double count = Math.Max(0, earnedPoints);
        double cost = FirstPointXP + PointCostLinear * count + PointCostQuadratic * count * count;
        return (int)Math.Min(Math.Max(1, MaxPointXP), Math.Max(1, cost));
    }

    public int GetRunXP(int kills, float combatSeconds, int completedWaves)
    {
        if (kills <= 0 && combatSeconds < MinimumCombatSeconds) return 0;
        double reward = MinimumRunXP + Math.Max(0, kills) / Math.Max(1, KillsPerXP)
            + Math.Floor(Math.Max(0f, combatSeconds) / Math.Max(1f, SecondsPerXP))
            + (double)Math.Max(0, completedWaves) * XPPerCompletedWave;
        return (int)Math.Min(int.MaxValue, Math.Max(0, reward));
    }
}

[Serializable]
public class TalentGraphDefinition
{
    public int Version = 1;
    public string Id;
    public string Name;
    public string StartNodeId;
    public TalentProgressionRules Rules;
    public TalentNodeDefinition[] Nodes;
    public TalentLinkDefinition[] Links;
}

// Graph rules are independent of UI positions. Content packs can append nodes and links.
public class TalentGraph
{
    private readonly Dictionary<string, TalentNodeDefinition> _nodes = new Dictionary<string, TalentNodeDefinition>();
    private readonly Dictionary<string, List<string>> _neighbors = new Dictionary<string, List<string>>();
    private readonly List<TalentNodeDefinition> _orderedNodes = new List<TalentNodeDefinition>();
    private readonly List<TalentLinkDefinition> _links = new List<TalentLinkDefinition>();
    private readonly TalentEffectRegistry _effects;

    public string StartNodeId { get; }
    public string Name { get; }
    public TalentProgressionRules Rules { get; }
    public IReadOnlyList<TalentNodeDefinition> Nodes => _orderedNodes;
    public IReadOnlyList<TalentLinkDefinition> Links => _links;
    public int TotalRanks { get; private set; }

    public TalentGraph(TalentGraphDefinition definition, TalentEffectRegistry effects)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.StartNodeId))
            throw new ArgumentException("Talent graph requires a start node.");
        StartNodeId = definition.StartNodeId;
        Name = definition.Name;
        Rules = definition.Rules ?? new TalentProgressionRules();
        _effects = effects;
        if (!TryAppend(definition, out string error)) throw new ArgumentException(error);
    }

    public TalentNodeDefinition GetNode(string id) => id != null && _nodes.TryGetValue(id, out var node) ? node : null;
    public IReadOnlyList<string> GetNeighbors(string id) => _neighbors[id];

    public bool TryAppend(TalentGraphDefinition content, out string error)
    {
        error = null;
        if (content == null || content.Version != 1 || content.Nodes == null || content.Links == null)
        { error = "Unsupported or incomplete talent content."; return false; }
        var nodes = new Dictionary<string, TalentNodeDefinition>(_nodes);
        foreach (var node in content.Nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id) || nodes.ContainsKey(node.Id) ||
                node.MaxRank < 1 || node.MaxRank > 100 || !Finite(node.Position.x) || !Finite(node.Position.y))
            { error = "Invalid or duplicate talent node ID, rank or position."; return false; }
            if (node.Effects != null)
                foreach (var effect in node.Effects)
                    if (effect == null || !_effects.Contains(effect.Id) || !Finite(effect.Value))
                    { error = "Unknown or invalid effect in node " + node.Id; return false; }
            nodes.Add(node.Id, node);
        }
        if (!nodes.ContainsKey(StartNodeId)) { error = "Missing start node."; return false; }
        var links = new List<TalentLinkDefinition>(_links);
        links.AddRange(content.Links);
        var adjacency = new Dictionary<string, List<string>>();
        foreach (string id in nodes.Keys) adjacency.Add(id, new List<string>());
        foreach (var link in links)
        {
            if (link == null || !nodes.ContainsKey(link.From ?? "") || !nodes.ContainsKey(link.To ?? "") || link.From == link.To ||
                adjacency[link.From].Contains(link.To))
            { error = "Invalid or duplicate talent connection."; return false; }
            adjacency[link.From].Add(link.To);
            adjacency[link.To].Add(link.From);
        }
        var reached = new HashSet<string> { StartNodeId };
        var queue = new Queue<string>(); queue.Enqueue(StartNodeId);
        while (queue.Count > 0)
            foreach (string id in adjacency[queue.Dequeue()])
                if (reached.Add(id)) queue.Enqueue(id);
        if (reached.Count != nodes.Count) { error = "Talent content contains unreachable nodes."; return false; }
        foreach (var node in content.Nodes) { _nodes.Add(node.Id, node); _orderedNodes.Add(node); }
        _neighbors.Clear(); foreach (var pair in adjacency) _neighbors.Add(pair.Key, pair.Value);
        _links.AddRange(content.Links);
        TotalRanks = 0;
        foreach (var node in _orderedNodes) if (node.Id != StartNodeId) TotalRanks += node.MaxRank;
        return true;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
