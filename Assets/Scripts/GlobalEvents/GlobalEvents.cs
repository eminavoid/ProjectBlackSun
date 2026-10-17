using UnityEngine;

public class LoseEvent : IGlobalEvent
{
    public string message = "";
    public int turnsSurvived;

    public LoseEvent(string message, int turnsSurvived)
    {
        this.message = message;
        this.turnsSurvived = turnsSurvived;
    }
}
