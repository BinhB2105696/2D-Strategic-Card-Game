using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed partial class DeckBuilderAppController
{
    [Serializable] private sealed class ButtonReference
    {
        public string key;
        public Button button;
    }

    [Header("Profile appearance")]
    [SerializeField] private Sprite[] avatarSprites;
    [SerializeField] private GameObject duelOverlayPrefab;
    [SerializeField, HideInInspector] private List<ButtonReference> buttonReferences = new List<ButtonReference>();
    [SerializeField, HideInInspector] private GameObject profilePage, settingsPage;
    [SerializeField, HideInInspector] private Image homeAvatar, profileAvatar;
    [SerializeField, HideInInspector] private TMP_Text homePlayerName, activeDeckText, profileIdText, statsText, historyText;
    [SerializeField, HideInInspector] private TMP_InputField profileNameInput;
    [SerializeField, HideInInspector] private Slider masterSlider, musicSlider, effectsSlider;
    [SerializeField, HideInInspector] private GameObject detailPanel, filterPanel, messagePanel, confirmPanel;
    [SerializeField, HideInInspector] private GameObject monsterFilterPage, spellFilterPage;
    [SerializeField, HideInInspector] private RectTransform detailArt;
    [SerializeField, HideInInspector] private TMP_Text detailName, detailType, detailEffect, confirmText;
    [SerializeField, HideInInspector] private Button detailFavorite, detailChange;
    [SerializeField, HideInInspector] private List<AppFilterOption> filterOptions = new List<AppFilterOption>();
    private CardData detailCard;
    private bool detailFromCatalog;
    private Action pendingConfirmation;
    private int draftAvatarIndex;

    public bool HasAuthoredUi => canvas != null;

#if UNITY_EDITOR
    // One-time authoring only. Subsequent imports never rebuild saved layouts.
    public void PrepareEditableUi()
    {
        if (Application.isPlaying || HasAuthoredUi) return;
        InitializeCatalog();
        profile = LocalDeckStore.CreatePreviewProfile(byId);
        buttonReferences.Clear();
        filterOptions.Clear();
        BuildUi();
        if (profile.decks.Count > 0)
        {
            editingDeck = Copy(profile.decks[0]);
            RebuildDeck();
            RebuildCatalog();
        }
        nameInput.text = "Player";
        profileNameInput.text = "Player";
        ShowPage(homePage);
    }

    public void PreviewPage(string page)
    {
        if (Application.isPlaying || !HasAuthoredUi) return;
        CloseModal();
        entryPage.SetActive(page == "GuestEntry");
        homePage.SetActive(page == "MainMenu");
        builderPage.SetActive(page == "DeckBuilder");
        profilePage.SetActive(page == "Profile");
        settingsPage.SetActive(page == "Settings");
        if (page == "CardDetail" || page == "Filter" || page == "Message")
        {
            builderPage.SetActive(true);
            OpenModalPanel(page == "CardDetail" ? detailPanel : page == "Filter" ? filterPanel : messagePanel);
            if (page == "Filter")
            {
                monsterFilterPage.SetActive(filterTab == 0);
                spellFilterPage.SetActive(filterTab == 1);
                RefreshFilterTabVisuals();
            }
        }
    }

    public void VerifyPreparedUi()
    {
        InitializeCatalog();
        profile = LocalDeckStore.CreatePreviewProfile(byId);
        BindUi();
        OpenBuilder();
        if (deckContent.childCount < 30 || catalogContent.childCount == 0)
            throw new InvalidOperationException("Deck/catalog preview content is incomplete.");
        foreach (CardData card in byId.Values) { ShowCardDetail(card, true); break; }
        ShowFilter();
        filterTab = 1; ShowFilter();
        OpenProfile(); OpenSettings(); ShowPage(homePage);
        if (duelOverlayPrefab == null || avatarSprites == null || avatarSprites.Length == 0)
            throw new InvalidOperationException("Profile avatars or result prefab are missing.");
    }
#endif

    private void RememberButton(Button button)
    {
        string key = button.name;
        Transform parent = button.transform.parent;
        while (parent != null && parent != portraitFrame)
        {
            key = parent.name + "/" + key;
            parent = parent.parent;
        }
        buttonReferences.Add(new ButtonReference { key = key, button = button });
    }

    private void Bind(string key, Action action)
    {
        ButtonReference reference = buttonReferences.Find(item => item.key == key);
        if (reference == null || reference.button == null)
            throw new InvalidOperationException("Missing authored app button: " + key);
        reference.button.onClick.RemoveAllListeners();
        reference.button.onClick.AddListener(() => action());
    }

    private void BindUi()
    {
        Bind("GuestEntry/ContinueButton", () =>
        {
            profile.guestName = CleanName(nameInput.text);
            ShowPage(homePage);
            PersistProfile();
        });
        Bind("MainMenu/DeckBuilding", OpenBuilder);
        Bind("MainMenu/Pve", StartPve);
        Bind("MainMenu/Pvp", () => SetMessage("Online PvP is coming later."));
        Bind("MainMenu/Profile", OpenProfile);
        Bind("MainMenu/ProfileShortcut", OpenProfile);
        Bind("MainMenu/SettingsShortcut", OpenSettings);
        Bind("DeckBuilder/PreviousDeck", () => SelectDeck(-1));
        Bind("DeckBuilder/NextDeck", () => SelectDeck(1));
        Bind("DeckBuilder/FavoriteInfo", () => SelectQuickCategory(4));
        Bind("DeckBuilder/Back", LeaveBuilder);
        Bind("DeckBuilder/Home", LeaveBuilder);
        Bind("DeckBuilder/Save", SaveDeck);
        Bind("DeckBuilder/Copy", CopyDeck);
        const string toolbarKey = "DeckBuilder/CatalogDrawer/FilterToolbar/";
        string[] names = { "ALL", "MONSTER", "SPELL", "TRAP", "* FAV" };
        for (int i = 0; i < names.Length; i++)
        {
            int choice = i;
            Bind(toolbarKey + "Category_" + names[i], () => SelectQuickCategory(choice));
        }
        Bind(toolbarKey + "ClearSearch", () => searchInput.text = "");
        Bind(toolbarKey + "AdvancedFilter", ShowFilter);
        Bind(toolbarKey + "SortButton", CycleSort);
        Bind(toolbarKey + "SortDirection", () => { sortDescending = !sortDescending; RebuildCatalog(); });
        searchInput.onValueChanged.RemoveAllListeners();
        searchInput.onValueChanged.AddListener(_ => RebuildCatalog());
        catalogPanel.GetComponentInChildren<DeckCatalogDragHandle>(true).Initialize(this);
        catalogHeight = catalogPanel.sizeDelta.y;
        Bind("ModalLayer/CardPanel/Favorite", ToggleDetailFavorite);
        Bind("ModalLayer/CardPanel/ChangeDeck", ChangeDetailDeck);
        Bind("ModalLayer/CardPanel/Close", CloseModal);
        Bind("ModalLayer/FilterPanel/MonsterTab", () => { filterTab = 0; ShowFilter(); });
        Bind("ModalLayer/FilterPanel/SpellTrapTab", () => { filterTab = 1; ShowFilter(); });
        Bind("ModalLayer/FilterPanel/Clear", () =>
        {
            ranks.Clear(); attributes.Clear(); monsterTypes.Clear(); spellTypes.Clear(); trapTypes.Clear();
            detailedScope = -1; quickCategory = -1; favoritesOnly = false;
            RebuildCatalog(); ShowFilter();
        });
        Bind("ModalLayer/FilterPanel/Apply", ApplyDetailedFilter);
        Bind("ModalLayer/FilterPanel/Close", ApplyDetailedFilter);
        Bind("ModalLayer/MessagePanel/Okay", CloseModal);
        Bind("ModalLayer/ConfirmPanel/Cancel", CloseModal);
        Bind("ModalLayer/ConfirmPanel/Discard", () =>
        {
            Action next = pendingConfirmation;
            CloseModal();
            next?.Invoke();
        });
        foreach (AppFilterOption option in filterOptions)
        {
            AppFilterOption captured = option;
            option.button.onClick.RemoveAllListeners();
            option.button.onClick.AddListener(() =>
            {
                HashSet<string> set = FilterSet(captured.group);
                if (!set.Add(captured.value)) set.Remove(captured.value);
                captured.Refresh(set.Contains(captured.value));
            });
        }
        Bind("Profile/Back", () => ShowPage(homePage));
        Bind("Profile/SaveProfile", SaveProfileDetails);
        Bind("Profile/PreviousAvatar", () => CycleAvatar(-1));
        Bind("Profile/NextAvatar", () => CycleAvatar(1));
        Bind("Settings/Back", SaveSettings);
        Bind("Settings/SaveSettings", SaveSettings);
        nameInput.characterLimit = profileNameInput.characterLimit = 24;
        nameInput.richText = profileNameInput.richText = false;
        homePlayerName.richText = guestNameText.richText = false;
        Clear(deckContent);
        Clear(catalogContent);
    }

    private void BuildAdditionalPages()
    {
        RectTransform parent = homePage.GetComponent<RectTransform>();
        Button shortcut = ButtonAt(parent, "ProfileShortcut", "", 30, 30, 35, 680, 155, Blue, OpenProfile);
        homeAvatar = Panel(shortcut.transform, "Avatar", Color.white, 12, 8, 130, 140);
        homeAvatar.preserveAspect = true;
        homePlayerName = Label(shortcut.transform, "Name", "Player", 37, White, 160, 25, 495, 90);
        homePlayerName.raycastTarget = false;
        ButtonAt(parent, "SettingsShortcut", "SETTINGS", 27, 765, 35, 280, 155, Blue, OpenSettings);
        activeDeckText = Label(parent, "SelectedDeck", "Deck: Starter Balance", 31, Cyan, 70, 490, 940, 65);
        profilePage = CreatePage("Profile");
        parent = profilePage.GetComponent<RectTransform>();
        Label(parent, "Title", "PLAYER PROFILE", 57, Gold, 50, 70, 980, 100);
        profileAvatar = Panel(parent, "Avatar", Color.white, 390, 220, 300, 300);
        profileAvatar.preserveAspect = true;
        ButtonAt(parent, "PreviousAvatar", "<", 50, 220, 330, 120, 95, Blue, null);
        ButtonAt(parent, "NextAvatar", ">", 50, 740, 330, 120, 95, Blue, null);
        profileNameInput = Input(parent, "PlayerName", "Player", 100, 560, 880, 95, "Player name");
        profileIdText = Label(parent, "LocalIdentity", "Local player", 24, Cyan, 65, 675, 950, 70);
        statsText = Label(parent, "Statistics", "Wins 0 / Losses 0 / Win rate --", 32, White, 60, 770, 960, 100);
        Label(parent, "HistoryTitle", "RECENT PvE MATCHES", 38, Gold, 65, 910, 950, 70);
        RectTransform historyViewport = Stretch(parent, "HistoryViewport", 65, 260, 65, 990);
        historyViewport.gameObject.AddComponent<Image>().color = Navy;
        historyViewport.gameObject.AddComponent<RectMask2D>();
        RectTransform historyContent = NewRect(historyViewport, "Content");
        historyContent.anchorMin = new Vector2(0, 1); historyContent.anchorMax = Vector2.one;
        historyContent.pivot = new Vector2(.5f, 1); historyContent.sizeDelta = Vector2.zero;
        historyText = Label(historyContent, "History", "No completed matches yet.", 30, White, 10, 10, 920, 600);
        historyText.alignment = TextAlignmentOptions.TopLeft;
        historyText.enableWordWrapping = true;
        historyText.overflowMode = TextOverflowModes.Overflow;
        ScrollRect scroll = historyViewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = historyViewport; scroll.content = historyContent; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        ButtonAt(parent, "Back", "HOME", 34, 50, 1740, 380, 110, Blue, null);
        ButtonAt(parent, "SaveProfile", "SAVE PROFILE", 34, 460, 1740, 570, 110, Blue, null);

        settingsPage = CreatePage("Settings");
        parent = settingsPage.GetComponent<RectTransform>();
        Label(parent, "Title", "SETTINGS", 60, Gold, 60, 150, 960, 120);
        masterSlider = CreateVolumeSlider(parent, "MasterVolume", "MASTER VOLUME", 430);
        musicSlider = CreateVolumeSlider(parent, "MusicVolume", "MUSIC", 690);
        effectsSlider = CreateVolumeSlider(parent, "EffectsVolume", "SOUND EFFECTS", 950);
        TMP_Text hint = Label(parent, "AudioHint", "Music and sound levels are saved for audio added to the game.", 30, Cyan, 90, 1240, 900, 160);
        hint.enableWordWrapping = true;
        ButtonAt(parent, "Back", "HOME", 34, 50, 1740, 380, 110, Blue, null);
        ButtonAt(parent, "SaveSettings", "SAVE SETTINGS", 34, 460, 1740, 570, 110, Blue, null);
    }

    private Slider CreateVolumeSlider(Transform parent, string name, string title, float y)
    {
        Label(parent, name + "Title", title, 39, White, 90, y, 900, 70);
        RectTransform rect = Panel(parent, name, Blue, 100, y + 90, 880, 70).rectTransform;
        Slider slider = rect.gameObject.AddComponent<Slider>();
        RectTransform handleArea = Stretch(rect, "HandleArea", 30, 0, 30, 0);
        Image handle = Panel(handleArea, "Handle", Cyan, 0, 0, 60, 70);
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
        return slider;
    }

    private void BuildModalTemplates()
    {
        Panel(modalLayer.transform, "Dim", new Color(0, 0, 0, .82f), 0, 0, 1080, 1920);
        RectTransform panel = Panel(modalLayer.transform, "CardPanel", Navy, 55, 300, 970, 1270).rectTransform;
        detailPanel = panel.gameObject;
        detailArt = NewRect(panel, "Artwork"); Position(detailArt, 360, 85, 250, 350);
        detailName = Label(panel, "Name", "Card name", 47, Gold, 35, 475, 900, 68);
        detailType = Label(panel, "Type", "Rank / Attribute / Type\nHP 1000   ATK 600   DEF 900", 31, Cyan, 38, 560, 890, 120);
        RectTransform effectViewport = Panel(panel, "EffectViewport", Navy, 38, 700, 890, 330).rectTransform;
        effectViewport.gameObject.AddComponent<RectMask2D>();
        detailEffect = Label(effectViewport, "Effect", "Card effect description", 32, White, 0, 0, 890, 330);
        detailEffect.alignment = TextAlignmentOptions.TopLeft;
        detailEffect.enableWordWrapping = true;
        detailEffect.overflowMode = TextOverflowModes.Overflow;
        ContentSizeFitter fit = detailEffect.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect effectScroll = effectViewport.gameObject.AddComponent<ScrollRect>();
        effectScroll.viewport = effectViewport; effectScroll.content = detailEffect.rectTransform;
        effectScroll.horizontal = false; effectScroll.movementType = ScrollRect.MovementType.Clamped;
        detailFavorite = ButtonAt(panel, "Favorite", "+*", 55, 40, 1110, 120, 100, Blue, null);
        detailChange = ButtonAt(panel, "ChangeDeck", "+ ADD", 37, 180, 1110, 500, 100, Blue, null);
        ButtonAt(panel, "Close", "CLOSE", 32, 700, 1110, 235, 100, Blue, null);
        panel = Panel(modalLayer.transform, "FilterPanel", Navy, 45, 220, 990, 1480).rectTransform;
        filterPanel = panel.gameObject;
        Label(panel, "Title", "DETAILED FILTER", 45, Gold, 30, 15, 930, 75);
        ButtonAt(panel, "MonsterTab", "MONSTERS", 33, 25, 94, 455, 66, Blue, null);
        ButtonAt(panel, "SpellTrapTab", "SPELLS / TRAPS", 33, 510, 94, 455, 66, Blue, null);
        monsterFilterPage = CreateFilterList(panel, "MonsterFilters", true);
        spellFilterPage = CreateFilterList(panel, "SpellTrapFilters", false);
        spellFilterPage.SetActive(false);
        ButtonAt(panel, "Clear", "CLEAR", 32, 20, 1340, 280, 100, Blue, null);
        ButtonAt(panel, "Apply", "APPLY", 35, 320, 1340, 370, 100, Blue, null);
        ButtonAt(panel, "Close", "CLOSE", 32, 710, 1340, 260, 100, Blue, null);
        panel = Panel(modalLayer.transform, "MessagePanel", Navy, 70, 685, 940, 520).rectTransform;
        messagePanel = panel.gameObject;
        messageText = Label(panel, "MessageText", "Message preview", 34, White, 45, 50, 850, 280);
        messageText.enableWordWrapping = true;
        ButtonAt(panel, "Okay", "OK", 37, 270, 360, 400, 100, Blue, null);
        panel = Panel(modalLayer.transform, "ConfirmPanel", Navy, 70, 685, 940, 520).rectTransform;
        confirmPanel = panel.gameObject;
        confirmText = Label(panel, "Prompt", "Discard unsaved deck changes?", 34, White, 45, 50, 850, 270);
        confirmText.enableWordWrapping = true;
        ButtonAt(panel, "Cancel", "KEEP EDITING", 30, 30, 360, 420, 100, Blue, null);
        ButtonAt(panel, "Discard", "DISCARD", 32, 490, 360, 420, 100, Blue, null);
    }

    private void RefreshFilterTabVisuals()
    {
        RefreshFilterTab("MonsterTab", filterTab == 0);
        RefreshFilterTab("SpellTrapTab", filterTab == 1);

        // Follow the authored viewport without rebuilding or resizing the saved UI.
        // Keep the frame outside its RectMask2D, and never intercept list input.
        RectTransform viewport = (filterTab == 0 ? monsterFilterPage : spellFilterPage)
            .GetComponent<RectTransform>();
        RectTransform frame = filterPanel.transform.Find("ActiveFilterFrame") as RectTransform;
        if (frame == null) frame = NewRect(filterPanel.transform, "ActiveFilterFrame");
        frame.anchorMin = viewport.anchorMin;
        frame.anchorMax = viewport.anchorMax;
        frame.pivot = viewport.pivot;
        frame.localScale = viewport.localScale;
        frame.localRotation = viewport.localRotation;
        frame.sizeDelta = viewport.sizeDelta + new Vector2(10, 20);
        frame.anchoredPosition = viewport.anchoredPosition +
            new Vector2(-5 + 10 * viewport.pivot.x, -5 + 20 * viewport.pivot.y);
        frame.SetAsLastSibling();
        FilterFrameEdge(frame, "Top", new Vector2(0, 1), Vector2.one, new Vector2(0, -3), Vector2.zero);
        FilterFrameEdge(frame, "Bottom", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 3));
        FilterFrameEdge(frame, "Left", Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(3, 0));
        FilterFrameEdge(frame, "Right", new Vector2(1, 0), Vector2.one, new Vector2(-3, 0), Vector2.zero);
    }

    private void RefreshFilterTab(string name, bool selected)
    {
        Button button = buttonReferences.Find(item => item.key == "ModalLayer/FilterPanel/" + name)?.button;
        if (button == null) return;
        button.GetComponent<Image>().color = selected ? Gold : Blue;
        button.GetComponentInChildren<TMP_Text>(true).color = selected ? Navy : White;
    }

    private static void FilterFrameEdge(RectTransform frame, string name, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform edge = frame.Find(name) as RectTransform;
        if (edge == null) edge = NewRect(frame, name);
        edge.anchorMin = min;
        edge.anchorMax = max;
        edge.offsetMin = offsetMin;
        edge.offsetMax = offsetMax;
        Image image = edge.GetComponent<Image>();
        if (image == null) image = edge.gameObject.AddComponent<Image>();
        image.color = Gold;
        image.raycastTarget = false;
    }

    private GameObject CreateFilterList(Transform parent, string name, bool monsters)
    {
        RectTransform viewport = Panel(parent, name, Navy, 25, 175, 940, 1130).rectTransform;
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = NewRect(viewport, "Content");
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        int y = 0;
        if (monsters)
        {
            BuildFilterGroup(content, "MONSTER RANK", typeof(MonsterRank), 0, ref y);
            BuildFilterGroup(content, "ATTRIBUTE", typeof(MonsterAttribute), 1, ref y);
            BuildFilterGroup(content, "MONSTER TYPE", typeof(MonsterType), 2, ref y);
        }
        else
        {
            BuildFilterGroup(content, "SPELL TYPE", typeof(SpellType), 3, ref y);
            BuildFilterGroup(content, "TRAP TYPE", typeof(TrapType), 4, ref y);
        }
        content.sizeDelta = new Vector2(0, y + 20);
        return viewport.gameObject;
    }

    private void BuildFilterGroup(Transform parent, string title, Type type, int group, ref int y)
    {
        Label(parent, title, title, 34, Gold, 20, y, 900, 52);
        y += 55;
        string[] options = Enum.GetNames(type);
        for (int i = 0; i < options.Length; i++)
        {
            Button button = ButtonAt(parent, title + "_" + options[i], "[ ] " + options[i], 28,
                20 + i % 2 * 465, y + i / 2 * 66, 445, 58, Navy, null);
            AppFilterOption option = button.gameObject.AddComponent<AppFilterOption>();
            option.group = group; option.value = options[i]; option.button = button;
            option.label = button.GetComponentInChildren<TMP_Text>();
            filterOptions.Add(option);
        }
        y += (options.Length + 1) / 2 * 66 + 20;
    }

    private void RefreshHome()
    {
        guestNameText.text = "Welcome, " + profile.guestName;
        homePlayerName.text = profile.guestName;
        homeAvatar.sprite = CurrentAvatar();
        activeDeckText.text = profile.decks.Count > 0 ? "Deck: " + profile.decks[profile.selectedDeck].name : "No valid deck";
    }

    private Sprite CurrentAvatar() => avatarSprites == null || avatarSprites.Length == 0 ? null :
        avatarSprites[Mathf.Clamp(profile.avatarIndex, 0, avatarSprites.Length - 1)];

    private void OpenProfile()
    {
        profileNameInput.SetTextWithoutNotify(profile.guestName);
        draftAvatarIndex = profile.avatarIndex;
        profileAvatar.sprite = CurrentAvatar();
        profileIdText.text = "Local ID: " + profile.localPlayerId;
        int total = profile.wins + profile.losses;
        statsText.text = "Wins " + profile.wins + " / Losses " + profile.losses +
            "\nWin rate " + (total == 0 ? "--" : (100f * profile.wins / total).ToString("0.0") + "%") +
            " (manual PvE; Auto is practice)";
        var lines = new List<string>();
        foreach (LocalMatchSummary match in profile.recentMatches)
            lines.Add(match.finishedUtc + "\n" + match.outcome + " / " + match.deckName +
                " / Turns " + match.turns + (match.usedAuto ? " / Auto practice" : ""));
        historyText.text = lines.Count == 0 ? "No matches yet." : string.Join("\n\n", lines);
        float height = Mathf.Max(600, historyText.GetPreferredValues(historyText.text, historyText.rectTransform.rect.width, 0).y + 40);
        historyText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        ((RectTransform)historyText.transform.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        ShowPage(profilePage);
    }

    private void CycleAvatar(int direction)
    {
        if (avatarSprites == null || avatarSprites.Length == 0) return;
        draftAvatarIndex = (draftAvatarIndex + direction + avatarSprites.Length) % avatarSprites.Length;
        profileAvatar.sprite = avatarSprites[draftAvatarIndex];
    }

    private void SaveProfileDetails()
    {
        profile.guestName = CleanName(profileNameInput.text);
        profile.avatarIndex = draftAvatarIndex;
        if (PersistProfile()) { ShowPage(homePage); SetMessage("Profile saved on this device."); }
    }

    private static string CleanName(string value) => string.IsNullOrWhiteSpace(value) ? "Player" :
        value.Trim().Substring(0, Mathf.Min(24, value.Trim().Length));

    private bool PersistProfile()
    {
        if (saveRequiresExplicitAction)
        {
            SetMessage("The existing save needs review. Save a valid deck explicitly before saving profile/settings or entering PvE.");
            return false;
        }
        if (AppProfileServices.Profiles.Save(profile, byId, out string error)) return true;
        SetMessage(error);
        return false;
    }

    private void OpenSettings()
    {
        masterSlider.SetValueWithoutNotify(profile.settings.masterVolume);
        musicSlider.SetValueWithoutNotify(profile.settings.musicVolume);
        effectsSlider.SetValueWithoutNotify(profile.settings.effectsVolume);
        ShowPage(settingsPage);
    }

    private void SaveSettings()
    {
        profile.settings.masterVolume = masterSlider.value;
        profile.settings.musicVolume = musicSlider.value;
        profile.settings.effectsVolume = effectsSlider.value;
        if (!PersistProfile()) return;
        AppAudioSettings.Apply(profile.settings);
        ShowPage(homePage);
    }

    private void StartPve()
    {
        if (profile.decks.Count == 0) { SetMessage("Save a valid deck first."); return; }
        SavedDeck deck = profile.decks[profile.selectedDeck];
        if (!LocalDeckStore.Validate(deck, byId, GameConfig.CreateDefault(), out string error))
        { SetMessage(error); return; }
        if (!PersistProfile()) return;
        if (duelOverlayPrefab == null) { SetMessage("PvE view is missing. Prepare the editable app UI in Edit Mode."); return; }
        AppMatchLaunch.Prepare(profile, deck, byId, CurrentAvatar(), duelOverlayPrefab);
        SceneManager.LoadScene("SampleScene");
    }

    private bool HasUnsavedDeck() => editingDeck != null && profile.decks.Count > 0 &&
        (editingDeck.name != profile.decks[profile.selectedDeck].name ||
         string.Join("|", editingDeck.cardIds) != string.Join("|", profile.decks[profile.selectedDeck].cardIds));

    private void ConfirmDiscard(Action next)
    {
        OpenModalPanel(confirmPanel);
        pendingConfirmation = next;
    }

    private void LeaveBuilder()
    {
        if (HasUnsavedDeck()) ConfirmDiscard(() => { editingDeck = null; ShowPage(homePage); });
        else ShowPage(homePage);
    }
}
