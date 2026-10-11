using System;
using System.Collections.Generic;

public class TalentBonuses
{
    public float Damage;
    public float WeaponRange;
    public float MaxHP;
    public float DamageReduction;
    public float PickupRadius;
    public float CritChance;
}

// IDs and handlers form the extension boundary; nodes do not contain executable code.
public class TalentEffectRegistry
{
    private readonly Dictionary<string, Action<TalentBonuses, float>> _handlers =
        new Dictionary<string, Action<TalentBonuses, float>>();

    public TalentEffectRegistry()
    {
        Register("core.damage", (bonuses, value) => bonuses.Damage += value);
        Register("core.range", (bonuses, value) => bonuses.WeaponRange += value);
        Register("core.max_hp", (bonuses, value) => bonuses.MaxHP += value);
        Register("core.reduction", (bonuses, value) => bonuses.DamageReduction += value);
        Register("core.pickup", (bonuses, value) => bonuses.PickupRadius += value);
        Register("core.crit", (bonuses, value) => bonuses.CritChance += value);
    }

    public void Register(string id, Action<TalentBonuses, float> handler)
    {
        if (string.IsNullOrWhiteSpace(id) || handler == null || _handlers.ContainsKey(id))
            throw new ArgumentException("Invalid or duplicate talent effect ID: " + id);
        _handlers.Add(id, handler);
    }

    public bool Contains(string id) => id != null && _handlers.ContainsKey(id);

    public void Apply(TalentNodeDefinition node, int rank, TalentBonuses bonuses)
    {
        if (node.Effects == null || rank <= 0) return;
        foreach (var effect in node.Effects)
            if (_handlers.TryGetValue(effect.Id, out var handler)) handler(bonuses, effect.Value * rank);
    }
}
