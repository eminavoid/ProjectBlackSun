using UnityEngine;
using Zeke.UI;

public class AddElementTest : MonoBehaviour
{
    [SerializeField] private UIWindow window;

    private void Start()
    {
        UnityEngine.UI.LayoutGroup layoutGroup = window.TryGetElement<UnityEngine.UI.LayoutGroup>("Test");
        Debug.Log(layoutGroup);
    }
}