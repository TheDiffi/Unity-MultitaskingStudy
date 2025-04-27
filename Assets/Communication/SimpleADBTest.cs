using UnityEngine;

public class SimpleADBTest2 : MonoBehaviour
{
    private AndroidJavaObject broadcastReceiver;
    private AndroidJavaObject unityActivity;
    private AndroidJavaObject unityContext;
    private const string INTENT_ACTION = "com.test.SIMPLE_MESSAGE";

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        unityContext = unityActivity.Call<AndroidJavaObject>("getApplicationContext");

        // Create and register the BroadcastReceiver at runtime
        AndroidJavaObject intentFilter = new AndroidJavaObject("android.content.IntentFilter");
        intentFilter.Call("addAction", INTENT_ACTION);

        broadcastReceiver = new AndroidJavaObject("com.samples.passthroughcamera.SimpleMessageReceiver");

        unityContext.Call<AndroidJavaObject>("registerReceiver", broadcastReceiver, intentFilter);
#endif

        // Start sending test messages automatically every 7 seconds
        InvokeRepeating("SendTestMessage", 0f, 7.0f);
    }

    // Call this from a UI Button's onClick event
    public void SendTestMessage()
    {
        // Send a simple log message that Node.js will pick up
        Debug.Log("QuestTest: HELLO_FROM_QUEST");
    }

    void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass javaClass = new AndroidJavaClass("com.samples.passthroughcamera.SimpleMessageReceiver");
        string lastMessage = javaClass.CallStatic<string>("getLastMessage");
        if (!string.IsNullOrEmpty(lastMessage))
        {
            Debug.Log("Message from adb: " + lastMessage);
            javaClass.SetStatic("lastMessage", "");
        }
#endif
    }

    private void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (unityContext != null && broadcastReceiver != null)
        {
            unityContext.Call("unregisterReceiver", broadcastReceiver);
        }
#endif
    }
}
