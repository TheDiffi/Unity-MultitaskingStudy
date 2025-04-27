using System;

/// <summary>
/// Common interface for Quest communication connectors.
/// This interface provides a common API for different communication methods
/// with the Meta Quest headset (WebSockets, ADB, etc).
/// </summary>
public interface IConnector
{
    /// <summary>
    /// Enum representing the different task types
    /// </summary>
    public enum TaskType
    {
        PowerStabilization,
        NBack
    }

    /// <summary>
    /// Event fired when the connector successfully connects
    /// </summary>
    event Action OnConnected;

    /// <summary>
    /// Event fired when the connector disconnects
    /// </summary>
    event Action OnDisconnected;

    /// <summary>
    /// Event fired when an error occurs in the connector
    /// </summary>
    event Action<string> OnError;

    /// <summary>
    /// Gets whether the connector is currently connected
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Establishes a connection
    /// </summary>
    void Connect();

    /// <summary>
    /// Terminates the connection
    /// </summary>
    void Disconnect();

    /// <summary>
    /// Registers a global event handler for a specific event type
    /// </summary>
    /// <param name="eventType">The type of event to handle</param>
    /// <param name="handler">The handler function</param>
    void On(string eventType, Action<object> handler);

    /// <summary>
    /// Unregisters a global event handler
    /// </summary>
    /// <param name="eventType">The type of event to remove the handler for</param>
    void Off(string eventType);

    /// <summary>
    /// Registers a task-specific event handler
    /// </summary>
    /// <param name="task">The task this handler is for</param>
    /// <param name="eventType">The type of event to handle</param>
    /// <param name="handler">The handler function</param>
    void On(TaskType task, string eventType, Action<object> handler);

    /// <summary>
    /// Unregisters a task-specific event handler
    /// </summary>
    /// <param name="task">The task this handler is for</param>
    /// <param name="eventType">The type of event to remove the handler for</param>
    void Off(TaskType task, string eventType);

    /// <summary>
    /// Sends a global message with event type and data
    /// </summary>
    /// <param name="eventType">The type of event to send</param>
    /// <param name="data">The data to send</param>
    void Send(string eventType, object data);

    /// <summary>
    /// Sends a task-specific message
    /// </summary>
    /// <param name="task">The task this message is for</param>
    /// <param name="eventType">The type of event to send</param>
    /// <param name="data">The data to send</param>
    void Send(TaskType task, string eventType, object data);

    /// <summary>
    /// Helper method to register a handler for the Power Stabilization task
    /// </summary>
    /// <param name="eventType">The type of event to handle</param>
    /// <param name="handler">The handler function</param>
    void RegisterPowerStabilizationHandler(string eventType, Action<object> handler);

    /// <summary>
    /// Helper method to send an event for the Power Stabilization task
    /// </summary>
    /// <param name="eventType">The type of event to send</param>
    /// <param name="data">The data to send</param>
    void SendPowerStabilizationEvent(string eventType, object data);

    /// <summary>
    /// Helper method to register a handler for the N-Back task
    /// </summary>
    /// <param name="eventType">The type of event to handle</param>
    /// <param name="handler">The handler function</param>
    void RegisterNBackHandler(string eventType, Action<object> handler);

    /// <summary>
    /// Helper method to send an event for the N-Back task
    /// </summary>
    /// <param name="eventType">The type of event to send</param>
    /// <param name="data">The data to send</param>
    void SendNBackEvent(string eventType, object data);

    /// <summary>
    /// Clears the debug log
    /// </summary>
    void ClearLog();
}