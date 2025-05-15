using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Handles physical button press events forwarded from the master program.
/// Attach this to any GameObject that needs to respond to physical button presses.
/// Provides configurable Unity events for "correct", "wrong", and "interrupt" buttons.
/// </summary>
public class PhysicalButtonHandler : MonoBehaviour
{
    // Constants for button types - easily changeable for future modifications
    public static class ButtonTypes
    {
        public const string CORRECT = "confirm";
        public const string WRONG = "wrong";
        public const string INTERRUPT = "interrupt";
    }

    [Header("Configuration")]
    [Tooltip("Reference to the MasterConnector that receives messages from the Node.js controller")]
    [SerializeField] private MasterConnector connector;

    [Tooltip("Debug mode will log all button press events to the console")]
    [SerializeField] private bool debugMode = false;

    [Header("Button Events")]
    [Tooltip("Event triggered when the 'correct' button is pressed")]
    public UnityEvent onCorrectButtonPressed;

    [Tooltip("Event triggered when the 'wrong' button is pressed")]
    public UnityEvent onWrongButtonPressed;

    [Tooltip("Event triggered when the 'interrupt' button is pressed")]
    public UnityEvent onInterruptButtonPressed;

    private bool isConnected = false;

    private void Start()
    {
        if (connector == null)
        {
            Debug.LogError("PhysicalButtonHandler: MasterConnector reference is missing. Please assign it in the Inspector.");
            enabled = false;
            return;
        }

        // Register for the physical-button-press events
        registerEvents();

        if (debugMode)
        {
            Debug.Log($"PhysicalButtonHandler initialized");
        }
    }

    private void registerEvents()
    {
        if (connector != null && !isConnected)
        {
            isConnected = true;
            // Register for the physical-button-press events
            connector.On("physical-button-press", HandlePhysicalButtonPress);
        }
    }

    private void OnDisable()
    {
        if (connector != null)
        {
            // Unregister from events when disabled
            connector.Off("physical-button-press");
            isConnected = false;
        }
    }

    /// <summary>
    /// Handles the physical button press events from the MasterConnector
    /// </summary>
    private void HandlePhysicalButtonPress(object data)
    {
        // Extract the button press data
        Dictionary<string, object> buttonData = data as Dictionary<string, object>;
        if (buttonData == null)
        {
            // Try to handle JObject from Newtonsoft.Json
            if (data is Newtonsoft.Json.Linq.JObject jObject)
            {
                buttonData = jObject.ToObject<Dictionary<string, object>>();
            }

            if (buttonData == null)
            {
                Debug.LogError("PhysicalButtonHandler: Received invalid data format for button press");
                return;
            }
        }

        if (buttonData != null && buttonData.TryGetValue("buttonType", out object buttonTypeObj) && buttonTypeObj is string buttonType)
        {
            if (debugMode)
            {
                Debug.Log($"PhysicalButtonHandler: Received '{buttonType}' type button press");
            }

            // Convert to lowercase for case-insensitive comparison
            string buttonTypeLower = buttonType.ToLower();

            // Use the constants for comparison instead of hardcoded strings
            // TestSendback();
            switch (buttonTypeLower)
            {
                case ButtonTypes.CORRECT:
                    onCorrectButtonPressed?.Invoke();
                    break;
                case ButtonTypes.WRONG:
                    onWrongButtonPressed?.Invoke();
                    break;
                case ButtonTypes.INTERRUPT:
                    onInterruptButtonPressed?.Invoke();
                    break;
                default:
                    Debug.LogWarning($"PhysicalButtonHandler: Unknown button type: {buttonType}");
                    break;
            }
        }
    }

    private void TestSendback()
    {
        var timestamp = System.DateTime.Now.ToString("o");
        connector.Send("button-press-received", $"{{\"timestamp\":\"{timestamp}\"}}");
    }
}