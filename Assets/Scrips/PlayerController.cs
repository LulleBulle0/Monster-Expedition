using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerController : MonoBehaviour
{
    public Vector2Int gridPosition;
    public float moveDuration = 0.18f; // seconds to move one tile

    Animator animator;
    bool isMoving;
    bool isRegistered;

    // Animator state names (configure in Inspector if your state names differ)
    public string walkStateName = "Walk";
    public string idleStateName = "Idle";

    int walkStateHash;
    int idleStateHash;

    void Start()
    {
        animator = GetComponent<Animator>();

        // Precompute hashes for faster crossfades
        if (animator != null)
        {
            walkStateHash = Animator.StringToHash(walkStateName);
            idleStateHash = Animator.StringToHash(idleStateName);
        }

        if (GridManager.Instance == null)
        {
            Debug.LogError("GridManager.Instance is null. Make sure a GameObject in the scene has the GridManager component and it's enabled.");
            return;
        }

        Debug.Log("Player registered? " + GridManager.Instance.IsOccupied(gridPosition));

        // Ensure starting gridPosition is inside the grid
        if (!GridManager.Instance.IsInsideGrid(gridPosition))
        {
            Debug.LogWarning($"Starting gridPosition {gridPosition} is outside the grid. Clamping to valid range.");
            gridPosition.x = Mathf.Clamp(gridPosition.x, 0, GridManager.Instance.width - 1);
            gridPosition.y = Mathf.Clamp(gridPosition.y, 0, GridManager.Instance.height - 1);
        }

        transform.position = GridManager.Instance.GridToWorld(gridPosition);

        // Register player as an occupied tile so GridManager knows this tile is taken
        isRegistered = GridManager.Instance.RegisterOccupant(gridPosition);
        if (!isRegistered)
        {
            Debug.LogWarning($"Failed to register player at {gridPosition}. Tile may already be occupied or out of bounds.");
        }

        Debug.Log($"Player initialized at {gridPosition} -> world {transform.position}");
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
            return; // ignore input while moving

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
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

    // Wrapper to check grid and start smooth movement
    void TryMove(Vector2Int direction)
    {
        if (isMoving)
            return;

        Vector2Int targetPos = gridPosition + direction;

        if (GridManager.Instance == null)
        {
            Debug.LogError("GridManager.Instance is null when trying to move.");
            return;
        }

        // If the target tile is occupied, try to push the occupier if it's a Log
        if (GridManager.Instance.IsOccupied(targetPos))
        {
            // Find the log instance that occupies the target tile (support multiple logs)
            Log[] logs = FindObjectsOfType<Log>();
            Log occupier = null;
            foreach (var l in logs)
            {
                if (l != null && l.gridPosition == targetPos)
                {
                    occupier = l;
                    break;
                }
            }

            if (occupier != null)
            {
                Debug.Log($"Attempting to push log at {targetPos}");

                // Try to push the log first
                if (!occupier.TryPush(direction))
                {
                    Debug.Log($"Push of log at {targetPos} failed.");
                    return;
                }

                Debug.Log($"Push of log at {targetPos} succeeded.");

                // After pushing the log, the target tile should be free. Move player's occupancy then start movement.
                if (!GridManager.Instance.MoveOccupant(gridPosition, targetPos))
                {
                    Debug.Log($"Move failed: could not move occupant from {gridPosition} to {targetPos} after pushing log.");
                    return;
                }

                FaceDirection(direction);
                StartCoroutine(SmoothMoveToTile(targetPos));
                return;
            }

            Debug.Log($"Move blocked: target {targetPos} is occupied but no pushable log found.");
            // occupied by something we don't know how to push
            return;
        }

        // Use GridManager's CanMoveTo which respects inspector-configured allowed tiles
        if (!GridManager.Instance.CanMoveTo(targetPos))
        {
            Debug.Log($"Move blocked: target {targetPos} is not allowed by GridManager.CanMoveTo.");
            return;
        }

        // Attempt to atomically move player's occupancy before starting the visual movement
        if (!GridManager.Instance.MoveOccupant(gridPosition, targetPos))
        {
            Debug.Log("Player tile occupied? " + GridManager.Instance.IsOccupied(gridPosition));
            Debug.Log($"Move failed: could not move occupant from {gridPosition} to {targetPos}.");
            return;
        }

        // Face the direction immediately (so rotation is responsive)
        FaceDirection(direction);

        // Begin smooth move
        StartCoroutine(SmoothMoveToTile(targetPos));
    }

    void FaceDirection(Vector2Int dir)
    {
        // Convert grid direction to world-space direction
        Vector3 worldDir = new Vector3(dir.x, 0f, dir.y);
        if (worldDir.sqrMagnitude <= 0f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(worldDir, Vector3.up);
        transform.rotation = targetRot; // immediate facing; Smooth rotation also applied during SmoothMoveToTile
    }

    IEnumerator SmoothMoveToTile(Vector2Int targetGridPos)
    {
        isMoving = true;
        gridPosition = targetGridPos;

        if (animator != null)
        {
            // Set bool for compatibility
            animator.SetBool("IsWalking", true);
            // Crossfade into the walk animation on layer 0 for smooth blending
            animator.CrossFadeInFixedTime(walkStateHash, 0.05f, 0);
        }

        Vector3 startWorld = transform.position;
        Vector3 endWorld = GridManager.Instance.GridToWorld(targetGridPos);

        Quaternion startRot = transform.rotation;
        // Determine rotation to face travel direction smoothly
        Vector3 travelDir = (endWorld - startWorld);
        if (travelDir.sqrMagnitude <= 0.0001f)
            travelDir = transform.forward;
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
            // Crossfade back to idle to avoid the idle state playing additively
            animator.CrossFadeInFixedTime(idleStateHash, 0.05f, 0);
        }

        isMoving = false;
        Debug.Log($"Player moved to {gridPosition}");
    }
}
