using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// InterruptTaskManager implements the Power Stabilization task as described in the implementation guide.
/// It controls the task flow, handles state transitions, processes user input, and manages data collection.
/// </summary>
public class InterruptTaskManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private IConnector nodeJSConnector;
    [SerializeField] private InterruptRenderer interruptRenderer; // Will be implemented by you later

    [Header("Configuration")]
    [SerializeField] private int traversalTime = 1000; // time in ms the cursor needs to traverse the line once
    [SerializeField] private int trialCount = 1;
    [SerializeField] private int sessionNumber = -1; // session number
    [SerializeField] private string studyId = "NOCONF"; // study ID

    // State tracking as per implementation guide
    private enum GameState { Idle, Started, InterruptTriggered, InProgress, Complete, TestMode }
    private GameState currentState = GameState.Idle;

    // Trial management
    private int currentTrial = 0;
    private int successCount = 0;
    private List<TrialData> trialDataList = new List<TrialData>();
    private float sessionStartTime;

    // Debug mode timestamp for button presses
    private System.DateTime debugSessionStartTime;

    private void Start()
    {
        // Initialize references if needed
        if (nodeJSConnector == null)
        {
            Debug.LogError("IConnector component not found. Please assign it in the inspector.");
        }

        if (interruptRenderer == null)
        {
            Debug.LogError("InterruptRenderer component not found. Please assign it in the inspector.");
        }
    }

    //event receivers for start, interrupt, task-over, exit, get-data
    private void OnEnable()
    {
        nodeJSConnector.RegisterPowerStabilizationHandler("configure", (data) => ConfigureTask(data));
        nodeJSConnector.RegisterPowerStabilizationHandler("start", _ => StartTask());
        nodeJSConnector.RegisterPowerStabilizationHandler("interrupt", _ => InterruptTask());
        nodeJSConnector.RegisterPowerStabilizationHandler("exit", _ => TaskOver());
        nodeJSConnector.RegisterPowerStabilizationHandler("get-data", _ => GetData());
        nodeJSConnector.RegisterPowerStabilizationHandler("debug", _ => DebugMode());
        nodeJSConnector.RegisterPowerStabilizationHandler("exit-debug", _ => ExitDebugMode());
    }

    private void OnDisable()
    {
        // Using the task-specific Off method
        nodeJSConnector.Off(IConnector.TaskType.PowerStabilization, "configure");
        nodeJSConnector.Off(IConnector.TaskType.PowerStabilization, "start");
        nodeJSConnector.Off(IConnector.TaskType.PowerStabilization, "interrupt");
        nodeJSConnector.Off(IConnector.TaskType.PowerStabilization, "exit");
        nodeJSConnector.Off(IConnector.TaskType.PowerStabilization, "get-data");
        nodeJSConnector.Off(IConnector.TaskType.PowerStabilization, "debug");
        nodeJSConnector.Off(IConnector.TaskType.PowerStabilization, "exit-debug");
    }

    private void ConfigureTask(object data)
    {
        Debug.Log("Configuration received: " + data.ToString());

        try
        {
            Dictionary<string, object> paramsDict = null;

            // The data is a JObject, so convert it to a dictionary
            if (data is Newtonsoft.Json.Linq.JObject jObject)
            {
                paramsDict = jObject.ToObject<Dictionary<string, object>>();
            }
            else
            {
                nodeJSConnector.SendPowerStabilizationEvent("configure-error", "Expected JObject format for configuration");
                Debug.LogError("Failed to parse configuration data: " + (data != null ? data.ToString() : "null"));
                return;
            }

            // Process parameters - we know numeric values are of type long
            if (paramsDict.TryGetValue("studyId", out object studyIdObj) && studyIdObj is string studyIdStr)
            {
                studyId = studyIdStr;
            }

            if (paramsDict.TryGetValue("sessionNumber", out object sessionObj) && sessionObj is long sessionLong)
            {
                sessionNumber = (int)sessionLong;
            }

            if (paramsDict.TryGetValue("traversalTime", out object timeObj) && timeObj is long timeLong)
            {
                traversalTime = (int)timeLong;
            }

            if (paramsDict.TryGetValue("trialCount", out object countObj) && countObj is long countLong)
            {
                trialCount = (int)countLong;
            }

            // Send success message
            nodeJSConnector.SendPowerStabilizationEvent("configure-success", "Configuration applied successfully");
            Debug.Log($"Configuration applied: studyId={studyId}, sessionNumber={sessionNumber}, traversalTime={traversalTime}, trialCount={trialCount}");

            // Update the state
            currentState = GameState.Idle;
        }
        catch (System.Exception ex)
        {
            nodeJSConnector.SendPowerStabilizationEvent("configure-error", "Error parsing configuration: " + ex.Message);
            Debug.LogException(ex);
        }
    }

    private void StartTask()
    {
        if (currentState != GameState.Idle)
        {
            Debug.LogWarning("Task cannot be started in current state: " + currentState);
            return;
        }

        // Reset trial data for new session
        currentTrial = 0;
        successCount = 0;
        trialDataList.Clear();
        sessionStartTime = Time.time;
        currentState = GameState.Started;

        // Start the interrupt task
        Debug.Log("Starting interrupt task...");
        nodeJSConnector.SendPowerStabilizationEvent("task-started", "Interrupt task started");
    }

    private void InterruptTask()
    {
        if (currentState is not GameState.Started and not GameState.Complete)
        {
            Debug.LogWarning("Cannot trigger interrupt in current state: " + currentState);
            return;
        }

        // Reset for new interrupt sequence
        currentTrial = 0;
        currentState = GameState.InterruptTriggered;

        // Simulate an interrupt
        Debug.Log("Interrupt triggered...");
        nodeJSConnector.SendPowerStabilizationEvent("interrupt-triggered", "Interrupt triggered");

        // Start the first trial
        StartNextTrial();
    }

    private void StartNextTrial()
    {
        if (currentTrial >= trialCount)
        {
            // All trials complete
            CompleteInterrupt();
            return;
        }

        currentState = GameState.InProgress;

        // Randomize speed by ±20% as per guide
        float speedVariation = 0.2f; // 20%
        int adjustedTraversalTime = traversalTime + (int)(Random.Range(-speedVariation, speedVariation) * traversalTime);

        // Start new trial
        Debug.Log($"Starting trial {currentTrial + 1}/{trialCount} with traversal time: {adjustedTraversalTime}ms");
        interruptRenderer.StartTrial(adjustedTraversalTime);
    }

    private void CompleteInterrupt()
    {
        currentState = GameState.Complete;
        Debug.Log("Interrupt sequence completed");
        nodeJSConnector.SendPowerStabilizationEvent("interrupt-complete", "Interrupt sequence completed");
    }

    private void TaskOver()
    {
        if (currentState == GameState.Idle)
        {
            Debug.LogWarning("Task is not active, cannot end");
            return;
        }

        Debug.Log("Task ending...");
        currentState = GameState.Idle;
        nodeJSConnector.SendPowerStabilizationEvent("task-complete", "Task completed");
    }

    private void GetData()
    {
        Debug.Log("Sending collected data...");

        // Calculate session statistics
        float totalDuration = Time.time - sessionStartTime;
        float successRate = trialCount > 0 ? (float)successCount / trialCount : 0;
        string performanceRating = EvaluatePerformance(successRate);

        // Send trial data
        foreach (var data in trialDataList)
        {
            nodeJSConnector.SendPowerStabilizationEvent("trial-data", data.ToString());
        }

        // Send session summary
        Dictionary<string, object> sessionSummary = new Dictionary<string, object>
        {
            { "studyId", studyId },
            { "sessionNumber", sessionNumber },
            { "totalDuration", totalDuration },
            { "successRate", successRate },
            { "performanceRating", performanceRating },
            { "totalTrials", trialCount },
            { "successfulTrials", successCount }
        };

        nodeJSConnector.SendPowerStabilizationEvent("session-summary", sessionSummary);
        nodeJSConnector.SendPowerStabilizationEvent("data-complete", "Data transfer complete");
    }

    private void DebugMode()
    {
        Debug.Log("Entering debug mode...");
        currentState = GameState.TestMode;

        // Initialize debug session start time for timestamp calculations
        debugSessionStartTime = System.DateTime.Now;

        // Use RunDebug instead of StartTrial for debug mode
        interruptRenderer.RunDebug();
        nodeJSConnector.SendPowerStabilizationEvent("debug-active", "Debug mode active");
    }

    private void ExitDebugMode()
    {
        Debug.Log("Exiting debug mode...");
        currentState = GameState.Idle;

        // Use StopDebug instead of StopTrial for exiting debug mode
        interruptRenderer.StopDebug();
        nodeJSConnector.SendPowerStabilizationEvent("debug-exited", "Debug mode exited");
    }

    // Called by InterruptRenderer when a trial is complete
    public void OnTrialComplete(int zoneIndex, float accuracy, float responseTime)
    {
        if (currentState is not GameState.InProgress and not GameState.TestMode)
        {
            Debug.LogWarning("Trial completed but task not in progress or test mode");
            return;
        }

        if (currentState == GameState.InProgress)
        {
            currentTrial++;
        }

        // Check if cursor was in the green zone (zone 2)
        bool inGreenZone = zoneIndex == 2;
        if (inGreenZone)
        {
            successCount++;
        }

        // Record trial data
        var trialData = new TrialData
        {
            StudyId = studyId,
            SessionNumber = sessionNumber,
            TrialNumber = currentTrial,
            Zone = zoneIndex,
            Accuracy = accuracy,
            ResponseTime = responseTime,
            Success = inGreenZone,
            Timestamp = Time.time - sessionStartTime
        };

        trialDataList.Add(trialData);

        // Send trial result to Node.js
        nodeJSConnector.SendPowerStabilizationEvent("trial-complete", trialData.ToString());

        // If in test mode, don't progress to next trial
        if (currentState == GameState.TestMode)
        {
            return;
        }

        // Start next trial or complete interrupt sequence
        if (currentTrial < trialCount)
        {
            StartNextTrial();
        }
        else
        {
            CompleteInterrupt();
        }
    }

    // Handle button press from UI or keyboard
    public void HandleButtonPress()
    {
        if (currentState != GameState.InProgress && currentState != GameState.TestMode)
        {
            Debug.LogWarning("Button pressed but task not in progress or test mode");
            return;
        }

        // In debug mode, log button press with timestamp
        if (currentState == GameState.TestMode)
        {
            System.TimeSpan elapsed = System.DateTime.Now - debugSessionStartTime;
            Debug.Log($"DEBUG MODE: Button pressed at {elapsed.TotalSeconds:F3}s since debug start");
        }

        // Let the renderer handle the button press and return the results
        interruptRenderer.HandleButtonPress();
    }

    private string EvaluatePerformance(float successRate)
    {
        if (successRate >= 0.8f) return "Excellent";
        if (successRate >= 0.6f) return "Good";
        if (successRate >= 0.4f) return "Mediocre";
        return "Poor";
    }

    // Data structure for trial results
    private class TrialData
    {
        public string StudyId { get; set; }
        public int SessionNumber { get; set; }
        public int TrialNumber { get; set; }
        public int Zone { get; set; }
        public float Accuracy { get; set; }
        public float ResponseTime { get; set; }
        public bool Success { get; set; }
        public float Timestamp { get; set; }

        public override string ToString()
        {
            return $"{StudyId},{SessionNumber},{Timestamp:F3},power-stabilization,trial-complete,{Accuracy:F2},{ResponseTime:F2},{Success}";
        }
    }
}