using UnityEngine;
using System;
using Debug = UnityEngine.Debug;

using System.Collections.Generic;

/// <summary>
/// InterruptTaskManager implements the Power Stabilization task as described in the implementation guide.
/// It controls the task flow, handles state transitions, processes user input, and manages data collection.
/// </summary>
public class CalibrationHandler : MonoBehaviour
{
    [SerializeField] private MasterConnector currentController;
    [SerializeField] private AprilTagPassthroughManager passthroughManager;
    [SerializeField] private GameObject targeterObject;

    private KeepInFrontOfEyes targeterScript => targeterObject.GetComponent<KeepInFrontOfEyes>();
    private bool isCalibrating = false;
    private bool targeterEnabled = false;

    private bool isPrimaryVirtual = true;
    private bool isInterruptVirtual = true;

    private int correctBtnId = 0;
    private int wrongBtnId = 1;
    private int nBackId = 2;
    private int interruptBtnId = 4;
    private int interruptId = 5;



    private bool eventsSetup = false;
    private void Start()
    {
        // Check if the connector is set and connected
        if (currentController == null)
        {
            Debug.LogError("No connector set. Please assign a NodeJS or ADB connector.");
            throw new InvalidOperationException("No connector set. Please assign a NodeJS or ADB connector.");
        }
        if (targeterObject == null || targeterObject.GetComponent<KeepInFrontOfEyes>() == null)
        {
            Debug.LogError("No targeter object set. Please assign a targeter object.");
            throw new InvalidOperationException("No targeter object set. Please assign a targeter object.");
        }

        InitEvents();
    }

    //event receivers for start, interrupt, task-over, exit, get-data
    private void InitEvents()
    {
        if (eventsSetup) return; // Avoid re-registering events
        eventsSetup = true;
        currentController.On("start-targeting", (data) => EnableTargeter());
        currentController.On("start-calibration", (data) => TriggerCalibration(data));
        currentController.On("session-complete", (data) => VanishAllObjects());

    }

    private void OnDisable()
    {
        // Using the task-specific Off method
        if (currentController == null) return;
        currentController.Off("calibrate");
    }

    void ParseConfigureTasks(object data)
    {
        Debug.Log("ConfigureTasks called with data: " + data);
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

                Debug.LogError("Data is not a valid JObject. Cannot convert to dictionary.");
                return;
            }

            // Process parameters - we know numeric values are of type long
            if (paramsDict.TryGetValue("primary", out object primaryObj) && primaryObj is bool _isPrimaryVirtual)
            {
                isPrimaryVirtual = _isPrimaryVirtual;
            }

            if (paramsDict.TryGetValue("interrupt", out object interruptObj) && interruptObj is bool _isInterruptVirtual)
            {
                isInterruptVirtual = _isInterruptVirtual;
            }

            //successfully set up the task
            currentController.Send("task-setup-success", null);

        }
        catch (Exception e)
        {
            Debug.LogError("Error in ConfigureTasks: " + e.Message);
            currentController.Send("task-setup-failure", null);
        }
    }

    public void EnableTargeter()
    {
        //ignore data
        if (targeterEnabled) return;
        passthroughManager.ClearTags();
        targeterEnabled = true;
        targeterScript.SetEnabled(true);
    }

    public void TriggerCalibration()
    {
        TriggerCalibration(null);
    }

    private void TriggerCalibration(object data)
    {
        ParseConfigureTasks(data);

        if (isCalibrating) return;
        isCalibrating = true;

        passthroughManager.ClearTags();

        var enabledTaskObjects = new List<int>();

        if (isPrimaryVirtual)
        {
            // Set up primary virtual configurations
            /*   enabledTaskObjects.Add(correctBtnId);
              enabledTaskObjects.Add(wrongBtnId); */
            enabledTaskObjects.Add(nBackId);
        }
        if (isInterruptVirtual)
        {
            // Set up interrupt virtual configurations
            /*  enabledTaskObjects.Add(interruptBtnId); */
            enabledTaskObjects.Add(interruptId);
        }

        Debug.Log("Triggering calibration with objects: " + string.Join(", ", enabledTaskObjects));
        var allItemsDetected = passthroughManager.PlaceObjectsAtTags(enabledTaskObjects, false);

        if (!allItemsDetected)
        {
            Debug.LogWarning("Failed to place all items at tags.");
            currentController.Send("calibration-failure", null);
        }
        else
        {
            Debug.Log("Calibration successful. Objects placed at tags.");
            currentController.Send("calibration-success", null);
        }

        // Clean up
        isCalibrating = false;
        targeterEnabled = false;
        targeterScript.SetEnabled(false);
    }

    public void VanishAllObjects()
    {
        Debug.Log("VanishAllObjects called");
        passthroughManager.ClearTags();
        targeterScript.SetEnabled(false);
        targeterEnabled = false;
    }
}