# Power Stabilization Task - Implementation Guide

This document provides a detailed implementation guide for the Power Stabilization Task system, including specifics that would help implement the same mechanics in Unity or other platforms.

## System Architecture

The Emergency Power Stabilization Task consists of these key components:

### 1. Hardware/Visual Components

-   **LED Strip/Visual Display**: Represents the power stability zones
-   **Cursor**: A visual indicator that moves across the zones
-   **Input Mechanism**: Button, touch sensor, or any input device

### 2. Core Game Logic

-   **State Machine**: Controls overall task flow
-   **Zone Management**: Defines and manages different zone types and boundaries
-   **Cursor Movement**: Controls cursor traversal with variable speed
-   **Input Detection**: Processes user input with debounce logic

### 3. Data Collection System

-   **Trial Recording**: Tracks individual trial results
-   **Session Management**: Handles overall experiment flow
-   **Performance Metrics**: Calculates accuracy and other performance indicators

## Visual Implementation Details

### Zone Layout

The display is divided into five distinct zones:

```
|<---- Red ----->|<-- Orange -->|<---- Green ---->|<-- Orange -->|<---- Red ----->|
|     Zone 0     |    Zone 1    |     Zone 2      |    Zone 3    |     Zone 4     |
```

Zone proportions (configurable):

-   Red zones: 40% (20% on each side)
-   Orange zones: 0% (can be changed if needed)
-   Green zone: 20% (centered)

### Cursor Implementation

The cursor is implemented as a visual indicator (dark area on the LED strip) that moves across the zones with these properties:

-   **Movement Pattern**: Left to right and back continuously
-   **Speed Calculation**:
    ```
    traversalTimeMs = baseTraversalTimeMs + random(-speedRange, speedRange)
    delayPerStep = traversalTimeMs / (totalSteps * 2)
    ```
    Where `totalSteps` is the width of the display and the multiplication by 2 accounts for the back-and-forth movement
-   **Randomization**: Each trial varies cursor speed by ±20% from base speed

### Color Mapping

Standard colors (easily adaptable for Unity):

-   Red zones: RGB(255, 0, 0)
-   Orange zones: RGB(255, 165, 0)
-   Green zone: RGB(0, 255, 0)
-   Cursor: Black/empty or contrasting color

## Game Logic Implementation

### State Machine

The task operates with the following states:

1. **IDLE**: System waiting for commands
2. **STARTED**: Session started but no active game
3. **INTERRUPT_TRIGGERED**: Emergency triggered, waiting for user acknowledgment
4. **IN_PROGRESS**: Active gameplay (trials)
5. **TEST_MODE**: Debug/demonstration mode

### Trial Management

Each emergency interrupt contains a configurable number of trials:

1. Initialize cursor position to start position (leftmost)
2. Set random traversal speed within bounds
3. Move cursor continuously until user input
4. Process input based on cursor position
5. Record result and display feedback
6. Repeat until trial count reached

### Input Processing

Input should be processed with these considerations:

-   **Debounce Logic**: Prevent multiple rapid inputs (20ms minimum in reference implementation)
-   **Just-Pressed Detection**: Only trigger on the initial press, not continuously while held
-   **Position Recording**: Capture exact cursor position at moment of input

## Data Collection Implementation

### Per-Trial Data

For each trial, collect:

-   **Timestamp**: Time since session start
-   **Zone Hit**: Which zone the cursor was in (0-4)
-   **Accuracy**: Distance from perfect center position
-   **Response Time**: How long the trial took
-   **Success**: Boolean indicating green zone hit

### Session Summary

For each session, calculate:

-   **Total Duration**: Complete time from start to finish
-   **Success Rate**: Percentage of green zone hits
-   **Average Accuracy**: Mean distance from perfect position
-   **Performance Classification**: Based on success rate

## Serial Communication Protocol

For Unity implementation, this protocol can be adapted to internal function calls or network messages:

### Configuration Messages

```
config <studyId>,<sessionNumber>,<traversalTime>,<trialCount>
```

Configures the task with specific parameters:

-   **studyId**: String identifier (max 9 chars)
-   **sessionNumber**: Integer session number
-   **traversalTime**: Integer time in milliseconds for cursor traversal
-   **trialCount**: Integer number of trials per interrupt

### Control Messages

-   `start`: Initialize and begin a session
-   `interrupt`: Trigger an emergency interrupt
-   `exit`: End current task
-   `get_data`: Retrieve collected data

### Data Format

Data is returned in a structured format with two main sections:

1. **Trial Events**: Individual trial results

    ```
    studyId,sessionNumber,timestamp,task_type,event_type,accuracy,speed,zone_correct
    ```

2. **Session Summary**: Overall session performance
    ```
    studyId,sessionNumber,start_time,completion_time,total_duration,average_accuracy,average_speed,total_correct
    ```

## Unity-Specific Implementation Considerations

When implementing in Unity, consider these approaches:

### Visual Representation

-   Use a UI Slider or custom horizontal bar with colored sections
-   Create a moving cursor element with Lerp-based movement
-   Use Unity's Time.deltaTime for smooth movement across different frame rates

### Input Handling

-   Use Input.GetButtonDown() or similar for initial press detection
-   Consider touch input for mobile implementations

### State Management

-   Implement using a State pattern or simple enum-based state machine
-   Use Coroutines for timed sequences and trials

### Data Collection

-   Use ScriptableObjects or a dedicated DataManager for consistent data storage
-   Implement CSV export functionality with System.IO

### Example Unity Code Structure

```csharp
public class PowerStabilizationTask : MonoBehaviour
{
    // Configuration
    [SerializeField] private string studyId = "DEFAULT";
    [SerializeField] private int sessionNumber = 1;
    [SerializeField] private float baseTraversalTime = 1000f; // in ms
    [SerializeField] private int trialCount = 5;

    // Visual elements
    [SerializeField] private RectTransform zoneDisplay;
    [SerializeField] private RectTransform cursor;

    // State tracking
    private enum GameState { Idle, Started, InterruptTriggered, InProgress }
    private GameState currentState = GameState.Idle;

    // Trial management
    private int currentTrial = 0;
    private int successCount = 0;
    private List<TrialResult> trialResults = new List<TrialResult>();

    // Movement
    private float cursorPosition = 0f;
    private int cursorDirection = 1;
    private float traversalTime;
    private float currentSpeed;

    // Start and update methods would implement the core game loop
    // Additional methods for input processing, data collection, etc.
}
```

## Algorithm: Zone Calculation

```
function calculateZone(position):
    // Normalize position to 0-100 scale
    normalizedPos = map(position, 0, displayWidth, 0, 100)

    // Calculate zone boundaries (example with default ratios)
    redZoneWidth = 20
    orangeZoneWidth = 0
    greenZoneWidth = 20

    // Calculate boundaries
    leftRedBoundary = redZoneWidth
    leftOrangeBoundary = leftRedBoundary + orangeZoneWidth
    greenBoundary = leftOrangeBoundary + greenZoneWidth
    rightOrangeBoundary = greenBoundary + orangeZoneWidth

    // Determine zone
    if normalizedPos < leftRedBoundary:
        return 0 // Left red zone
    else if normalizedPos < leftOrangeBoundary:
        return 1 // Left orange zone
    else if normalizedPos < greenBoundary:
        return 2 // Green zone
    else if normalizedPos < rightOrangeBoundary:
        return 3 // Right orange zone
    else:
        return 4 // Right red zone
```

## Algorithm: Cursor Movement

```
function updateCursor(deltaTime):
    // Calculate step size based on traversal time
    stepSize = (displayWidth * 2) / traversalTime * deltaTime

    // Update position
    cursorPosition += cursorDirection * stepSize

    // Reverse direction at boundaries
    if cursorPosition >= displayWidth:
        cursorPosition = displayWidth
        cursorDirection = -1
    else if cursorPosition <= 0:
        cursorPosition = 0
        cursorDirection = 1
```

## Algorithm: Randomize Speed

```
function randomizeSpeed(percentage):
    // Calculate range for randomization
    speedRange = baseTraversalTime * (percentage / 100)

    // Apply random variation within range
    traversalTime = baseTraversalTime + random(-speedRange, speedRange)
```

## Performance Evaluation

The system evaluates user performance based on these criteria:

-   **Excellent**: ≥80% success rate (green zone hits)
-   **Good**: 60-79% success rate
-   **Mediocre**: 40-59% success rate
-   **Poor**: <40% success rate

## Testing Implementation

For proper implementation testing:

1. Verify cursor movement is smooth and consistent
2. Ensure zone boundaries are correctly calculated
3. Test input detection with various timing scenarios
4. Validate data collection with sample trials
5. Test different difficulty levels via traversal time adjustment

This guide provides the essential details needed to implement the Power Stabilization Task in any platform, including Unity, while maintaining functional equivalence with the original hardware implementation.
