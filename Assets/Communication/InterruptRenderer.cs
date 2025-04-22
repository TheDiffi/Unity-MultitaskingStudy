using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// InterruptRenderer is a placeholder implementation that simulates the power stabilization task's
/// visual elements and cursor movement according to the implementation guide.
/// This is a mock version that can be replaced with a full implementation later.
/// </summary>
public class InterruptRenderer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider zoneSlider; // Optional UI slider to visualize the zones
    [SerializeField] private Image cursorImage; // Optional UI image to show cursor position

    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugVisuals = true;
    [SerializeField] private Color redZoneColor = Color.red;
    [SerializeField] private Color orangeZoneColor = new Color(1f, 0.65f, 0f); // Orange
    [SerializeField] private Color greenZoneColor = Color.green;
    [SerializeField] private Color cursorColor = Color.black;

    // Zone configuration
    private float redZoneWidth = 0.2f; // 20% on each side
    private float orangeZoneWidth = 0.15f; // 15% on each side
    private float greenZoneWidth = 0.3f; // 30% in the center

    // Cursor movement
    private float cursorPosition = 0f; // 0 to 1 range
    private int cursorDirection = 1; // 1 = right, -1 = left
    private float traversalTimeMs = 1000f;
    private bool isMoving = false;
    private float trialStartTime;

    // Reference to the manager
    private InterruptTaskManager taskManager;

    // Coroutine reference for cursor movement
    private Coroutine movementCoroutine;

    private void Awake()
    {
        // Find task manager if not set
        if (taskManager == null)
        {
            taskManager = GetComponent<InterruptTaskManager>();
            if (taskManager == null)
            {
                taskManager = FindObjectOfType<InterruptTaskManager>();
            }
        }

        // Initialize cursor position
        cursorPosition = 0f;
    }

    /// <summary>
    /// Start a new trial with the specified traversal time
    /// </summary>
    /// <param name="traversalTimeMs">Time in milliseconds to traverse the full zone width</param>
    public void StartTrial(int traversalTimeMs)
    {
        this.traversalTimeMs = traversalTimeMs;
        cursorPosition = 0f;
        cursorDirection = 1;
        isMoving = true;
        trialStartTime = Time.time;

        Debug.Log($"[InterruptRenderer] Starting trial with traversal time: {traversalTimeMs}ms");

        // Start cursor movement
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }
        movementCoroutine = StartCoroutine(MoveCursor());

        // Update visualization
        UpdateDebugVisuals();
    }

    /// <summary>
    /// Stop the current trial and cursor movement
    /// </summary>
    public void StopTrial()
    {
        isMoving = false;

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
    public void HandleButtonPress()
    {
        if (!isMoving)
        {
            Debug.LogWarning("[InterruptRenderer] Button pressed but cursor is not moving");
            return;
        }

        // Calculate which zone the cursor is in
        int zoneIndex = CalculateZone(cursorPosition);

        // Calculate accuracy (0.0 = perfect center, 1.0 = edge of display)
        float accuracy = CalculateAccuracy(cursorPosition);

        // Calculate response time
        float responseTime = Time.time - trialStartTime;

        Debug.Log($"[InterruptRenderer] Button pressed: Zone {zoneIndex}, Accuracy {accuracy:F2}, Time {responseTime:F2}s");

        // Stop the trial
        StopTrial();

        // Notify task manager of trial completion
        if (taskManager != null)
        {
            taskManager.OnTrialComplete(zoneIndex, accuracy, responseTime);
        }
        else
        {
            Debug.LogError("[InterruptRenderer] Cannot report trial completion: Task manager not found");
        }
    }

    /// <summary>
    /// Determine which zone the cursor is in
    /// </summary>
    private int CalculateZone(float position)
    {
        // Calculate zone boundaries
        float leftRedBoundary = redZoneWidth;
        float leftOrangeBoundary = leftRedBoundary + orangeZoneWidth;
        float greenLeftBoundary = leftOrangeBoundary;
        float greenRightBoundary = greenLeftBoundary + greenZoneWidth;
        float rightOrangeBoundary = greenRightBoundary + orangeZoneWidth;

        // Determine zone
        if (position < leftRedBoundary)
        {
            return 0; // Left red zone
        }
        else if (position < leftOrangeBoundary)
        {
            return 1; // Left orange zone
        }
        else if (position < greenRightBoundary)
        {
            return 2; // Green zone
        }
        else if (position < rightOrangeBoundary)
        {
            return 3; // Right orange zone
        }
        else
        {
            return 4; // Right red zone
        }
    }

    /// <summary>
    /// Calculate accuracy based on distance from perfect center
    /// </summary>
    private float CalculateAccuracy(float position)
    {
        // Calculate center position (0.5 for a normalized 0-1 range)
        float centerPosition = 0.5f;

        // Calculate normalized distance from center (0 = perfect, 0.5 = edge)
        float distanceFromCenter = Mathf.Abs(position - centerPosition);

        // Convert to accuracy (1.0 = perfect, 0.0 = edge)
        return 1.0f - (distanceFromCenter * 2.0f);
    }

    /// <summary>
    /// Start debug mode with a fixed, easy-to-test traversal time
    /// </summary>
    public void RunDebug()
    {
        // Use a slower traversal time for easier testing in debug mode
        int debugTraversalTime = 3000; // 3 seconds, significantly slower for easy testing

        cursorPosition = 0f;
        cursorDirection = 1;
        isMoving = true;
        trialStartTime = Time.time;

        Debug.Log($"[InterruptRenderer] Starting DEBUG mode with traversal time: {debugTraversalTime}ms");

        // Start cursor movement with debug traversal time
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }
        movementCoroutine = StartCoroutine(MoveCursor(debugTraversalTime));

        // Update visualization
        UpdateDebugVisuals();
    }

    /// <summary>
    /// Stop debug mode
    /// </summary>
    public void StopDebug()
    {
        isMoving = false;

        // Stop movement coroutine
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }

        Debug.Log($"[InterruptRenderer] DEBUG mode stopped");
    }

    /// <summary>
    /// Coroutine to move the cursor back and forth
    /// </summary>
    private IEnumerator MoveCursor(int customTraversalTime = 0)
    {
        // Use the custom traversal time if provided, otherwise use the instance variable
        float actualTraversalTime = customTraversalTime > 0 ? customTraversalTime : this.traversalTimeMs;

        while (isMoving)
        {
            // Calculate step size based on traversal time
            float stepSize = (1.0f / (actualTraversalTime / 1000.0f)) * Time.deltaTime;

            // Update position
            cursorPosition += cursorDirection * stepSize;

            // Reverse direction at boundaries
            if (cursorPosition >= 1.0f)
            {
                cursorPosition = 1.0f;
                cursorDirection = -1;
            }
            else if (cursorPosition <= 0.0f)
            {
                cursorPosition = 0.0f;
                cursorDirection = 1;
            }

            // Update visuals
            UpdateDebugVisuals();

            yield return null;
        }
    }

    /// <summary>
    /// Update debug visualization elements
    /// </summary>
    private void UpdateDebugVisuals()
    {
        if (!showDebugVisuals)
            return;

        // Update slider position if available
        if (zoneSlider != null)
        {
            zoneSlider.value = cursorPosition;
        }

        // Update cursor position if available
        if (cursorImage != null)
        {
            RectTransform rt = cursorImage.rectTransform;
            Vector2 anchorPos = rt.anchorMin;
            anchorPos.x = cursorPosition;
            rt.anchorMin = anchorPos;
            rt.anchorMax = new Vector2(anchorPos.x, rt.anchorMax.y);
        }
    }

    /// <summary>
    /// Draw debug zones in the scene view
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showDebugVisuals)
            return;

        // Only draw if we have a reference point
        if (transform == null)
            return;

        Vector3 position = transform.position;
        float width = 2.0f; // Arbitrary width for visualization
        float height = 0.2f;

        // Calculate zone positions
        float leftRedEnd = redZoneWidth * width;
        float leftOrangeEnd = (redZoneWidth + orangeZoneWidth) * width;
        float greenEnd = (redZoneWidth + orangeZoneWidth + greenZoneWidth) * width;
        float rightOrangeEnd = (redZoneWidth + orangeZoneWidth + greenZoneWidth + orangeZoneWidth) * width;

        // Draw red zones
        Gizmos.color = redZoneColor;
        Gizmos.DrawCube(position + new Vector3(leftRedEnd / 2, 0, 0), new Vector3(leftRedEnd, height, 0.1f));
        Gizmos.DrawCube(position + new Vector3(width - (width - rightOrangeEnd) / 2, 0, 0),
                        new Vector3(width - rightOrangeEnd, height, 0.1f));

        // Draw orange zones
        Gizmos.color = orangeZoneColor;
        Gizmos.DrawCube(position + new Vector3(leftRedEnd + (leftOrangeEnd - leftRedEnd) / 2, 0, 0),
                        new Vector3(leftOrangeEnd - leftRedEnd, height, 0.1f));
        Gizmos.DrawCube(position + new Vector3(greenEnd + (rightOrangeEnd - greenEnd) / 2, 0, 0),
                        new Vector3(rightOrangeEnd - greenEnd, height, 0.1f));

        // Draw green zone
        Gizmos.color = greenZoneColor;
        Gizmos.DrawCube(position + new Vector3(leftOrangeEnd + (greenEnd - leftOrangeEnd) / 2, 0, 0),
                        new Vector3(greenEnd - leftOrangeEnd, height, 0.1f));

        // Draw cursor
        Gizmos.color = cursorColor;
        float cursorWidth = 0.05f;
        Gizmos.DrawCube(position + new Vector3(cursorPosition * width, 0, 0), new Vector3(cursorWidth, height * 1.2f, 0.15f));
    }
}