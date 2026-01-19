using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    public int width = 10;
    public int height = 10;

    // Use a List so tiles can be edited in the Inspector, then copy into a HashSet for lookup performance
    [SerializeField]
    private List<Vector2Int> blockedTilesFromInspector = new List<Vector2Int>();

    private HashSet<Vector2Int> blockedTiles = new HashSet<Vector2Int>();

    void Awake()
    {
        // Singleton safety
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple GridManager instances found; destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Populate runtime hashset from inspector list
        blockedTiles = new HashSet<Vector2Int>(blockedTilesFromInspector);

        // Example runtime blocked tile (kept from original code)
        blockedTiles.Add(new Vector2Int(2, 2));
    }

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return new Vector3(gridPos.x, 0, gridPos.y);
    }

    public bool IsInsideGrid(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    public bool IsWalkable(Vector2Int pos)
    {
        if (!IsInsideGrid(pos))
            return false;

        if (blockedTiles.Contains(pos))
            return false;

        return true;
    }
}
