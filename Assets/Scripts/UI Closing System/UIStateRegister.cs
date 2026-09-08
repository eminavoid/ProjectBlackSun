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
        if (id == ID) return; //  avoid stack overflow, can sitll happen with circular dependency tho....

        if (closedByIDS.Contains(ID))
        {
            onClose?.Invoke();
            gameObject.SetActive(false);
        }
    }

    private void Awake()
    {
        UIStateController.Register(this);
    }

    private void OnDisable()
    {
        onClose?.Invoke();
    }

    private void OnEnable()
    {
        UIStateController.MenuOpened(this);
    }
}