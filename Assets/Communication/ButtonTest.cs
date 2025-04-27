using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// ButtonTest script containing various functions that can be attached to TextMesh Pro UI buttons.
/// </summary>
public class ButtonTest : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private IConnector nodeJSConnector;

    [Header("Configuration")]
    [SerializeField] private float feedbackDuration = 1.5f;

    private void Start()
    {
        // Initialize references if needed
        if (nodeJSConnector == null)
        {
            nodeJSConnector = FindFirstObjectByType<ADBConnector>();
        }

        if (statusText != null)
        {
            statusText.text = "Ready";
        }
    }

    /// <summary>
    /// Simple button handler that displays a debug message.
    /// Attach this to button click events.
    /// </summary>
    public void OnButtonClick()
    {
        Debug.Log("Button clicked!");
        ShowFeedback("Button clicked!");
    }

    /// <summary>
    /// Sends a test message to the Node.js server.
    /// Attach this to button click events.
    /// </summary>
    public void SendTestMessage()
    {
        if (nodeJSConnector != null && nodeJSConnector.IsConnected)
        {
            // Use general Send for general events that don't belong to a specific task
            nodeJSConnector.Send("button_event", "Button was pressed");
            ShowFeedback("Message sent to Node.js");
        }
        else
        {
            Debug.LogWarning("Cannot send message: Node.js connector not found or not connected");
            ShowFeedback("Error: Not connected to Node.js", Color.red);
        }
    }

    /// <summary>
    /// Sends a test message to the Node.js server.
    /// Attach this to button click events.
    /// </summary>
    public void SendTrialComplete()
    {
        if (nodeJSConnector != null && nodeJSConnector.IsConnected)
        {
            nodeJSConnector.SendNBackEvent("trial-complete", "Trial complete");
            ShowFeedback("Message sent to Node.js");
        }
        else
        {
            Debug.LogWarning("Cannot send message: Node.js connector not found or not connected");
            ShowFeedback("Error: Not connected to Node.js", Color.red);
        }
    }

    /// <summary>
    /// Sends a test message to the Node.js server.
    /// Attach this to button click events.
    /// </summary>
    public void SendTaskComplete()
    {
        if (nodeJSConnector != null && nodeJSConnector.IsConnected)
        {
            nodeJSConnector.SendNBackEvent("task-complete", "Task complete");
            ShowFeedback("Message sent to Node.js");
        }
        else
        {
            Debug.LogWarning("Cannot send message: Node.js connector not found or not connected");
            ShowFeedback("Error: Not connected to Node.js", Color.red);
        }
    }

    /// <summary>
    /// Sends a test message to the Node.js server.
    /// Attach this to button click events.
    /// </summary>
    public void SendInterruptComplete()
    {
        if (nodeJSConnector != null && nodeJSConnector.IsConnected)
        {
            nodeJSConnector.SendPowerStabilizationEvent("interrupt-complete", "Interrupt complete");
            ShowFeedback("Message sent to Node.js");
        }
        else
        {
            Debug.LogWarning("Cannot send message: Node.js connector not found or not connected");
            ShowFeedback("Error: Not connected to Node.js", Color.red);
        }
    }

    /// <summary>
    /// Connects to the Node.js server.
    /// Attach this to button click events.
    /// </summary>
    public void ConnectToNodeJS()
    {
        if (nodeJSConnector != null)
        {
            nodeJSConnector.Connect();
            ShowFeedback("Connecting to Node.js...", Color.yellow);
        }
        else
        {
            Debug.LogError("IConnector component not found");
            ShowFeedback("Error: IConnector not found", Color.red);
        }
    }

    /// <summary>
    /// Disconnects from the Node.js server.
    /// Attach this to button click events.
    /// </summary>
    public void DisconnectFromNodeJS()
    {
        if (nodeJSConnector != null && nodeJSConnector.IsConnected)
        {
            nodeJSConnector.Disconnect();
            ShowFeedback("Disconnected from Node.js");
        }
        else
        {
            ShowFeedback("Already disconnected");
        }
    }

    /// <summary>
    /// Toggles the connection to the Node.js server.
    /// Attach this to button click events.
    /// </summary>
    public void ToggleConnection()
    {
        if (nodeJSConnector != null)
        {
            if (nodeJSConnector.IsConnected)
            {
                nodeJSConnector.Disconnect();
                ShowFeedback("Disconnected from Node.js");
            }
            else
            {
                nodeJSConnector.Connect();
                ShowFeedback("Connecting to Node.js...", Color.yellow);
            }
        }
        else
        {
            Debug.LogError("IConnector component not found");
            ShowFeedback("Error: IConnector not found", Color.red);
        }
    }

    /// <summary>
    /// Example of sending a specific command to Node.js.
    /// Attach this to button click events.
    /// </summary>
    public void SendCommand(string command)
    {
        if (nodeJSConnector != null && nodeJSConnector.IsConnected)
        {
            nodeJSConnector.Send("command", command);
            ShowFeedback($"Sent command: {command}");
        }
        else
        {
            Debug.LogWarning("Cannot send command: Not connected to Node.js");
            ShowFeedback("Error: Not connected to Node.js", Color.red);
        }
    }

    /// <summary>
    /// Shows feedback in the status text if available.
    /// </summary>
    private void ShowFeedback(string message, Color? color = null)
    {
        if (statusText != null)
        {
            statusText.text = message;

            statusText.color = color.HasValue ? color.Value : Color.white;

            // Reset the text after a delay
            _ = StartCoroutine(ResetStatusText());
        }
    }

    /// <summary>
    /// Coroutine to reset the status text after a delay.
    /// </summary>
    private IEnumerator ResetStatusText()
    {
        yield return new WaitForSeconds(feedbackDuration);

        if (statusText != null)
        {
            statusText.text = nodeJSConnector != null && nodeJSConnector.IsConnected ?
                "Connected" : "Disconnected";
            statusText.color = nodeJSConnector != null && nodeJSConnector.IsConnected ?
                Color.green : Color.white;
        }
    }
}