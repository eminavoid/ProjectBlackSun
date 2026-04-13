using UnityEngine;

public class GlobalReferences : Singleton<GlobalReferences>
{
    [SerializeField] private Canvas screenCanvas;

    public static Canvas ScreenCanvas => Instance.screenCanvas;
}