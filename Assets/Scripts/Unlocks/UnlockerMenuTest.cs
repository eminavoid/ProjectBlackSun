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

    [Space]

    [SerializeField] private int amount = 3;
    [SerializeField] private UIWindow windowPrefab;

    [Space]

    [SerializeField] private UnityEvent onOptionSelected;

    private List<Unlockeable> unlockeables;

    public void GenerateUnlocks()
    {
        CreateWindows(GenerateOptions());
    }

    private void Awake()
    {
        unlockeables = locked.GetCopy();
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
        //debug, make random with rarity

        List<Unlockeable> options = new List<Unlockeable>();

        for (int i = 0; i < amount; i++)
        {
            options.Add(unlockeables[0]);
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