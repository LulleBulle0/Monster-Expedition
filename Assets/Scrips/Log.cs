using UnityEngine;

public class Log : MonoBehaviour
{
    public Vector2Int gridPosition;

    void Start()
    {
        transform.position = GridManager.Instance.GridToWorld(gridPosition);
        if (!GridManager.Instance.RegisterOccupant(gridPosition))
        {
            Debug.LogWarning($"Failed to register log at {gridPosition}. Tile may already be occupied or out of bounds.");
        }
    }

    void OnDestroy()
    {
        if (GridManager.Instance != null)
            GridManager.Instance.UnregisterOccupant(gridPosition);
    }

    public bool TryPush(Vector2Int direction)
    {
        Vector2Int target = gridPosition + direction;

        // If the destination is not currently walkable (water, tree, etc.), and it's a static block (e.g. water/tree),
        // convert it into a permanent walkable tile and consume this log to create a bridge.
        if (!GridManager.Instance.IsWalkable(target) && GridManager.Instance.IsStaticallyBlocked(target) && !GridManager.Instance.IsOccupied(target))
        {
            Vector3 targetWorld = GridManager.Instance.GridToWorld(target);

            // Try to find and destroy a Tree at that world position to make the conversion seamless.
            Transform[] allTransforms = Object.FindObjectsOfType<Transform>();
            GameObject foundTree = null;
            foreach (var t in allTransforms)
            {
                if (t == null || t.gameObject == null)
                    continue;

                if (t.gameObject.name.ToLower().Contains("tree"))
                {
                    if (Vector3.Distance(t.position, targetWorld) < 0.6f)
                    {
                        foundTree = t.gameObject;
                        break;
                    }
                }

                if (Vector3.Distance(t.position, targetWorld) < 0.1f)
                {
                    foundTree = t.gameObject;
                    break;
                }
            }

            if (foundTree != null)
            {
                Debug.Log($"Destroying tree '{foundTree.name}' at {target} to make tile walkable.");
                Object.Destroy(foundTree);
            }
            else
            {
                Debug.Log($"No tree GameObject found at {targetWorld} but tile is statically blocked; making walkable anyway.");
            }

            // Remove static block so the tile becomes walkable
            bool unblocked = GridManager.Instance.MakeTileWalkable(target);
            Debug.Log($"MakeTileWalkable({target}) returned {unblocked}");

            // Unregister this log's occupancy so the tile it occupied becomes free for the player
            GridManager.Instance.UnregisterOccupant(gridPosition);

            // Optionally: create a visual bridge object here instead of leaving an empty tile.
            // For now, destroy this log to represent it becoming a static bridge.
            Debug.Log($"Log at {gridPosition} consumed to create bridge at {target}.");
            Destroy(gameObject);

            return true;
        }

        // Check logical grid allows movement
        if (!GridManager.Instance.CanMoveTo(target))
        {
            Debug.Log($"Log push blocked: target {target} is not allowed (CanMoveTo returned false). Current log at {gridPosition}.");
            return false;
        }

        // Attempt atomic move of occupancy
        if (GridManager.Instance.MoveOccupant(gridPosition, target))
        {
            var previous = gridPosition;
            gridPosition = target;
            transform.position = GridManager.Instance.GridToWorld(gridPosition);
            Debug.Log($"Log pushed from {previous} to {gridPosition}");
            return true;
        }

        Debug.Log($"Log push failed: MoveOccupant returned false from {gridPosition} to {target}.");
        return false;
    }
}
