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

    [Serializable]
    private class NodeJSMessage
    {
        public string type;
        public object data;
    }

    // Dictionary to store event handlers
    private Dictionary<string, List<Action<object>>> eventHandlers = new Dictionary<string, List<Action<object>>>();

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

            // Trigger registered handlers for this event type
            if (eventHandlers.TryGetValue(message.type, out List<Action<object>> handlers))
            {
                foreach (var handler in handlers)
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
        if (!eventHandlers.TryGetValue(eventType, out List<Action<object>> handlers))
        {
            handlers = new List<Action<object>>();
            eventHandlers[eventType] = handlers;
        }

        handlers.Add(handler);
        Log($"Registered handler for event type: {eventType}");
    }

    // Remove an event handler
    public void Off(string eventType, Action<object> handler)
    {
        if (eventHandlers.TryGetValue(eventType, out List<Action<object>> handlers))
        {
            _ = handlers.Remove(handler);
            if (handlers.Count == 0)
            {
                _ = eventHandlers.Remove(eventType);
            }
        }
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