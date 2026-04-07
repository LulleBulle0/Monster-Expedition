using System.Collections.Generic;
using UnityEngine;

public class UndoManager : MonoBehaviour
{
    public static UndoManager Instance { get; private set; }

    private Stack<UndoState> undoStack = new Stack<UndoState>();

    [System.Serializable]
    public class UndoState
    {
        public Vector2Int playerGridPosition;
        public Vector3 playerWorldPosition;
        public Quaternion playerRotation;
        public List<Vector2Int> bridgeTiles = new List<Vector2Int>();
        public List<LogUndoState> logs = new List<LogUndoState>();
    }

    [System.Serializable]
    public class LogUndoState
    {
        public Log log;
        public Vector2Int gridPosition;
        public Vector3 worldPosition;
        public Quaternion rotation;
        public bool isBridge;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple UndoManager instances found; destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public UndoState CaptureState(PlayerController player)
    {
        if (player == null || GridManager.Instance == null || HasBusyObjects(player))
        {
            return null;
        }

        UndoState state = new UndoState();
        state.playerGridPosition = player.gridPosition;
        state.playerWorldPosition = player.transform.position;
        state.playerRotation = player.transform.rotation;
        state.bridgeTiles = GridManager.Instance.GetBridgeTilesSnapshot();

        Log[] logs = FindObjectsOfType<Log>();
        for (int i = 0; i < logs.Length; i++)
        {
            Log log = logs[i];
            if (log == null)
            {
                continue;
            }

            LogUndoState logState = new LogUndoState();
            logState.log = log;
            logState.gridPosition = log.gridPosition;
            logState.worldPosition = log.transform.position;
            logState.rotation = log.transform.rotation;
            logState.isBridge = log.IsBridge;
            state.logs.Add(logState);
        }

        return state;
    }

    public void PushCapturedState(UndoState state)
    {
        if (state == null)
        {
            return;
        }

        undoStack.Push(state);
    }

    public bool TryUndo(PlayerController player)
    {
        if (player == null || undoStack.Count == 0 || HasBusyObjects(player))
        {
            return false;
        }

        UndoState state = undoStack.Pop();
        RestoreState(player, state);
        return true;
    }

    public void RestoreState(PlayerController player, UndoState state)
    {
        if (player == null || state == null || GridManager.Instance == null)
        {
            return;
        }

        GridManager.Instance.RestoreBridgeTiles(state.bridgeTiles);
        GridManager.Instance.ClearOccupants();

        player.RestoreFromUndo(state.playerGridPosition, state.playerWorldPosition, state.playerRotation);
        bool playerRegistered = GridManager.Instance.RegisterOccupant(player.gridPosition);
        player.SetRegisteredFromUndo(playerRegistered);

        for (int i = 0; i < state.logs.Count; i++)
        {
            LogUndoState logState = state.logs[i];
            if (logState == null || logState.log == null)
            {
                continue;
            }

            logState.log.RestoreFromUndo(logState.gridPosition, logState.worldPosition, logState.rotation, logState.isBridge);

            bool logRegistered = false;
            if (!logState.isBridge)
            {
                logRegistered = GridManager.Instance.RegisterLog(logState.log, logState.gridPosition);
            }

            logState.log.SetRegisteredFromUndo(logRegistered);
        }
    }

    public void ClearHistory()
    {
        undoStack.Clear();
    }

    bool HasBusyObjects(PlayerController player)
    {
        if (player != null && player.IsBusy)
        {
            return true;
        }

        Log[] logs = FindObjectsOfType<Log>();
        for (int i = 0; i < logs.Length; i++)
        {
            if (logs[i] != null && logs[i].IsBusy)
            {
                return true;
            }
        }

        return false;
    }
}
