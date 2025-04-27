using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using TMPro;
using static IConnector;

/// <summary>
/// This class provides the same interface as NodeJSConnector but uses ADB for communication
/// instead of WebSockets. It can be used as a drop-in replacement.
/// </summary>
public class ADBConnector : MonoBehaviour, IConnector
{
    [Header("Logging")]
    [SerializeField] private bool addTimestampsToLog = true;
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private int maxLogLines = 15;

    private List<string> logLines = new List<string>();

    [Header("Communication Configuration")]
    public bool connectOnStart = true;
    public bool reconnectOnDisconnect = true;
    public float reconnectDelay = 3f;
    public float messagePollInterval = 0.1f; // How frequently to check for incoming messages
    private bool isReconnecting = false;
    private float reconnectTimer = 0f;
    private const string MESSAGE_PREFIX = "QUEST_MESSAGE:";

    private AndroidJavaObject broadcastReceiver;
    private AndroidJavaObject unityActivity;
    private AndroidJavaObject unityContext;
    private AndroidJavaClass javaReceiverClass;
    private const string INTENT_ACTION = "com.test.SIMPLE_MESSAGE";

    [Serializable]
    private class ADBMessage
    {
        public string type;
        public string task;
        public object data;
    }

    // Dictionary to store event handlers - similar to NodeJSConnector
    private Dictionary<string, Action<object>> globalEventHandlers = new Dictionary<string, Action<object>>();
    private Dictionary<string, Action<object>> powerStabilizationHandlers = new Dictionary<string, Action<object>>();
    private Dictionary<string, Action<object>> nBackHandlers = new Dictionary<string, Action<object>>();

    // Connection status events
    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnError;

    public bool IsConnected { get; private set; } = false;

    void Start()
    {
        // Initialize log
        if (debugText != null)
        {
            debugText.text = "ADB Communication Log:";
        }

        if (connectOnStart)
        {
            Connect();
        }
    }

    void Update()
    {
        // Handle reconnection if needed
        if (isReconnecting)
        {
            reconnectTimer += Time.deltaTime;
            if (reconnectTimer >= reconnectDelay)
            {
                reconnectTimer = 0f;
                Connect();
            }
        }

        // Poll for incoming messages
        if (IsConnected)
        {
            CheckForMessages();
        }
    }

    private void CheckForMessages()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (javaReceiverClass != null)
        {
            string lastMessage = javaReceiverClass.CallStatic<string>("getLastMessage");
            if (!string.IsNullOrEmpty(lastMessage))
            {
                Log($"Received: {lastMessage}");
                javaReceiverClass.SetStatic("lastMessage", "");
                
                // Process the message if it contains valid JSON
                if (lastMessage.Contains("{") && lastMessage.Contains("}"))
                {
                    // Extract the JSON part of the message
                    int startIndex = lastMessage.IndexOf('{');
                    int endIndex = lastMessage.LastIndexOf('}');
                    if (startIndex >= 0 && endIndex > startIndex)
                    {
                        string jsonPart = lastMessage.Substring(startIndex, endIndex - startIndex + 1);
                        ProcessMessage(jsonPart);
                    }
                }
                else
                {
                    Log($"Invalid message format: {lastMessage}", LogType.Warning);
                }
            }
        }
#endif
    }

    public void Connect()
    {
        isReconnecting = false;
        Log("Initializing ADB communication...");

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            unityContext = unityActivity.Call<AndroidJavaObject>("getApplicationContext");

            // Create and register the BroadcastReceiver
            AndroidJavaObject intentFilter = new AndroidJavaObject("android.content.IntentFilter");
            intentFilter.Call("addAction", INTENT_ACTION);

            broadcastReceiver = new AndroidJavaObject("com.samples.passthroughcamera.SimpleMessageReceiver");
            unityContext.Call<AndroidJavaObject>("registerReceiver", broadcastReceiver, intentFilter);

            // Get Java class for accessing the lastMessage field
            javaReceiverClass = new AndroidJavaClass("com.samples.passthroughcamera.SimpleMessageReceiver");

            IsConnected = true;
            Log("ADB communication initialized successfully");  
            OnConnected?.Invoke();
        }
        catch (Exception e)
        {
            Log($"Failed to initialize ADB communication: {e.Message}", LogType.Error);
            OnError?.Invoke(e.Message);

            if (reconnectOnDisconnect)
            {
                isReconnecting = true;
                reconnectTimer = 0f;
                Log($"Will attempt to reconnect in {reconnectDelay} seconds...");
            }
        }
#else
        Log("ADB communication is only available on Android devices", LogType.Warning);
        // Simulate connection for testing in Editor
        IsConnected = true;
        OnConnected?.Invoke();
#endif
    }

    // Process received messages and dispatch to appropriate event handlers
    private void ProcessMessage(string messageText)
    {
        try
        {
            ADBMessage message = JsonConvert.DeserializeObject<ADBMessage>(messageText);
            if (message == null || string.IsNullOrEmpty(message.type))
            {
                Log($"Received message with invalid format: {messageText}", LogType.Warning);
                return;
            }

            // Check if we have task-specific handlers for this event
            if (message.task == "powerstabilization" && powerStabilizationHandlers.TryGetValue(message.type, out Action<object> powerHandler))
            {
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
            Log($"Error parsing message: {e.Message}", LogType.Error);
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
            Log($"Cannot send event '{eventType}': ADB communication is not connected", LogType.Warning);
            return;
        }

        try
        {
            ADBMessage message = new ADBMessage
            {
                type = eventType,
                data = data
            };

            string json = JsonConvert.SerializeObject(message);
            SendViaADB(json);
            Log($"Sent event type '{eventType}': {json}");
        }
        catch (Exception e)
        {
            Log($"Error sending message: {e.Message}", LogType.Error);
            OnError?.Invoke(e.Message);
        }
    }

    // Send a message with task, event type and data
    public void Send(TaskType task, string eventType, object data)
    {
        if (!IsConnected)
        {
            Log($"Cannot send event '{eventType}' for task '{task}': ADB communication is not connected", LogType.Warning);
            return;
        }

        try
        {
            string taskString = task == TaskType.PowerStabilization ? "powerstabilization" : "nback";

            ADBMessage message = new ADBMessage
            {
                type = eventType,
                task = taskString,
                data = data
            };

            string json = JsonConvert.SerializeObject(message);
            SendViaADB(json);
            Log($"Sent event type '{eventType}' for task '{taskString}': {json}");
        }
        catch (Exception e)
        {
            Log($"Error sending message: {e.Message}", LogType.Error);
            OnError?.Invoke(e.Message);
        }
    }

    private void SendViaADB(string messageContent)
    {
        // Send the message via Debug.Log with the specific prefix
        Debug.Log($"{MESSAGE_PREFIX}{messageContent}");
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

    public void Disconnect()
    {
        if (IsConnected)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (unityContext != null && broadcastReceiver != null)
            {
                try
                {
                    unityContext.Call("unregisterReceiver", broadcastReceiver);
                }
                catch (Exception e)
                {
                    Log($"Error unregistering receiver: {e.Message}", LogType.Error);
                }
            }
#endif
            IsConnected = false;
            isReconnecting = false;
            Log("ADB communication disconnected");
            OnDisconnected?.Invoke();
        }
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    // Clear the log display
    public void ClearLog()
    {
        logLines.Clear();
        if (debugText != null)
        {
            debugText.text = "ADB Communication Log:";
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
            StringBuilder sb = new StringBuilder("ADB Communication Log:\n");
            foreach (var line in logLines)
            {
                _ = sb.AppendLine(line);
            }

            debugText.text = sb.ToString();
        }
    }
}