using UnityEngine;
using TMPro;
using System.Collections;
using System;

/// <summary>
/// ButtonTest script containing various functions that can be attached to TextMesh Pro UI buttons.
/// </summary>
public class ButtonTest : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI statusText;
    [Header("Communication")]
    [Tooltip("Set either NodeJSConnector OR ADBConnector, not both")]
    [SerializeField]
    private NodeJSConnector nodeJSConnector;

    [Tooltip("Set either NodeJSConnector OR ADBConnector, not both")]
    [SerializeField]
    private ADBConnector adbConnector;

    // Automatically selects which connector to use (NodeJS has priority if both are set)
    private IConnector currentConnector => (IConnector)adbConnector ?? nodeJSConnector;

    [Header("Configuration")]
    [SerializeField] private float feedbackDuration = 1.5f;

    private void Start()
    {
        // Check if the connector is set and connected
        if (currentConnector == null)
        {
            Debug.LogError("No connector set. Please assign a NodeJS or ADB connector.");
            throw new InvalidOperationException("No connector set. Please assign a NodeJS or ADB connector.");
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
        if (currentConnector != null && currentConnector.IsConnected)
        {
            // Use general Send for general events that don't belong to a specific task
            currentConnector.Send("button_event", "Button was pressed");
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
        if (currentConnector != null && currentConnector.IsConnected)
        {
            currentConnector.SendNBackEvent("trial-complete", "Trial complete");
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
        if (currentConnector != null && currentConnector.IsConnected)
        {
            currentConnector.SendNBackEvent("task-complete", "Task complete");
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
        if (currentConnector != null && currentConnector.IsConnected)
        {
            currentConnector.SendPowerStabilizationEvent("interrupt-complete", "Interrupt complete");
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
        if (currentConnector != null)
        {
            currentConnector.Connect();
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
        if (currentConnector != null && currentConnector.IsConnected)
        {
            currentConnector.Disconnect();
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
        if (currentConnector != null)
        {
            if (currentConnector.IsConnected)
            {
                currentConnector.Disconnect();
                ShowFeedback("Disconnected from Node.js");
            }
            else
            {
                currentConnector.Connect();
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
        if (currentConnector != null && currentConnector.IsConnected)
        {
            currentConnector.Send("command", command);
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
            statusText.text = currentConnector != null && currentConnector.IsConnected ?
                "Connected" : "Disconnected";
            statusText.color = currentConnector != null && currentConnector.IsConnected ?
                Color.green : Color.white;
        }
    }
}