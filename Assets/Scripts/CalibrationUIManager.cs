/* using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages UI for the tag spatial anchor calibration and provides buttons for additional
/// spatial anchor operations like loading and erasing anchors.
/// </summary>
public class CalibrationUIManager : MonoBehaviour
{
    [SerializeField] private TagSpatialAnchorManager anchorManager;

    [Header("UI References")]
    [SerializeField] private Button calibrateButton;
    [SerializeField] private Button loadAnchorsButton;
    [SerializeField] private Button eraseAnchorsButton;
    [SerializeField] private TextMeshProUGUI statusText;

    private void Start()
    {
        if (anchorManager == null)
        {
            anchorManager = FindAnyObjectByType<TagSpatialAnchorManager>();
            if (anchorManager == null)
            {
                Debug.LogError("CalibrationUIManager: TagSpatialAnchorManager not found!");
                enabled = false;
                return;
            }
        }

        // Set up button listeners
        if (calibrateButton != null)
        {
            calibrateButton.onClick.AddListener(OnCalibrateButtonPressed);
        }

        if (loadAnchorsButton != null)
        {
            loadAnchorsButton.onClick.AddListener(OnLoadAnchorsButtonPressed);
        }

        if (eraseAnchorsButton != null)
        {
            eraseAnchorsButton.onClick.AddListener(OnEraseAnchorsButtonPressed);
        }

        if (statusText != null)
        {
            statusText.text = "Ready";
        }
    }

    /// <summary>
    /// Called when the calibrate button is pressed
    /// </summary>
    public void OnCalibrateButtonPressed()
    {
        if (statusText != null)
        {
            statusText.text = "Calibrating anchors...";

            // Reset the text after calibration completes (typical calibration takes 2-3 seconds)
            _ = StartCoroutine(ResetStatusAfterDelay("Calibration complete", 3.0f));
        }

        anchorManager.OnCalibrateButtonPressed();
    }

    /// <summary>
    /// Called when the load anchors button is pressed
    /// </summary>
    public void OnLoadAnchorsButtonPressed()
    {
        if (statusText != null)
        {
            statusText.text = "Loading saved anchors...";

            // Reset the text after loading completes
            _ = StartCoroutine(ResetStatusAfterDelay("Anchors loaded", 2.0f));
        }

        anchorManager.OnLoadAnchorsButtonPressed();
    }

    /// <summary>
    /// Called when the erase anchors button is pressed
    /// </summary>
    public void OnEraseAnchorsButtonPressed()
    {
        if (statusText != null)
        {
            statusText.text = "Erasing saved anchors...";

            // Reset the text after erasing completes
            _ = StartCoroutine(ResetStatusAfterDelay("Anchors erased", 2.0f));
        }

        anchorManager.OnEraseAnchorsButtonPressed();
    }

    private IEnumerator ResetStatusAfterDelay(string completionMessage, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (statusText != null)
        {
            statusText.text = completionMessage;

            // After another short delay, reset to ready
            yield return new WaitForSeconds(2.0f);
            statusText.text = "Ready";
        }
    }
} */