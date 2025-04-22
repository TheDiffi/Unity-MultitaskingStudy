using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NBackTask : MonoBehaviour
{
    [SerializeField]
    private Renderer stimulusRenderer;

    [Header("Timings")]
    [SerializeField] private float stimulusDuration = 2f;
    [SerializeField] private float interStimulusInterval = 2f;
    [SerializeField] private float feedbackDuration = 0.1f;


    [Header("Task Settings")]
    [SerializeField] private int nBackLevel = 1;
    [SerializeField] private int totalTrials = 30;

    [SerializeField]
    private NodeJSConnector nodeJSConnector;
    private Color[] colors = { Color.red, Color.green, Color.blue, Color.yellow, new Color(0.5f, 0, 0.5f), Color.white };
    private int currentTrial = 0;
    private int[] colorSequence;
    private bool awaitingResponse;
    private bool targetTrial;
    private float trialStartTime;
    private List<TrialData> trialDataList = new List<TrialData>();

    private bool isPaused = false;
    private Coroutine trialCoroutine;
    private Coroutine debugCoroutine;
    private bool inDebugMode = false;


    void Start()
    {
        // Register handlers using the NBack task-specific registration method
        nodeJSConnector.RegisterNBackHandler("start", _ => StartTask());
        nodeJSConnector.RegisterNBackHandler("pause", _ => PauseTask());
        nodeJSConnector.RegisterNBackHandler("resume", _ => ResumeTask());
        nodeJSConnector.RegisterNBackHandler("stop", _ => ExitTask());
        nodeJSConnector.RegisterNBackHandler("debug", _ => DebugMode());
        nodeJSConnector.RegisterNBackHandler("exit-debug", _ => ExitDebug());
        nodeJSConnector.RegisterNBackHandler("exit", _ => ExitTask());
        nodeJSConnector.RegisterNBackHandler("get-data", _ => GetData());
        nodeJSConnector.RegisterNBackHandler("configure", (data) => ConfigureTask(data));
    }

    void StartTask()
    {
        if (trialCoroutine != null)
            StopCoroutine(trialCoroutine);

        if (colorSequence == null || colorSequence.Length == 0)
        {
            string errorMessage = "No color sequence configured. Configure the task before starting.";
            Debug.LogError(errorMessage);
            nodeJSConnector.SendNBackEvent("configure-error", errorMessage);

            // Don't start the trials
            return;
        }

        trialDataList.Clear();
        currentTrial = 0;
        trialCoroutine = StartCoroutine(RunTrials());
    }

    void PauseTask()
    {
        isPaused = !isPaused;
    }

    void ResumeTask()
    {
        // Resume is just unpausing the task
        isPaused = false;
        nodeJSConnector.SendNBackEvent("task-resumed", "Task resumed");
    }

    void DebugMode()
    {
        Debug.Log("Debug mode activated");
        inDebugMode = true;

        // Start color cycling coroutine
        if (debugCoroutine != null)
            StopCoroutine(debugCoroutine);

        debugCoroutine = StartCoroutine(CycleColorsInDebugMode());

        // Log start of debug mode
        nodeJSConnector.SendNBackEvent("debug-mode", "Debug mode activated");
    }

    void ExitDebug()
    {
        Debug.Log("Exiting debug mode");
        inDebugMode = false;

        // Stop color cycling
        if (debugCoroutine != null)
        {
            StopCoroutine(debugCoroutine);
            debugCoroutine = null;
        }

        stimulusRenderer.material.color = Color.black;
        nodeJSConnector.SendNBackEvent("debug-mode", "Debug mode deactivated");
    }

    IEnumerator CycleColorsInDebugMode()
    {
        int colorIndex = 0;

        while (inDebugMode)
        {
            // Cycle through available colors
            stimulusRenderer.material.color = colors[colorIndex];

            // Log the current color in debug mode
            Debug.Log($"Debug mode color: {GetColorNameFromIndex(colorIndex)}");

            // Move to next color
            colorIndex = (colorIndex + 1) % colors.Length;

            // Wait for 1 second before changing color
            yield return new WaitForSeconds(1f);
        }
    }

    // Helper function to get color name from index - for logging purposes
    private string GetColorNameFromIndex(int colorIndex)
    {
        return colorIndex switch
        {
            0 => "Red",
            1 => "Green",
            2 => "Blue",
            3 => "Yellow",
            4 => "Purple",
            5 => "White",
            _ => "Unknown"
        };
    }

    void ExitTask()
    {
        if (trialCoroutine != null)
            StopCoroutine(trialCoroutine);
        stimulusRenderer.material.color = Color.black;
        trialDataList.Clear();
    }

    void GetData()
    {
        foreach (var data in trialDataList)
            nodeJSConnector.SendNBackEvent("trial-data", data.ToString());

        // Change from "nback-data-complete" to match what the Node.js controller expects
        nodeJSConnector.SendNBackEvent("data-complete", "Data transfer complete");
    }

    void ConfigureTask(object data)
    {
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
                nodeJSConnector.SendNBackEvent("configure-error", "Expected JObject format for configuration");
                Debug.LogError("Failed to parse configuration data: " + (data != null ? data.ToString() : "null"));
                return;
            }

            // Process numeric parameters - we know they are all of type long
            if (paramsDict.TryGetValue("stimDuration", out object stimDurationObj) && stimDurationObj is long stimDurationLong)
            {
                stimulusDuration = stimDurationLong / 1000f;
            }

            if (paramsDict.TryGetValue("interStimulusInterval", out object isiObj) && isiObj is long isiLong)
            {
                interStimulusInterval = isiLong / 1000f;
            }

            if (paramsDict.TryGetValue("nBackLevel", out object nBackObj) && nBackObj is long nBackLong)
            {
                nBackLevel = (int)nBackLong;
            }

            if (paramsDict.TryGetValue("trialsNumber", out object trialsObj) && trialsObj is long trialsLong)
            {
                totalTrials = (int)trialsLong;
            }

            // Parse the sequence data - working with JArray directly
            if (paramsDict.TryGetValue("sequence", out object sequenceObj) && sequenceObj is Newtonsoft.Json.Linq.JArray jArray)
            {
                List<int> colorIndices = new List<int>();

                foreach (var item in jArray)
                {
                    string colorName = item.ToString();
                    int colorIndex = GetColorIndexFromName(colorName);
                    if (colorIndex >= 0)
                    {
                        colorIndices.Add(colorIndex);
                    }
                    else
                    {
                        Debug.LogWarning($"Unknown color name in sequence: {colorName}");
                    }
                }

                if (colorIndices.Count > 0)
                {
                    colorSequence = colorIndices.ToArray();
                    totalTrials = colorSequence.Length;
                    Debug.Log($"Parsed sequence with {colorSequence.Length} colors");
                }
                else
                {
                    Debug.LogWarning("No valid colors found in sequence");
                }
            }

            nodeJSConnector.SendNBackEvent("configure-success", "Configuration applied successfully");
            Debug.Log($"Configuration applied: stimDuration={stimulusDuration}s, ISI={interStimulusInterval}s, nBackLevel={nBackLevel}, trials={totalTrials}");
        }
        catch (Exception ex)
        {
            nodeJSConnector.SendNBackEvent("configure-error", "Error parsing configuration: " + ex.Message);
            Debug.LogException(ex);
        }
    }

    // Helper function to convert color name to color index
    private int GetColorIndexFromName(string colorName)
    {
        return colorName.ToLower() switch
        {
            "red" => 0,
            "green" => 1,
            "blue" => 2,
            "yellow" => 3,
            "purple" => 4,
            _ => -1,
        };
    }

    IEnumerator RunTrials()
    {
        while (currentTrial < totalTrials)
        {
            if (isPaused)
            {
                yield return null;
                continue;
            }

            targetTrial = currentTrial >= nBackLevel && colorSequence[currentTrial] == colorSequence[currentTrial - nBackLevel];

            // Show the stimulus color
            stimulusRenderer.material.color = colors[colorSequence[currentTrial]];
            trialStartTime = Time.time;
            awaitingResponse = true;

            // Wait for input instead of automatically advancing
            while (awaitingResponse)
            {
                yield return null;
                // This loop will exit when HandleResponse is called by button press
            }

            // The stimulus is already black and feedback has been shown in HandleResponse
            // Now wait for the inter-stimulus interval before the next trial
            yield return new WaitForSeconds(interStimulusInterval);

            currentTrial++;
        }

        nodeJSConnector.SendNBackEvent("task-complete", "Task complete");
    }

    public void OnCorrectButtonPressed()
    {
        HandleResponse(true);
    }

    public void OnWrongButtonPressed()
    {
        HandleResponse(false);
    }

    void HandleResponse(bool isConfirm)
    {
        // Log button presses if in debug mode
        if (inDebugMode)
        {
            string buttonType = isConfirm ? "Correct" : "Wrong";
            Debug.Log($"Debug mode: {buttonType} button pressed");
            nodeJSConnector.SendNBackEvent("debug-button-press", buttonType);
            return;
        }

        if (!awaitingResponse) return;

        float reactionTime = Time.time - trialStartTime;
        var result = targetTrial == isConfirm
            ? targetTrial ? "Correct response" : "Correct rejection"
            : targetTrial ? "Missed target" : "False alarm";

        // First, show visual feedback
        _ = StartCoroutine(FeedbackFlash());

        // Then, send event to nodejs
        nodeJSConnector.SendNBackEvent("trial-complete", result);

        // Finally, record the trial data
        RecordTrial(isConfirm, reactionTime, result);

        // Mark that we've received the response
        awaitingResponse = false;
    }

    IEnumerator FeedbackFlash()
    {
        stimulusRenderer.material.color = colors[5];
        yield return new WaitForSeconds(feedbackDuration);
        stimulusRenderer.material.color = Color.black;
    }

    void RecordTrial(bool response, float reactionTime, string result)
    {
        trialDataList.Add(new TrialData
        {
            TrialNumber = currentTrial + 1,
            ColorIndex = colorSequence[currentTrial],
            IsTarget = targetTrial,
            ResponseMade = response,
            ReactionTime = reactionTime,
            Result = result,
            Timestamp = Time.time
        });
    }
}

