using UnityEngine;
using Zeke.UI;
using TMPro;

public class DoctrinePopUp : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;

    private int newDoctrines = 0;

    private UIWindow popUpInstance;

    public void OnDoctrineReceived()
    {
        newDoctrines += 1;

        if (newDoctrines == 1)
        {
            popUpInstance = PopUpManager.SpawnPopUp(rectTransform, PopUpManager.Direction.upperRight, -20f, true);
        }
        else
        {
            popUpInstance.TryGetElement<TextMeshProUGUI>("Amount").SetText(newDoctrines.ToString());
        }
    }

    public void ClearNewDoctrinesVisual()
    {
        newDoctrines = 0;
        PopUpManager.RemovePopUp(popUpInstance);
        popUpInstance = null;
    }
}