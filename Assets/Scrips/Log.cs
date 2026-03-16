using UnityEngine;

public class Log : MonoBehaviour
{
    public Vector2Int gridPosition;

    private bool isRegistered;

    void Start()
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError("GridManager.Instance is null. Make sure a GameObject in the scene has the GridManager component and it's enabled.");
            return;
        }

        transform.position = GridManager.Instance.GridToWorld(gridPosition);
        isRegistered = GridManager.Instance.RegisterOccupant(gridPosition);

        if (!isRegistered)
        {
            Debug.LogWarning($"Failed to register log at {gridPosition}. Tile may already be occupied or out of bounds.");
        }
    }

    void OnDestroy()
    {
        if (GridManager.Instance != null && isRegistered)
        {
            GridManager.Instance.UnregisterOccupant(gridPosition);
            isRegistered = false;
        }
    }

    public bool TryPush(Vector2Int direction)
    {
        if (GridManager.Instance == null)
            return false;

        Vector2Int targetPos = gridPosition + direction;

        if (!GridManager.Instance.IsInsideGrid(targetPos))
            return false;

        // If statically blocked -> convert to floor
        if (GridManager.Instance.IsStaticallyBlocked(targetPos)
            && !GridManager.Instance.IsOccupied(targetPos))
        {
            Debug.Log("Log creating bridge at " + targetPos);

            GridManager.Instance.MakeTileWalkable(targetPos);

            if (isRegistered)
            {
                GridManager.Instance.UnregisterOccupant(gridPosition);
                isRegistered = false;
            }

            Destroy(gameObject);
            return true;
        }

        // If can't move there normally -> fail
        if (!GridManager.Instance.CanMoveTo(targetPos))
            return false;

        if (!GridManager.Instance.MoveOccupant(gridPosition, targetPos))
            return false;

        gridPosition = targetPos;
        transform.position = GridManager.Instance.GridToWorld(gridPosition);

        return true;
    }
}
