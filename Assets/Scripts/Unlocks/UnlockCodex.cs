using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zeke.UI;

public class UnlockCodex : MonoBehaviour
{
    [SerializeField] private UIWindow window;

    [Space]

    [SerializeField] private Unlocked seeds;
    [SerializeField] private UIWindow seedWindowPrefab;

    [SerializeField] private Unlocked doctrines;
    [SerializeField] private UIWindow doctrineWindowPrefab;

    private Menu currentMenu = Menu.None;

    private enum Menu
    {
        None,
        Seeds,
        Doctrines
    }

    public void LoadUnlockedSeeds()
    {
        ClearLayout();
        currentMenu = Menu.Seeds;
        PopulateLayout<Seed>(seeds, seedWindowPrefab);
    }

    public void LoadUnlockedDoctrines()
    {
        ClearLayout();
        currentMenu = Menu.Doctrines;
        PopulateLayout<Doctrine>(doctrines, doctrineWindowPrefab);
    }

    public void RefreshMenu()
    {
        if (currentMenu == Menu.None) return;

        Menu menu = currentMenu;

        ClearLayout();

        switch (menu)
        {
            case Menu.Seeds:
                LoadUnlockedSeeds();
                break;

            case Menu.Doctrines:
                LoadUnlockedDoctrines();
                break;
        }
    }

    private void ClearLayout()
    {
        Transform root = window.TryGetElement<LayoutGroup>("Layout Group").transform;

        foreach (Transform children in root)
        {
            Destroy(children.gameObject);
        }

        currentMenu = Menu.None;
    }

    private void PopulateLayout<T>(Unlocked unlocked, UIWindow windowPrefab) where T : ScriptableObject
    {
        Transform root = window.TryGetElement<LayoutGroup>("Layout Group").transform;

        List<ScriptableObject> scriptables = unlocked.GetCopy();

        for (int i = 0; i < scriptables.Count; i++)
        {
            T scriptable = (T)scriptables[i];
            UIWindow instance = Instantiate(windowPrefab, root);

            instance.TryGetElement<TextMeshProUGUI>("Name").text = scriptable.name;
        }
    }
}