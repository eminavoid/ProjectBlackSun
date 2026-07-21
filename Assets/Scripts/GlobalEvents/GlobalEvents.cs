using UnityEngine;

public class LoseEvent : IGlobalEvent
{
    public string message = "";

    public LoseEvent(string message)
    {
        this.message = message;
    }
}