using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Fallback bounds when no land tiles are detected")]
    public int width = 10;
    public int height = 10;

    [Header("Manual island setup")]
    [SerializeField] private bool buildWalkableTilesFromScene = true;
    [SerializeField] private Transform landTilesRoot;
    [SerializeField] private bool buildBlockedTilesFromScene = true;
    [SerializeField] private Transform rockBlockersRoot;
    [SerializeField] private int boundsPadding = 2;

    [Header("Optional manual overrides")]
    [SerializeField] private List<Vector2Int> blockedTilesFromInspector = new List<Vector2Int>();
    [SerializeField] private List<Vector2Int> allowedTilesFromInspector = new List<Vector2Int>();

    private HashSet<Vector2Int> baseGroundTiles = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> bridgeTiles = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> rockBlockedTiles = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> occupiedTiles = new HashSet<Vector2Int>();
    private Dictionary<Vector2Int, Log> logsByTile = new Dictionary<Vector2Int, Log>();

    private int minGridX;
    private int maxGridX;
    private int minGridY;
    private int maxGridY;

    public int MinGridX { get { return minGridX; } }
    public int MaxGridX { get { return maxGridX; } }
    public int MinGridY { get { return minGridY; } }
    public int MaxGridY { get { return maxGridY; } }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple GridManager instances found; destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RebuildGridData();
    }

    [ContextMenu("Rebuild Grid Data")]
    public void RebuildGridData()
    {
        occupiedTiles.Clear();
        logsByTile.Clear();
        bridgeTiles.Clear();
        BuildTileSets();
    }

    void BuildTileSets()
    {
        HashSet<Vector2Int> sourceWalkableTiles = new HashSet<Vector2Int>();
        HashSet<Vector2Int> sourceRockTiles = new HashSet<Vector2Int>(blockedTilesFromInspector);

        if (buildWalkableTilesFromScene)
        {
            CollectSceneWalkableTiles(sourceWalkableTiles);
        }

        if (buildBlockedTilesFromScene)
        {
            CollectSceneBlockedTiles(sourceRockTiles);
        }

        if (allowedTilesFromInspector != null)
        {
            for (int i = 0; i < allowedTilesFromInspector.Count; i++)
            {
                sourceWalkableTiles.Add(allowedTilesFromInspector[i]);
            }
        }

        if (sourceWalkableTiles.Count > 0)
        {
            BuildBoundsFromTileSet(sourceWalkableTiles, sourceRockTiles);
            baseGroundTiles = new HashSet<Vector2Int>(sourceWalkableTiles);
            rockBlockedTiles = new HashSet<Vector2Int>(sourceRockTiles);
            return;
        }

        int safeWidth = Mathf.Max(1, width);
        int safeHeight = Mathf.Max(1, height);
        SetBounds(0, safeWidth - 1, 0, safeHeight - 1);

        rockBlockedTiles = new HashSet<Vector2Int>(sourceRockTiles);
        baseGroundTiles = new HashSet<Vector2Int>();

        for (int x = minGridX; x <= maxGridX; x++)
        {
            for (int y = minGridY; y <= maxGridY; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (!rockBlockedTiles.Contains(pos))
                {
                    baseGroundTiles.Add(pos);
                }
            }
        }
    }

    void CollectSceneWalkableTiles(HashSet<Vector2Int> outTiles)
    {
        ManualLandTile[] sceneTiles;

        if (landTilesRoot != null)
        {
            sceneTiles = landTilesRoot.GetComponentsInChildren<ManualLandTile>(true);
        }
        else
        {
            sceneTiles = FindObjectsOfType<ManualLandTile>();
        }

        for (int i = 0; i < sceneTiles.Length; i++)
        {
            if (sceneTiles[i] == null)
            {
                continue;
            }

            outTiles.Add(sceneTiles[i].GridPosition);
        }
    }

    void CollectSceneBlockedTiles(HashSet<Vector2Int> outTiles)
    {
        ManualRockBlocker[] blockers;

        if (rockBlockersRoot != null)
        {
            blockers = rockBlockersRoot.GetComponentsInChildren<ManualRockBlocker>(true);
        }
        else
        {
            blockers = FindObjectsOfType<ManualRockBlocker>();
        }

        for (int i = 0; i < blockers.Length; i++)
        {
            if (blockers[i] == null)
            {
                continue;
            }

            outTiles.Add(blockers[i].GridPosition);
        }
    }

    void BuildBoundsFromTileSet(HashSet<Vector2Int> walkableTiles, HashSet<Vector2Int> blockedOverrides)
    {
        bool hasAnyTile = false;
        int foundMinX = 0;
        int foundMaxX = 0;
        int foundMinY = 0;
        int foundMaxY = 0;

        foreach (Vector2Int pos in walkableTiles)
        {
            if (!hasAnyTile)
            {
                foundMinX = pos.x;
                foundMaxX = pos.x;
                foundMinY = pos.y;
                foundMaxY = pos.y;
                hasAnyTile = true;
                continue;
            }

            foundMinX = Mathf.Min(foundMinX, pos.x);
            foundMaxX = Mathf.Max(foundMaxX, pos.x);
            foundMinY = Mathf.Min(foundMinY, pos.y);
            foundMaxY = Mathf.Max(foundMaxY, pos.y);
        }

        foreach (Vector2Int pos in blockedOverrides)
        {
            if (!hasAnyTile)
            {
                foundMinX = pos.x;
                foundMaxX = pos.x;
                foundMinY = pos.y;
                foundMaxY = pos.y;
                hasAnyTile = true;
                continue;
            }

            foundMinX = Mathf.Min(foundMinX, pos.x);
            foundMaxX = Mathf.Max(foundMaxX, pos.x);
            foundMinY = Mathf.Min(foundMinY, pos.y);
            foundMaxY = Mathf.Max(foundMaxY, pos.y);
        }

        if (!hasAnyTile)
        {
            int safeWidth = Mathf.Max(1, width);
            int safeHeight = Mathf.Max(1, height);
            SetBounds(0, safeWidth - 1, 0, safeHeight - 1);
            return;
        }

        int safePadding = Mathf.Max(0, boundsPadding);
        SetBounds(
            foundMinX - safePadding,
            foundMaxX + safePadding,
            foundMinY - safePadding,
            foundMaxY + safePadding);
    }

    void SetBounds(int newMinX, int newMaxX, int newMinY, int newMaxY)
    {
        minGridX = newMinX;
        maxGridX = newMaxX;
        minGridY = newMinY;
        maxGridY = newMaxY;

        width = Mathf.Max(1, maxGridX - minGridX + 1);
        height = Mathf.Max(1, maxGridY - minGridY + 1);
    }

    void EnsureBoundsContain(Vector2Int pos)
    {
        if (IsInsideGrid(pos))
        {
            return;
        }

        SetBounds(
            Mathf.Min(minGridX, pos.x),
            Mathf.Max(maxGridX, pos.x),
            Mathf.Min(minGridY, pos.y),
            Mathf.Max(maxGridY, pos.y));
    }

    bool IsWalkableSurfaceTile(Vector2Int pos)
    {
        return baseGroundTiles.Contains(pos) || bridgeTiles.Contains(pos);
    }

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return new Vector3(gridPos.x, 0f, gridPos.y);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.z));
    }

    public Vector2Int ClampToBounds(Vector2Int pos)
    {
        return new Vector2Int(
            Mathf.Clamp(pos.x, minGridX, maxGridX),
            Mathf.Clamp(pos.y, minGridY, maxGridY));
    }

    public bool IsInsideGrid(Vector2Int pos)
    {
        return pos.x >= minGridX && pos.x <= maxGridX && pos.y >= minGridY && pos.y <= maxGridY;
    }

    public bool IsGroundTile(Vector2Int pos)
    {
        if (!IsInsideGrid(pos))
        {
            return false;
        }

        if (!IsWalkableSurfaceTile(pos))
        {
            return false;
        }

        if (rockBlockedTiles.Contains(pos))
        {
            return false;
        }

        return true;
    }

    public bool IsRockBlocked(Vector2Int pos)
    {
        return IsInsideGrid(pos) && rockBlockedTiles.Contains(pos);
    }

    public bool IsBridgeableGap(Vector2Int pos)
    {
        if (!IsInsideGrid(pos))
        {
            return false;
        }

        if (IsWalkableSurfaceTile(pos))
        {
            return false;
        }

        if (rockBlockedTiles.Contains(pos))
        {
            return false;
        }

        return true;
    }

    public bool IsWalkable(Vector2Int pos)
    {
        return IsGroundTile(pos) && !occupiedTiles.Contains(pos);
    }

    public bool CanMoveTo(Vector2Int target)
    {
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

    public bool TryGetClosestGroundTile(Vector2Int from, out Vector2Int closest)
    {
        closest = from;

        bool found = false;
        int bestDistance = int.MaxValue;

        foreach (Vector2Int tile in baseGroundTiles)
        {
            if (!IsGroundTile(tile))
            {
                continue;
            }

            int dx = tile.x - from.x;
            int dy = tile.y - from.y;
            int distance = dx * dx + dy * dy;

            if (!found || distance < bestDistance)
            {
                bestDistance = distance;
                closest = tile;
                found = true;
            }
        }

        foreach (Vector2Int tile in bridgeTiles)
        {
            if (!IsGroundTile(tile))
            {
                continue;
            }

            int dx = tile.x - from.x;
            int dy = tile.y - from.y;
            int distance = dx * dx + dy * dy;

            if (!found || distance < bestDistance)
            {
                bestDistance = distance;
                closest = tile;
                found = true;
            }
        }

        return found;
    }

    public List<Vector2Int> GetGroundTilesSnapshot()
    {
        List<Vector2Int> result = new List<Vector2Int>();

        foreach (Vector2Int tile in baseGroundTiles)
        {
            if (IsGroundTile(tile))
            {
                result.Add(tile);
            }
        }

        foreach (Vector2Int tile in bridgeTiles)
        {
            if (IsGroundTile(tile) && !result.Contains(tile))
            {
                result.Add(tile);
            }
        }

        return result;
    }

    public List<Vector2Int> GetBridgeTilesSnapshot()
    {
        return new List<Vector2Int>(bridgeTiles);
    }

    public void RestoreBridgeTiles(List<Vector2Int> restoredBridgeTiles)
    {
        bridgeTiles.Clear();

        if (restoredBridgeTiles == null)
        {
            return;
        }

        for (int i = 0; i < restoredBridgeTiles.Count; i++)
        {
            Vector2Int pos = restoredBridgeTiles[i];
            EnsureBoundsContain(pos);

            if (rockBlockedTiles.Contains(pos) || baseGroundTiles.Contains(pos))
            {
                continue;
            }

            bridgeTiles.Add(pos);
        }
    }

    public void ClearOccupants()
    {
        occupiedTiles.Clear();
        logsByTile.Clear();
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

    public bool RegisterOccupant(Vector2Int pos)
    {
        if (!IsGroundTile(pos))
        {
            Debug.LogWarning("RegisterOccupant failed: " + pos + " is not a valid ground tile.");
            return false;
        }

        if (occupiedTiles.Contains(pos))
        {
            Debug.LogWarning("RegisterOccupant failed: " + pos + " is already occupied.");
            return false;
        }

        occupiedTiles.Add(pos);
        return true;
    }

    public bool UnregisterOccupant(Vector2Int pos)
    {
        logsByTile.Remove(pos);
        return occupiedTiles.Remove(pos);
    }

    public bool MoveOccupant(Vector2Int from, Vector2Int to)
    {
        if (!IsInsideGrid(from) || !occupiedTiles.Contains(from))
        {
            return false;
        }

        if (!IsGroundTile(to))
        {
            return false;
        }

        if (occupiedTiles.Contains(to))
        {
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
        if (!IsInsideGrid(pos))
        {
            return true;
        }

        return rockBlockedTiles.Contains(pos) || !IsWalkableSurfaceTile(pos);
    }

    public bool MakeTileWalkable(Vector2Int pos)
    {
        EnsureBoundsContain(pos);

        if (rockBlockedTiles.Contains(pos))
        {
            Debug.LogWarning("MakeTileWalkable failed: " + pos + " is a rock-blocked tile.");
            return false;
        }

        if (!baseGroundTiles.Contains(pos))
        {
            bridgeTiles.Add(pos);
        }

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
