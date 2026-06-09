using System.Collections.Generic;
using TMPro;
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
    private readonly List<PlaceSlotsData> placeSlots = new List<PlaceSlotsData>();

    //cuando el jugador saca una doctrina puede poner una libremente
    //cuando el jugador saca una doctrina no puede sacar mas durante el timer pero si poner 1 mas

    private class PlaceSlotsData
    {
        public bool canPlace;
        public int ticksRequired;
        public int ticks;

        public void StartCooldown()
        {
            canPlace = false;
            ticks = 0;
        }

        public PlaceSlotsData(int ticksRequired)
        {
            this.ticksRequired = ticksRequired;
            canPlace = true;
            ticks = ticksRequired;
        }
    }

    public void OnDoctrineSelected(Doctrine doctrine)
    {
        AddDoctrine(doctrine);
        selectorMenu.gameObject.SetActive(false);
    }

    public void OpenSelectorMenu()
    {
        if (doctrineSlots.Count >= maxDoctrines) return;
        if (!CanPlaceFreeAny()) return;

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

            slotWindow.TryGetElement<TextMeshProUGUI>("Name").text = stats.DoctrineInventory[i].Name;
            slotWindow.TryGetElement<TextMeshProUGUI>("Description").text = stats.DoctrineInventory[i].Description;

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
        StartFreeSlotTimer();

        UpdateAddDoctrineMenuState();
    }

    public void RemoveDoctrine(Doctrine doctrine)
    {
        for (int i = 0; i < doctrine.StatUpdates.Count; i++)
        {
            stats.ChangeStat(doctrine.StatUpdates[i].playerStat, -doctrine.StatUpdates[i].changeAmount);
        }

        RemoveDoctrineSlot(doctrine);
        UpdateAddDoctrineMenuState();
    }

    private void Awake()
    {
        GameTime.OnTurnEnded += Tick;

        for (int i = 0; i < maxDoctrines; i++)
        {
            placeSlots.Add(new PlaceSlotsData(changeCooldown));
        }
    }

    private void OnDestroy()
    {
        GameTime.OnTurnEnded -= Tick;
    }

    private void Start()
    {
        if (selectorMenu != null) selectorMenu.gameObject.SetActive(false);
        if (window != null) window.gameObject.SetActive(false);
        UpdateAddDoctrineMenuState();
    }

    private void Tick()
    {
        for (int i = placeSlots.Count - 1; i >= 0; i--)
        {
            placeSlots[i].ticks += 1;

            if (placeSlots[i].ticks >= placeSlots[i].ticksRequired)
            {
                placeSlots[i].canPlace = true;
            }
        }

        UpdateAddDoctrineMenuState();
    }

    private bool CanPlaceFreeAny()
    {
        for (int i = 0; i < placeSlots.Count; i++)
        {
            if (placeSlots[i].canPlace) return true;
        }

        return false;
    }

    private int GetFreeSlotsAmount()
    {
        int amount = 0;

        for (int i = 0; i < placeSlots.Count; i++)
        {
            if (placeSlots[i].canPlace)
            {
                amount += 1;
            }
        }

        return amount;
    }

    private void StartFreeSlotTimer()
    {
        for (int i = 0; i < placeSlots.Count; i++)
        {
            if (placeSlots[i].canPlace)
            {
                placeSlots[i].StartCooldown();
                break;
            }
        }

        Debug.Log("---------------------------------");

        for (int i = 0; i < placeSlots.Count; i++)
        {
            Debug.Log($"{placeSlots[i].canPlace} timer: {placeSlots[i].ticks} / {placeSlots[i].ticksRequired}");
        }
    }

    private void UpdateAddDoctrineMenuState()
    {
        if (addDoctrineSlot != null)
        {
            addDoctrineSlot.gameObject.SetActive(doctrineSlots.Count < maxDoctrines);
            addDoctrineSlot.transform.SetSiblingIndex(9999);
        }

        if (window == null) return;

        TextMeshProUGUI replacesLeftLabel = window.TryGetElement<TextMeshProUGUI>("ReplacesLeft");
        if (replacesLeftLabel != null)
        {
            replacesLeftLabel.text = $"Add Left: {GetFreeSlotsAmount()}";
        }
    }

    private void CreateDoctrineSlot(Doctrine doctrine)
    {
        LayoutGroup layout = window.TryGetElement<LayoutGroup>("Layout");
        UIWindow slotWindow = Instantiate(doctrineSlot, layout.transform);
        Button clickbox = slotWindow.TryGetElement<Button>("Clickbox");

        slotWindow.TryGetElement<TextMeshProUGUI>("Name").text = doctrine.Name;
        slotWindow.TryGetElement<TextMeshProUGUI>("Description").text = doctrine.Description;

        clickbox.onClick.AddListener(() => RemoveDoctrine(doctrine));

        doctrineSlots.Add(new DoctrineSlot(slotWindow, doctrine));
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

        public DoctrineSlot(UIWindow window, Doctrine doctrine)
        {
            this.window = window;
            this.doctrine = doctrine;
        }
    }
}