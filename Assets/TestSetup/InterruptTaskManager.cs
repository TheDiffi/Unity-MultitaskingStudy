using UnityEngine;
using System.Collections.Generic;
using System;
using System.Diagnostics; // For high-precision timing
using static MasterConnector;
using Debug = UnityEngine.Debug;

/// <summary>
/// InterruptTaskManager implements the Power Stabilization task as described in the implementation guide.
/// It controls the task flow, handles state transitions, processes user input, and manages data collection.
/// </summary>
public class InterruptTaskManager : MonoBehaviour
{
    [Header("Communication")]
    [SerializeField] private MasterConnector currentController;

    [Header("References")]
    [SerializeField] private InterruptRenderer interruptRenderer; // Updated to use our new renderer

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
    private List<InterruptTrialData> trialDataList = new List<InterruptTrialData>();

    // Debug mode timestamp for button presses
    private Stopwatch debugStopwatch = new Stopwatch();
    private bool eventsSetup = false;

    private void Start()
    {
        // Check if the connector is set and connected
        if (currentController == null)
        {
            Debug.LogError("No connector set. Please assign a NodeJS or ADB connector.");
            throw new InvalidOperationException("No connector set. Please assign a NodeJS or ADB connector.");
        }

        if (interruptRenderer == null)
        {
            Debug.LogError("InterruptRenderer component not found. Please assign it in the inspector.");
        }
        InitEvents();
    }

    //event receivers for start, interrupt, task-over, exit, get-data
    private void InitEvents()
    {
        if (eventsSetup) return; // Avoid re-registering events
        eventsSetup = true;
        currentController.RegisterPowerStabilizationHandler("configure", (data) => ConfigureTask(data));
        currentController.RegisterPowerStabilizationHandler("start", _ => StartTask());
        currentController.RegisterPowerStabilizationHandler("interrupt", _ => InterruptTask());
        currentController.RegisterPowerStabilizationHandler("exit", _ => TaskOver());
        currentController.RegisterPowerStabilizationHandler("get-data", _ => GetData());
        currentController.RegisterPowerStabilizationHandler("debug", _ => DebugMode());
        currentController.RegisterPowerStabilizationHandler("exit-debug", _ => ExitDebugMode());
    }

    private void OnDisable()
    {
        // Using the task-specific Off method
        if (currentController == null) return;
        currentController.Off(TaskType.PowerStabilization, "configure");
        currentController.Off(TaskType.PowerStabilization, "start");
        currentController.Off(TaskType.PowerStabilization, "interrupt");
        currentController.Off(TaskType.PowerStabilization, "exit");
        currentController.Off(TaskType.PowerStabilization, "get-data");
        currentController.Off(TaskType.PowerStabilization, "debug");
        currentController.Off(TaskType.PowerStabilization, "exit-debug");
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
                currentController.SendPowerStabilizationEvent("configure-error", "Expected JObject format for configuration");
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
            currentController.SendPowerStabilizationEvent("configure-success", "Configuration applied successfully");
            Debug.Log($"Configuration applied: studyId={studyId}, sessionNumber={sessionNumber}, traversalTime={traversalTime}, trialCount={trialCount}");

            // Update the state
            currentState = GameState.Idle;
        }
        catch (Exception ex)
        {
            currentController.SendPowerStabilizationEvent("configure-error", "Error parsing configuration: " + ex.Message);
            Debug.LogException(ex);
        }
    }

    private void StartTask()
    {
        Debug.Log("Starting interrupt task...");
        if (currentState != GameState.Idle)
        {
            Debug.LogWarning("Task cannot be started in current state: " + currentState);
            return;
        }

        // Reset trial data for new session
        currentTrial = 0;
        successCount = 0;
        trialDataList.Clear();

        // Use Stopwatch for precise timing
        SessionStopwatch.StartSession();

        currentState = GameState.Started;

        // Start the interrupt task
        currentController.SendPowerStabilizationEvent("task-started", "Interrupt task started");

        // Send live data for task start
        var startData = new Dictionary<string, object> {
            { "studyId", studyId },
            { "sessionNumber", sessionNumber },
            { "traversalTime", traversalTime },
            { "trialCount", trialCount },
            { "timestamp", DateTime.Now.ToString("o") }
        };
        currentController.SendPowerStabilizationLiveData("task-started", startData);
    }

    private void InterruptTask()
    {
        Debug.Log("Interrupt triggered...");
        if (currentState is not GameState.Started and not GameState.Complete)
        {
            Debug.LogWarning("Cannot trigger interrupt in current state: " + currentState);
            return;
        }

        // Reset for new interrupt sequence
        currentTrial = 0;
        currentState = GameState.InterruptTriggered;

        // Simulate an interrupt
        currentController.SendPowerStabilizationEvent("interrupt-triggered", "Interrupt triggered");

        // Send live data for interrupt trigger
        var interruptData = new Dictionary<string, object> {
            { "studyId", studyId },
            { "sessionNumber", sessionNumber },
            { "timestamp", DateTime.Now.ToString("o") }
        };
        currentController.SendPowerStabilizationLiveData("interrupt-triggered", interruptData);

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

        // Start new trial
        Debug.Log($"Starting trial {currentTrial + 1}/{trialCount} with traversal time: {traversalTime}ms");
        if (interruptRenderer != null)
            interruptRenderer.StartTrial(traversalTime);
    }

    private void CompleteInterrupt()
    {
        Debug.Log("Interrupt sequence completed");
        currentState = GameState.Complete;
        currentController.SendPowerStabilizationEvent("interrupt-complete", "Interrupt sequence completed");
    }

    private void TaskOver()
    {
        Debug.Log("Ending interrupt task...");
        if (currentState == GameState.Idle)
        {
            Debug.LogWarning("Task is not active, cannot end");
            return;
        }

        Debug.Log("Task ending...");
        currentState = GameState.Idle;

        // Stop the session timer
        SessionStopwatch.StopSession();

        currentController.SendPowerStabilizationEvent("task-complete", "Task completed");
    }

    private void GetData()
    {
        Debug.Log("Sending collected interrupt data...");

        // Calculate session statistics using high-precision timing
        float totalDuration = SessionStopwatch.get.ElapsedMilliseconds / 1000f;
        float successRate = trialCount > 0 ? (float)successCount / trialCount : 0;
        string performanceRating = EvaluatePerformance(successRate);

        // Send trial data
        foreach (var data in trialDataList)
            currentController.SendPowerStabilizationEvent("trial-data", data.ToString());


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

        currentController.SendPowerStabilizationEvent("session-summary", JsonUtility.ToJson(sessionSummary));
        currentController.SendPowerStabilizationEvent("data-complete", "Data transfer complete");
    }

    private void DebugMode()
    {
        Debug.Log("Entering debug mode...");
        currentState = GameState.TestMode;

        // Start a high-precision timer for debug mode
        debugStopwatch.Reset();
        debugStopwatch.Start();

        // Use RunDebug instead of StartTrial for debug mode
        if (interruptRenderer != null)
            interruptRenderer.RunDebug();
        currentController.SendPowerStabilizationEvent("debug-active", "Debug mode active");
    }

    private void ExitDebugMode()
    {
        Debug.Log("Exiting debug mode...");
        currentState = GameState.Idle;

        // Stop the debug timer
        debugStopwatch.Stop();

        // Use StopDebug instead of StopTrial for exiting debug mode
        if (interruptRenderer != null)
            interruptRenderer.StopDebug();
        currentController.SendPowerStabilizationEvent("debug-exited", "Debug mode exited");
    }

    // Called by InterruptRenderer when a trial is complete
    public void OnTrialComplete(int zoneIndex, int accuracy, float responseTime)
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

        bool inGreenZone = zoneIndex == 2;
        if (inGreenZone)
        {
            successCount++;
        }

        // Record trial data with high-precision timestamp
        string timestamp = SessionStopwatch.ElapsedToLocalTime(SessionStopwatch.get.ElapsedMilliseconds).ToString("o");
        int reactionTimeMs = Mathf.RoundToInt(responseTime * 1000); // Convert response time to milliseconds

        var trialData = new InterruptTrialData
        {
            study_id = studyId,
            session_number = sessionNumber,
            accuracy = accuracy,        // Integer accuracy value from InterruptRenderer
            speed = traversalTime,      // Speed is the cursor traversal time in ms
            zone_correct = inGreenZone, // Whether the user hit the correct zone
            timestamp = timestamp,
            reaction_time_power = reactionTimeMs // Store user reaction time in milliseconds
        };

        trialDataList.Add(trialData);

        currentController.SendPowerStabilizationEvent("trial-complete", trialData.ToString());

        // Send live data for trial completion
        currentController.SendPowerStabilizationLiveData("trial-complete", trialData);

        if (currentState == GameState.TestMode)
        {
            return;
        }

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
    public void HandleInput()
    {
        if (currentState is not GameState.InProgress and not GameState.TestMode)
        {
            Debug.LogWarning("Button pressed but task not in progress or test mode");
            return;
        }
        if (currentState is GameState.TestMode)
        {
            double elapsed = debugStopwatch.Elapsed.TotalSeconds;
            Debug.Log($"DEBUG MODE: Button pressed at {elapsed:F3}s since debug start");
            currentController.SendPowerStabilizationEvent("debug-button-press", elapsed.ToString("F3"));

            // Send live data for debug button press
            var debugPressData = new Dictionary<string, object> {
                { "elapsedTime", elapsed },
                { "timestamp", DateTime.Now.ToString("o") }
            };
            currentController.SendPowerStabilizationLiveData("debug-button-press", debugPressData);
        }

        // Send live data for button press in regular mode
        if (currentState is GameState.InProgress)
        {
            var inputData = new Dictionary<string, object> {
                { "studyId", studyId },
                { "sessionNumber", sessionNumber },
                { "currentTrial", currentTrial + 1 },
                { "elapsedMilliseconds", SessionStopwatch.get.ElapsedMilliseconds },
                { "timestamp", DateTime.Now.ToString("o") }
            };
            currentController.SendPowerStabilizationLiveData("input-registered", inputData);
        }

        if (interruptRenderer != null)
        {
            (int zoneIndex, int accuracy, float responseTime) = interruptRenderer.HandleButtonPress();
            OnTrialComplete(zoneIndex, accuracy, responseTime);
        }
        else
        {
            OnTrialComplete(1, 12, 12312.0f); // Placeholder values for testing
        }
    }

    private string EvaluatePerformance(float successRate)
    {
        if (successRate >= 0.8f) return "Excellent";
        if (successRate >= 0.6f) return "Good";
        if (successRate >= 0.4f) return "Mediocre";
        return "Poor";
    }

    // Data structure for trial results
    private class InterruptTrialData
    {
        public string study_id;
        public int session_number;
        public string timestamp;
        public int accuracy;
        public int speed;
        public bool zone_correct;
        public int reaction_time_power;

        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }
    }
}