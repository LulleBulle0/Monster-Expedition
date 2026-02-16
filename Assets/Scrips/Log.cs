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

        // Check logical grid allows movement
        if (!GridManager.Instance.CanMoveTo(target))
            return false;

        // Attempt atomic move of occupancy
        if (GridManager.Instance.MoveOccupant(gridPosition, target))
        {
            gridPosition = target;
            transform.position = GridManager.Instance.GridToWorld(gridPosition);
            return true;
        }

        return false;
    }
}
