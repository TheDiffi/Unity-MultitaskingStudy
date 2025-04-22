using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using NativeWebSocket;
using Newtonsoft.Json;
using TMPro;

public class NodeJSConnector : MonoBehaviour
{
    [Header("Logging")]
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private int maxLogLines = 15;
    [SerializeField] private bool logTimestamps = true;

    private List<string> logLines = new List<string>();

    [Header("WebSocket Configuration")]
    public string serverAddress = "localhost";
    public int serverPort = 8080;
    public bool connectOnStart = true;
    public bool reconnectOnDisconnect = true;
    public float reconnectDelay = 3f;

    private WebSocket websocket;
    private bool isReconnecting = false;
    private float reconnectTimer = 0f;

    public enum TaskType
    {
        PowerStabilization,
        NBack
    }

    [Serializable]
    private class NodeJSMessage
    {
        public string type;
        public string task;
        public object data;
    }

    // Dictionary to store event handlers - changed to store single actions instead of lists
    private Dictionary<string, Action<object>> globalEventHandlers = new Dictionary<string, Action<object>>();

    // Dedicated dictionaries for the two specific tasks - changed to store single actions instead of lists
    private Dictionary<string, Action<object>> powerStabilizationHandlers = new Dictionary<string, Action<object>>();
    private Dictionary<string, Action<object>> nBackHandlers = new Dictionary<string, Action<object>>();

    // Connection status events
    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnError;

    public bool IsConnected => websocket != null && websocket.State == WebSocketState.Open;

    void Start()
    {
        // Initialize log
        if (debugText != null)
        {
            debugText.text = "WebSocket Log:";
        }

        if (connectOnStart)
        {
            Connect();
        }
    }

    void Update()
    {
        if (websocket != null)
        {
            // Required to process WebSocket events
            websocket.DispatchMessageQueue();
        }

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
    }


    public async void Connect()
    {
        if (websocket != null)
        {
            // Close existing connection if any
            await websocket.Close();
        }

        isReconnecting = false;
        string url = $"ws://{serverAddress}:{serverPort}";
        Log($"Connecting to WebSocket server at {url}...");

        websocket = new WebSocket(url);

        websocket.OnOpen += () =>
        {
            Log("Connected to Node.js server");
            OnConnected?.Invoke();
        };

        websocket.OnMessage += (bytes) =>
        {
            string messageText = Encoding.UTF8.GetString(bytes);
            Log($"Received: {messageText}");
            ProcessMessage(messageText);
        };

        websocket.OnError += (e) =>
        {
            Log($"WebSocket Error: {e}", LogType.Error);
            OnError?.Invoke(e);
        };

        websocket.OnClose += (code) =>
        {
            Log($"WebSocket connection closed with code: {code}");
            OnDisconnected?.Invoke();

            if (reconnectOnDisconnect)
            {
                isReconnecting = true;
                reconnectTimer = 0f;
                Log($"Will attempt to reconnect in {reconnectDelay} seconds...");
            }
        };

        try
        {
            await websocket.Connect();
        }
        catch (Exception e)
        {
            Log($"Failed to connect to WebSocket server: {e.Message}", LogType.Error);
            OnError?.Invoke(e.Message);

            if (reconnectOnDisconnect)
            {
                isReconnecting = true;
                reconnectTimer = 0f;
            }
        }
    }

    // Process received messages and dispatch to appropriate event handlers
    private void ProcessMessage(string messageText)
    {
        try
        {
            NodeJSMessage message = JsonConvert.DeserializeObject<NodeJSMessage>(messageText);
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
            Log($"Cannot send event '{eventType}': WebSocket is not connected", LogType.Warning);
            return;
        }

        try
        {
            NodeJSMessage message = new NodeJSMessage
            {
                type = eventType,
                data = data
            };

            string json = JsonConvert.SerializeObject(message);
            _ = websocket.SendText(json);
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
            Log($"Cannot send event '{eventType}' for task '{task}': WebSocket is not connected", LogType.Warning);
            return;
        }

        try
        {
            string taskString = task == TaskType.PowerStabilization ? "powerstabilization" : "nback";

            NodeJSMessage message = new NodeJSMessage
            {
                type = eventType,
                task = taskString,
                data = data
            };

            string json = JsonConvert.SerializeObject(message);
            _ = websocket.SendText(json);
            Log($"Sent event type '{eventType}' for task '{taskString}': {json}");
        }
        catch (Exception e)
        {
            Log($"Error sending message: {e.Message}", LogType.Error);
            OnError?.Invoke(e.Message);
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

    public void Disconnect()
    {
        if (websocket != null)
        {
            isReconnecting = false;
            _ = websocket.Close();
        }
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null)
        {
            await websocket.Close();
        }
    }

    // Clear the log display
    public void ClearLog()
    {
        logLines.Clear();
        if (debugText != null)
        {
            debugText.text = "WebSocket Log:";
        }
    }

    // Custom logging method that logs to both console and TextMeshPro
    private void Log(string message, LogType logType = LogType.Log)
    {
        // Format message with timestamp if needed
        string formattedMessage = logTimestamps
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
            StringBuilder sb = new StringBuilder("WebSocket Log:\n");
            foreach (var line in logLines)
            {
                _ = sb.AppendLine(line);
            }

            debugText.text = sb.ToString();
        }
    }

}