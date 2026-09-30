using UnityEditor;
using UnityEngine;

public static class CardInstanceDiagnostics
{
    [MenuItem("Card Game/Diagnostics/Run CardInstance Smoke Test")]
    public static void RunSmokeTest()
    {
        CardData monsterData = FindFirstMonster();

        if (monsterData == null)
        {
            Debug.LogError(
                "CardInstance test needs at least one Monster CardData asset.");
            return;
        }

        CardDefinition definition =
            CardDataConverter.ToDefinition(monsterData);
        CardInstance instance = new CardInstance(1, 0, definition);

        instance.MoveToZone(CardZone.Hand, false);
        instance.Summon(BattlePosition.Attack);
        instance.AllocateEnergy(definition.AttackEnergyRequirement);
        instance.ReceiveDamage(300);

        Debug.Log(
            "CardInstance smoke test passed.\n" +
            $"Card: {definition.CardName} ({definition.CardId})\n" +
            $"Instance: {instance.InstanceId}, Owner: Player " +
            $"{instance.OwnerPlayerIndex}\n" +
            $"Zone: {instance.Zone}, Position: " +
            $"{instance.BattlePosition}\n" +
            $"HP: {instance.CurrentHealth}/{definition.BaseHealth}, " +
            $"ATK: {instance.CurrentAttack}, " +
            $"DEF: {instance.CurrentDefense}\n" +
            $"Allocated Energy: {instance.AllocatedEnergy}, " +
            $"Summoned this turn: {instance.WasSummonedThisTurn}",
            monsterData);
    }

    private static CardData FindFirstMonster()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:CardData",
            new[] { "Assets/Data/Cards/Monsters" });

        foreach (string guid in guids)
        {
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(
                AssetDatabase.GUIDToAssetPath(guid));

            if (card != null && card.Category == CardCategory.Monster)
            {
                return card;
            }
        }

        return null;
    }
}
