using System.Collections.Generic;
using UnityEngine;

public static class WeightedSelect
{
    public static T SelectElement<T>(List<T> elements) where T : IWeighted
    {
        int maxRollWeight = CalculateMaxRollWeight(elements);
        int weightRoll = Random.Range(0, maxRollWeight);

        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i].Weight > weightRoll)
            {
                return elements[i];
            }
            else
            {
                weightRoll -= elements[i].Weight;
            }
        }

        return default;
    }

    private static int CalculateMaxRollWeight<T>(List<T> elements) where T : IWeighted
    {
        int maxRollWeight = 0;

        for (int i = 0; i < elements.Count; i++)
        {
            maxRollWeight += elements[i].Weight;
        }

        return maxRollWeight;
    }
}