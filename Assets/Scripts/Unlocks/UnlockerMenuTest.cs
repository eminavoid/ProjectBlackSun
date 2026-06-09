using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zeke.UI;

public class UnlockerMenuTest : MonoBehaviour
{
    [SerializeField] private Locked locked;
    [SerializeField] private UIWindow window;
    [SerializeField] private UIWindow windowPrefab;

    [Space]

    [SerializeField] private int amount = 3;
    [SerializeField] private List<RarityChance> drops;

    [Space]

    [SerializeField] private UnityEvent onOptionSelected;

    [Serializable]
    private struct RarityChance : IWeighted
    {
        public Rarity rarity;
        [Min(1)] public int weight;

        public readonly int Weight => weight;
    }

    public void GenerateUnlocks()
    {
        CreateWindows(GenerateOptions());
    }

    private void OnOptionSelected(Unlockeable unlockeable)
    {
        unlockeable.Unlock();
        ClearRoot();

        onOptionSelected?.Invoke();
    }

    private void ClearRoot()
    {
        Transform root = window.TryGetElement<LayoutGroup>("Layout Group").transform;

        foreach(Transform children in root.transform)
        {
            Destroy(children.gameObject);
        }
    }

    private List<Unlockeable> GenerateOptions()
    {
        List<Unlockeable> options = new List<Unlockeable>();

        for (int i = 0; i < amount; i++)
        {
            Rarity randomRarity = WeightedSelect.SelectElement(drops).rarity;
            List<Unlockeable> unlockeables = locked.GetRarity(randomRarity);
            Unlockeable randomUnlockeable = unlockeables[UnityEngine.Random.Range(0, unlockeables.Count)];

            options.Add(randomUnlockeable);
        }

        return options;
    }

    private void CreateWindows(List<Unlockeable> options)
    {
        Transform root = window.TryGetElement<LayoutGroup>("Layout Group").transform;

        for (int i = 0; i < options.Count; i++)
        {
            UIWindow instance = Instantiate(windowPrefab, root);

            Unlockeable unlockeable = options[i];
            instance.TryGetElement<TextMeshProUGUI>("Name").text = options[i].name;
            instance.TryGetElement<Button>("Clickbox").onClick.AddListener(() => OnOptionSelected(unlockeable));
        }
    }
}