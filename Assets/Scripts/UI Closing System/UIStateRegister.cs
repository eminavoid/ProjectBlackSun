using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class UIStateRegister : MonoBehaviour
{
    [SerializeField] private string id;

    [SerializeField] private List<string> closedByIDS;

    [SerializeField] private UnityEvent onClose;

    public string ID => id;

    public void OnUIOpened(string ID)
    {
        if (closedByIDS.Contains(ID))
        {
            onClose?.Invoke();
            gameObject.SetActive(false);
            UIStateController.MenuOpened(this);
        }
    }

    private void OnDisable()
    {
        onClose?.Invoke();
        UIStateController.MenuOpened(this);
    }
}