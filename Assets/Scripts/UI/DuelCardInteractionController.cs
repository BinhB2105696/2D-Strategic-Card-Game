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
            SetPanelVisible(false);
            return;
        }

        SetPanelVisible(true);
        List<ActionOption> options = BuildLegalOptions(selectedCard);
        int specialActionCount = 0;

        if (selectedCard.OwnerPlayerIndex == state.CurrentPlayerIndex &&
            selectedCard.Zone == CardZone.Hand &&
            selectedCard.Definition.Category == CardCategory.Monster &&
            HasAnyLegalSummon(selectedCard))
        {
            AddButton("Summon", BeginSummon);
            specialActionCount++;
        }

        if (selectedCard.OwnerPlayerIndex == state.CurrentPlayerIndex &&
            selectedCard.Zone == CardZone.MonsterZone &&
            HasAnyLegalEnergyAllocation(selectedCard))
        {
            AddButton("Allocate Energy", ShowEnergyAllocation);
            specialActionCount++;
        }

        foreach (ActionOption option in options)
        {
            GameAction action = option.Action;
            AddButton(option.Label, () => Submit(action));
        }
        AddButton("Close", CloseSelection);

        string guidance;
        if (!string.IsNullOrEmpty(resultMessage))
            guidance = resultMessage;
        else if (options.Count + specialActionCount > 0)
            guidance = "Choose a legal action.";
        else
            guidance = GetNoActionReason(selectedCard);

        actionText.text = BuildCardDetails(selectedCard) + "\n" + guidance;
        actionText.margin = new Vector4(10f, 8f, 10f, 142f);
        actionText.fontSize = 25f;
    }

    private List<ActionOption> BuildLegalOptions(CardInstance card)
    {
        List<ActionOption> options = new List<ActionOption>();
        if (card.OwnerPlayerIndex != state.CurrentPlayerIndex)
            return options;

        if (card.Zone == CardZone.Hand)
        {
            if (card.Definition.Category == CardCategory.Spell &&
                     card.Definition.SpellType == SpellType.Normal)
                AddNormalSpellOption(card, options);
        }
        else if (card.Zone == CardZone.MonsterZone)
        {
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

    private void AddNormalSpellOption(
        CardInstance spell,
        List<ActionOption> options)
    {
        PlayerState player = state.Players[state.CurrentPlayerIndex];
        for (int zoneIndex = 0;
             zoneIndex < player.SpellTrapZones.Count;
             zoneIndex++)
        {
            GameAction action = GameAction.ActivateNormalSpell(
                state.CurrentPlayerIndex,
                spell.InstanceId,
                zoneIndex);
            if (IsLegal(action))
            {
                options.Add(new ActionOption(
                    "Activate Z" + (zoneIndex + 1),
                    action));
                return;
            }
        }
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
        if (card.OwnerPlayerIndex != state.CurrentPlayerIndex)
            return "Opponent card: no player action available.";
        if (card.Zone == CardZone.Hand &&
            card.Definition.Category == CardCategory.Trap)
            return "Trap setting/activation is not connected yet.";
        if (card.Zone == CardZone.Hand &&
            card.Definition.Category == CardCategory.Spell)
            return "Only the verified Normal Spell flow is connected.";
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
            background.raycastTarget = true;

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
