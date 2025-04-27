using System;
using System.Linq;
using System.Text;

using UnityEngine;


/// <summary>
/// This class provides a interface for connecting to the Meta Quest headset via ADB (Android Debug Bridge).
/// </summary>
public class ADBConnector : MonoBehaviour, IConnector
{
    [Header("Communication Configuration")]
    public bool connectOnStart = true;
    public bool reconnectOnDisconnect = true;
    public float reconnectDelay = 3f;
    [Tooltip("How frequently to check for new messages (in seconds)")]
    public float pollingInterval = 0.1f;
    private float pollingTimer = 0f;
    private bool isReconnecting = false;
    private float reconnectTimer = 0f;
    private const string MESSAGE_PREFIX = "QUEST_MESSAGE:";

    private AndroidJavaObject broadcastReceiver;
    private AndroidJavaObject unityActivity;
    private AndroidJavaObject unityContext;
    private AndroidJavaClass javaReceiverClass;
    private const string INTENT_ACTION = "com.test.SIMPLE_MESSAGE";


    // Connection status events
    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnError;
    public event Action<string> OnMessageReceived;

    public bool IsConnected { get; private set; } = false;

    void Start()
    {
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

        // Poll for incoming messages based on polling interval
        if (IsConnected)
        {
            pollingTimer += Time.deltaTime;
            if (pollingTimer >= pollingInterval)
            {
                CheckForMessages();
                pollingTimer = 0f;
            }
        }
    }

    private void CheckForMessages()
    {
        if (javaReceiverClass != null)
        {
            string lastMessage = javaReceiverClass.CallStatic<string>("getLastMessage");
            if (!string.IsNullOrEmpty(lastMessage))
            {
                Debug.Log($"Received message: {lastMessage}");
                javaReceiverClass.SetStatic("lastMessage", "");

                // Try to decode the message if it's base64 encoded
                string decodedMessage = DecodeBase64IfEncoded(lastMessage);

                // Process the message if it contains valid JSON
                if (decodedMessage.Contains("{") && decodedMessage.Contains("}"))
                {
                    // Extract the JSON part of the message
                    int startIndex = decodedMessage.IndexOf('{');
                    int endIndex = decodedMessage.LastIndexOf('}');
                    if (startIndex >= 0 && endIndex > startIndex)
                    {
                        string jsonPart = decodedMessage.Substring(startIndex, endIndex - startIndex + 1);
                        OnMessageReceived?.Invoke(jsonPart);
                    }
                }
                else
                {
                    Debug.Log($"Invalid message format: {decodedMessage}");
                }
            }
        }
    }

    /// <summary>
    /// Attempts to decode a string from base64.
    /// </summary>
    /// <param name="input">The potentially base64 encoded string</param>
    /// <returns>The decoded string if input was base64, otherwise the original input</returns>
    private string DecodeBase64IfEncoded(string input)
    {
        try
        {
            byte[] data = Convert.FromBase64String(input);
            string decoded = Encoding.UTF8.GetString(data);

            // If the decoded string has reasonable printable characters, it was likely base64
            // Otherwise, return the original input
            if (decoded.Any(c => c > 31 && c < 127)) // Check for printable ASCII
            {
                Debug.Log("Successfully decoded base64 message");
                return decoded;
            }

            throw new FormatException("Decoded string does not contain valid printable characters.");
        }
        catch
        {
            Debug.Log("Input was not base64 encoded, returning original input.");
        }

        return input;
    }


    public void Connect()
    {
        isReconnecting = false;
        Debug.Log("Initializing ADB communication...");

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
            Debug.Log("ADB communication initialized successfully");
            OnConnected?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to initialize ADB communication: {e.Message}");
            OnError?.Invoke(e.Message);

            if (reconnectOnDisconnect)
            {
                isReconnecting = true;
                reconnectTimer = 0f;
                Debug.Log($"Will attempt to reconnect in {reconnectDelay} seconds...");
            }
        }
#else
        Debug.LogWarning("ADB communication is only available on Android devices");
        // Simulate connection for testing in Editor
        IsConnected = true;
        OnConnected?.Invoke();
#endif
    }

    public void Send(string messageContent)
    {
        SendViaADB(messageContent);
    }

    private void SendViaADB(string messageContent)
    {
        // Send the message via Debug.Log with the specific prefix
        Debug.Log($"{MESSAGE_PREFIX}{messageContent}");
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
                    Debug.LogError($"Error unregistering receiver: {e.Message}");
                }
            }
#endif
            IsConnected = false;
            isReconnecting = false;
            Debug.Log("ADB communication disconnected");
            OnDisconnected?.Invoke();
        }
    }
}