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

    // Optional: inspector-driven allowed tiles. If empty, all inside-grid tiles are allowed (except blocked)
    [SerializeField]
    private List<Vector2Int> allowedTilesFromInspector = new List<Vector2Int>();

    private HashSet<Vector2Int> allowedTiles = new HashSet<Vector2Int>();

    // Tracks dynamic occupants (actors, movable logs, etc.) that temporarily block tiles
    private HashSet<Vector2Int> occupiedTiles = new HashSet<Vector2Int>();

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

        // Populate allowed tiles: if inspector list is empty, allow all inside-grid tiles except blocked
        if (allowedTilesFromInspector != null && allowedTilesFromInspector.Count > 0)
        {
            allowedTiles = new HashSet<Vector2Int>(allowedTilesFromInspector);
            // ensure blocked tiles are not allowed
            foreach (var b in blockedTiles)
                allowedTiles.Remove(b);
        }
        else
        {
            allowedTiles = new HashSet<Vector2Int>();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var pos = new Vector2Int(x, y);
                    if (!blockedTiles.Contains(pos))
                        allowedTiles.Add(pos);
                }
            }
        }
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

        // Check static blocked tiles
        if (blockedTiles.Contains(pos))
            return false;

        // Check dynamic occupancy
        if (occupiedTiles.Contains(pos))
            return false;

        return true;
    }

    // Returns true when the logical grid allows movement to the target tile.
    // Uses inspector-provided allowed tiles when present, otherwise defaults to all inside-grid tiles minus blocked tiles.
    public bool CanMoveTo(Vector2Int target)
    {
        if (!IsInsideGrid(target))
            return false;

        // If allowedTiles was configured or computed, check it
        if (allowedTiles != null && allowedTiles.Count > 0)
            return allowedTiles.Contains(target) && IsWalkable(target);

        // Fallback: rely on IsWalkable
        return IsWalkable(target);
    }

    // Dynamic occupancy APIs - used by movable actors (logs, NPCs) to mark tiles they occupy.
    // Treat movable objects as actors: register their occupied tiles and update when they move.

    // Returns true if the tile is currently occupied by a dynamic actor/object
    public bool IsOccupied(Vector2Int pos)
    {
        return occupiedTiles.Contains(pos);
    }

    // Try to register an occupant at `pos`. Returns true on success, false if already occupied or out of bounds or statically blocked.
    public bool RegisterOccupant(Vector2Int pos)
    {
        if (!IsInsideGrid(pos))
            return false;
        if (blockedTiles.Contains(pos))
            return false;
        if (occupiedTiles.Contains(pos))
            return false;
        occupiedTiles.Add(pos);
        return true;
    }

    // Unregister occupant at pos. Returns true if removed.
    public bool UnregisterOccupant(Vector2Int pos)
    {
        return occupiedTiles.Remove(pos);
    }

    // Move an occupant atomically from `from` to `to`.
    // Returns true if moved, false if `to` is occupied or out of bounds or `from` wasn't occupied or `to` is statically blocked.
    public bool MoveOccupant(Vector2Int from, Vector2Int to)
    {
        if (!IsInsideGrid(to) || !IsInsideGrid(from))
            return false;

        // Ensure there's an occupant at 'from'
        if (!occupiedTiles.Contains(from))
            return false; // nothing occupied at 'from'

        // Destination must not be statically blocked or already occupied
        if (blockedTiles.Contains(to))
            return false;
        if (occupiedTiles.Contains(to))
            return false;

        // Perform the move
        occupiedTiles.Remove(from);
        occupiedTiles.Add(to);
        return true;
    }

    // Returns true if the tile is statically blocked (from inspector or map)
    public bool IsStaticallyBlocked(Vector2Int pos)
    {
        return blockedTiles.Contains(pos);
    }

    // Make a statically blocked tile walkable at runtime (e.g. when a log creates a bridge).
    // Returns true if the tile was previously blocked and is now unblocked.
    public bool MakeTileWalkable(Vector2Int pos)
    {
        if (blockedTiles.Remove(pos))
        {
            // Keep allowedTiles in sync: add this position so CanMoveTo will allow it when appropriate
            if (allowedTiles == null)
                allowedTiles = new HashSet<Vector2Int>();
            allowedTiles.Add(pos);
            return true;
        }
        return false;
    }
}
