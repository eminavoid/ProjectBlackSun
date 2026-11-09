using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zeke.UI;

public class UnlockerMenuTest : MonoBehaviour
{
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

    public static event Action<Unlockeable> RewardChosen;

    public void GenerateUnlocks()
    {
        CreateWindows(GenerateOptions());
    }

    private void Start()
    {
        window.gameObject.SetActive(false);
    }

    private void OnOptionSelected(Unlockeable unlockeable)
    {
        if (!TutorialSession.ClaimFixedUnlock(unlockeable))
        {
            unlockeable.Unlock();
        }
        else if (unlockeable.Scriptable is Doctrine doctrine)
        {
            DoctrinesController doctrines = FindAnyObjectByType<DoctrinesController>();
            if (doctrines != null && doctrines.Stats != null)
                doctrines.Stats.RememberDoctrine(doctrine);
        }

        ClearRoot();

        onOptionSelected?.Invoke();
        RewardChosen?.Invoke(unlockeable);
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
        if (TutorialSession.FixedUnlock != null)
            return new List<Unlockeable> { TutorialSession.FixedUnlock };

        List<Unlockeable> options = new List<Unlockeable>();
        if (drops == null || drops.Count == 0) return options;

        for (int i = 0; i < amount; i++)
        {
            Rarity randomRarity = WeightedSelect.SelectElement(drops).rarity;

            List<Unlockeable> unlockeables = UnlockerManager.GetList(UnlockCategory.Seed, randomRarity);
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