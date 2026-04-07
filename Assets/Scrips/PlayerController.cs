using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerController : MonoBehaviour
{
    public Vector2Int gridPosition;
    public float moveDuration = 0.18f;

    [SerializeField] private bool deriveGridPositionFromTransform = true;
    [SerializeField] private bool snapToNearestGroundIfInvalid = true;

    Animator animator;
    bool isMoving;
    bool isRegistered;
    float baseY;

    public string walkStateName = "Walk";
    public string idleStateName = "Idle";

    int walkStateHash;
    int idleStateHash;

    public bool IsBusy
    {
        get { return isMoving; }
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        baseY = transform.position.y;

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

        if (deriveGridPositionFromTransform)
        {
            gridPosition = GridManager.Instance.WorldToGrid(transform.position);
        }

        if (!GridManager.Instance.IsGroundTile(gridPosition))
        {
            Vector2Int fallback;
            if (snapToNearestGroundIfInvalid && GridManager.Instance.TryGetClosestGroundTile(gridPosition, out fallback))
            {
                Debug.LogWarning("Player start tile " + gridPosition + " is not valid. Snapping player to nearest ground tile " + fallback + ".");
                gridPosition = fallback;
            }
            else
            {
                Debug.LogError("Player start tile " + gridPosition + " is not valid and no fallback tile was found.");
                return;
            }
        }

        transform.position = GridToWorldWithHeight(gridPosition);

        isRegistered = GridManager.Instance.RegisterOccupant(gridPosition);
        if (!isRegistered)
        {
            Debug.LogWarning("Failed to register player at " + gridPosition + ". Tile may already be occupied.");
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
            if (kb.zKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
            {
                if (UndoManager.Instance != null && UndoManager.Instance.TryUndo(this))
                {
                    return;
                }
            }

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
        if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Backspace))
        {
            if (UndoManager.Instance != null && UndoManager.Instance.TryUndo(this))
            {
                return;
            }
        }

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

    public void RestoreFromUndo(Vector2Int restoredGridPosition, Vector3 restoredWorldPosition, Quaternion restoredRotation)
    {
        StopAllCoroutines();
        isMoving = false;
        isRegistered = false;

        gridPosition = restoredGridPosition;
        transform.position = restoredWorldPosition;
        transform.rotation = restoredRotation;

        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.CrossFadeInFixedTime(idleStateHash, 0.05f, 0);
        }
    }

    public void SetRegisteredFromUndo(bool registered)
    {
        isRegistered = registered;
    }

    void TryMove(Vector2Int direction)
    {
        if (isMoving || GridManager.Instance == null)
        {
            return;
        }

        Vector2Int targetPos = gridPosition + direction;
        UndoManager.UndoState pendingUndoState = null;

        Log targetLog;
        if (GridManager.Instance.TryGetLog(targetPos, out targetLog))
        {
            if (UndoManager.Instance != null)
            {
                pendingUndoState = UndoManager.Instance.CaptureState(this);
            }

            if (!targetLog.TryPush(direction))
            {
                return;
            }

            if (!GridManager.Instance.MoveOccupant(gridPosition, targetPos))
            {
                if (UndoManager.Instance != null && pendingUndoState != null)
                {
                    UndoManager.Instance.RestoreState(this, pendingUndoState);
                }
                return;
            }

            if (UndoManager.Instance != null && pendingUndoState != null)
            {
                UndoManager.Instance.PushCapturedState(pendingUndoState);
            }

            FaceDirection(direction);
            StartCoroutine(SmoothMoveToTile(targetPos));
            return;
        }

        if (!GridManager.Instance.CanMoveTo(targetPos))
        {
            return;
        }

        if (UndoManager.Instance != null)
        {
            pendingUndoState = UndoManager.Instance.CaptureState(this);
        }

        if (!GridManager.Instance.MoveOccupant(gridPosition, targetPos))
        {
            return;
        }

        if (UndoManager.Instance != null && pendingUndoState != null)
        {
            UndoManager.Instance.PushCapturedState(pendingUndoState);
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

        transform.rotation = Quaternion.LookRotation(worldDir, Vector3.up);
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
        Vector3 endWorld = GridToWorldWithHeight(targetGridPos);

        Quaternion startRot = transform.rotation;
        Vector3 flatTravelDir = endWorld - startWorld;
        flatTravelDir.y = 0f;
        if (flatTravelDir.sqrMagnitude <= 0.0001f)
        {
            flatTravelDir = transform.forward;
            flatTravelDir.y = 0f;
        }
        Quaternion targetRot = Quaternion.LookRotation(flatTravelDir.normalized, Vector3.up);

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
    }

    Vector3 GridToWorldWithHeight(Vector2Int targetGridPos)
    {
        Vector3 worldPos = GridManager.Instance.GridToWorld(targetGridPos);
        worldPos.y = baseY;
        return worldPos;
    }
}
