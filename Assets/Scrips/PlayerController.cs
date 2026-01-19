using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerController : MonoBehaviour
{
    public Vector2Int gridPosition;

    void Start()
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError("GridManager.Instance is null. Make sure a GameObject in the scene has the GridManager component and it's enabled.");
            return;
        }

        // Ensure starting gridPosition is inside the grid
        if (!GridManager.Instance.IsInsideGrid(gridPosition))
        {
            Debug.LogWarning($"Starting gridPosition {gridPosition} is outside the grid. Clamping to valid range.");
            gridPosition.x = Mathf.Clamp(gridPosition.x, 0, GridManager.Instance.width - 1);
            gridPosition.y = Mathf.Clamp(gridPosition.y, 0, GridManager.Instance.height - 1);
        }

        transform.position = GridManager.Instance.GridToWorld(gridPosition);
        Debug.Log($"Player initialized at {gridPosition} -> world {transform.position}");
    }

    void Update()
    {
        bool moved = false;

        // New Input System
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
            {
                Move(Vector2Int.up);
                moved = true;
            }
            if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
            {
                Move(Vector2Int.down);
                moved = true;
            }
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)
            {
                Move(Vector2Int.left);
                moved = true;
            }
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
            {
                Move(Vector2Int.right);
                moved = true;
            }

            if (!moved && kb.anyKey.wasPressedThisFrame)
            {
                string pressed = "";
                if (kb.wKey.wasPressedThisFrame) pressed += "W ";
                if (kb.sKey.wasPressedThisFrame) pressed += "S ";
                if (kb.aKey.wasPressedThisFrame) pressed += "A ";
                if (kb.dKey.wasPressedThisFrame) pressed += "D ";
                if (kb.upArrowKey.wasPressedThisFrame) pressed += "UpArrow ";
                if (kb.downArrowKey.wasPressedThisFrame) pressed += "DownArrow ";
                if (kb.leftArrowKey.wasPressedThisFrame) pressed += "LeftArrow ";
                if (kb.rightArrowKey.wasPressedThisFrame) pressed += "RightArrow ";
                Debug.Log($"New Input System detected keys: {pressed}. GameObject active: {gameObject.activeInHierarchy}, script enabled: {enabled}, GridManager.Instance present: {GridManager.Instance != null}");
            }
        }

    }

    void Move(Vector2Int direction)
    {
        Vector2Int targetPos = gridPosition + direction;

        if (GridManager.Instance == null)
        {
            Debug.LogError("GridManager.Instance is null when trying to move.");
            return;
        }

        // Debug info to help identify why movement doesn't happen
        Debug.Log($"Player Move requested: from {gridPosition} to {targetPos} (dir {direction})");

        if (!GridManager.Instance.IsInsideGrid(targetPos))
        {
            Debug.Log("Move blocked: target is outside grid.");
            return;
        }

        if (!GridManager.Instance.IsWalkable(targetPos))
        {
            Debug.Log("Move blocked: target tile is not walkable (blocked).");
            return;
        }

        // perform move
        gridPosition = targetPos;
        transform.position = GridManager.Instance.GridToWorld(gridPosition);
        Debug.Log($"Player moved to {gridPosition}");
    }
}
