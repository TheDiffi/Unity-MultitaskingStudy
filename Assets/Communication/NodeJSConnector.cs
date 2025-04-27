using System;
using System.Text;
using UnityEngine;
using NativeWebSocket;


//The websocket implementation is based on the NativeWebSocket library, which is a Unity-compatible WebSocket client for C#.
public class NodeJSConnector : MonoBehaviour, IConnector
{
    [Header("WebSocket Configuration")]
    public string serverAddress = "localhost";
    public int serverPort = 8080;
    public bool connectOnStart = true;
    public bool reconnectOnDisconnect = true;
    public float reconnectDelay = 3f;

    private WebSocket websocket;
    private bool isReconnecting = false;
    private float reconnectTimer = 0f;

    // Connection status events
    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnError;
    public event Action<string> OnMessageReceived;

    public bool IsConnected => websocket != null && websocket.State == WebSocketState.Open;

    void Start()
    {
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
        Debug.Log($"Connecting to WebSocket server at {url}...");

        websocket = new WebSocket(url);

        websocket.OnOpen += () =>
        {
            Debug.Log("Connected to Node.js server");
            OnConnected?.Invoke();
        };

        websocket.OnMessage += (bytes) =>
        {
            string messageText = Encoding.UTF8.GetString(bytes);
            Debug.Log($"Received: {messageText}");
            ProcessMessage(messageText);
        };

        websocket.OnError += (e) =>
        {
            Debug.LogError($"WebSocket Error: {e}");
            OnError?.Invoke(e);
        };

        websocket.OnClose += (code) =>
        {
            Debug.Log($"WebSocket connection closed with code: {code}");
            OnDisconnected?.Invoke();

            if (reconnectOnDisconnect)
            {
                isReconnecting = true;
                reconnectTimer = 0f;
                Debug.Log($"Will attempt to reconnect in {reconnectDelay} seconds...");
            }
        };

        try
        {
            await websocket.Connect();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to WebSocket server: {e.Message}");
            OnError?.Invoke(e.Message);

            if (reconnectOnDisconnect)
            {
                isReconnecting = true;
                reconnectTimer = 0f;
            }
        }
    }

    // Process received messages
    private void ProcessMessage(string messageText)
    {
        OnMessageReceived?.Invoke(messageText);
    }

    // Send a message to the server
    public void Send(string msg)
    {
        _ = websocket.SendText(msg);
    }

    public void Disconnect()
    {
        if (websocket != null)
        {
            isReconnecting = false;
            _ = websocket.Close();
        }
    }
}