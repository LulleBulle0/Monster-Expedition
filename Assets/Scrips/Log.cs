using System.Collections;
using UnityEngine;

public class Log : MonoBehaviour
{
    public Vector2Int gridPosition;

    [SerializeField] private float moveDuration = 0.18f;
    [SerializeField] private float hopHeight = 0.2f;
    [SerializeField] private float bridgeFallDuration = 0.25f;

    private bool isRegistered;
    private Quaternion standingRotation;
    private float baseY;
    private LogState state = LogState.Standing;

    public bool IsBridge
    {
        get { return state == LogState.Bridge; }
    }

    public bool IsBusy
    {
        get { return state == LogState.Moving; }
    }

    private enum LogState
    {
        Standing,
        Moving,
        Bridge
    }

    void Start()
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError("GridManager.Instance is null.");
            return;
        }

        baseY = transform.position.y;
        standingRotation = transform.rotation;

        SnapStandingToGrid(gridPosition);

        isRegistered = GridManager.Instance.RegisterLog(this, gridPosition);
        if (!isRegistered)
        {
            Debug.LogWarning("Failed to register log at " + gridPosition);
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
        if (GridManager.Instance == null || state != LogState.Standing)
        {
            return false;
        }

        Vector2Int targetPos = gridPosition + direction;
        if (!GridManager.Instance.IsInsideGrid(targetPos))
        {
            return false;
        }

        // Pushed into water or a hole: become a bridge.
        if (GridManager.Instance.IsStaticallyBlocked(targetPos) && !GridManager.Instance.IsOccupied(targetPos))
        {
            CreateBridge(targetPos, direction);
            return true;
        }

        // Normal land movement: keep the tree as a 1-tile piece.
        if (!GridManager.Instance.CanMoveTo(targetPos))
        {
            return false;
        }

        if (!GridManager.Instance.MoveOccupant(gridPosition, targetPos))
        {
            return false;
        }

        gridPosition = targetPos;
        StartCoroutine(AnimateMoveToTile(targetPos));
        return true;
    }

    void CreateBridge(Vector2Int targetPos, Vector2Int direction)
    {
        Debug.Log("Log creating bridge at " + targetPos);

        if (isRegistered)
        {
            GridManager.Instance.UnregisterOccupant(gridPosition);
            isRegistered = false;
        }

        gridPosition = targetPos;
        StartCoroutine(AnimateBridgeFall(targetPos, direction));
    }

    IEnumerator AnimateMoveToTile(Vector2Int targetPos)
    {
        state = LogState.Moving;

        Vector3 startWorld = transform.position;
        Vector3 endWorld = GridToWorldWithHeight(targetPos);

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);

            Vector3 nextPos = Vector3.Lerp(startWorld, endWorld, t);
            nextPos.y += Mathf.Sin(t * Mathf.PI) * hopHeight;

            transform.position = nextPos;
            transform.rotation = standingRotation;

            yield return null;
        }

        SnapStandingToGrid(targetPos);
        state = LogState.Standing;
    }

    IEnumerator AnimateBridgeFall(Vector2Int targetPos, Vector2Int direction)
    {
        state = LogState.Moving;

        Vector3 moveDir = new Vector3(direction.x, 0f, direction.y);
        Vector3 pivot = transform.position + (moveDir + Vector3.down) * 0.5f;
        Vector3 axis = Vector3.Cross(Vector3.up, moveDir);

        float elapsed = 0f;
        float rotatedAngle = 0f;

        while (elapsed < bridgeFallDuration)
        {
            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;

            float step = 90f * deltaTime / bridgeFallDuration;
            if (rotatedAngle + step > 90f)
            {
                step = 90f - rotatedAngle;
            }

            transform.RotateAround(pivot, axis, step);
            rotatedAngle += step;

            yield return null;
        }

        if (!GridManager.Instance.MakeTileWalkable(targetPos))
        {
            Debug.LogWarning("Failed to turn bridge tile into a walkable tile at " + targetPos);
        }

        Vector3 finalWorldPos = GridToWorldWithHeight(targetPos);
        transform.position = finalWorldPos;
        state = LogState.Bridge;
    }

    void SnapStandingToGrid(Vector2Int targetPos)
    {
        transform.position = GridToWorldWithHeight(targetPos);
        transform.rotation = standingRotation;
    }

    Vector3 GridToWorldWithHeight(Vector2Int targetPos)
    {
        Vector3 worldPos = GridManager.Instance.GridToWorld(targetPos);
        worldPos.y = baseY;
        return worldPos;
    }
}
