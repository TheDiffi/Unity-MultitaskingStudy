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
        stimulusRenderer.material.color = Color.magenta;
    }

    void ExitDebug()
    {
        Debug.Log("Exiting debug mode");
        stimulusRenderer.material.color = Color.black;
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
            // Handle data as a dictionary - this should be just the params object
            if (data is Dictionary<string, object> paramsDict)
            {
                // Extract parameters directly from the dictionary
                if (paramsDict.TryGetValue("stimDuration", out object stimDurationObj) && stimDurationObj is long stimDurationValue)
                {
                    stimulusDuration = stimDurationValue / 1000f;
                }

                if (paramsDict.TryGetValue("interStimulusInterval", out object isiObj) && isiObj is long isiValue)
                {
                    interStimulusInterval = isiValue / 1000f;
                }

                if (paramsDict.TryGetValue("nBackLevel", out object nBackObj) && nBackObj is long nBackValue)
                {
                    nBackLevel = (int)nBackValue;
                }

                if (paramsDict.TryGetValue("trialsNumber", out object trialsObj) && trialsObj is long trialsValue)
                {
                    totalTrials = (int)trialsValue;
                }

                // Parse the sequence data if available - now a direct array of color strings
                if (paramsDict.TryGetValue("sequence", out object sequenceObj) && sequenceObj is List<object> sequenceList)
                {
                    List<int> colorIndices = new List<int>();

                    // Process each color string in the sequence
                    foreach (var colorObj in sequenceList)
                    {
                        if (colorObj is string colorName)
                        {
                            int colorIndex = GetColorIndexFromName(colorName);
                            if (colorIndex >= 0)
                            {
                                colorIndices.Add(colorIndex);
                            }
                        }
                    }

                    if (colorIndices.Count > 0)
                    {
                        colorSequence = colorIndices.ToArray();
                        totalTrials = colorSequence.Length;

                        Debug.Log($"Parsed sequence with {colorSequence.Length} colors");
                    }
                }

                nodeJSConnector.SendNBackEvent("configure-success", "Configuration applied successfully");
                Debug.Log($"Configuration applied: stimDuration={stimulusDuration}s, ISI={interStimulusInterval}s, nBackLevel={nBackLevel}, trials={totalTrials}");
                return;
            }

            nodeJSConnector.SendNBackEvent("configure-error", "Invalid configuration format");
            Debug.LogError("Failed to parse configuration data: " + (data != null ? data.ToString() : "null"));
        }
        catch (System.Exception ex)
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

            stimulusRenderer.material.color = colors[colorSequence[currentTrial]];
            trialStartTime = Time.time;
            awaitingResponse = true;

            yield return new WaitForSeconds(stimulusDuration);

            stimulusRenderer.material.color = Color.black;

            yield return new WaitForSeconds(interStimulusInterval);

            if (awaitingResponse)
            {
                RecordTrial(false, 0f, "No response");
                nodeJSConnector.SendNBackEvent("trial-complete", "No response");
            }

            awaitingResponse = false;
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
        if (!awaitingResponse) return;

        float reactionTime = Time.time - trialStartTime;
        var result = targetTrial == isConfirm
            ? targetTrial ? "Correct response" : "Correct rejection"
            : targetTrial ? "Missed target" : "False alarm";
        RecordTrial(isConfirm, reactionTime, result);
        nodeJSConnector.SendNBackEvent("trial-complete", result);

        awaitingResponse = false;
        _ = StartCoroutine(FeedbackFlash());
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

