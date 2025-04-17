using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TraceManager : MonoBehaviour
{
    private static TraceManager _instance;
    public static TraceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TraceManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("TraceManager");
                    _instance = go.AddComponent<TraceManager>();
                }
            }
            return _instance;
        }
    }

    // Dictionary to track active traces and their coroutines
    private Dictionary<Cell, Coroutine> activeTraces = new Dictionary<Cell, Coroutine>();

    void Awake()
    {
        // Ensure we have only one instance
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    // Leave a trace on a cell
    public void LeaveTrace(Cell cell, string traceType, float duration)
    {
        if (cell == null) return;

        // If there's already a trace on this cell, stop it
        if (activeTraces.TryGetValue(cell, out Coroutine coroutine))
        {
            StopCoroutine(coroutine);
            activeTraces.Remove(cell);
        }

        // Set the new trace
        cell.cellEvent = traceType;

        // Start a new coroutine to clear the trace after the duration
        Coroutine newCoroutine = StartCoroutine(ClearTraceAfterDelay(cell, traceType, duration));
        activeTraces[cell] = newCoroutine;
    }

    // Clear a trace after a delay
    private IEnumerator ClearTraceAfterDelay(Cell cell, string traceType, float duration)
    {
        yield return new WaitForSeconds(duration);

        // Make sure the cell still exists and still has the same trace
        if (cell != null && cell.cellEvent == traceType)
        {
            cell.cellEvent = "None";
        }

        // Remove from active traces
        if (activeTraces.ContainsKey(cell))
        {
            activeTraces.Remove(cell);
        }
    }

    // Clear all traces immediately
    public void ClearAllTraces()
    {
        foreach (var kvp in activeTraces)
        {
            if (kvp.Key != null)
            {
                kvp.Key.cellEvent = "None";
            }
            StopCoroutine(kvp.Value);
        }
        activeTraces.Clear();
    }

    // Check if a cell has a specific trace
    public bool HasTrace(Cell cell, string traceType)
    {
        return cell != null && cell.cellEvent == traceType;
    }
}