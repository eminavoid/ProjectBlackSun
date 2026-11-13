using UnityEngine;

public class LoseEvent : IGlobalEvent
{
    public string message = "";
    public int turnsSurvived;
    public string title;
    public string detail;

    public LoseEvent(string message, int turnsSurvived, string title = null, string detail = null)
    {
        this.message = message;
        this.turnsSurvived = turnsSurvived;
        this.title = title;
        this.detail = detail;
    }
}

public class VictoryEvent : IGlobalEvent
{
    public string title;
    public string message;
    public string detail;

    public VictoryEvent(string title, string message, string detail)
    {
        this.title = title;
        this.message = message;
        this.detail = detail;
    }
}
