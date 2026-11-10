using Driving;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Optional preset controls, independent of menu tracking and positioning.</summary>
public sealed class DrivingScenarioUI : MonoBehaviour
{
    [SerializeField] DrivingScenarioManager scenarios;
    [SerializeField] GameObject section;
    [SerializeField] Button[] buttons;
    [SerializeField] TMP_Text scenarioLabel;
    UnityAction[] callbacks;
    void OnEnable()
    {
        if (section != null) section.SetActive(scenarios != null);
        if (scenarios == null) return;
        callbacks = new UnityAction[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
        {
            var scenario = (DrivingScenarioType)i;
            callbacks[i] = () => scenarios.ApplyScenario(scenario);
            buttons[i].onClick.AddListener(callbacks[i]);
        }
        scenarios.ScenarioChanged += Refresh;
        Refresh();
    }
    void OnDisable()
    {
        if (scenarios == null) return;
        scenarios.ScenarioChanged -= Refresh;
        if (callbacks == null) return;
        for (int i = 0; i < buttons.Length; i++) buttons[i].onClick.RemoveListener(callbacks[i]);
    }
    void Refresh()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            bool selected = i == (int)scenarios.ActiveScenario;
            buttons[i].GetComponent<Image>().color = selected ? new Color(0.15f, 0.55f, 0.7f) : new Color(0.18f, 0.22f, 0.28f);
            if (selected) scenarioLabel.text = "Scenario: " + buttons[i].GetComponentInChildren<TMP_Text>().text;
        }
    }
}
