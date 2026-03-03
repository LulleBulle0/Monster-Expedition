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
        Vector2Int targetPos = gridPosition + direction;

        if (!GridManager.Instance.IsInsideGrid(targetPos))
            return false;

        // If statically blocked → convert to floor
        if (GridManager.Instance.IsStaticallyBlocked(targetPos)
            && !GridManager.Instance.IsOccupied(targetPos))
        {
            Debug.Log("Log creating bridge at " + targetPos);

            GridManager.Instance.MakeTileWalkable(targetPos);
            GridManager.Instance.UnregisterOccupant(gridPosition);

            Destroy(gameObject);
            return true;
        }

        // If can't move there normally → fail
        if (!GridManager.Instance.CanMoveTo(targetPos))
            return false;

        if (!GridManager.Instance.MoveOccupant(gridPosition, targetPos))
            return false;

        gridPosition = targetPos;
        transform.position = GridManager.Instance.GridToWorld(gridPosition);

        return true;
    }
}
