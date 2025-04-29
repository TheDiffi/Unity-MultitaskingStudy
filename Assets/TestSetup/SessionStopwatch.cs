using System.Diagnostics;
using UnityEngine;

public static class SessionStopwatch
{
    private static bool isRunning = false;
    public static string sessionStartTime { get; private set; } = string.Empty;
    public static Stopwatch get { get; } = new Stopwatch();

    public static void StartSession()
    {
        if (!isRunning)
        {
            get.Stop();
            get.Reset();
            get.Start();
            //get iso time
            sessionStartTime = System.DateTime.UtcNow.ToString("HH:mm:ss.fffZ");
            isRunning = true;
        }
        else
        {
            UnityEngine.Debug.LogWarning("Session is already running. Use StopSession() to stop it before starting a new one.");
        }
    }

    public static void StopSession()
    {
        if (isRunning)
        {
            get.Stop();
            isRunning = false;
        }
    }
}


