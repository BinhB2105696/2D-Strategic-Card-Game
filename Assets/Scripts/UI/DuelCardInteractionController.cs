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
    private readonly Dictionary<long, Image> targetHighlights =
        new Dictionary<long, Image>();
    private readonly List<GameAction> pendingTargetActions =
        new List<GameAction>();
    private GameObject energyPanel;
    private TMP_Text energyText;
    private RectTransform energyAmountButtonArea;
    private RectTransform energyFooterButtonArea;
    private readonly List<GameObject> energyAmountButtons =
        new List<GameObject>();
    private readonly List<GameObject> energyFooterButtons =
        new List<GameObject>();
    private readonly Dictionary<int, Button> energyAmountButtonViews =
        new Dictionary<int, Button>();
    private readonly Dictionary<long, GameObject> cardObjects =
        new Dictionary<long, GameObject>();
    private readonly List<long> selectedTributes = new List<long>();
    private long selectedTargetInstanceId;
    private CardInstance selectedCard;
    private Button tributeConfirmButton;
    private Button targetConfirmButton;
    private GameAction selectedEnergyAction;
    private Button energyConfirmButton;

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
        IReadOnlyList<GameAction> legalActions =
            GameRules.GetLegalActions(state, state.InteractionPlayerIndex);
        List<GameAction> legalResponses = new List<GameAction>();
        GameAction passAction = null;
        for (int index = 0; index < legalActions.Count; index++)
        {
            GameAction action = legalActions[index];
            if (action.Type == GameActionType.ActivateCard)
                legalResponses.Add(action);
            else if (action.Type == GameActionType.PassResponse)
                passAction = action;
        }
        if (legalResponses.Count == 0 || passAction == null)
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

        Button passButton = AddPanelButton(
            detailButtonArea,
            detailButtons,
            "NO / PASS",
            () => Submit(passAction));
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
        List<GameAction> cardActions = GetLegalActionsForSource(selectedCard);
        List<ActionOption> options = BuildDisplayOptions(
            selectedCard,
            cardActions);
        List<GameAction> activationActions = FindActions(
            cardActions,
            GameActionType.ActivateCard);
        int specialActionCount = 0;

        if (ContainsActionType(cardActions, GameActionType.SummonMonster))
        {
            AddPanelButton(
                detailButtonArea,
                detailButtons,
                "Summon",
                BeginSummon);
            specialActionCount++;
        }

        if (ContainsActionType(cardActions, GameActionType.AllocateEnergy))
        {
            AddPanelButton(
                detailButtonArea,
                detailButtons,
                "Allocate Energy",
                ShowEnergyAllocation);
            specialActionCount++;
        }

        if (activationActions.Count > 0)
        {
            List<GameAction> actions = activationActions;
            AddPanelButton(
                detailButtonArea,
                detailButtons,
                BuildActivationLabel(selectedCard),
                () => BeginActivation(actions));
            specialActionCount++;
        }

        foreach (ActionOption option in options)
        {
            GameAction action = option.Action;
            AddPanelButton(
                detailButtonArea,
                detailButtons,
                option.Label,
                () => Submit(action));
        }
        if (state.HasOpenResponseWindow)
        {
            GameAction pass = FindAction(
                GameRules.GetLegalActions(
                    state,
                    state.InteractionPlayerIndex),
                GameActionType.PassResponse);
            if (pass != null)
            {
                AddPanelButton(
                    detailButtonArea,
                    detailButtons,
                    "Pass",
                    () => Submit(pass));
            }
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

    private List<GameAction> GetLegalActionsForSource(CardInstance card)
    {
        List<GameAction> results = new List<GameAction>();
        if (card == null || state == null)
            return results;

        IReadOnlyList<GameAction> legalActions = GameRules.GetLegalActions(
            state,
            state.InteractionPlayerIndex);
        for (int index = 0; index < legalActions.Count; index++)
        {
            GameAction action = legalActions[index];
            if (action.SourceInstanceId == card.InstanceId)
                results.Add(action);
        }
        return results;
    }

    private List<ActionOption> BuildDisplayOptions(
        CardInstance card,
        IReadOnlyList<GameAction> cardActions)
    {
        List<ActionOption> options = new List<ActionOption>();
        for (int index = 0; index < cardActions.Count; index++)
        {
            GameAction action = cardActions[index];
            if (action.Type == GameActionType.SummonMonster ||
                action.Type == GameActionType.AllocateEnergy ||
                action.Type == GameActionType.ActivateCard)
                continue;

            options.Add(new ActionOption(
                BuildActionLabel(card, action),
                action));
        }
        return options;
    }

    private string BuildActionLabel(CardInstance card, GameAction action)
    {
        switch (action.Type)
        {
            case GameActionType.SetTrap:
                return "Set Trap";
            case GameActionType.ChangeBattlePosition:
                return action.BattlePosition == BattlePosition.Attack
                    ? "To ATK"
                    : "To DEF";
            case GameActionType.Attack:
                return action.TargetInstanceId == 0
                    ? "Direct Attack"
                    : "Attack Z" +
                      (FindMonsterZoneIndex(
                          state.Players[1 - action.PlayerIndex],
                          action.TargetInstanceId) + 1);
            default:
                return action.Type.ToString();
        }
    }

    private static string BuildActivationLabel(CardInstance card)
    {
        if (card.Definition.Category == CardCategory.Monster)
            return "Activate Effect";
        if (card.Definition.Category == CardCategory.Trap)
            return "Activate Trap";
        if (card.Definition.SpellType == SpellType.Field)
            return "Activate Field";
        if (card.Definition.SpellType == SpellType.Equip)
            return "Equip";
        return "Activate";
    }

    private static List<GameAction> FindActions(
        IReadOnlyList<GameAction> actions,
        GameActionType type)
    {
        List<GameAction> matches = new List<GameAction>();
        for (int index = 0; index < actions.Count; index++)
        {
            if (actions[index].Type == type)
                matches.Add(actions[index]);
        }
        return matches;
    }

    private void BeginActivation(List<GameAction> actions)
    {
        if (actions == null || actions.Count == 0)
            return;

        List<GameAction> targetedActions = new List<GameAction>();
        for (int index = 0; index < actions.Count; index++)
        {
            if (actions[index].TargetInstanceId != 0)
                targetedActions.Add(actions[index]);
        }

        if (targetedActions.Count > 0)
        {
            ShowTargetSelection(targetedActions);
            return;
        }

        GameAction action = actions[0];
        if (selectedCard.Definition.Category == CardCategory.Monster)
            ShowMonsterEffectConfirmation(action);
        else
            Submit(action);
    }

    private void ShowTargetSelection(List<GameAction> actions)
    {
        SetPanelVisible(false);
        CloseEnergyPanel();
        ClearDetailRuntime();
        detailPanel.SetActive(true);

        pendingTargetActions.AddRange(actions);
        List<long> targetIds = new List<long>();
        for (int index = 0; index < actions.Count; index++)
        {
            long targetId = actions[index].TargetInstanceId;
            if (targetId != 0 && !targetIds.Contains(targetId))
                targetIds.Add(targetId);
        }

        for (int index = 0; index < targetIds.Count; index++)
        {
            CardInstance target = state.FindCard(targetIds[index]);
            if (target == null)
                continue;

            Image highlight = CreateTargetCard(
                target,
                index,
                targetIds.Count);
            targetHighlights[target.InstanceId] = highlight;
        }

        targetConfirmButton = AddPanelButton(
            detailButtonArea,
            detailButtons,
            "OK",
            ConfirmTargetActivation);
        AddPanelButton(
            detailButtonArea,
            detailButtons,
            "Cancel",
            CancelCardWorkflow);
        LayoutPanelButtons(detailButtons);
        UpdateTargetSelectionVisuals();
    }

    private void ToggleTarget(long instanceId)
    {
        selectedTargetInstanceId = selectedTargetInstanceId == instanceId
            ? 0
            : instanceId;
        UpdateTargetSelectionVisuals();
    }

    private void UpdateTargetSelectionVisuals()
    {
        detailText.text = "Select 1 target for " +
            selectedCard.Definition.CardName + "\nSelected " +
            (selectedTargetInstanceId == 0 ? "0/1" : "1/1");

        foreach (KeyValuePair<long, Image> pair in targetHighlights)
        {
            pair.Value.color = pair.Key == selectedTargetInstanceId
                ? new Color32(224, 174, 48, 245)
                : new Color32(22, 77, 139, 235);
        }

        if (targetConfirmButton != null)
            targetConfirmButton.interactable = selectedTargetInstanceId != 0;
    }

    private void ConfirmTargetActivation()
    {
        if (selectedTargetInstanceId == 0)
            return;

        for (int index = 0; index < pendingTargetActions.Count; index++)
        {
            GameAction action = pendingTargetActions[index];
            if (action.TargetInstanceId == selectedTargetInstanceId)
            {
                Submit(action);
                return;
            }
        }
    }

    private static bool ContainsActionType(
        IReadOnlyList<GameAction> actions,
        GameActionType type)
    {
        return FindAction(actions, type) != null;
    }

    private static GameAction FindAction(
        IReadOnlyList<GameAction> actions,
        GameActionType type)
    {
        for (int index = 0; index < actions.Count; index++)
        {
            if (actions[index].Type == type)
                return actions[index];
        }
        return null;
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
            if (monster == null || !IsLegalTributeCandidate(monster.InstanceId))
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

            if (!CanCompleteSelectedTributes())
                selectedTributes.Remove(instanceId);
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
        {
            tributeConfirmButton.interactable =
                selectedTributes.Count == required &&
                (FindLegalSummon(
                     BattlePosition.Attack,
                     selectedTributes) != null ||
                 FindLegalSummon(
                     BattlePosition.Defense,
                     selectedTributes) != null);
        }
    }

    private void ShowSummonPositionChoices()
    {
        CloseDetailPanel();
        CloseEnergyPanel();
        ClearButtons();
        SetPanelVisible(true);

        GameAction attackSummon = FindLegalSummon(
            BattlePosition.Attack,
            selectedTributes);
        if (attackSummon != null)
        {
            AddButton(
                "Attack Position",
                () => SubmitSummon(BattlePosition.Attack));
        }

        GameAction defenseSummon = FindLegalSummon(
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
        GameAction action = FindLegalSummon(
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
        ClearEnergyRuntime();
        energyPanel.SetActive(true);

        PlayerState player = state.Players[state.CurrentPlayerIndex];
        energyText.text = "Allocate Energy to " +
            selectedCard.Definition.CardName +
            "\nAvailable Energy: " + player.AvailableEnergy;

        List<GameAction> cardActions = GetLegalActionsForSource(selectedCard);
        for (int index = 0; index < cardActions.Count; index++)
        {
            GameAction action = cardActions[index];
            if (action.Type != GameActionType.AllocateEnergy)
                continue;

            GameAction selectableAction = action;
            Button amountButton = AddPanelButton(
                energyAmountButtonArea,
                energyAmountButtons,
                action.Amount.ToString(),
                () => SelectEnergyAmount(selectableAction));
            energyAmountButtonViews[action.Amount] = amountButton;
        }

        energyConfirmButton = AddPanelButton(
            energyFooterButtonArea,
            energyFooterButtons,
            "OK",
            ConfirmEnergyAllocation);
        energyConfirmButton.interactable = false;
        AddPanelButton(
            energyFooterButtonArea,
            energyFooterButtons,
            "Cancel",
            CancelEnergyAllocation);
        LayoutPanelButtons(energyAmountButtons);
        LayoutPanelButtons(energyFooterButtons);
    }

    private void SelectEnergyAmount(GameAction action)
    {
        selectedEnergyAction = action;
        foreach (KeyValuePair<int, Button> pair in energyAmountButtonViews)
        {
            SetButtonColor(
                pair.Value,
                pair.Key == action.Amount
                    ? new Color32(224, 174, 48, 245)
                    : new Color32(22, 77, 139, 245));
        }

        if (energyConfirmButton != null)
            energyConfirmButton.interactable = true;
    }

    private void ConfirmEnergyAllocation()
    {
        if (selectedEnergyAction != null)
            Submit(selectedEnergyAction);
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

    private GameAction FindLegalSummon(
        BattlePosition position,
        IReadOnlyList<long> tributes)
    {
        List<GameAction> actions = GetLegalActionsForSource(selectedCard);
        for (int index = 0; index < actions.Count; index++)
        {
            GameAction action = actions[index];
            if (action.Type == GameActionType.SummonMonster &&
                action.BattlePosition == position &&
                HaveSameIds(action.TributeInstanceIds, tributes))
            {
                return action;
            }
        }
        return null;
    }

    private bool IsLegalTributeCandidate(long instanceId)
    {
        List<GameAction> actions = GetLegalActionsForSource(selectedCard);
        for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
        {
            GameAction action = actions[actionIndex];
            if (action.Type != GameActionType.SummonMonster)
                continue;

            for (int tributeIndex = 0;
                 tributeIndex < action.TributeInstanceIds.Count;
                 tributeIndex++)
            {
                if (action.TributeInstanceIds[tributeIndex] == instanceId)
                    return true;
            }
        }
        return false;
    }

    private bool CanCompleteSelectedTributes()
    {
        List<GameAction> actions = GetLegalActionsForSource(selectedCard);
        for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
        {
            GameAction action = actions[actionIndex];
            if (action.Type == GameActionType.SummonMonster &&
                ContainsAllIds(
                    action.TributeInstanceIds,
                    selectedTributes))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsAllIds(
        IReadOnlyList<long> available,
        IReadOnlyList<long> selected)
    {
        for (int selectedIndex = 0;
             selectedIndex < selected.Count;
             selectedIndex++)
        {
            bool found = false;
            for (int availableIndex = 0;
                 availableIndex < available.Count;
                 availableIndex++)
            {
                if (selected[selectedIndex] == available[availableIndex])
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
        }
        return true;
    }

    private static bool HaveSameIds(
        IReadOnlyList<long> left,
        IReadOnlyList<long> right)
    {
        return left.Count == right.Count && ContainsAllIds(left, right);
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

        List<GameAction> responseActions = actions;
        Button activateButton = AddPanelButton(
            detailButtonArea,
            detailButtons,
            "ACTIVATE EFFECT",
            () => BeginActivation(responseActions));
        SetButtonColor(
            activateButton,
            new Color32(19, 85, 151, 250));
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

        energyAmountButtonArea = CreateRuntimeArea(
            panel,
            "RuntimeEnergyAmountButtons",
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(-50f, 150f),
            new Vector2(0f, 112f));
        energyFooterButtonArea = CreateRuntimeArea(
            panel,
            "RuntimeEnergyFooterButtons",
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(-50f, 72f),
            new Vector2(0f, 24f));
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

    private Image CreateTargetCard(
        CardInstance target,
        int index,
        int count)
    {
        GameObject slotObject = new GameObject(
            "TargetChoice_" + target.InstanceId,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        slotObject.layer = detailCardArea.gameObject.layer;
        RectTransform slot = slotObject.GetComponent<RectTransform>();
        slot.SetParent(detailCardArea, false);
        slot.anchorMin = new Vector2((float)index / count, 0f);
        slot.anchorMax = new Vector2((float)(index + 1) / count, 1f);
        slot.offsetMin = new Vector2(12f, 10f);
        slot.offsetMax = new Vector2(-12f, -10f);

        Image background = slotObject.GetComponent<Image>();
        background.color = new Color32(22, 77, 139, 235);
        Button button = slotObject.GetComponent<Button>();
        button.targetGraphic = background;
        long instanceId = target.InstanceId;
        button.onClick.AddListener(() => ToggleTarget(instanceId));

        CreateCardPreview(target, slot, new Vector2(0f, 22f), 0.68f);

        int zoneIndex = FindMonsterZoneIndex(
            state.Players[target.OwnerPlayerIndex],
            target.InstanceId);
        string ownerLabel = target.OwnerPlayerIndex ==
            state.InteractionPlayerIndex
            ? "Your"
            : "Opponent";
        TMP_Text label = CreatePanelLabel(
            slot,
            "TargetLabel",
            ownerLabel + " Z" + (zoneIndex + 1),
            24f);
        RectTransform labelRect = label.rectTransform;
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
        targetHighlights.Clear();
        pendingTargetActions.Clear();
        selectedTargetInstanceId = 0;
        tributeConfirmButton = null;
        targetConfirmButton = null;
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
        ClearEnergyRuntime();
        if (energyPanel != null)
            energyPanel.SetActive(false);
    }

    private void ClearEnergyRuntime()
    {
        ClearRuntimeObjects(energyAmountButtons);
        ClearRuntimeObjects(energyFooterButtons);
        energyAmountButtonViews.Clear();
        selectedEnergyAction = null;
        energyConfirmButton = null;
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
