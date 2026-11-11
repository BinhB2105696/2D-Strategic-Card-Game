using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PvpUiOption
{
    public string label;
    public Action click;
    public PvpUiOption(string label, Action click) { this.label = label; this.click = click; }
}

public sealed class PvpHudBindings
{
    public Button phase, battle, end, settings, closeSettings, surrender, accept, cancel;
    public Button[] tabs;
    public GameObject phases, settingsPanel, confirmation;
    public GameObject[] contents;
    public TMP_Text confirmationText, timer;
}
