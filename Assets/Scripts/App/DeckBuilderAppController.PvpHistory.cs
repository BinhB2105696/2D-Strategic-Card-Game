using System;
using System.Linq;
using TMPro;
using UnityEngine;

public sealed partial class DeckBuilderAppController
{
    private GameObject pvpHistoryPanel, pvpHistoryReturn;
    private TMP_Text pvpHistoryText;

    private void OpenPvpHistory(GameObject returnPanel)
    {
        pvpHistoryReturn = returnPanel;
        if (pvpHistoryPanel == null)
        {
            RectTransform panel = Panel(modalLayer.transform, "PvpHistory", Navy, 40, 230, 1000, 1450).rectTransform;
            pvpHistoryPanel = panel.gameObject;
            Label(panel, "Title", "PVP HISTORY - THIS DEVICE", 40, Gold, 30, 30, 940, 85);
            pvpHistoryText = PvpText(panel, "History", 29, 40, 140, 920, 1120);
            pvpHistoryText.enableWordWrapping = true;
            ButtonAt(panel, "Back", "BACK", 34, 200, 1300, 600, 100, Blue,
                () => OpenModalPanel(pvpHistoryReturn));
        }
        try
        {
            PvpHistoryData history = PvpLocalHistory.Load();
            int counted = history.wins + history.losses + history.draws;
            pvpHistoryText.text = "Wins " + history.wins + " / Losses " + history.losses + " / Draws " + history.draws +
                "\nWin rate: " + (counted == 0 ? "--" : (100f * history.wins / counted).ToString("0.0") + "%") +
                "\nCancelled: " + history.cancelled + " (excluded)\nLocal, host-reported results; no online ranking.\n\n" +
                string.Join("\n\n", history.recent.Take(10).Select(record =>
                    record.utc.Substring(0, Math.Min(16, record.utc.Length)).Replace('T', ' ') +
                    " UTC / " + record.outcome + " vs " + record.opponent + "\n" + record.reason + " / turns " + record.turns));
        }
        catch (Exception exception) { pvpHistoryText.text = "Could not read PvP history: " + exception.Message; }
        OpenModalPanel(pvpHistoryPanel);
    }
}
