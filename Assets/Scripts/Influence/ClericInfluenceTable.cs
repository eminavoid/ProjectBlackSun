using UnityEngine;

/// <summary>
/// Tabla GDD de diminishing returns para generación de influencia por clérigos.
/// </summary>
public static class ClericInfluenceTable
{
    // Indexed by cleric count 1..15; 16+ uses last entry.
    private static readonly Vector2Int[] Ranges =
    {
        new Vector2Int(1, 5),   // 1
        new Vector2Int(2, 9),   // 2
        new Vector2Int(3, 12),  // 3
        new Vector2Int(4, 14),  // 4
        new Vector2Int(5, 15),  // 5
        new Vector2Int(6, 15),  // 6
        new Vector2Int(7, 15),  // 7
        new Vector2Int(8, 15),  // 8
        new Vector2Int(9, 15),  // 9
        new Vector2Int(10, 15), // 10
        new Vector2Int(11, 15), // 11
        new Vector2Int(12, 15), // 12
        new Vector2Int(13, 15), // 13
        new Vector2Int(14, 15), // 14
        new Vector2Int(15, 15)  // 15+
    };

    public static Vector2Int GetRange(int clerics)
    {
        if (clerics <= 0) return Vector2Int.zero;
        int index = Mathf.Clamp(clerics, 1, Ranges.Length) - 1;
        return Ranges[index];
    }

    public static int Roll(int clerics)
    {
        Vector2Int range = GetRange(clerics);
        if (range.y <= 0) return 0;
        return Random.Range(range.x, range.y + 1);
    }
}
