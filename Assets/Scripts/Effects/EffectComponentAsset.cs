using UnityEngine;

public abstract class EffectConditionAsset : ScriptableObject
{
    public abstract EffectConditionDefinition CreateDefinition(int amount);
}

public abstract class EffectCostAsset : ScriptableObject
{
    public abstract EffectCostDefinition CreateDefinition(int amount);
}

public abstract class EffectTargetAsset : ScriptableObject
{
    public abstract EffectTargetType CreateDefinition();
}

public abstract class EffectResolutionAsset : ScriptableObject
{
    public abstract EffectOperationDefinition CreateDefinition(int amount);
}

public abstract class ContinuousModifierAsset : ScriptableObject
{
    public abstract IContinuousModifier CreateModifier(int amount);
}

public interface IContinuousModifier
{
    int ModifyHealth(CardInstance source, CardInstance target);
    int ModifyAttack(CardInstance source, CardInstance target);
    int ModifyDefense(CardInstance source, CardInstance target);
}
