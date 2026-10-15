using UnityEngine;

[CreateAssetMenu(
    fileName = "Search_Basic_Monster_From_Deck",
    menuName = "Card Game/Effects/Resolution/Search Basic Monster From Deck")]
public sealed class SearchBasicMonsterFromDeckResolutionAsset : EffectResolutionAsset
{
    public override EffectOperationDefinition CreateDefinition(int amount)
    {
        return new EffectOperationDefinition(
            EffectOperationType.SearchBasicMonsterFromDeck,
            amount);
    }
}
