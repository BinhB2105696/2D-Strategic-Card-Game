using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Runtime-only bridge from card taps to concrete, validated GameActions.
// It reuses CardActionPanel and does not edit the saved scene or prefabs.
public sealed class DuelCardInteractionController : MonoBehaviour
{
    private sealed class ActionOption
    {
        public string Label { get; }
        public GameAction Action { get; }

        public ActionOption(string label, GameAction action)
        {
            Label = label;
            Action = action;
        }
    }

    private DuelBoardReadOnlyDemo board;
    private GameState state;
    private GameObject actionPanel;
    private TMP_Text actionText;
    private RectTransform buttonArea;
    private readonly List<GameObject> buttons = new List<GameObject>();
    private GameObject detailPanel;
    private TMP_Text detailText;
    private RectTransform detailCardArea;
    private RectTransform detailButtonArea;
    private readonly List<GameObject> detailCards = new List<GameObject>();
    private readonly List<GameObject> detailButtons = new List<GameObject>();
    private readonly Dictionary<long, Image> tributeHighlights =
        new Dictionary<long, Image>();
    private GameObject energyPanel;
    private TMP_Text energyText;
    private RectTransform energyButtonArea;
    private readonly List<GameObject> energyButtons = new List<GameObject>();
    private readonly Dictionary<long, GameObject> cardObjects =
        new Dictionary<long, GameObject>();
    private readonly List<long> selectedTributes = new List<long>();
    private CardInstance selectedCard;
    private Button tributeConfirmButton;

    public void Initialize(DuelBoardReadOnlyDemo duelBoard)
    {
        board = duelBoard;
        Transform panel = transform.Find(
            "SafeAreaRoot/OverlayLayer/CardActionPanel");
        if (panel == null)
        {
            Debug.LogError("CardActionPanel is missing from OverlayLayer.", this);
            return;
        }

        actionPanel = panel.gameObject;
        Transform placeholder = panel.Find("PlaceholderText");
        actionText = placeholder != null
            ? placeholder.GetComponent<TMP_Text>()
            : null;
        if (actionText == null)
        {
            Debug.LogError(
                "CardActionPanel/PlaceholderText is missing.",
                this);
            return;
        }

        CreateButtonArea(panel);
        actionPanel.SetActive(false);

        InitializeDetailPanel();
        InitializeEnergyPanel();
    }

    public void BeginRender(GameState newState)
    {
        state = newState;
        selectedCard = null;
        selectedTributes.Clear();
        cardObjects.Clear();
        CloseDetailPanel();
        CloseEnergyPanel();
        SetPanelVisible(false);
    }

    public void BindCard(
        CardInstance instance,
        GameObject cardObject,
        bool allowTap)
    {
        if (instance == null || cardObject == null)
            return;

        cardObjects[instance.InstanceId] = cardObject;
        if (!allowTap)
            return;

        Button button = cardObject.GetComponent<Button>();
        if (button == null)
            button = cardObject.AddComponent<Button>();

        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.targetGraphic = cardObject.GetComponent<Graphic>() ??
            cardObject.GetComponentInChildren<Graphic>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SelectCard(instance));
    }

    public void ShowMatchResult(GameState completedState)
    {
        if (completedState == null || !completedState.IsFinished)
            return;

        selectedCard = null;
        selectedTributes.Clear();
        SetPanelVisible(false);
        CloseEnergyPanel();
        ClearDetailRuntime();
        detailPanel.SetActive(true);
        detailText.text = "Player " +
            (completedState.WinnerPlayerIndex + 1) +
            " Wins\nThe opponent's LP reached 0.";
        AddPanelButton(
            detailButtonArea,
            detailButtons,
            "Close",
            CloseDetailPanel);
        LayoutPanelButtons(detailButtons);
    }

    public void CloseAllCardWindows()
    {
        CloseSelection();
    }

    public void ShowResponsePrompt()
    {
        if (state == null || !state.HasOpenResponseWindow || state.IsFinished)
            return;

        ClearSelectionWindows();
        IReadOnlyList<GameAction> legalResponses =
            GameRules.GetLegalResponseActions(
                state,
                state.InteractionPlayerIndex);
        if (legalResponses.Count == 0)
            return;

        ClearDetailRuntime();
        detailPanel.SetActive(true);

        ChainState chain = state.ActiveChain;
        if (chain.Links.Count == 0 &&
            chain.WindowType == ResponseWindowType.AttackDeclaration)
        {
            detailText.text =
                "Your opponent declared an attack.\n" +
                "Activate a card or effect?";
        }
        else
        {
            detailText.text =
                "A card or effect was activated (Chain Link " +
                chain.Links.Count + ").\nActivate a response?";
        }

        Dictionary<long, List<GameAction>> actionsByCard =
            new Dictionary<long, List<GameAction>>();
        foreach (GameAction action in legalResponses)
        {
            if (!actionsByCard.TryGetValue(
                    action.SourceInstanceId,
                    out List<GameAction> cardActions))
            {
                cardActions = new List<GameAction>();
                actionsByCard.Add(action.SourceInstanceId, cardActions);
            }
            cardActions.Add(action);
        }

        int cardIndex = 0;
        foreach (KeyValuePair<long, List<GameAction>> pair in actionsByCard)
        {
            CardInstance card = state.FindCard(pair.Key);
            if (card != null)
            {
                CreateResponseCardChoice(
                    card,
                    pair.Value,
                    cardIndex,
                    actionsByCard.Count);
                cardIndex++;
            }
        }

        int playerIndex = state.InteractionPlayerIndex;
        Button passButton = AddPanelButton(
            detailButtonArea,
            detailButtons,
            "NO / PASS",
            () => Submit(GameAction.PassResponse(playerIndex)));
        SetButtonColor(passButton, new Color32(142, 31, 31, 250));
        LayoutPanelButtons(detailButtons);
    }

    private void SelectCard(CardInstance card)
    {
        if (state == null || state.IsFinished || card == null)
            return;

        if (selectedCard != null && selectedCard.InstanceId == card.InstanceId)
        {
            CloseSelection();
            return;
        }

        DuelHudPrototypeController hudController =
            GetComponentInChildren<DuelHudPrototypeController>(true);
        hudController?.PrepareForCardSelection();
        selectedCard = card;
        ShowActions(string.Empty);
    }

    private void ShowActions(string resultMessage)
    {
        ClearButtons();
        if (selectedCard == null || state == null)
        {
            CloseDetailPanel();
            return;
        }

        SetPanelVisible(false);
        CloseEnergyPanel();
        ClearDetailRuntime();
        detailPanel.SetActive(true);
        CreateCardInformation(selectedCard);
        List<ActionOption> options = BuildLegalOptions(selectedCard);
        int specialActionCount = 0;

        int interactionPlayer = state.InteractionPlayerIndex;
        if (!state.HasOpenResponseWindow &&
            selectedCard.OwnerPlayerIndex == interactionPlayer &&
            selectedCard.Zone == CardZone.Hand &&
            selectedCard.Definition.Category == CardCategory.Monster &&
            HasAnyLegalSummon(selectedCard))
        {
            AddPanelButton(
                detailButtonArea,
                detailButtons,
                "Summon",
                BeginSummon);
            specialActionCount++;
        }

        if (!state.HasOpenResponseWindow &&
            selectedCard.OwnerPlayerIndex == interactionPlayer &&
            selectedCard.Zone == CardZone.MonsterZone &&
            HasAnyLegalEnergyAllocation(selectedCard))
        {
            AddPanelButton(
                detailButtonArea,
                detailButtons,
                "Allocate Energy",
                ShowEnergyAllocation);
            specialActionCount++;
        }

        foreach (ActionOption option in options)
        {
            GameAction action = option.Action;
            if (selectedCard.Definition.Category == CardCategory.Monster &&
                action.Type == GameActionType.ActivateCard)
            {
                AddPanelButton(
                    detailButtonArea,
                    detailButtons,
                    option.Label,
                    () => ShowMonsterEffectConfirmation(action));
            }
            else
            {
                AddPanelButton(
                    detailButtonArea,
                    detailButtons,
                    option.Label,
                    () => Submit(action));
            }
        }
        if (state.HasOpenResponseWindow)
        {
            int playerIndex = state.InteractionPlayerIndex;
            AddPanelButton(
                detailButtonArea,
                detailButtons,
                "Pass",
                () => Submit(GameAction.PassResponse(playerIndex)));
        }
        AddPanelButton(
            detailButtonArea,
            detailButtons,
            "Close",
            CloseSelection);

        string guidance;
        if (!string.IsNullOrEmpty(resultMessage))
            guidance = resultMessage;
        else if (options.Count + specialActionCount > 0)
            guidance = "Choose a legal action.";
        else
            guidance = GetNoActionReason(selectedCard);

        detailText.text = guidance;
        LayoutPanelButtons(detailButtons);
    }

    private List<ActionOption> BuildLegalOptions(CardInstance card)
    {
        List<ActionOption> options = new List<ActionOption>();
        int playerIndex = state.InteractionPlayerIndex;
        if (card.OwnerPlayerIndex != playerIndex)
            return options;

        if (state.HasOpenResponseWindow)
        {
            AddTrapResponseOption(card, options);
            return options;
        }

        if (card.Zone == CardZone.Hand)
        {
            if (card.Definition.Category == CardCategory.Spell)
                AddSpellActivationOptions(card, options);
            else if (card.Definition.Category == CardCategory.Trap)
                AddTrapSetOption(card, options);
        }
        else if (card.Zone == CardZone.MonsterZone)
        {
            AddMonsterEffectOption(card, options);
            AddFieldMonsterOptions(card, options);
        }

        return options;
    }

    private bool HasAnyLegalSummon(CardInstance monster)
    {
        PlayerState player = state.Players[state.CurrentPlayerIndex];
        List<long[]> tributeChoices = BuildTributeChoices(
            player,
            monster.Definition.TributeCount);
        BattlePosition[] positions =
        {
            BattlePosition.Attack,
            BattlePosition.Defense
        };

        foreach (BattlePosition position in positions)
        {
            foreach (long[] tributes in tributeChoices)
            {
                GameAction action = FindPreferredSummon(
                    monster,
                    position,
                    tributes);
                if (action != null)
                    return true;
            }
        }

        return false;
    }

    private GameAction FindPreferredSummon(
        CardInstance monster,
        BattlePosition position,
        IReadOnlyList<long> tributes)
    {
        PlayerState player = state.Players[state.CurrentPlayerIndex];
        int[] preferredZones = { 1, 2, 0 };
        foreach (int zoneIndex in preferredZones)
        {
            if (zoneIndex < 0 || zoneIndex >= player.MonsterZones.Count)
                continue;

            GameAction action = GameAction.SummonMonster(
                state.CurrentPlayerIndex,
                monster.InstanceId,
                zoneIndex,
                position,
                ToArray(tributes));
            if (IsLegal(action))
                return action;
        }

        return null;
    }

    private void AddSpellActivationOptions(
        CardInstance spell,
        List<ActionOption> options)
    {
        int playerIndex = state.InteractionPlayerIndex;
        PlayerState player = state.Players[playerIndex];
        if (spell.Definition.SpellType == SpellType.Field)
        {
            GameAction action = GameAction.ActivateCard(
                playerIndex,
                spell.InstanceId);
            if (IsLegal(action))
                options.Add(new ActionOption("Activate Field", action));
            return;
        }

        int zoneIndex = FindPreferredSpellTrapZone(player);
        if (zoneIndex < 0)
            return;

        if (spell.Definition.SpellType == SpellType.Equip)
        {
            EffectDefinition effect = FindOpenActivationEffect(spell);
            if (effect == null)
                return;
            int targetOwnerIndex = effect.TargetType ==
                EffectTargetType.OpponentMonster
                ? 1 - playerIndex
                : playerIndex;
            PlayerState targetOwner = state.Players[targetOwnerIndex];
            for (int index = 0;
                 index < targetOwner.MonsterZones.Count;
                 index++)
            {
                CardInstance target = targetOwner.MonsterZones[index];
                if (target == null)
                    continue;

                GameAction action = GameAction.ActivateCard(
                    playerIndex,
                    spell.InstanceId,
                    zoneIndex,
                    target.InstanceId);
                if (IsLegal(action))
                {
                    options.Add(new ActionOption(
                        (targetOwnerIndex == playerIndex
                            ? "Equip Z"
                            : "Equip Opp Z") +
                        (index + 1) + " " +
                        target.Definition.CardName,
                        action));
                }
            }
            return;
        }

        EffectDefinition normalEffect = FindOpenActivationEffect(spell);
        if (normalEffect != null && normalEffect.TargetCount == 1)
        {
            int targetOwnerIndex = normalEffect.TargetType ==
                EffectTargetType.OpponentMonster
                ? 1 - playerIndex
                : playerIndex;
            PlayerState targetOwner = state.Players[targetOwnerIndex];
            for (int index = 0;
                 index < targetOwner.MonsterZones.Count;
                 index++)
            {
                CardInstance target = targetOwner.MonsterZones[index];
                if (target == null)
                    continue;

                GameAction targetedSpell = GameAction.ActivateCard(
                    playerIndex,
                    spell.InstanceId,
                    zoneIndex,
                    target.InstanceId);
                if (IsLegal(targetedSpell))
                {
                    options.Add(new ActionOption(
                        "Activate on Z" + (index + 1),
                        targetedSpell));
                }
            }
            return;
        }

        GameAction normalSpell = GameAction.ActivateCard(
            playerIndex,
            spell.InstanceId,
            zoneIndex);
        if (IsLegal(normalSpell))
            options.Add(new ActionOption("Activate", normalSpell));
    }

    private void AddTrapSetOption(
        CardInstance trap,
        List<ActionOption> options)
    {
        int playerIndex = state.InteractionPlayerIndex;
        int zoneIndex = FindPreferredSpellTrapZone(state.Players[playerIndex]);
        if (zoneIndex < 0)
            return;

        GameAction action = GameAction.SetTrap(
            playerIndex,
            trap.InstanceId,
            zoneIndex);
        if (IsLegal(action))
            options.Add(new ActionOption("Set Trap", action));
    }

    private void AddTrapResponseOption(
        CardInstance trap,
        List<ActionOption> options)
    {
        if (trap.Definition.Category != CardCategory.Trap ||
            trap.Zone != CardZone.SpellTrapZone ||
            !trap.IsFaceDown)
        {
            return;
        }

        int playerIndex = state.InteractionPlayerIndex;
        long targetId = 0;
        ChainState chain = state.ActiveChain;
        if (chain.Links.Count == 0 && chain.PendingAttack != null)
            targetId = chain.PendingAttack.AttackerInstanceId;

        GameAction action = GameAction.ActivateCard(
            playerIndex,
            trap.InstanceId,
            targetInstanceId: targetId);
        if (IsLegal(action))
            options.Add(new ActionOption("Activate Trap", action));
    }

    private static int FindPreferredSpellTrapZone(PlayerState player)
    {
        int[] preferredZones = { 1, 2, 0 };
        foreach (int zoneIndex in preferredZones)
        {
            if (zoneIndex >= 0 &&
                zoneIndex < player.SpellTrapZones.Count &&
                player.SpellTrapZones[zoneIndex] == null)
            {
                return zoneIndex;
            }
        }
        return -1;
    }

    private static EffectDefinition FindOpenActivationEffect(
        CardInstance card)
    {
        for (int index = 0; index < card.Definition.Effects.Count; index++)
        {
            EffectDefinition effect = card.Definition.Effects[index];
            if (effect.Trigger == EffectTriggerType.ManualActivation &&
                effect.ActivationWindow ==
                    EffectActivationWindow.OpenGameState)
            {
                return effect;
            }
        }
        return null;
    }

    private void AddMonsterEffectOption(
        CardInstance monster,
        List<ActionOption> options)
    {
        GameAction action = GameAction.ActivateCard(
            state.InteractionPlayerIndex,
            monster.InstanceId);
        if (IsLegal(action))
            options.Add(new ActionOption("Activate Effect", action));
    }

    private void AddFieldMonsterOptions(
        CardInstance monster,
        List<ActionOption> options)
    {
        BattlePosition opposite = monster.BattlePosition == BattlePosition.Attack
            ? BattlePosition.Defense
            : BattlePosition.Attack;
        GameAction changePosition = GameAction.ChangeBattlePosition(
            state.CurrentPlayerIndex,
            monster.InstanceId,
            opposite);
        if (IsLegal(changePosition))
        {
            options.Add(new ActionOption(
                opposite == BattlePosition.Attack ? "To ATK" : "To DEF",
                changePosition));
        }

        PlayerState opponent = state.Players[1 - state.CurrentPlayerIndex];
        bool foundTarget = false;
        for (int index = 0; index < opponent.MonsterZones.Count; index++)
        {
            CardInstance target = opponent.MonsterZones[index];
            if (target == null)
                continue;

            GameAction attack = GameAction.Attack(
                state.CurrentPlayerIndex,
                monster.InstanceId,
                target.InstanceId);
            if (IsLegal(attack))
            {
                options.Add(new ActionOption(
                    "Attack Z" + (index + 1),
                    attack));
                foundTarget = true;
            }
        }

        if (!foundTarget)
        {
            GameAction directAttack = GameAction.Attack(
                state.CurrentPlayerIndex,
                monster.InstanceId);
            if (IsLegal(directAttack))
                options.Add(new ActionOption("Direct Attack", directAttack));
        }
    }

    private bool HasAnyLegalEnergyAllocation(CardInstance monster)
    {
        PlayerState player = state.Players[state.CurrentPlayerIndex];
        for (int amount = 1; amount <= player.AvailableEnergy; amount++)
        {
            if (IsLegal(GameAction.AllocateEnergy(
                state.CurrentPlayerIndex,
                monster.InstanceId,
                amount)))
            {
                return true;
            }
        }

        return false;
    }

    private void BeginSummon()
    {
        selectedTributes.Clear();
        if (selectedCard.Definition.TributeCount == 0)
            ShowSummonPositionChoices();
        else
            ShowTributeSelection();
    }

    private void ShowTributeSelection()
    {
        SetPanelVisible(false);
        CloseEnergyPanel();
        ClearDetailRuntime();
        detailPanel.SetActive(true);
        tributeHighlights.Clear();

        PlayerState player = state.Players[state.CurrentPlayerIndex];
        for (int zoneIndex = 0;
             zoneIndex < player.MonsterZones.Count;
             zoneIndex++)
        {
            CardInstance monster = player.MonsterZones[zoneIndex];
            if (monster == null)
                continue;

            Image highlight = CreateTributeCard(monster, zoneIndex);
            tributeHighlights[monster.InstanceId] = highlight;
        }

        tributeConfirmButton = AddPanelButton(
            detailButtonArea,
            detailButtons,
            "OK",
            ShowSummonPositionChoices);
        AddPanelButton(
            detailButtonArea,
            detailButtons,
            "Cancel",
            CancelCardWorkflow);
        LayoutPanelButtons(detailButtons);
        UpdateTributeSelectionVisuals();
    }

    private void ToggleTribute(long instanceId)
    {
        if (selectedTributes.Contains(instanceId))
        {
            selectedTributes.Remove(instanceId);
        }
        else if (selectedTributes.Count < selectedCard.Definition.TributeCount)
        {
            selectedTributes.Add(instanceId);
            PlayerState player = state.Players[state.CurrentPlayerIndex];
            selectedTributes.Sort((left, right) =>
                FindMonsterZoneIndex(player, left).CompareTo(
                    FindMonsterZoneIndex(player, right)));
        }

        UpdateTributeSelectionVisuals();
    }

    private void UpdateTributeSelectionVisuals()
    {
        int required = selectedCard.Definition.TributeCount;
        detailText.text = "Select " + required + " tribute(s) for " +
            selectedCard.Definition.CardName + "\nSelected " +
            selectedTributes.Count + "/" + required;

        foreach (KeyValuePair<long, Image> pair in tributeHighlights)
        {
            pair.Value.color = selectedTributes.Contains(pair.Key)
                ? new Color32(224, 174, 48, 245)
                : new Color32(22, 77, 139, 235);
        }

        if (tributeConfirmButton != null)
            tributeConfirmButton.interactable = selectedTributes.Count == required;
    }

    private void ShowSummonPositionChoices()
    {
        CloseDetailPanel();
        CloseEnergyPanel();
        ClearButtons();
        SetPanelVisible(true);

        GameAction attackSummon = FindPreferredSummon(
            selectedCard,
            BattlePosition.Attack,
            selectedTributes);
        if (attackSummon != null)
        {
            AddButton(
                "Attack Position",
                () => SubmitSummon(BattlePosition.Attack));
        }

        GameAction defenseSummon = FindPreferredSummon(
            selectedCard,
            BattlePosition.Defense,
            selectedTributes);
        if (defenseSummon != null)
        {
            AddButton(
                "Defense Position",
                () => SubmitSummon(BattlePosition.Defense));
        }

        AddButton("Cancel", CancelCardWorkflow);
        actionText.text = BuildCardDetails(selectedCard) +
            "\nChoose the summon position.";
        actionText.margin = new Vector4(10f, 8f, 10f, 142f);
        actionText.fontSize = 25f;
    }

    private void SubmitSummon(BattlePosition position)
    {
        GameAction action = FindPreferredSummon(
            selectedCard,
            position,
            selectedTributes);
        if (action == null)
        {
            ShowActions("No legal Monster Zone is available.");
            return;
        }

        Submit(action);
    }

    private void ShowEnergyAllocation()
    {
        SetPanelVisible(false);
        CloseDetailPanel();
        ClearRuntimeObjects(energyButtons);
        energyPanel.SetActive(true);

        PlayerState player = state.Players[state.CurrentPlayerIndex];
        energyText.text = "Allocate Energy to " +
            selectedCard.Definition.CardName +
            "\nAvailable Energy: " + player.AvailableEnergy;

        for (int amount = 1; amount <= player.AvailableEnergy; amount++)
        {
            GameAction action = GameAction.AllocateEnergy(
                state.CurrentPlayerIndex,
                selectedCard.InstanceId,
                amount);
            if (!IsLegal(action))
                continue;

            int selectedAmount = amount;
            AddPanelButton(
                energyButtonArea,
                energyButtons,
                selectedAmount.ToString(),
                () => Submit(GameAction.AllocateEnergy(
                    state.CurrentPlayerIndex,
                    selectedCard.InstanceId,
                    selectedAmount)));
        }

        AddPanelButton(
            energyButtonArea,
            energyButtons,
            "Cancel",
            CancelEnergyAllocation);
        LayoutPanelButtons(energyButtons);
    }

    private void CancelEnergyAllocation()
    {
        CloseSelection();
    }

    private void CancelCardWorkflow()
    {
        CloseSelection();
    }

    private void Submit(GameAction action)
    {
        ActionResult result = board.TryApplyAction(action);
        if (result.Succeeded)
        {
            Debug.Log(
                "Card UI action accepted: " + action +
                " (" + result.Events.Count + " event(s)).",
                this);
            return;
        }

        CloseDetailPanel();
        CloseEnergyPanel();
        ShowActions("Rejected by GameRules: " + result.ErrorMessage);
        Debug.LogWarning(
            "Card UI action rejected: " + result.ErrorMessage,
            this);
    }

    private bool IsLegal(GameAction action)
    {
        return string.IsNullOrEmpty(GameRules.ValidateAction(state, action));
    }

    private string GetNoActionReason(CardInstance card)
    {
        if (card.OwnerPlayerIndex != state.InteractionPlayerIndex)
            return "Opponent card: no player action available.";
        if (state.HasOpenResponseWindow)
            return "This card cannot respond now. Choose another Trap or Pass.";
        if (card.Zone == CardZone.Hand &&
            card.Definition.Category == CardCategory.Trap)
            return "This Trap cannot be Set now: check Main Phase and open zones.";
        if (card.Zone == CardZone.Hand &&
            card.Definition.Category == CardCategory.Spell)
            return "This Spell has no legal activation or target now.";
        if (card.Zone == CardZone.Hand &&
            card.Definition.Category == CardCategory.Monster)
            return "No legal summon: check Energy, tributes, and zones.";
        return "No legal action is available for this card now.";
    }

    private static string BuildCardDetails(CardInstance card)
    {
        if (!card.Definition.IsMonster)
            return card.Definition.CardName + " | " + card.Definition.Category;

        return card.Definition.CardName +
            " | HP " + card.CurrentHealth +
            " | ATK " + card.CurrentAttack +
            " | DEF " + card.CurrentDefense +
            " | Energy " + card.AllocatedEnergy + "/" +
            card.Definition.AttackEnergyRequirement;
    }

    private static long[] ToArray(IReadOnlyList<long> values)
    {
        long[] result = new long[values.Count];
        for (int index = 0; index < values.Count; index++)
            result[index] = values[index];
        return result;
    }

    private static int FindMonsterZoneIndex(
        PlayerState player,
        long instanceId)
    {
        for (int index = 0; index < player.MonsterZones.Count; index++)
        {
            CardInstance monster = player.MonsterZones[index];
            if (monster != null && monster.InstanceId == instanceId)
                return index;
        }
        return -1;
    }

    private static List<long[]> BuildTributeChoices(
        PlayerState player,
        int requiredCount)
    {
        List<long> available = new List<long>();
        for (int index = 0; index < player.MonsterZones.Count; index++)
        {
            CardInstance monster = player.MonsterZones[index];
            if (monster != null)
                available.Add(monster.InstanceId);
        }

        List<long[]> results = new List<long[]>();
        AddTributeChoices(
            available,
            requiredCount,
            0,
            new List<long>(),
            results);
        return results;
    }

    private static void AddTributeChoices(
        IReadOnlyList<long> available,
        int requiredCount,
        int startIndex,
        List<long> current,
        List<long[]> results)
    {
        if (current.Count == requiredCount)
        {
            results.Add(current.ToArray());
            return;
        }

        for (int index = startIndex; index < available.Count; index++)
        {
            current.Add(available[index]);
            AddTributeChoices(
                available,
                requiredCount,
                index + 1,
                current,
                results);
            current.RemoveAt(current.Count - 1);
        }
    }

    private void ShowMonsterEffectConfirmation(GameAction action)
    {
        ClearButtons();
        SetPanelVisible(false);
        ClearDetailRuntime();
        detailPanel.SetActive(true);
        detailText.text =
            "Activate the effect of \"" +
            selectedCard.Definition.CardName + "\"?";

        GameObject bodyObject = new GameObject(
            "EffectConfirmationText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        bodyObject.layer = detailCardArea.gameObject.layer;
        RectTransform bodyRect = bodyObject.GetComponent<RectTransform>();
        bodyRect.SetParent(detailCardArea, false);
        bodyRect.anchorMin = new Vector2(0.08f, 0.12f);
        bodyRect.anchorMax = new Vector2(0.92f, 0.88f);
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;

        TextMeshProUGUI body = bodyObject.GetComponent<TextMeshProUGUI>();
        body.font = actionText.font;
        body.fontSize = 31f;
        body.color = Color.white;
        body.alignment = TextAlignmentOptions.Center;
        body.enableWordWrapping = true;
        body.text = selectedCard.Definition.EffectText;
        body.raycastTarget = false;
        detailCards.Add(bodyObject);

        Button noButton = AddPanelButton(
            detailButtonArea,
            detailButtons,
            "NO",
            () => ShowActions(string.Empty));
        SetButtonColor(noButton, new Color32(142, 31, 31, 250));
        Button yesButton = AddPanelButton(
            detailButtonArea,
            detailButtons,
            "YES",
            () => Submit(action));
        SetButtonColor(yesButton, new Color32(19, 85, 151, 250));
        LayoutPanelButtons(detailButtons);
    }

    private void CreateCardInformation(CardInstance card)
    {
        GameObject contentObject = new GameObject(
            "RuntimeCardInformation",
            typeof(RectTransform));
        contentObject.layer = detailCardArea.gameObject.layer;
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.SetParent(detailCardArea, false);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(18f, 12f);
        content.offsetMax = new Vector2(-18f, -12f);
        detailCards.Add(contentObject);

        CreateCardPreview(card, content, new Vector2(-245f, 0f), 0.82f);

        TMP_Text information = CreatePanelLabel(
            content,
            "InformationText",
            BuildFullCardInformation(card),
            27f);
        RectTransform informationRect = information.rectTransform;
        informationRect.anchorMin = informationRect.anchorMax =
            new Vector2(0.5f, 0.5f);
        informationRect.pivot = new Vector2(0.5f, 0.5f);
        informationRect.anchoredPosition = new Vector2(145f, 0f);
        informationRect.sizeDelta = new Vector2(470f, 390f);
        information.alignment = TextAlignmentOptions.TopLeft;
        information.enableWordWrapping = true;
    }

    private void CreateResponseCardChoice(
        CardInstance card,
        List<GameAction> actions,
        int index,
        int count)
    {
        GameObject choiceObject = new GameObject(
            "ResponseChoice_" + card.InstanceId,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        choiceObject.layer = detailCardArea.gameObject.layer;
        RectTransform choice = choiceObject.GetComponent<RectTransform>();
        choice.SetParent(detailCardArea, false);
        choice.anchorMin = new Vector2((float)index / count, 0f);
        choice.anchorMax = new Vector2((float)(index + 1) / count, 1f);
        choice.offsetMin = new Vector2(12f, 10f);
        choice.offsetMax = new Vector2(-12f, -10f);

        Image background = choiceObject.GetComponent<Image>();
        background.color = new Color32(9, 38, 65, 235);
        Button button = choiceObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(() =>
            ShowResponseCardInformation(card, actions));

        CreateCardPreview(card, choice, new Vector2(0f, 22f), 0.68f);
        TMP_Text label = CreatePanelLabel(
            choice,
            "CardName",
            card.Definition.CardName,
            24f);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 4f);
        labelRect.sizeDelta = new Vector2(-10f, 42f);
        detailCards.Add(choiceObject);
    }

    private void ShowResponseCardInformation(
        CardInstance card,
        List<GameAction> actions)
    {
        selectedCard = card;
        ClearDetailRuntime();
        detailPanel.SetActive(true);
        detailText.text = "This card can respond to the current event.";
        CreateCardInformation(card);

        foreach (GameAction action in actions)
        {
            GameAction response = action;
            Button activateButton = AddPanelButton(
                detailButtonArea,
                detailButtons,
                "ACTIVATE EFFECT",
                () => Submit(response));
            SetButtonColor(
                activateButton,
                new Color32(19, 85, 151, 250));
        }
        AddPanelButton(
            detailButtonArea,
            detailButtons,
            "BACK",
            ShowResponsePrompt);
        LayoutPanelButtons(detailButtons);
    }

    private void CreateCardPreview(
        CardInstance card,
        RectTransform parent,
        Vector2 position,
        float scale)
    {
        if (!cardObjects.TryGetValue(card.InstanceId, out GameObject sourceCard))
            return;

        GameObject preview = Instantiate(sourceCard, parent);
        preview.name = "InformationPreview_" + card.InstanceId;
        Button previewButton = preview.GetComponent<Button>();
        if (previewButton != null)
        {
            previewButton.onClick.RemoveAllListeners();
            previewButton.enabled = false;
        }
        CardView view = preview.GetComponent<CardView>();
        if (view != null)
            view.SetFaceDown(false);
        foreach (Graphic graphic in preview.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        RectTransform previewRect = preview.GetComponent<RectTransform>();
        previewRect.anchorMin = previewRect.anchorMax =
            previewRect.pivot = new Vector2(0.5f, 0.5f);
        previewRect.anchoredPosition = position;
        previewRect.sizeDelta = new Vector2(250f, 350f);
        previewRect.localScale = Vector3.one * scale;
        previewRect.localRotation = Quaternion.identity;
    }

    private static string BuildFullCardInformation(CardInstance card)
    {
        CardDefinition definition = card.Definition;
        if (definition.Category == CardCategory.Monster)
        {
            return definition.CardName + "\n" +
                "<" + definition.MonsterRank + "> [" +
                definition.MonsterType + "]\n" +
                "Attribute: " + definition.MonsterAttribute + "\n" +
                "HP " + card.CurrentHealth + "   ATK " +
                card.CurrentAttack + "   DEF " + card.CurrentDefense + "\n" +
                "Attack Energy: " + card.AllocatedEnergy + "/" +
                definition.AttackEnergyRequirement + "\n\nEffect\n" +
                definition.EffectText;
        }

        string type = definition.Category == CardCategory.Spell
            ? "[" + definition.SpellType + " Spell]"
            : "[" + definition.TrapType + " Trap]";
        return definition.CardName + "\n" + type +
            "   Speed " + (int)definition.Speed +
            "\n\nEffect\n" + definition.EffectText;
    }

    private void InitializeDetailPanel()
    {
        Transform panel = transform.Find(
            "SafeAreaRoot/OverlayLayer/CardDetailPanel");
        if (panel == null)
        {
            Debug.LogError("CardDetailPanel is missing from OverlayLayer.", this);
            return;
        }

        detailPanel = panel.gameObject;
        Image background = detailPanel.GetComponent<Image>();
        if (background != null)
        {
            background.raycastTarget = true;
            background.color = new Color32(8, 28, 47, 246);
            Outline outline = detailPanel.GetComponent<Outline>();
            if (outline == null)
                outline = detailPanel.AddComponent<Outline>();
            outline.effectColor = new Color32(53, 211, 235, 230);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        Transform placeholder = panel.Find("PlaceholderText");
        detailText = placeholder != null
            ? placeholder.GetComponent<TMP_Text>()
            : null;
        if (detailText == null)
        {
            Debug.LogError("CardDetailPanel/PlaceholderText is missing.", this);
            return;
        }

        RectTransform textRect = detailText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0f, -55f);
        textRect.sizeDelta = new Vector2(-90f, 260f);
        detailText.fontSize = 34f;
        detailText.alignment = TextAlignmentOptions.Center;

        detailCardArea = CreateRuntimeArea(
            panel,
            "RuntimeTributeCards",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(780f, 440f),
            new Vector2(0f, 30f));
        detailButtonArea = CreateRuntimeArea(
            panel,
            "RuntimeDetailButtons",
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(-80f, 140f),
            new Vector2(0f, 70f));
        detailPanel.SetActive(false);
    }

    private void InitializeEnergyPanel()
    {
        Transform panel = transform.Find(
            "SafeAreaRoot/OverlayLayer/EnergyAllocationPopup");
        if (panel == null)
        {
            Debug.LogError(
                "EnergyAllocationPopup is missing from OverlayLayer.",
                this);
            return;
        }

        energyPanel = panel.gameObject;
        Image background = energyPanel.GetComponent<Image>();
        if (background != null)
            background.raycastTarget = true;

        Transform placeholder = panel.Find("PlaceholderText");
        energyText = placeholder != null
            ? placeholder.GetComponent<TMP_Text>()
            : null;
        if (energyText == null)
        {
            Debug.LogError(
                "EnergyAllocationPopup/PlaceholderText is missing.",
                this);
            return;
        }

        RectTransform textRect = energyText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0f, -36f);
        textRect.sizeDelta = new Vector2(-60f, 145f);
        energyText.fontSize = 30f;
        energyText.alignment = TextAlignmentOptions.Center;

        energyButtonArea = CreateRuntimeArea(
            panel,
            "RuntimeEnergyButtons",
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(-50f, 210f),
            new Vector2(0f, 28f));
        energyPanel.SetActive(false);
    }

    private Image CreateTributeCard(CardInstance monster, int zoneIndex)
    {
        GameObject slotObject = new GameObject(
            "TributeZone_" + zoneIndex,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        slotObject.layer = detailCardArea.gameObject.layer;
        RectTransform slot = slotObject.GetComponent<RectTransform>();
        slot.SetParent(detailCardArea, false);
        slot.anchorMin = new Vector2(zoneIndex / 3f, 0f);
        slot.anchorMax = new Vector2((zoneIndex + 1) / 3f, 1f);
        slot.offsetMin = new Vector2(14f, 10f);
        slot.offsetMax = new Vector2(-14f, -10f);

        Image background = slotObject.GetComponent<Image>();
        background.color = new Color32(22, 77, 139, 235);
        Button button = slotObject.GetComponent<Button>();
        button.targetGraphic = background;
        long instanceId = monster.InstanceId;
        button.onClick.AddListener(() => ToggleTribute(instanceId));

        if (cardObjects.TryGetValue(instanceId, out GameObject sourceCard))
        {
            GameObject preview = Instantiate(sourceCard, slot);
            preview.name = "Preview_" + instanceId;
            Button previewButton = preview.GetComponent<Button>();
            if (previewButton != null)
            {
                previewButton.onClick.RemoveAllListeners();
                previewButton.enabled = false;
            }
            foreach (Graphic graphic in
                     preview.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            RectTransform previewRect = preview.GetComponent<RectTransform>();
            previewRect.anchorMin = previewRect.anchorMax =
                previewRect.pivot = new Vector2(0.5f, 0.5f);
            previewRect.anchoredPosition = new Vector2(0f, 10f);
            previewRect.sizeDelta = new Vector2(250f, 350f);
            previewRect.localScale = Vector3.one * 0.62f;
            previewRect.localRotation = Quaternion.identity;
        }

        TMP_Text zoneLabel = CreatePanelLabel(
            slot,
            "ZoneLabel",
            "Z" + (zoneIndex + 1),
            26f);
        RectTransform labelRect = zoneLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 4f);
        labelRect.sizeDelta = new Vector2(-10f, 42f);

        detailCards.Add(slotObject);
        return background;
    }

    private Button AddPanelButton(
        RectTransform parent,
        List<GameObject> collection,
        string label,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateButtonObject(parent, label, action);
        collection.Add(buttonObject);
        return buttonObject.GetComponent<Button>();
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button == null)
            return;
        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = color;
    }

    private GameObject CreateButtonObject(
        RectTransform parent,
        string label,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(
            "Action_" + label.Replace(" ", string.Empty),
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.layer = parent.gameObject.layer;
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color32(22, 77, 139, 245);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        TMP_Text text = CreatePanelLabel(rect, "Label", label, 24f);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(6f, 5f);
        textRect.offsetMax = new Vector2(-6f, -5f);
        return buttonObject;
    }

    private TMP_Text CreatePanelLabel(
        RectTransform parent,
        string name,
        string value,
        float fontSize)
    {
        GameObject labelObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        labelObject.layer = parent.gameObject.layer;
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = actionText.font;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRuntimeArea(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 position)
    {
        GameObject area = new GameObject(name, typeof(RectTransform));
        area.layer = parent.gameObject.layer;
        RectTransform rect = area.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static void LayoutPanelButtons(List<GameObject> panelButtons)
    {
        int count = panelButtons.Count;
        if (count == 0)
            return;

        int columns = Math.Min(3, count);
        int rows = (count + columns - 1) / columns;
        for (int index = 0; index < count; index++)
        {
            int column = index % columns;
            int row = index / columns;
            RectTransform rect =
                panelButtons[index].GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(
                (float)column / columns,
                1f - (float)(row + 1) / rows);
            rect.anchorMax = new Vector2(
                (float)(column + 1) / columns,
                1f - (float)row / rows);
            rect.offsetMin = new Vector2(5f, 5f);
            rect.offsetMax = new Vector2(-5f, -5f);
        }
    }

    private void ClearDetailRuntime()
    {
        ClearRuntimeObjects(detailCards);
        ClearRuntimeObjects(detailButtons);
        tributeHighlights.Clear();
        tributeConfirmButton = null;
    }

    private static void ClearRuntimeObjects(List<GameObject> objects)
    {
        foreach (GameObject item in objects)
        {
            if (item != null)
                Destroy(item);
        }
        objects.Clear();
    }

    private void CloseDetailPanel()
    {
        ClearDetailRuntime();
        if (detailPanel != null)
            detailPanel.SetActive(false);
    }

    private void CloseEnergyPanel()
    {
        ClearRuntimeObjects(energyButtons);
        if (energyPanel != null)
            energyPanel.SetActive(false);
    }

    private void CreateButtonArea(Transform parent)
    {
        GameObject area = new GameObject(
            "RuntimeActionButtons",
            typeof(RectTransform));
        area.layer = parent.gameObject.layer;
        buttonArea = area.GetComponent<RectTransform>();
        buttonArea.SetParent(parent, false);
        buttonArea.anchorMin = new Vector2(0f, 0f);
        buttonArea.anchorMax = new Vector2(1f, 0f);
        buttonArea.pivot = new Vector2(0.5f, 0f);
        buttonArea.anchoredPosition = new Vector2(0f, 16f);
        buttonArea.sizeDelta = new Vector2(-48f, 126f);
    }

    private void AddButton(string label, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(
            "Action_" + label.Replace(" ", string.Empty),
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.layer = buttonArea.gameObject.layer;
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(buttonArea, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color32(22, 77, 139, 245);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        labelObject.layer = buttonObject.layer;
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(5f, 4f);
        labelRect.offsetMax = new Vector2(-5f, -4f);

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.font = actionText.font;
        text.fontSize = 21f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        buttons.Add(buttonObject);
        LayoutButtons();
    }

    private void LayoutButtons()
    {
        int count = buttons.Count;
        int columns = Math.Min(3, count);
        int rows = (count + columns - 1) / columns;
        for (int index = 0; index < count; index++)
        {
            int column = index % columns;
            int row = index / columns;
            RectTransform rect = buttons[index].GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(
                (float)column / columns,
                1f - (float)(row + 1) / rows);
            rect.anchorMax = new Vector2(
                (float)(column + 1) / columns,
                1f - (float)row / rows);
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
        }
    }

    private void ClearButtons()
    {
        foreach (GameObject button in buttons)
        {
            if (button != null)
                Destroy(button);
        }
        buttons.Clear();
    }

    private void CloseSelection()
    {
        bool returnToResponse = state != null && state.HasOpenResponseWindow;
        ClearSelectionWindows();
        if (returnToResponse)
            ShowResponsePrompt();
    }

    private void ClearSelectionWindows()
    {
        selectedTributes.Clear();
        CloseDetailPanel();
        CloseEnergyPanel();
        selectedCard = null;
        SetPanelVisible(false);
    }

    private void SetPanelVisible(bool visible)
    {
        if (!visible)
            ClearButtons();
        if (actionPanel != null)
            actionPanel.SetActive(visible);
    }
}
