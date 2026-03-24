using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class SimpleProceduralLevelGenerator : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PlayerController player;
    [SerializeField] private Log logPrefab;
    [SerializeField] private GameObject landPrefab;
    [SerializeField] private GameObject waterPrefab;
    [SerializeField] private GameObject goalPrefab;
    [SerializeField] private Transform tilesParent;
    [SerializeField] private Transform propsParent;

    [Header("Generation settings")]
    [SerializeField] private int mapWidth = 10;
    [SerializeField] private int mapHeight = 8;
    [SerializeField] private bool randomizeSeed = true;
    [SerializeField] private int seed = 12345;
    [SerializeField] private int leftIslandSteps = 10;
    [SerializeField] private int rightIslandSteps = 10;
    [SerializeField] private int extraIslandCount = 2;
    [SerializeField] private int extraIslandSteps = 5;

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();

    private void Awake()
    {
        if (gridManager == null)
        {
            gridManager = GridManager.Instance;
        }

        if (gridManager == null)
        {
            Debug.LogError("SimpleProceduralLevelGenerator: GridManager reference missing.");
            return;
        }

        if (player == null)
        {
            Debug.LogError("SimpleProceduralLevelGenerator: PlayerController reference missing.");
            return;
        }

        if (randomizeSeed)
        {
            seed = System.Environment.TickCount;
        }

        Random.InitState(seed);

        GeneratedLevel level = GenerateLevel();
        gridManager.LoadMap(level.terrain);

        BuildTiles(level.terrain);
        PlacePlayer(level.playerStart);
        SpawnLogs(level.logPositions);
        SpawnGoal(level.goalPos);

        Debug.Log("Generated level with seed: " + seed);
    }

    private GeneratedLevel GenerateLevel()
    {
        int safeWidth = Mathf.Max(mapWidth, 8);
        int safeHeight = Mathf.Max(mapHeight, 6);

        int[,] terrain = new int[safeWidth, safeHeight];

        // We build a guaranteed solvable skeleton first:
        // player island -> tree -> 1 water gap -> goal island.
        bool horizontal = Random.value > 0.5f;
        Vector2Int pushDirection;
        Vector2Int playerStart;
        Vector2Int treePos;
        Vector2Int gapPos;
        Vector2Int goalPos;
        Vector2Int leftIslandCenter;
        Vector2Int rightIslandCenter;

        if (horizontal)
        {
            int gapX = Random.Range(3, safeWidth - 3);
            int gapY = Random.Range(2, safeHeight - 2);

            bool pushRight = Random.value > 0.5f;
            if (pushRight)
            {
                pushDirection = Vector2Int.right;
                gapPos = new Vector2Int(gapX, gapY);
                treePos = gapPos + Vector2Int.left;
                playerStart = gapPos + Vector2Int.left * 3;
                goalPos = gapPos + Vector2Int.right * 3;
                leftIslandCenter = gapPos + Vector2Int.left * 2;
                rightIslandCenter = gapPos + Vector2Int.right * 2;
            }
            else
            {
                pushDirection = Vector2Int.left;
                gapPos = new Vector2Int(gapX, gapY);
                treePos = gapPos + Vector2Int.right;
                playerStart = gapPos + Vector2Int.right * 3;
                goalPos = gapPos + Vector2Int.left * 3;
                leftIslandCenter = goalPos + Vector2Int.right;
                rightIslandCenter = playerStart + Vector2Int.left;
            }
        }
        else
        {
            int gapX = Random.Range(2, safeWidth - 2);
            int gapY = Random.Range(3, safeHeight - 3);

            bool pushUp = Random.value > 0.5f;
            if (pushUp)
            {
                pushDirection = Vector2Int.up;
                gapPos = new Vector2Int(gapX, gapY);
                treePos = gapPos + Vector2Int.down;
                playerStart = gapPos + Vector2Int.down * 3;
                goalPos = gapPos + Vector2Int.up * 3;
                leftIslandCenter = gapPos + Vector2Int.down * 2;
                rightIslandCenter = gapPos + Vector2Int.up * 2;
            }
            else
            {
                pushDirection = Vector2Int.down;
                gapPos = new Vector2Int(gapX, gapY);
                treePos = gapPos + Vector2Int.up;
                playerStart = gapPos + Vector2Int.up * 3;
                goalPos = gapPos + Vector2Int.down * 3;
                leftIslandCenter = goalPos + Vector2Int.up;
                rightIslandCenter = playerStart + Vector2Int.down;
            }
        }

        // Main islands.
        CarveBlob(terrain, leftIslandCenter, leftIslandSteps);
        CarveBlob(terrain, rightIslandCenter, rightIslandSteps);

        // Extra decorative islands.
        for (int i = 0; i < extraIslandCount; i++)
        {
            Vector2Int extraCenter = new Vector2Int(
                Random.Range(1, safeWidth - 1),
                Random.Range(1, safeHeight - 1));
            CarveBlob(terrain, extraCenter, extraIslandSteps);
        }

        // Force the guaranteed path to exist.
        Vector2Int pushStandPos = treePos - pushDirection;
        Vector2Int landingPos = gapPos + pushDirection;

        MakeLineLand(terrain, playerStart, pushStandPos);
        MakeLineLand(terrain, landingPos, goalPos);
        SetLand(terrain, treePos);
        SetLand(terrain, pushStandPos);
        SetLand(terrain, landingPos);
        SetLand(terrain, playerStart);
        SetLand(terrain, goalPos);

        // The actual bridge gap must stay water.
        SetWater(terrain, gapPos);

        GeneratedLevel level = new GeneratedLevel();
        level.terrain = terrain;
        level.playerStart = playerStart;
        level.goalPos = goalPos;
        level.logPositions = new List<Vector2Int> { treePos };
        return level;
    }

    private void CarveBlob(int[,] terrain, Vector2Int start, int steps)
    {
        Vector2Int current = ClampInside(start, terrain);
        for (int i = 0; i < steps; i++)
        {
            SetLand(terrain, current);

            Vector2Int dir = RandomCardinalDirection();
            current = ClampInside(current + dir, terrain);
        }
    }

    private void MakeLineLand(int[,] terrain, Vector2Int from, Vector2Int to)
    {
        Vector2Int current = from;
        SetLand(terrain, current);

        while (current.x != to.x)
        {
            current.x += current.x < to.x ? 1 : -1;
            SetLand(terrain, current);
        }

        while (current.y != to.y)
        {
            current.y += current.y < to.y ? 1 : -1;
            SetLand(terrain, current);
        }
    }

    private void BuildTiles(int[,] terrain)
    {
        ClearSpawnedObjects();

        for (int x = 0; x < terrain.GetLength(0); x++)
        {
            for (int y = 0; y < terrain.GetLength(1); y++)
            {
                GameObject prefab = terrain[x, y] == 1 ? landPrefab : waterPrefab;
                if (prefab == null)
                {
                    continue;
                }

                Vector3 pos = gridManager.GridToWorld(new Vector2Int(x, y));
                GameObject spawned = Instantiate(prefab, pos, Quaternion.identity, tilesParent);
                spawnedObjects.Add(spawned);
            }
        }
    }

    private void PlacePlayer(Vector2Int startPos)
    {
        player.gridPosition = startPos;

        Vector3 worldPos = gridManager.GridToWorld(startPos);
        worldPos.y = player.transform.position.y;
        player.transform.position = worldPos;
    }

    private void SpawnLogs(List<Vector2Int> logPositions)
    {
        if (logPrefab == null)
        {
            Debug.LogWarning("SimpleProceduralLevelGenerator: logPrefab is missing.");
            return;
        }

        for (int i = 0; i < logPositions.Count; i++)
        {
            Vector2Int pos = logPositions[i];
            Vector3 worldPos = gridManager.GridToWorld(pos);
            Log logInstance = Instantiate(logPrefab, worldPos, Quaternion.identity, propsParent);
            logInstance.gridPosition = pos;
            spawnedObjects.Add(logInstance.gameObject);
        }
    }

    private void SpawnGoal(Vector2Int goalPos)
    {
        if (goalPrefab == null)
        {
            return;
        }

        Vector3 worldPos = gridManager.GridToWorld(goalPos);
        GameObject goal = Instantiate(goalPrefab, worldPos, Quaternion.identity, propsParent);
        spawnedObjects.Add(goal);
    }

    private void ClearSpawnedObjects()
    {
        for (int i = 0; i < spawnedObjects.Count; i++)
        {
            if (spawnedObjects[i] != null)
            {
                Destroy(spawnedObjects[i]);
            }
        }

        spawnedObjects.Clear();
    }

    private static Vector2Int RandomCardinalDirection()
    {
        int index = Random.Range(0, 4);
        switch (index)
        {
            case 0: return Vector2Int.up;
            case 1: return Vector2Int.down;
            case 2: return Vector2Int.left;
            default: return Vector2Int.right;
        }
    }

    private static Vector2Int ClampInside(Vector2Int pos, int[,] terrain)
    {
        int maxX = terrain.GetLength(0) - 2;
        int maxY = terrain.GetLength(1) - 2;
        pos.x = Mathf.Clamp(pos.x, 1, maxX);
        pos.y = Mathf.Clamp(pos.y, 1, maxY);
        return pos;
    }

    private static void SetLand(int[,] terrain, Vector2Int pos)
    {
        if (IsInside(pos, terrain))
        {
            terrain[pos.x, pos.y] = 1;
        }
    }

    private static void SetWater(int[,] terrain, Vector2Int pos)
    {
        if (IsInside(pos, terrain))
        {
            terrain[pos.x, pos.y] = 0;
        }
    }

    private static bool IsInside(Vector2Int pos, int[,] terrain)
    {
        return pos.x >= 0 && pos.x < terrain.GetLength(0) && pos.y >= 0 && pos.y < terrain.GetLength(1);
    }

    private struct GeneratedLevel
    {
        public int[,] terrain;
        public Vector2Int playerStart;
        public Vector2Int goalPos;
        public List<Vector2Int> logPositions;
    }
}
