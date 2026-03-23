using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerController : MonoBehaviour
{
    public Vector2Int gridPosition;
    public float moveDuration = 0.18f;

    Animator animator;
    bool isMoving;
    bool isRegistered;

    public string walkStateName = "Walk";
    public string idleStateName = "Idle";

    int walkStateHash;
    int idleStateHash;

    void Start()
    {
        animator = GetComponent<Animator>();

        if (animator != null)
        {
            walkStateHash = Animator.StringToHash(walkStateName);
            idleStateHash = Animator.StringToHash(idleStateName);
        }

        if (GridManager.Instance == null)
        {
            Debug.LogError("GridManager.Instance is null. Make sure a GameObject in the scene has the GridManager component and it is enabled.");
            return;
        }

        if (!GridManager.Instance.IsInsideGrid(gridPosition))
        {
            Debug.LogWarning("Starting gridPosition " + gridPosition + " is outside the grid. Clamping to valid range.");
            gridPosition.x = Mathf.Clamp(gridPosition.x, 0, GridManager.Instance.width - 1);
            gridPosition.y = Mathf.Clamp(gridPosition.y, 0, GridManager.Instance.height - 1);
        }

        Vector3 pos = GridManager.Instance.GridToWorld(gridPosition);
        pos.y = transform.position.y;
        transform.position = pos;

        isRegistered = GridManager.Instance.RegisterOccupant(gridPosition);
        if (!isRegistered)
        {
            Debug.LogWarning("Failed to register player at " + gridPosition + ". Tile may already be occupied or out of bounds.");
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

    void Update()
    {
        if (isMoving)
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                TryMove(Vector2Int.up);
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
                TryMove(Vector2Int.down);
            else if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)
                TryMove(Vector2Int.left);
            else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
                TryMove(Vector2Int.right);
        }
#else
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            TryMove(Vector2Int.up);
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            TryMove(Vector2Int.down);
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            TryMove(Vector2Int.left);
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            TryMove(Vector2Int.right);
#endif
    }

    void TryMove(Vector2Int direction)
    {
        if (isMoving || GridManager.Instance == null)
        {
            return;
        }

        Vector2Int targetPos = gridPosition + direction;

        Log targetLog;
        if (GridManager.Instance.TryGetLog(targetPos, out targetLog))
        {
            if (!targetLog.TryPush(direction))
            {
                Debug.Log("Push of log at " + targetPos + " failed.");
                return;
            }

            if (!GridManager.Instance.MoveOccupant(gridPosition, targetPos))
            {
                Debug.Log("Move failed: could not move player from " + gridPosition + " to " + targetPos + " after pushing log.");
                return;
            }

            FaceDirection(direction);
            StartCoroutine(SmoothMoveToTile(targetPos));
            return;
        }

        if (GridManager.Instance.IsOccupied(targetPos))
        {
            Debug.Log("Move blocked: target " + targetPos + " is occupied by a non-pushable object.");
            return;
        }

        if (!GridManager.Instance.CanMoveTo(targetPos))
        {
            Debug.Log("Move blocked: target " + targetPos + " is not allowed by GridManager.CanMoveTo.");
            return;
        }

        if (!GridManager.Instance.MoveOccupant(gridPosition, targetPos))
        {
            Debug.Log("Move failed: could not move occupant from " + gridPosition + " to " + targetPos + ".");
            return;
        }

        FaceDirection(direction);
        StartCoroutine(SmoothMoveToTile(targetPos));
    }

    void FaceDirection(Vector2Int dir)
    {
        Vector3 worldDir = new Vector3(dir.x, 0f, dir.y);
        if (worldDir.sqrMagnitude <= 0f)
        {
            return;
        }

        Quaternion targetRot = Quaternion.LookRotation(worldDir, Vector3.up);
        transform.rotation = targetRot;
    }

    IEnumerator SmoothMoveToTile(Vector2Int targetGridPos)
    {
        isMoving = true;
        gridPosition = targetGridPos;

        if (animator != null)
        {
            animator.SetBool("IsWalking", true);
            animator.CrossFadeInFixedTime(walkStateHash, 0.05f, 0);
        }

        Vector3 startWorld = transform.position;
        Vector3 endWorld = GridManager.Instance.GridToWorld(targetGridPos);

        Quaternion startRot = transform.rotation;
        Vector3 travelDir = endWorld - startWorld;
        if (travelDir.sqrMagnitude <= 0.0001f)
        {
            travelDir = transform.forward;
        }
        Quaternion targetRot = Quaternion.LookRotation(travelDir.normalized, Vector3.up);

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            transform.position = Vector3.Lerp(startWorld, endWorld, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        transform.position = endWorld;
        transform.rotation = targetRot;

        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.CrossFadeInFixedTime(idleStateHash, 0.05f, 0);
        }

        isMoving = false;
        Debug.Log("Player moved to " + gridPosition);
    }
}
