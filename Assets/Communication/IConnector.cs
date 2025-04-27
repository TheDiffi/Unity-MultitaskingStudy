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
    /// Terminates the connection
    /// </summary>
    void Disconnect();

    /// <summary>
    /// Connects to the server
    /// </summary>
    void Connect();

    /// <summary>
    /// Sends a global message with event type and data
    /// </summary>
    /// <param name="eventType">The type of event to send</param>
    /// <param name="data">The data to send</param>
    void Send(string message);

    /// <summary>
    /// Event fired when a message is received from the server
    /// </summary>
    /// <param name="message">The message received</param>
    event Action<string> OnMessageReceived;

}