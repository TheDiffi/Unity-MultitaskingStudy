using UnityEngine;

public class DebugEvents : MonoBehaviour
{

    public void onButtonClick()
    {
        Debug.Log("Button clicked!");
    }

    public void LogDebugMessage(string message)
    {
        Debug.Log(message);
    }
}
