using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zeke.UI;

public class DoctrinesController : MonoBehaviour
{
    [SerializeField] private PlayerStats stats;

    [Header("Windows")]

    [SerializeField] private UIWindow window;
    [SerializeField] private UIWindow doctrineSlot;
    [SerializeField] private UIWindow selectorMenu;
    [SerializeField] private UIWindow selectorSlot;

    [Space]

    [SerializeField] private UIWindow addDoctrineSlot;

    [Header("Settings")]

    [SerializeField] private int maxDoctrines = 3;
    [SerializeField] private int changeCooldown = 3;

    private readonly List<DoctrineSlot> doctrineSlots = new List<DoctrineSlot>();

    public void OnDoctrineSelected(Doctrine doctrine)
    {
        AddDoctrine(doctrine);
        selectorMenu.gameObject.SetActive(false);
    }

    public void OpenSelectorMenu()
    {
        if (doctrineSlots.Count >= maxDoctrines) return;

        selectorMenu.gameObject.SetActive(true);

        LayoutGroup layout = selectorMenu.TryGetElement<LayoutGroup>("Layout");

        List<Transform> children = new List<Transform>();

        foreach (Transform child in layout.transform)
        {
            children.Add(child);
        }

        for (int i = children.Count - 1; i >= 0; i--)
        {
            Destroy(children[i].gameObject);
        }

        for (int i = 0; i < stats.DoctrineInventory.Count; i++)
        {
            UIWindow slotWindow = Instantiate(selectorSlot, layout.transform);
            Button clickBox = slotWindow.TryGetElement<Button>("Clickbox");

            Doctrine doctrine = stats.DoctrineInventory[i];

            clickBox.onClick.AddListener(() => OnDoctrineSelected(doctrine));
        }
    }

    public void AddDoctrine(Doctrine doctrine)
    {
        for (int i = 0; i < doctrine.StatUpdates.Count; i++)
        {
            stats.ChangeStat(doctrine.StatUpdates[i].playerStat, doctrine.StatUpdates[i].changeAmount);
        }

        CreateDoctrineSlot(doctrine);
        UpdateAddDoctrineSlotVisibility();
    }

    public void RemoveDoctrine(Doctrine doctrine)
    {
        for (int i = 0; i < doctrine.StatUpdates.Count; i++)
        {
            stats.ChangeStat(doctrine.StatUpdates[i].playerStat, -doctrine.StatUpdates[i].changeAmount);
        }

        RemoveDoctrineSlot(doctrine);
        UpdateAddDoctrineSlotVisibility();
    }

    private void Awake()
    {
        GameTime.OnTurnEnded += Tick;
    }

    private void Start()
    {
        selectorMenu.gameObject.SetActive(false);
        window.gameObject.SetActive(false);
        UpdateAddDoctrineSlotVisibility();
    }

    private void Tick()
    {
        for (int i = 0; i < doctrineSlots.Count; i++)
        {
            doctrineSlots[i].changeCooldown -= 1;
        }
    }

    private void UpdateAddDoctrineSlotVisibility()
    {
        addDoctrineSlot.gameObject.SetActive(doctrineSlots.Count < maxDoctrines);
        addDoctrineSlot.transform.SetSiblingIndex(9999);
    }

    private void CreateDoctrineSlot(Doctrine doctrine)
    {
        LayoutGroup layout = window.TryGetElement<LayoutGroup>("Layout");
        UIWindow slotWindow = Instantiate(doctrineSlot, layout.transform);
        Button clickbox = slotWindow.TryGetElement<Button>("Clickbox");

        clickbox.onClick.AddListener(() => RemoveDoctrine(doctrine));

        doctrineSlots.Add(new DoctrineSlot(slotWindow, doctrine, changeCooldown));
    }

    private void RemoveDoctrineSlot(Doctrine doctrine)
    {
        for (int i = 0; i < doctrineSlots.Count; i++)
        {
            if (doctrineSlots[i].doctrine == doctrine)
            {
                doctrineSlots[i].window.DestroyWindow();
                doctrineSlots.RemoveAt(i);
                break;
            }
        }
    }

    private class DoctrineSlot
    {
        public UIWindow window;
        public Doctrine doctrine;
        public int changeCooldown;

        public DoctrineSlot(UIWindow window, Doctrine doctrine, int changeCooldown)
        {
            this.window = window;
            this.doctrine = doctrine;
            this.changeCooldown = changeCooldown;
        }
    }
}