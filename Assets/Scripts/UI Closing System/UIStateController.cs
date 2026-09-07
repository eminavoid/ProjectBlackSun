using System.Collections.Generic;

public class UIStateController : Singleton<UIStateController>
{
    private readonly List<UIStateRegister> readers = new List<UIStateRegister>(64);

    public static void Register(UIStateRegister stateReader)
    {
        Instance.readers.Add(stateReader);
    }

    public static void MenuOpened(UIStateRegister stateReader)
    {
        for (int i = 0; i < Instance.readers.Count; i++)
        {
            Instance.readers[i].OnUIOpened(stateReader.ID);
        }
    }
}