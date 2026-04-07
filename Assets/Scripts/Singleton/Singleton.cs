using UnityEngine;

[DefaultExecutionOrder(-1)]
public abstract class Singleton<T> : MonoBehaviour where T : Component
{
    public static bool IsNull => instance == null;

    private static T instance;
    protected static T Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = FindAnyObjectByType<T>();

            if (instance != null) return instance;

            return CreateSingletonGameObject();
        }
    }

    static T CreateSingletonGameObject()
    {
        GameObject newGameObject = new GameObject(typeof(T).Name + " [Auto]");
        instance = newGameObject.AddComponent<T>();

        return instance;
    }

    protected virtual void OnInitialization() { }

    protected void Awake()
    {
        InitializeSingleton();
    }

    void InitializeSingleton()
    {
        if (!Application.isPlaying) return;

        instance = this as T;
        OnInitialization();
    }
}