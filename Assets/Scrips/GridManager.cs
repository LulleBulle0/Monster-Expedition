using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    public int width = 10;
    public int height = 10;

    [SerializeField]
    private List<Vector2Int> blockedTilesFromInspector = new List<Vector2Int>();

    private HashSet<Vector2Int> blockedTiles = new HashSet<Vector2Int>();

    [SerializeField]
    private List<Vector2Int> allowedTilesFromInspector = new List<Vector2Int>();

    private HashSet<Vector2Int> allowedTiles = new HashSet<Vector2Int>();

    // Tracks all dynamic occupants that currently block movement.
    private HashSet<Vector2Int> occupiedTiles = new HashSet<Vector2Int>();

    // Lets the player instantly look up a pushable log without FindObjectsOfType.
    private Dictionary<Vector2Int, Log> logsByTile = new Dictionary<Vector2Int, Log>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple GridManager instances found; destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        blockedTiles = new HashSet<Vector2Int>(blockedTilesFromInspector);
        occupiedTiles.Clear();
        logsByTile.Clear();

        BuildAllowedTiles();
    }

    void BuildAllowedTiles()
    {
        allowedTiles.Clear();

        if (allowedTilesFromInspector != null && allowedTilesFromInspector.Count > 0)
        {
            allowedTiles = new HashSet<Vector2Int>(allowedTilesFromInspector);
            foreach (Vector2Int blockedPos in blockedTiles)
            {
                allowedTiles.Remove(blockedPos);
            }
            return;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (!blockedTiles.Contains(pos))
                {
                    allowedTiles.Add(pos);
                }
            }
        }
    }

    // New: lets a procedural generator replace the map at runtime.
    // 1 = land/walkable, 0 = water/hole/not walkable.
    public void LoadMap(int[,] map)
    {
        if (map == null)
        {
            Debug.LogError("LoadMap failed: map is null.");
            return;
        }

        width = map.GetLength(0);
        height = map.GetLength(1);

        blockedTiles.Clear();
        allowedTiles.Clear();
        occupiedTiles.Clear();
        logsByTile.Clear();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (map[x, y] == 1)
                {
                    allowedTiles.Add(pos);
                }
                else
                {
                    blockedTiles.Add(pos);
                }
            }
        }
    }

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return new Vector3(gridPos.x, 0f, gridPos.y);
    }

    public bool IsInsideGrid(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    public bool IsWalkable(Vector2Int pos)
    {
        if (!IsInsideGrid(pos))
        {
            return false;
        }

        if (blockedTiles.Contains(pos))
        {
            return false;
        }

        if (occupiedTiles.Contains(pos))
        {
            return false;
        }

        return true;
    }

    public bool CanMoveTo(Vector2Int target)
    {
        if (!IsInsideGrid(target))
        {
            return false;
        }

        if (allowedTiles != null && allowedTiles.Count > 0)
        {
            return allowedTiles.Contains(target) && IsWalkable(target);
        }

        return IsWalkable(target);
    }

    public bool IsOccupied(Vector2Int pos)
    {
        return occupiedTiles.Contains(pos);
    }

    public bool TryGetLog(Vector2Int pos, out Log log)
    {
        return logsByTile.TryGetValue(pos, out log);
    }

    public bool RegisterLog(Log log, Vector2Int pos)
    {
        if (log == null)
        {
            Debug.LogWarning("RegisterLog failed: log is null.");
            return false;
        }

        if (!RegisterOccupant(pos))
        {
            return false;
        }

        logsByTile[pos] = log;
        return true;
    }

    // Generic occupancy methods kept for the player and other future actors.
    public bool RegisterOccupant(Vector2Int pos)
    {
        if (!IsInsideGrid(pos))
        {
            Debug.Log("RegisterOccupant failed: " + pos + " is outside grid");
            return false;
        }

        if (occupiedTiles.Contains(pos))
        {
            Debug.Log("RegisterOccupant failed: " + pos + " already occupied");
            return false;
        }

        occupiedTiles.Add(pos);
        Debug.Log("RegisterOccupant: " + pos + " now occupied. occupiedTiles count=" + occupiedTiles.Count);
        return true;
    }

    public bool UnregisterOccupant(Vector2Int pos)
    {
        logsByTile.Remove(pos);

        bool removed = occupiedTiles.Remove(pos);
        Debug.Log("UnregisterOccupant: " + pos + " removed=" + removed + ". occupiedTiles count=" + occupiedTiles.Count);
        return removed;
    }

    public bool MoveOccupant(Vector2Int from, Vector2Int to)
    {
        Debug.Log("Attempting MoveOccupant from " + from + " to " + to);

        if (!IsInsideGrid(from) || !IsInsideGrid(to))
        {
            Debug.Log("Failed: outside grid");
            return false;
        }

        if (!occupiedTiles.Contains(from))
        {
            Debug.Log("Failed: 'from' tile not occupied");
            return false;
        }

        if (blockedTiles.Contains(to))
        {
            Debug.Log("Failed: destination statically blocked");
            return false;
        }

        if (occupiedTiles.Contains(to))
        {
            Debug.Log("Failed: destination already occupied");
            return false;
        }

        occupiedTiles.Remove(from);
        occupiedTiles.Add(to);

        Log movedLog;
        if (logsByTile.TryGetValue(from, out movedLog))
        {
            logsByTile.Remove(from);
            logsByTile[to] = movedLog;
        }

        return true;
    }

    public bool IsStaticallyBlocked(Vector2Int pos)
    {
        return blockedTiles.Contains(pos);
    }

    public bool MakeTileWalkable(Vector2Int pos)
    {
        if (!IsInsideGrid(pos))
        {
            Debug.Log("MakeTileWalkable failed: " + pos + " is outside grid");
            return false;
        }

        if (!blockedTiles.Contains(pos))
        {
            Debug.Log("MakeTileWalkable: " + pos + " was not blocked");
            return false;
        }

        bool removed = blockedTiles.Remove(pos);
        if (!removed)
        {
            Debug.Log("MakeTileWalkable failed to remove " + pos + " from blockedTiles");
            return false;
        }

        allowedTiles.Add(pos);
        occupiedTiles.Remove(pos);
        logsByTile.Remove(pos);

        Debug.Log("MakeTileWalkable: tile " + pos + " unblocked and now walkable.");
        return true;
    }

    public bool IsBlockedTile(Vector2Int pos)
    {
        return IsStaticallyBlocked(pos);
    }

    public void SetTileWalkable(Vector2Int pos)
    {
        MakeTileWalkable(pos);
    }

    public bool CreateBridgeAt(Vector2Int pos)
    {
        return MakeTileWalkable(pos);
    }
}
