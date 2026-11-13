using UnityEngine;

[CreateAssetMenu(fileName = "Expansion_Condition", menuName = "Card Game/Effects/Condition/Expansion Condition")]
public sealed class ExpansionConditionAsset : EffectConditionAsset
{
    [SerializeField] private EffectConditionType condition = EffectConditionType.None;
    public override EffectConditionDefinition CreateDefinition(int amount) => new EffectConditionDefinition(condition, amount);
}
