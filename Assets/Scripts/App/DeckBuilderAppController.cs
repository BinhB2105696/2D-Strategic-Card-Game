using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A separate, touch-first app shell. The existing duel scene is unchanged.
public sealed partial class DeckBuilderAppController : MonoBehaviour
{
    [SerializeField] private CardData[] catalogCards;
    [SerializeField] private long[] catalogUpdatedTicks;
    [SerializeField] private GameObject monsterCardPrefab;
    [SerializeField] private GameObject spellTrapCardPrefab;
    [SerializeField] private TMP_FontAsset uiFont;
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField, Range(.1f, 1f)] private float gridCardScale = .56f;

    private readonly Dictionary<string, CardData> byId =
        new Dictionary<string, CardData>(StringComparer.Ordinal);
    private readonly Dictionary<string, long> updatedById =
        new Dictionary<string, long>(StringComparer.Ordinal);
    private readonly HashSet<string> ranks = new HashSet<string>();
    private readonly HashSet<string> attributes = new HashSet<string>();
    private readonly HashSet<string> monsterTypes = new HashSet<string>();
    private readonly HashSet<string> spellTypes = new HashSet<string>();
    private readonly HashSet<string> trapTypes = new HashSet<string>();

    private LocalPlayerProfile profile;
    private SavedDeck editingDeck;
    [SerializeField, HideInInspector] private Canvas canvas;
    [SerializeField, HideInInspector] private RectTransform safeRoot;
    [SerializeField, HideInInspector] private RectTransform portraitFrame;
    [SerializeField, HideInInspector] private GameObject entryPage;
    [SerializeField, HideInInspector] private GameObject homePage;
    [SerializeField, HideInInspector] private GameObject builderPage;
    [SerializeField, HideInInspector] private GameObject modalLayer;
    [SerializeField, HideInInspector] private RectTransform catalogPanel;
    [SerializeField, HideInInspector] private RectTransform catalogContent;
    [SerializeField, HideInInspector] private RectTransform deckContent;
    [SerializeField, HideInInspector] private GameObject toolbar;
    [SerializeField, HideInInspector] private TMP_Text countText;
    [SerializeField, HideInInspector] private TMP_Text deckNameText;
    [SerializeField, HideInInspector] private TMP_Text guestNameText;
    [SerializeField, HideInInspector] private TMP_Text summaryText;
    [SerializeField, HideInInspector] private TMP_Text sortText;
    [SerializeField, HideInInspector] private TMP_Text directionText;
    [SerializeField, HideInInspector] private TMP_Text messageText;
    [SerializeField, HideInInspector] private TMP_InputField nameInput;
    [SerializeField, HideInInspector] private TMP_InputField searchInput;
    [SerializeField, HideInInspector] private Button saveButton;
    private int quickCategory = -1;
    private bool favoritesOnly;
    private bool sortDescending;
    private int filterTab;
    // -1: quick category; 0: detailed Monsters; 1: detailed Spells/Traps.
    private int detailedScope = -1;
    private SortMode sortMode = SortMode.Name;
    private float catalogHeight = 1120f;
    private bool saveRequiresExplicitAction;

    private static readonly Color Navy = new Color(.035f, .095f, .18f, .98f);
    private static readonly Color Blue = new Color(.045f, .22f, .43f, 1f);
    private static readonly Color Cyan = new Color(.20f, .72f, .93f, 1f);
    private static readonly Color Gold = new Color(1f, .78f, .25f, 1f);
    private static readonly Color White = new Color(.96f, .97f, 1f, 1f);

    private enum SortMode { Name, Rank, Attack, Defense, Health, Newest, Type }

    public float CatalogHeight => catalogHeight;
    public float UiScale => Mathf.Max(.01f,
        canvas == null ? 1f : canvas.scaleFactor * portraitFrame.localScale.x);

    private void Awake()
    {
        InitializeCatalog();
        profile = AppProfileServices.Profiles.Load(byId, out string warning);
        saveRequiresExplicitAction = !string.IsNullOrEmpty(warning) &&
            File.Exists(LocalDeckStore.SavePath);
        if (canvas == null)
        {
            Debug.LogError("AppShell UI is not prepared. In Edit Mode run Card Game > App Shell > Prepare Editable UI.", this);
            enabled = false;
            return;
        }
        BindUi();
        AppAudioSettings.Apply(profile.settings);
        nameInput.SetTextWithoutNotify(profile.guestName);
        ShowPage(AppMatchLaunch.ReturningHome ? homePage : entryPage);
        AppMatchLaunch.ReturningHome = false;
        if (!string.IsNullOrEmpty(warning)) SetMessage(warning);
    }

    private void InitializeCatalog()
    {
        byId.Clear();
        updatedById.Clear();
        if (catalogCards == null) return;
        foreach (CardData card in catalogCards)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.CardId))
                continue;
            if (byId.ContainsKey(card.CardId))
            {
                Debug.LogError("Duplicate Card ID in app catalog: " + card.CardId, card);
                continue;
            }
            byId.Add(card.CardId, card);
            int index = Array.IndexOf(catalogCards, card);
            updatedById[card.CardId] = catalogUpdatedTicks != null &&
                index < catalogUpdatedTicks.Length ? catalogUpdatedTicks[index] : 0;
        }
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("AppCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;

        safeRoot = Stretch(canvasObject.transform, "SafeAreaRoot", 0, 0, 0, 0);
        portraitFrame = NewRect(safeRoot, "PortraitFrame");
        portraitFrame.anchorMin = portraitFrame.anchorMax =
            portraitFrame.pivot = new Vector2(.5f, .5f);
        portraitFrame.sizeDelta = new Vector2(1080, 1920);
        portraitFrame.anchoredPosition = Vector2.zero;
        safeRoot.gameObject.AddComponent<AppPortraitLayout>().Configure(portraitFrame);
        Image baseImage = portraitFrame.gameObject.AddComponent<Image>();
        baseImage.color = Navy;
        if (backgroundSprite != null)
        {
            baseImage.sprite = backgroundSprite;
            baseImage.preserveAspect = false;
            Panel(portraitFrame, "BackgroundShade", Navy, 0, 0, 1080, 1920);
        }

        if (GetComponentInChildren<EventSystem>(true) == null)
        {
            GameObject events = new GameObject("AppEventSystem",
                typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(transform, false);
        }

        entryPage = CreatePage("GuestEntry");
        BuildEntry();
        homePage = CreatePage("MainMenu");
        BuildHome();
        builderPage = CreatePage("DeckBuilder");
        BuildBuilder();
        modalLayer = CreatePage("ModalLayer");
        BuildAdditionalPages();
        BuildModalTemplates();
        modalLayer.SetActive(false);
        ShowPage(homePage);
    }

    private GameObject CreatePage(string name)
    {
        return Stretch(portraitFrame, name, 0, 0, 0, 0).gameObject;
    }

    private void BuildEntry()
    {
        RectTransform parent = entryPage.GetComponent<RectTransform>();
        Label(parent, "Title", "2D STRATEGIC\nCARD GAME", 74, Gold,
            70, 340, 940, 230);
        Label(parent, "Subtitle", "LOCAL GUEST PROFILE", 36, Cyan,
            95, 600, 890, 70);
        Label(parent, "Hint", "Your guest name is saved on this device. Online accounts come later.",
            29, White, 90, 710, 900, 110);
        nameInput = Input(parent, "GuestName", profile.guestName,
            105, 865, 870, 88, null);
        ButtonAt(parent, "ContinueButton", "ENTER GAME", 40,
            135, 1000, 810, 110, Blue, () =>
            {
                profile.guestName = string.IsNullOrWhiteSpace(nameInput.text)
                    ? "Player" : nameInput.text.Trim();
                ShowPage(homePage);
                if (!saveRequiresExplicitAction && profile.decks.Count > 0 &&
                    !AppProfileServices.Profiles.Save(profile, byId, out string error))
                    SetMessage(error);
            });
    }

    private void BuildHome()
    {
        RectTransform parent = homePage.GetComponent<RectTransform>();
        Label(parent, "HomeTitle", "MAIN MENU", 74, Gold, 80, 260, 920, 125);
        guestNameText = Label(parent, "PlayerName", "Guest profile", 35, Cyan,
            80, 410, 920, 65);
        ButtonAt(parent, "DeckBuilding", "DECK BUILDING", 42,
            90, 610, 900, 135, Blue, OpenBuilder);
        ButtonAt(parent, "Pve", "DUEL MODE", 42,
            90, 795, 900, 135, Blue, OpenDuelModes);
        ButtonAt(parent, "Pvp", "PVP - ONLINE", 42,
            90, 980, 900, 135, Blue, OpenPvpRoom);
        ButtonAt(parent, "Profile", "CHANGE GUEST NAME", 34,
            200, 1210, 680, 95, Navy, () => ShowPage(entryPage));
        Label(parent, "BuildNote",
            "Your selected saved deck is used in PvE.",
            29, White, 80, 1430, 920, 155);
    }

    private void BuildBuilder()
    {
        RectTransform parent = builderPage.GetComponent<RectTransform>();
        Panel(parent, "Header", Navy, 0, 0, 1080, 265);
        Label(parent, "Heading", "DECK BUILDING", 57, Gold,
            40, 20, 700, 75);
        deckNameText = Label(parent, "DeckName", "", 37, White,
            45, 112, 730, 63);
        countText = Label(parent, "DeckCount", "", 36, Cyan,
            760, 115, 275, 58);
        ButtonAt(parent, "PreviousDeck", "<", 40, 35, 190, 115, 70,
            Blue, () => SelectDeck(-1));
        ButtonAt(parent, "NextDeck", ">", 40, 165, 190, 115, 70,
            Blue, () => SelectDeck(1));
        ButtonAt(parent, "FavoriteInfo", "* FAVORITES", 28,
            355, 190, 280, 70, Blue,
            () => SelectQuickCategory(4));

        RectTransform deckViewport = Stretch(parent, "DeckViewport",
            12, 145, 12, 275);
        deckViewport.gameObject.AddComponent<Image>().color =
            new Color(.015f, .055f, .11f, .95f);
        deckViewport.gameObject.AddComponent<RectMask2D>();
        deckContent = CreateGridScroll(deckViewport, "DeckCards", 150, 215);

        catalogPanel = NewRect(parent, "CatalogDrawer");
        catalogPanel.anchorMin = new Vector2(0, 0);
        catalogPanel.anchorMax = new Vector2(1, 0);
        catalogPanel.pivot = new Vector2(.5f, 0);
        catalogPanel.anchoredPosition = new Vector2(0, 145);
        catalogPanel.sizeDelta = new Vector2(0, catalogHeight);
        catalogPanel.gameObject.AddComponent<Image>().color = Navy;
        Image rail = Panel(catalogPanel, "TopRail", Blue, 0, 0, 1080, 68);
        DeckCatalogDragHandle handle = rail.gameObject.AddComponent<DeckCatalogDragHandle>();
        handle.Initialize(this);
        Label(rail.transform, "DragLabel", "CARD CATALOG - DRAG TO OPEN / CLOSE",
            34, White, 36, 8, 1000, 50).raycastTarget = false;
        summaryText = Label(catalogPanel, "Summary", "", 28, Cyan,
            32, 73, 1020, 52);

        toolbar = NewRect(catalogPanel, "FilterToolbar").gameObject;
        RectTransform tools = toolbar.GetComponent<RectTransform>();
        tools.anchorMin = new Vector2(0, 1);
        tools.anchorMax = new Vector2(1, 1);
        tools.pivot = new Vector2(.5f, 1);
        tools.anchoredPosition = new Vector2(0, -125);
        tools.sizeDelta = new Vector2(0, 225);
        string[] categories = { "ALL", "MONSTER", "SPELL", "TRAP", "* FAV" };
        for (int i = 0; i < categories.Length; i++)
        {
            int choice = i;
            ButtonAt(tools, "Category_" + categories[i], categories[i], 28,
                18 + i * 211, 0, 200, 58, Blue, () =>
                { SelectQuickCategory(choice); });
        }
        searchInput = Input(tools, "Search", "", 20, 70, 630, 60,
            "Search name or effect text...");
        searchInput.onValueChanged.AddListener(_ => RebuildCatalog());
        ButtonAt(tools, "ClearSearch", "X", 30, 660, 70, 65, 60,
            Blue, () => searchInput.text = "");
        ButtonAt(tools, "AdvancedFilter", "DETAIL FILTER", 25,
            739, 70, 320, 60, Blue, ShowFilter);
        ButtonAt(tools, "SortButton", "SORT BY", 26,
            20, 145, 235, 61, Blue, CycleSort);
        sortText = Label(tools, "SortName", "", 26, White,
            280, 150, 520, 54);
        directionText = Label(tools, "Direction", "", 35, Gold,
            935, 145, 105, 58);
        ButtonAt(tools, "SortDirection", "UP/DN", 27,
            840, 145, 92, 61, Blue, () =>
            { sortDescending = !sortDescending; RebuildCatalog(); });

        RectTransform catalogViewport = Stretch(catalogPanel,
            "CatalogViewport", 12, 10, 12, 355);
        catalogViewport.gameObject.AddComponent<Image>().color =
            new Color(.01f, .075f, .15f, 1f);
        catalogViewport.gameObject.AddComponent<RectMask2D>();
        catalogContent = CreateGridScroll(catalogViewport,
            "CatalogCards", 150, 215);

        Panel(parent, "BottomBar", Navy, 0, 1775, 1080, 145);
        ButtonAt(parent, "Back", "<", 54, 12, 1790, 138, 105,
            Blue, () => ShowPage(homePage));
        saveButton = ButtonAt(parent, "Save", "SAVE", 36,
            165, 1790, 300, 105, Blue, SaveDeck);
        ButtonAt(parent, "Copy", "COPY DECK", 32,
            480, 1790, 300, 105, Blue, CopyDeck);
        ButtonAt(parent, "Home", "HOME", 40, 795, 1790, 270, 105,
            Blue, () => ShowPage(homePage));
    }

    public void SetCatalogHeight(float height)
    {
        catalogHeight = Mathf.Clamp(height, 125f, 1120f);
        catalogPanel.sizeDelta = new Vector2(0, catalogHeight);
        bool expanded = catalogHeight > 430f;
        toolbar.SetActive(expanded);
        Transform catalogViewport = catalogPanel.Find("CatalogViewport");
        catalogViewport.gameObject.SetActive(expanded);
    }

    public void ToggleCatalog() => SetCatalogHeight(
        catalogHeight > 430f ? 125f : 1120f);

    public void SnapCatalog() => SetCatalogHeight(
        catalogHeight > 620f ? 1120f : 125f);

    private void OpenBuilder()
    {
        if (profile.decks.Count == 0)
        {
            SetMessage("Card catalog is incomplete. Prepare sample cards in Edit Mode.");
            return;
        }
        editingDeck = Copy(profile.decks[profile.selectedDeck]);
        quickCategory = -1;
        favoritesOnly = false;
        detailedScope = -1;
        SetCatalogHeight(catalogHeight);
        RebuildDeck();
        RebuildCatalog();
        ShowPage(builderPage);
    }

    private void SelectDeck(int direction)
    {
        if (HasUnsavedDeck())
        {
            ConfirmDiscard(() => SelectDeckConfirmed(direction));
            return;
        }
        SelectDeckConfirmed(direction);
    }

    private void SelectDeckConfirmed(int direction)
    {
        if (profile.decks.Count == 0) return;
        profile.selectedDeck = (profile.selectedDeck + direction +
            profile.decks.Count) % profile.decks.Count;
        editingDeck = Copy(profile.decks[profile.selectedDeck]);
        RebuildDeck();
        RebuildCatalog();
        if (!saveRequiresExplicitAction &&
            !AppProfileServices.Profiles.Save(profile, byId, out string error))
            SetMessage(error);
    }

    private void RebuildDeck()
    {
        Clear(deckContent);
        List<CardData> cards = new List<CardData>();
        foreach (string id in editingDeck.cardIds)
            if (byId.TryGetValue(id, out CardData card)) cards.Add(card);
        cards.Sort(DeckOrder);
        int slots = Mathf.Max(30, cards.Count);
        slots = Mathf.Min(40, slots);
        for (int i = 0; i < slots; i++)
        {
            if (i < cards.Count)
            {
                CardData displayed = cards[i];
                CardTile(deckContent, displayed, false, 0,
                    () => ShowCardDetail(displayed, false));
            }
            else
            {
                RectTransform slot = NewRect(deckContent, "Empty_" + (i + 1));
                slot.gameObject.AddComponent<Image>().color =
                    new Color(.07f, .18f, .32f, .7f);
                Label(slot, "Number", (i + 1).ToString(), 36, Cyan,
                    0, 60, 150, 90);
            }
        }
        countText.text = cards.Count + "/" + GameConfig.CreateDefault().MaximumDeckSize;
        deckNameText.text = editingDeck.name;
        saveButton.interactable = cards.Count >= GameConfig.CreateDefault().MinimumDeckSize;
    }

    private void RebuildCatalog()
    {
        if (catalogContent == null || editingDeck == null) return;
        Clear(catalogContent);
        List<CardData> cards = new List<CardData>(byId.Values);
        cards.RemoveAll(card => !Matches(card));
        cards.Sort(CompareCatalog);
        foreach (CardData card in cards)
        {
            int copies = editingDeck.cardIds.FindAll(id => id == card.CardId).Count;
            CardTile(catalogContent, card, true, copies,
                () => ShowCardDetail(card, true));
        }
        summaryText.text = cards.Count + " cards  -  " +
            (favoritesOnly ? "Favorites  -  " : "") +
            "Sort: " + sortMode + (sortDescending ? " DESC" : " ASC");
        sortText.text = sortMode.ToString();
        directionText.text = sortDescending ? "DN" : "UP";
    }

    private bool Matches(CardData card)
    {
        if (quickCategory >= 0 && (int)card.Category != quickCategory)
            return false;
        if (favoritesOnly && !profile.favoriteCardIds.Contains(card.CardId))
            return false;
        string query = searchInput == null ? null : searchInput.text.Trim();
        if (!string.IsNullOrEmpty(query) &&
            card.CardName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
            (card.EffectText == null || card.EffectText.IndexOf(query,
                StringComparison.OrdinalIgnoreCase) < 0))
            return false;
        if (detailedScope < 0) return true;
        if (detailedScope == 0)
        {
            if (card.Category != CardCategory.Monster) return false;
            return (ranks.Count == 0 || ranks.Contains(card.MonsterRank.ToString())) &&
                (attributes.Count == 0 || attributes.Contains(
                    card.MonsterAttribute.ToString())) &&
                (monsterTypes.Count == 0 || monsterTypes.Contains(
                    card.MonsterType.ToString()));
        }
        if (card.Category == CardCategory.Monster) return false;
        bool hasSubtypeSelection = spellTypes.Count > 0 || trapTypes.Count > 0;
        if (card.Category == CardCategory.Spell)
            return !hasSubtypeSelection || spellTypes.Contains(card.SpellType.ToString());
        return !hasSubtypeSelection || trapTypes.Contains(card.TrapType.ToString());
    }

    private void SelectQuickCategory(int choice)
    {
        favoritesOnly = choice == 4;
        quickCategory = choice == 4 ? -1 : choice - 1;
        detailedScope = -1;
        RebuildCatalog();
    }

    private void ApplyDetailedFilter()
    {
        detailedScope = filterTab;
        quickCategory = -1;
        favoritesOnly = false;
        RebuildCatalog();
        CloseModal();
    }

    private int CompareCatalog(CardData a, CardData b)
    {
        int result = 0;
        switch (sortMode)
        {
            case SortMode.Rank: result = (a.Category == CardCategory.Monster ?
                (int)a.MonsterRank : -1).CompareTo(
                b.Category == CardCategory.Monster ? (int)b.MonsterRank : -1); break;
            case SortMode.Attack: result = a.BaseAttack.CompareTo(b.BaseAttack); break;
            case SortMode.Defense: result = a.BaseDefense.CompareTo(b.BaseDefense); break;
            case SortMode.Health: result = a.BaseHealth.CompareTo(b.BaseHealth); break;
            case SortMode.Newest:
                result = updatedById[a.CardId].CompareTo(updatedById[b.CardId]); break;
            case SortMode.Type: result = DeckOrder(a, b); break;
            default: result = string.Compare(a.CardName, b.CardName,
                StringComparison.OrdinalIgnoreCase); break;
        }
        if (sortDescending) result = -result;
        return result != 0 ? result : string.Compare(a.CardId, b.CardId,
            StringComparison.Ordinal);
    }

    private static int DeckOrder(CardData a, CardData b)
    {
        int value = a.Category.CompareTo(b.Category);
        if (value != 0) return value;
        if (a.Category == CardCategory.Monster)
            value = a.MonsterRank.CompareTo(b.MonsterRank);
        else if (a.Category == CardCategory.Spell)
            value = a.SpellType.CompareTo(b.SpellType);
        else value = a.TrapType.CompareTo(b.TrapType);
        return value != 0 ? value : string.Compare(a.CardName, b.CardName,
            StringComparison.OrdinalIgnoreCase);
    }

    private void CycleSort()
    {
        sortMode = (SortMode)(((int)sortMode + 1) % Enum.GetValues(typeof(SortMode)).Length);
        RebuildCatalog();
    }

    private void ShowCardDetail(CardData card, bool fromCatalog)
    {
        detailCard = card;
        detailFromCatalog = fromCatalog;
        OpenModalPanel(detailPanel);
        Clear(detailArt);
        GameObject prefab = card.Category == CardCategory.Monster ? monsterCardPrefab : spellTrapCardPrefab;
        if (prefab != null)
        {
            GameObject visual = Instantiate(prefab, detailArt);
            RectTransform rect = visual.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(250, 350);
            visual.GetComponent<CardView>()?.SetCardData(card);
        }
        detailName.text = card.CardName;
        detailType.text = card.Category == CardCategory.Monster ?
            card.MonsterRank + " / " + card.MonsterAttribute + " / " + card.MonsterType +
            "\nHP " + card.BaseHealth + "   ATK " + card.BaseAttack + "   DEF " + card.BaseDefense +
            "   Energy " + card.AttackEnergyRequirement :
            card.Category == CardCategory.Spell ? card.SpellType + " Spell" : card.TrapType + " Trap";
        detailEffect.text = card.EffectText ?? "";
        detailFavorite.GetComponentInChildren<TMP_Text>().text =
            profile.favoriteCardIds.Contains(card.CardId) ? "*" : "+*";
        detailChange.GetComponentInChildren<TMP_Text>().text = fromCatalog ? "+ ADD" : "- REMOVE";
    }

    private void ChangeDetailDeck()
    {
        if (detailCard == null || editingDeck == null) return;
        if (detailFromCatalog)
        {
            if (!LocalDeckStore.CanAdd(editingDeck, detailCard.CardId, out string reason, detailCard.MaxCopiesPerDeck))
            { SetMessage(reason); return; }
            editingDeck.cardIds.Add(detailCard.CardId);
        }
        else editingDeck.cardIds.Remove(detailCard.CardId);
        RebuildDeck();
        RebuildCatalog();
        CloseModal();
    }

    private void ToggleDetailFavorite()
    {
        if (detailCard == null) return;
        if (!profile.favoriteCardIds.Remove(detailCard.CardId))
            profile.favoriteCardIds.Add(detailCard.CardId);
        RebuildCatalog();
        ShowCardDetail(detailCard, detailFromCatalog);
        PersistProfile();
    }

    private void ShowFilter()
    {
        OpenModalPanel(filterPanel);
        monsterFilterPage.SetActive(filterTab == 0);
        spellFilterPage.SetActive(filterTab == 1);
        RefreshFilterTabVisuals();
        foreach (AppFilterOption option in filterOptions)
            option.Refresh(FilterSet(option.group).Contains(option.value));
    }

    private HashSet<string> FilterSet(int group)
    {
        switch (group)
        {
            case 0: return ranks;
            case 1: return attributes;
            case 2: return monsterTypes;
            case 3: return spellTypes;
            default: return trapTypes;
        }
    }

    private void SaveDeck()
    {
        SavedDeck previous = profile.decks[profile.selectedDeck];
        profile.decks[profile.selectedDeck] = Copy(editingDeck);
        if (AppProfileServices.Profiles.Save(profile, byId, out string error))
        {
            saveRequiresExplicitAction = false;
            SetMessage("Saved " + editingDeck.name + " on this device.");
        }
        else
        {
            profile.decks[profile.selectedDeck] = previous;
            SetMessage(error);
        }
    }

    private void CopyDeck()
    {
        if (profile.decks.Count >= LocalDeckStore.MaximumSavedDecks)
        {
            SetMessage("Maximum " + LocalDeckStore.MaximumSavedDecks + " saved decks. Select one to edit.");
            return;
        }
        if (!LocalDeckStore.Validate(editingDeck, byId,
            GameConfig.CreateDefault(), out string error))
        { SetMessage(error); return; }
        SavedDeck copy = Copy(editingDeck);
        copy.deckId = Guid.NewGuid().ToString("N");
        copy.name += " Copy";
        profile.decks.Add(copy);
        profile.selectedDeck = profile.decks.Count - 1;
        editingDeck = Copy(copy);
        RebuildDeck();
        RebuildCatalog();
        SetMessage("Copy created. Press SAVE to store it on this device.");
    }

    private static SavedDeck Copy(SavedDeck deck) => new SavedDeck
    {
        deckId = deck.deckId,
        name = deck.name,
        cardIds = new List<string>(deck.cardIds)
    };

    private void OpenModalPanel(GameObject panel)
    {
        modalLayer.SetActive(true);
        foreach (Transform child in modalLayer.transform)
            child.gameObject.SetActive(child.gameObject == panel || child.name == "Dim");
        panel.transform.SetAsLastSibling();
    }

    private void CloseModal()
    {
        modalLayer.SetActive(false);
        pendingConfirmation = null;
    }

    private void SetMessage(string message)
    {
        OpenModalPanel(messagePanel);
        messageText.text = message;
    }

    private void ShowPage(GameObject page)
    {
        entryPage.SetActive(page == entryPage);
        homePage.SetActive(page == homePage);
        builderPage.SetActive(page == builderPage);
        if (profilePage != null) profilePage.SetActive(page == profilePage);
        if (settingsPage != null) settingsPage.SetActive(page == settingsPage);
        if (page == homePage && profile != null) RefreshHome();
        if (modalLayer != null) CloseModal();
    }

    private void CardTile(RectTransform parent, CardData card,
        bool inCatalog, int copies, Action onTap)
    {
        RectTransform wrapper = NewRect(parent, "Card_" + card.CardId);
        Image hit = wrapper.gameObject.AddComponent<Image>();
        hit.color = new Color(.07f, .17f, .31f, 1f);
        Button button = wrapper.gameObject.AddComponent<Button>();
        button.onClick.AddListener(() => onTap());
        GameObject prefab = card.Category == CardCategory.Monster ?
            monsterCardPrefab : spellTrapCardPrefab;
        if (prefab != null)
        {
            GameObject visual = Instantiate(prefab, wrapper);
            RectTransform rect = visual.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0, -5);
            rect.sizeDelta = new Vector2(250, 350);
            rect.localScale = Vector3.one * gridCardScale;
            visual.GetComponent<CardView>()?.SetCardData(card);
        }
        if (inCatalog && copies > 0)
        {
            Label(wrapper, "Count", copies + "/" + card.MaxCopiesPerDeck, 27, Gold,
                88, 173, 60, 35);
        }
    }

    private RectTransform CreateGridScroll(RectTransform viewport, string name,
        float cellWidth, float cellHeight)
    {
        RectTransform content = NewRect(viewport, name);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cellWidth, cellHeight);
        grid.spacing = new Vector2(15, 12);
        grid.padding = new RectOffset(22, 15, 12, 12);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        return content;
    }

    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    private static RectTransform NewRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static RectTransform Stretch(Transform parent, string name,
        float left, float bottom, float right, float top)
    {
        RectTransform rect = NewRect(parent, name);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    private static void Position(RectTransform rect, float x, float y,
        float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private Image Panel(Transform parent, string name, Color color,
        float x, float y, float width, float height)
    {
        RectTransform rect = NewRect(parent, name);
        Position(rect, x, y, width, height);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private TextMeshProUGUI Label(Transform parent, string name, string value,
        float size, Color color, float x, float y, float width, float height)
    {
        RectTransform rect = NewRect(parent, name);
        Position(rect, x, y, width, height);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = uiFont;
        label.text = value;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private Button ButtonAt(Transform parent, string name, string title,
        float fontSize, float x, float y, float width, float height,
        Color color, Action click)
    {
        RectTransform rect = NewRect(parent, name);
        Position(rect, x, y, width, height);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = rect.gameObject.AddComponent<Button>();
        RememberButton(button);
        if (click != null) button.onClick.AddListener(() => click());
        Label(rect, "Label", title, fontSize, White, 0, 0, width, height)
            .raycastTarget = false;
        return button;
    }

    private TMP_InputField Input(Transform parent, string name, string value,
        float x, float y, float width, float height, string placeholder)
    {
        RectTransform rect = NewRect(parent, name);
        Position(rect, x, y, width, height);
        rect.gameObject.AddComponent<Image>().color = new Color(.01f, .06f, .13f, 1f);
        TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
        TextMeshProUGUI text = Label(rect, "Text", value, 31, White,
            16, 5, width - 32, height - 10);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        input.textComponent = text;
        input.textViewport = rect;
        if (!string.IsNullOrEmpty(placeholder))
        {
            TextMeshProUGUI hint = Label(rect, "Placeholder", placeholder,
                29, new Color(.55f, .66f, .75f, 1f),
                16, 5, width - 32, height - 10);
            hint.alignment = TextAlignmentOptions.MidlineLeft;
            input.placeholder = hint;
        }
        input.text = value;
        return input;
    }
}
