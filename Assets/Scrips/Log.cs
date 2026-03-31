using System.Collections;
using UnityEngine;

public class Log : MonoBehaviour
{
    public Vector2Int gridPosition;

    [SerializeField] private bool deriveGridPositionFromTransform = true;
    [SerializeField] private bool snapToNearestGroundIfInvalid = true;
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

        if (deriveGridPositionFromTransform)
        {
            gridPosition = GridManager.Instance.WorldToGrid(transform.position);
        }

        if (!GridManager.Instance.IsGroundTile(gridPosition))
        {
            Vector2Int fallback;
            if (snapToNearestGroundIfInvalid && GridManager.Instance.TryGetClosestGroundTile(gridPosition, out fallback))
            {
                Debug.LogWarning("Log start tile " + gridPosition + " is not valid. Snapping log to nearest ground tile " + fallback + ".");
                gridPosition = fallback;
            }
            else
            {
                Debug.LogError("Log start tile " + gridPosition + " is not valid and no fallback tile was found.");
                return;
            }
        }

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

        if (GridManager.Instance.IsStaticallyBlocked(targetPos) && !GridManager.Instance.IsOccupied(targetPos))
        {
            CreateBridge(targetPos, direction);
            return true;
        }

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

        GridManager.Instance.MakeTileWalkable(targetPos);
        transform.position = GridToWorldWithHeight(targetPos);
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
