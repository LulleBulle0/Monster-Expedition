using System.Collections;
using UnityEngine;

public class Log : MonoBehaviour
{
    public Vector2Int gridPosition;

    private bool isRegistered;
    private bool isRolling;

    void Start()
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError("GridManager.Instance is null.");
            return;
        }

        transform.position = GridManager.Instance.GridToWorld(gridPosition);

        isRegistered = GridManager.Instance.RegisterOccupant(gridPosition);

        if (!isRegistered)
        {
            Debug.LogWarning($"Failed to register log at {gridPosition}");
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
        if (GridManager.Instance == null || isRolling)
            return false;

        Vector2Int targetPos = gridPosition + direction;

        if (!GridManager.Instance.IsInsideGrid(targetPos))
            return false;

        // WATER / HOLE → create bridge
        if (GridManager.Instance.IsStaticallyBlocked(targetPos)
            && !GridManager.Instance.IsOccupied(targetPos))
        {
            CreateBridge(targetPos, direction);
            return true;
        }

        // NORMAL MOVE
        if (!GridManager.Instance.CanMoveTo(targetPos))
            return false;

        if (!GridManager.Instance.MoveOccupant(gridPosition, targetPos))
            return false;

        gridPosition = targetPos;

        StartCoroutine(Roll(direction));

        return true;
    }

    void CreateBridge(Vector2Int targetPos, Vector2Int direction)
    {
        Debug.Log("Log creating bridge at " + targetPos);

        GridManager.Instance.MakeTileWalkable(targetPos);

        GridManager.Instance.MoveOccupant(gridPosition, targetPos);

        gridPosition = targetPos;

        StartCoroutine(Roll(direction));
    }

    IEnumerator Roll(Vector2Int direction)
    {
        isRolling = true;

        Vector3 moveDir = new Vector3(direction.x, 0, direction.y);

        Vector3 pivot =
            transform.position +
            (moveDir + Vector3.down) * 0.5f;

        Vector3 axis = Vector3.Cross(Vector3.up, moveDir);

        float duration = 0.25f;
        float angle = 0f;
        float speed = 90f / duration;

        while (angle < 90f)
        {
            float step = speed * Time.deltaTime;

            transform.RotateAround(pivot, axis, step);

            angle += step;

            yield return null;
        }

        transform.position = GridManager.Instance.GridToWorld(gridPosition);

        isRolling = false;
    }
}