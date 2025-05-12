using UnityEngine;
using System.Collections;
using System.Diagnostics; // For Stopwatch
using Debug = UnityEngine.Debug;

/// <summary>
/// InterruptRenderer implements a physical LED-based version of the power stabilization task.
/// It adapts the NeoPixel strip functionality from EmergencyPowerStabilization to work with
/// the InterruptTaskManager class using the same interface as InterruptRendererMock.
/// </summary>
public class InterruptRenderer : MonoBehaviour
{
    [Header("Hardware References")]
    [SerializeField] private NeoPixelStrip neoPixelStrip;
    [SerializeField] private int pixelCount => neoPixelStrip.PixelCount;

    [Header("Zone Configuration")]
    [SerializeField] private float redZoneWidth = 0.4f; // 40% on each side
    [SerializeField] private float greenZoneWidth = 0.2f; // 20% in the center

    [Header("Colors")]
    [SerializeField] private Color redZoneColor = Color.red;
    [SerializeField] private Color greenZoneColor = Color.green;
    [SerializeField] private Color cursorColor = Color.black;

    // Cursor movement
    private float cursorPosition = 0f; // 0 to 1 range (normalized)
    private int cursorDirection = 1; // 1 = right, -1 = left
    private float traversalTimeMs = 1000f;
    private bool isMoving = false;
    private Stopwatch trialStopwatch = new Stopwatch();

    // Reference to the manager
    private InterruptTaskManager taskManager;

    // Coroutine reference for cursor movement
    private Coroutine movementCoroutine;

    // Pre-calculated zone data
    private Color[] zoneColors;
    private int[] pixelZones;
    private int cursorPixelPosition = 0;

    private void Start()
    {
        // Find task manager if not set
        if (taskManager == null)
        {
            taskManager = GetComponent<InterruptTaskManager>();
            if (taskManager == null)
            {
                taskManager = FindFirstObjectByType<InterruptTaskManager>();
            }
        }

        // Validate the NeoPixel strip
        if (neoPixelStrip == null)
        {
            Debug.LogError("NeoPixelStrip component is not assigned!");
            return;
        }

        neoPixelStrip.InitializePixels();

        // Initialize cursor position
        cursorPosition = 0f;

        // Pre-calculate and cache the zone information
        InitializeZones();

    }

    private void InitializeZones()
    {
        // Create arrays to store zone data for each pixel
        zoneColors = new Color[pixelCount];
        pixelZones = new int[pixelCount];

        // Calculate zone boundaries (normalized to 0-1 range)
        float totalWidth = redZoneWidth + greenZoneWidth + redZoneWidth;

        // Normalize zone widths (should sum to 1.0)
        float normalizedRedWidth = redZoneWidth / totalWidth;
        float normalizedGreenWidth = greenZoneWidth / totalWidth;

        float leftRedBoundary = normalizedRedWidth;
        float greenRightBoundary = leftRedBoundary + normalizedGreenWidth;

        // Pre-calculate zone for each pixel position
        for (int i = 0; i < pixelCount; i++)
        {
            // Normalize the position to 0-1 range
            float normalizedPos = (float)i / (pixelCount - 1);

            // Determine the zone
            int zone;
            if (normalizedPos < leftRedBoundary)
            {
                zone = 0; // Left red zone
                zoneColors[i] = redZoneColor;
            }
            else if (normalizedPos < greenRightBoundary)
            {
                zone = 1; // Green zone
                zoneColors[i] = greenZoneColor;
            }
            else
            {
                zone = 2; // Right red zone
                zoneColors[i] = redZoneColor;
            }

            pixelZones[i] = zone;
        }

        // Set initial strip visualization - show the zones
        SetStripZones();
        neoPixelStrip.TurnOffAll();
    }

    /// <summary>
    /// Start a new trial with the specified traversal time
    /// </summary>
    /// <param name="newTraversalTimeMs">Time in milliseconds to traverse the full zone width</param>
    public void StartTrial(int newTraversalTimeMs)
    {
        traversalTimeMs = newTraversalTimeMs;
        cursorPosition = 0f;
        cursorPixelPosition = 0;
        cursorDirection = 1;
        isMoving = true;

        Debug.Log($"[InterruptRenderer] Starting trial with traversal time: {newTraversalTimeMs}ms");

        // Use Stopwatch for precision timing
        trialStopwatch.Reset();
        trialStopwatch.Start();

        // Start cursor movement
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }
        movementCoroutine = StartCoroutine(MoveCursor());
    }

    /// <summary>
    /// Stop the current trial and cursor movement
    /// </summary>
    public void StopTrial()
    {
        isMoving = false;
        trialStopwatch.Stop();

        // Stop movement coroutine
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }

        Debug.Log($"[InterruptRenderer] Trial stopped");
    }

    /// <summary>
    /// Process a button press during a trial
    /// </summary>
    public (int zoneIndex, int accuracy, float responseTime) HandleButtonPress()
    {
        if (!isMoving)
        {
            Debug.LogWarning("[InterruptRenderer] Button pressed but cursor is not moving");
            return (-1, 0, 0f);
        }

        // Get elapsed time in seconds with high precision
        float responseTime = trialStopwatch.ElapsedMilliseconds / 1000f;

        // Calculate which zone the cursor is in
        int zoneIndex = pixelZones[cursorPixelPosition];

        // Calculate accuracy, absolute number of pixels from center 
        int accuracy = CalculateAccuracy(cursorPixelPosition);

        Debug.Log($"[InterruptRenderer] Button pressed: Zone {zoneIndex}, Accuracy {accuracy:F2}, Time {responseTime:F2}s");

        // Stop the trial
        StopTrial();

        // Map our zone indices (0=left red, 1=green, 2=right red) to TaskManager expected values (0=left red, 2=green, 4=right red)
        int mappedZoneIndex = zoneIndex == 0 ? 0 : (zoneIndex == 1 ? 2 : 4);
        return (mappedZoneIndex, accuracy, responseTime);
    }

    /// <summary>
    /// Calculate accuracy, absolute number of pixels from center 
    /// </summary>
    private int CalculateAccuracy(int pixelPosition)
    {
        // Calculate the center pixel position for the green zone
        int centerPixel = Mathf.RoundToInt(pixelCount * (redZoneWidth + greenZoneWidth / 2f) / (redZoneWidth + greenZoneWidth + redZoneWidth));

        // Calculate the absolute difference from the center pixel
        int accuracy = Mathf.Abs(pixelPosition - centerPixel);

        return accuracy;
    }

    /// <summary>
    /// Start debug mode with a fixed, easy-to-test traversal time
    /// </summary>
    public void RunDebug()
    {
        // Use a slower traversal time for easier testing in debug mode
        int debugTraversalTime = 1000; // 3 seconds, significantly slower for easy testing

        cursorPosition = 0f;
        cursorPixelPosition = 0;
        cursorDirection = 1;
        isMoving = true;

        // Use Stopwatch for precision timing
        trialStopwatch.Reset();
        trialStopwatch.Start();

        Debug.Log($"[InterruptRenderer] Starting DEBUG mode with traversal time: {debugTraversalTime}ms");

        // Start cursor movement with debug traversal time
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }
        movementCoroutine = StartCoroutine(MoveCursor(debugTraversalTime));

        // Show the zones
        SetStripZones();
    }

    /// <summary>
    /// Stop debug mode
    /// </summary>
    public void StopDebug()
    {
        isMoving = false;
        trialStopwatch.Stop();

        // Stop movement coroutine
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }

        // Clear the strip
        neoPixelStrip.StopAnimations();

        Debug.Log($"[InterruptRenderer] DEBUG mode stopped");
    }

    /// <summary>
    /// Coroutine to move the cursor back and forth with precise timing
    /// </summary>
    private IEnumerator MoveCursor(int customTraversalTime = 0)
    {
        // Use the custom traversal time if provided, otherwise use the instance variable
        float actualTraversalTime = customTraversalTime > 0 ? customTraversalTime : traversalTimeMs;

        // Calculate the step time in seconds (how long it takes to move one pixel)
        float stepTime = actualTraversalTime / 0.5f / 1000f / (pixelCount - 1);

        // Calculate how many fixed updates we need to wait before moving the cursor
        // This ensures more accurate timing compared to frame-dependent timing
        float timeAccumulator = 0f;

        Debug.Log($"[InterruptRenderer] Starting cursor movement with traversal time: {actualTraversalTime}ms, step time: {stepTime * 1000}ms per pixel");

        while (isMoving)
        {
            timeAccumulator += Time.fixedDeltaTime;

            // If enough time has accumulated, move the cursor
            if (timeAccumulator >= stepTime)
            {
                // Reset accumulator, subtracting exactly the step time to maintain precision
                timeAccumulator -= stepTime;

                // Update cursor position
                cursorPixelPosition += cursorDirection;
                cursorPosition = (float)cursorPixelPosition / (pixelCount - 1);

                // Reverse direction at boundaries
                if (cursorPixelPosition >= pixelCount - 1)
                {
                    cursorPixelPosition = pixelCount - 1;
                    cursorDirection = -1;

                    // Log timestamp when reaching right edge
                    float timeElapsed = trialStopwatch.ElapsedMilliseconds / 1000f;
                    Debug.Log($"[InterruptRenderer] Cursor reached RIGHT edge at {timeElapsed:F3}s");
                }
                else if (cursorPixelPosition <= 0)
                {
                    cursorPixelPosition = 0;
                    cursorDirection = 1;

                    // Log timestamp when reaching left edge
                    float timeElapsed = trialStopwatch.ElapsedMilliseconds / 1000f;
                    Debug.Log($"[InterruptRenderer] Cursor reached LEFT edge at {timeElapsed:F3}s");
                }

                // Update visual representation
                RenderLEDs();
            }

            // Wait for the next fixed update
            yield return new WaitForFixedUpdate();
        }
    }

    /// <summary>
    /// Render the zones and cursor position on the NeoPixel strip
    /// </summary>
    private void RenderLEDs()
    {
        Color[] colors = new Color[pixelCount];
        System.Array.Copy(zoneColors, colors, pixelCount);

        // Draw cursor (3 black pixels if possible)
        if (cursorPixelPosition > 0)
            colors[cursorPixelPosition - 1] = cursorColor;

        colors[cursorPixelPosition] = cursorColor;

        if (cursorPixelPosition < pixelCount - 1)
            colors[cursorPixelPosition + 1] = cursorColor;

        // Update the NeoPixel strip
        neoPixelStrip.SetPixelColors(colors);
    }

    /// <summary>
    /// Display just the zone colors on the strip without cursor
    /// </summary>
    private void SetStripZones()
    {
        neoPixelStrip.SetPixelColors(zoneColors);
    }

    // When the zone configuration changes in the editor, we need to recalculate zones
    private void OnValidate()
    {
        if (Application.isPlaying && zoneColors != null && pixelZones != null)
        {
            InitializeZones();
        }
    }
}