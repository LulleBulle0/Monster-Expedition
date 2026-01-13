using UnityEngine;

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

        transform.position = GridManager.Instance.GridToWorld(gridPosition);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
            Move(Vector2Int.up);
        if (Input.GetKeyDown(KeyCode.S))
            Move(Vector2Int.down);
        if (Input.GetKeyDown(KeyCode.A))
            Move(Vector2Int.left);
        if (Input.GetKeyDown(KeyCode.D))
            Move(Vector2Int.right);
    }

    void Move(Vector2Int direction)
    {
        Vector2Int targetPos = gridPosition + direction;

        if (GridManager.Instance.IsWalkable(targetPos))
        {
            gridPosition = targetPos;
            transform.position = GridManager.Instance.GridToWorld(gridPosition);
        }
    }
}
