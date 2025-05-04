using System.Diagnostics;
using UnityEngine;
using System;

public static class SessionStopwatch
{
    private static bool isRunning = false;
    public static string sessionStartTimeISO { get; private set; } = string.Empty;
    public static DateTime sessionStartDateTime { get; private set; }
    public static Stopwatch get { get; } = new Stopwatch();

    public static void StartSession()
    {
        if (!isRunning)
        {
            get.Stop();
            get.Reset();
            get.Start();

            // Store both the string representation and actual DateTime object
            sessionStartDateTime = DateTime.Now;
            sessionStartTimeISO = sessionStartDateTime.ToString("HH:mm:ss.fffZ");
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

    /// <summary>
    /// Converts elapsed milliseconds since session start to a local DateTime
    /// </summary>
    /// <param name="elapsedMs">Elapsed milliseconds since session start</param>
    /// <returns>The local DateTime corresponding to the elapsed time</returns>
    public static DateTime ElapsedToLocalTime(long elapsedMs)
    {
        return sessionStartDateTime.AddMilliseconds(elapsedMs);
    }
}


