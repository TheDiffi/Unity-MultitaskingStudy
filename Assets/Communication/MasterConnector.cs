using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using TMPro;

public class MasterConnector : MonoBehaviour
{
    /// <summary>
    /// Enum representing the different task types
    /// </summary>
    public enum TaskType
    {
        PowerStabilization,
        NBack
    }
    private class JsonMessage
    {
        public string type;
        public string task;
        public object data;
    }

    [Header("Logging")]
    [SerializeField] private bool addTimestampsToLog = true;
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private int maxLogLines = 15;
    private List<string> logLines = new List<string>();


    [Header("Communication")]
    [SerializeField] private bool useWebSocket = true;
    [Tooltip("Set either NodeJSConnector OR ADBConnector, not both")]
    [SerializeField]
    private NodeJSConnector nodeJSConnector;
    [Tooltip("Set either NodeJSConnector OR ADBConnector, not both")]
    [SerializeField]
    private ADBConnector adbConnector;
    // Automatically selects which connector to use (NodeJS has priority if both are set)
    private IConnector currentConnector => useWebSocket ? nodeJSConnector : adbConnector;
    public bool IsConnected => currentConnector?.IsConnected ?? false;
    [SerializeField] private bool sendDebugMessages = true;


    [Header("Event Handlers")]
    // Dictionary to store event handlers - changed to store single actions instead of lists
    private Dictionary<string, Action<object>> globalEventHandlers = new Dictionary<string, Action<object>>();
    // Dedicated dictionaries for the two specific tasks - changed to store single actions instead of lists
    private Dictionary<string, Action<object>> powerStabilizationHandlers = new Dictionary<string, Action<object>>();
    private Dictionary<string, Action<object>> nBackHandlers = new Dictionary<string, Action<object>>();

    public void Connect()
    {
        currentConnector?.Connect();
    }

    public void Disconnect()
    {
        currentConnector?.Disconnect();
    }

    void Start()
    {
        // Initialize log
        if (debugText != null)
        {
            debugText.text = "Communication Log:";
        }
        if (currentConnector == null)
        {
            Debug.LogError("No connector set. Please assign a NodeJS or ADB connector.");
            throw new InvalidOperationException("No connector set. Please assign a NodeJS or ADB connector.");
        }
        if (currentConnector.IsConnected)
        {
            OnConnected();
        }
        else
        {
            // Connect to the selected connector
            currentConnector.OnConnected += OnConnected;
        }

        if (sendDebugMessages)
        {
            // Start sending test messages automatically every 7 seconds
            InvokeRepeating("SendTestMessage", 0f, 7.0f);
        }

    }

    void SendTestMessage()
    {
        // Send a simple log message that Node.js will pick up
        currentConnector.Send("{\"test\": \"HELLO_FROM_QUEST\"}");
        Log("Test message sent to Node.js: HELLO_FROM_QUEST", LogType.Log);
    }

    private void OnConnected()
    {
        Log("Connection established! Registering message handler.", LogType.Log);
        //register event handlers for connection
        currentConnector.OnMessageReceived += (message) =>
        {
            ParseAndHandleMessage(message);
        };
    }


    // Process received messages and dispatch to appropriate event handlers
    private void ParseAndHandleMessage(string messageText)
    {
        try
        {
            JsonMessage message = JsonConvert.DeserializeObject<JsonMessage>(messageText);
            if (message == null || string.IsNullOrEmpty(message.type))
            {
                Log($"Received message with invalid format: {messageText}", LogType.Warning);
                return;
            }

            // Check if we have task-specific handlers for this event
            if (message.task == "powerstabilization" && powerStabilizationHandlers.TryGetValue(message.type, out Action<object> powerHandler))
            {
                Debug.Log($"Received power stabilization event: {message.type}");
                try
                {
                    powerHandler(message.data);
                }
                catch (Exception e)
                {
                    Log($"Error in power stabilization handler for event '{message.type}': {e.Message}", LogType.Error);
                }
            }
            else if (message.task == "nback" && nBackHandlers.TryGetValue(message.type, out Action<object> nBackHandler))
            {
                Debug.Log($"Received n-back event: {message.type}");
                try
                {
                    nBackHandler(message.data);
                }
                catch (Exception e)
                {
                    Log($"Error in n-back handler for event '{message.type}': {e.Message}", LogType.Error);
                }
            }
            // Fall back to global event handlers for this type
            else if (globalEventHandlers.TryGetValue(message.type, out Action<object> handler))
            {
                try
                {
                    handler(message.data);
                }
                catch (Exception e)
                {
                    Log($"Error in event handler for '{message.type}': {e.Message}", LogType.Error);
                }
            }
            else
            {
                Log($"No handlers registered for event type: {message.type}" + (string.IsNullOrEmpty(message.task) ? "" : $" and task: {message.task}"), LogType.Warning);
            }
        }
        catch (Exception e)
        {
            Log($"Error parsing message from Node.js: {e.Message}", LogType.Error);
            Log($"Raw message: {messageText}", LogType.Error);
        }
    }

    // Register an event handler for a specific event type
    public void On(string eventType, Action<object> handler)
    {
        if (globalEventHandlers.ContainsKey(eventType))
        {
            throw new InvalidOperationException($"An event handler for '{eventType}' is already registered. Only one handler per event type is allowed.");
        }
        globalEventHandlers[eventType] = handler;
        Log($"Registered handler for event type: {eventType}");
    }

    // Remove an event handler
    public void Off(string eventType)
    {
        _ = globalEventHandlers.Remove(eventType);
    }

    // Register an event handler for a specific task and event type
    public void On(TaskType task, string eventType, Action<object> handler)
    {
        Dictionary<string, Action<object>> taskHandlers =
            task == TaskType.PowerStabilization ? powerStabilizationHandlers : nBackHandlers;

        if (taskHandlers.ContainsKey(eventType))
        {
            throw new InvalidOperationException($"An event handler for task '{task}' and event type '{eventType}' is already registered. Only one handler per event type is allowed.");
        }

        taskHandlers[eventType] = handler;
        Log($"Registered handler for task '{task}' and event type: {eventType}");
    }

    // Remove a task-specific event handler
    public void Off(TaskType task, string eventType)
    {
        Dictionary<string, Action<object>> taskHandlers =
            task == TaskType.PowerStabilization ? powerStabilizationHandlers : nBackHandlers;

        _ = taskHandlers.Remove(eventType);
    }

    // Send a message with event type and data
    public void Send(string eventType, object data)
    {
        if (!IsConnected)
        {
            Log($"Cannot send event '{eventType}': Not connected", LogType.Warning);
            return;
        }

        try
        {
            JsonMessage message = new JsonMessage
            {
                type = eventType,
                data = data
            };

            string json = JsonConvert.SerializeObject(message);
            currentConnector.Send(json);
            Log($"Sent event type '{eventType}': {json}");
        }
        catch (Exception e)
        {
            Log($"Error sending message: {e.Message}", LogType.Error);

        }
    }

    // Send a message with task, event type and data
    public void Send(TaskType task, string eventType, object data)
    {
        if (!IsConnected)
        {
            Log($"Cannot send event '{eventType}' for task '{task}': Not connected", LogType.Warning);
            return;
        }

        try
        {
            string taskString = task == TaskType.PowerStabilization ? "powerstabilization" : "nback";

            JsonMessage message = new JsonMessage
            {
                type = eventType,
                task = taskString,
                data = data
            };

            string json = JsonConvert.SerializeObject(message);
            currentConnector.Send(json);
            Log($"Sent event type '{eventType}' for task '{taskString}': {json}");
        }
        catch (Exception e)
        {
            Log($"Error sending message: {e.Message}", LogType.Error);

        }
    }

    // Helper methods for power stabilization task
    public void RegisterPowerStabilizationHandler(string eventType, Action<object> handler)
    {
        On(TaskType.PowerStabilization, eventType, handler);
    }

    public void SendPowerStabilizationEvent(string eventType, object data)
    {
        Send(TaskType.PowerStabilization, eventType, data);
    }

    // Helper methods for n-back task
    public void RegisterNBackHandler(string eventType, Action<object> handler)
    {
        On(TaskType.NBack, eventType, handler);
    }

    public void SendNBackEvent(string eventType, object data)
    {
        Send(TaskType.NBack, eventType, data);
    }



    private async void OnApplicationQuit()
    {
        currentConnector.Disconnect();
        await System.Threading.Tasks.Task.Delay(1000); // Wait for 1 second to ensure disconnection
    }

    // Clear the log display
    public void ClearLog()
    {
        logLines.Clear();
        if (debugText != null)
        {
            debugText.text = "Communication Log:";
        }
    }

    // Custom logging method that logs to both console and TextMeshPro
    private void Log(string message, LogType logType = LogType.Log)
    {
        // Format message with timestamp if needed
        string formattedMessage = addTimestampsToLog
            ? $"[{DateTime.Now:HH:mm:ss}] {message}"
            : message;

        // Log to console based on type
        switch (logType)
        {
            case LogType.Error:
                Debug.LogError(formattedMessage);
                break;
            case LogType.Warning:
                Debug.LogWarning(formattedMessage);
                break;
            default:
                Debug.Log(formattedMessage);
                break;
        }

        // Log to TextMeshPro if available
        if (debugText != null)
        {
            // Add to log lines
            logLines.Add(formattedMessage);

            // Keep only last N lines
            while (logLines.Count > maxLogLines)
            {
                logLines.RemoveAt(0);
            }

            // Update text
            StringBuilder sb = new StringBuilder("Communication Log:\n");
            foreach (var line in logLines)
            {
                _ = sb.AppendLine(line);
            }

            debugText.text = sb.ToString();
        }
    }

}