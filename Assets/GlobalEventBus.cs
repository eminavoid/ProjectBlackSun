using System.Collections.Generic;
using System;

public static class GlobalEventBus
{
    private readonly static Dictionary<Type, Delegate> events = new Dictionary<Type, Delegate>();

    public static void Invoke(IGlobalEvent eventData)
    {
        Type type = eventData.GetType();

        if (events.TryGetValue(type, out Delegate @delegate))
        {
            @delegate?.DynamicInvoke(eventData);
        }
    }

    public static void Subscribe<T>(Action<T> action) where T : IGlobalEvent
    {
        Type type = typeof(T);

        if (events.ContainsKey(type))
        {
            events[type] = Delegate.Combine(events[type], action);
        }
        else
        {
            events[type] = action;
        }
    }

    public static void Unsubscribe<T>(Action<T> action) where T : IGlobalEvent
    {
        Type type = typeof(T);

        if (events.ContainsKey(type))
        {
            events[type] = Delegate.Remove(events[type], action);

            if (events[type] == null)
            {
                events.Remove(type);
            }
        }
    }
}

public interface IGlobalEvent { }