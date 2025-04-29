using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AprilTag;

public class AnchorCalibrator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AprilTagPassthroughManager aprilTagManager;

    [Header("Anchor Configuration")]
    [SerializeField] private List<AnchorTagPair> anchorTagPairs = new List<AnchorTagPair>();

    [Header("Calibration Settings")]
    [SerializeField] private int samplesToCollect = 10;
    [SerializeField] private bool autoCalibrate = false;

    // Keep track of active anchors and their mapping to tag IDs
    private Dictionary<int, AnchorData> tagToAnchorMap = new Dictionary<int, AnchorData>();
    private bool isCalibrating = false;

    [System.Serializable]
    public class AnchorTagPair
    {
        public int tagId;
        public GameObject anchorObject;
    }

    private class AnchorData
    {
        public GameObject gameObject;
        public OVRSpatialAnchor anchor;
    }

    private void Start()
    {
        // Find AprilTagManager if not assigned
        if (aprilTagManager == null)
        {
            aprilTagManager = FindAnyObjectByType<AprilTagPassthroughManager>();
            if (aprilTagManager == null)
            {
                Debug.LogError("AnchorCalibrator: AprilTagPassthroughManager not found!");
                enabled = false;
                return;
            }
        }

        // Initialize anchor map
        InitializeAnchors();

        // Auto-calibrate if enabled
        if (autoCalibrate)
        {
            StartCoroutine(CalibrateAnchors());
        }
    }

    private void InitializeAnchors()
    {
        tagToAnchorMap.Clear();

        foreach (var pair in anchorTagPairs)
        {
            if (pair.anchorObject != null)
            {
                // Ensure the GameObject has an OVRSpatialAnchor component
                OVRSpatialAnchor anchor = pair.anchorObject.GetComponent<OVRSpatialAnchor>();
                if (anchor == null)
                {
                    anchor = pair.anchorObject.AddComponent<OVRSpatialAnchor>();
                }

                // Add to mapping
                tagToAnchorMap[pair.tagId] = new AnchorData
                {
                    gameObject = pair.anchorObject,
                    anchor = anchor
                };
            }
        }
    }

    /// <summary>
    /// Starts the calibration process for all anchors
    /// </summary>
    public void CalibrateAllAnchors()
    {
        if (!isCalibrating)
        {
            StartCoroutine(CalibrateAnchors());
        }
        else
        {
            Debug.LogWarning("Calibration already in progress. Please wait.");
        }
    }

    /// <summary>
    /// Calibration coroutine that samples April Tag positions
    /// </summary>
    private IEnumerator CalibrateAnchors()
    {
        isCalibrating = true;
        Debug.Log("Starting anchor calibration...");

        // Wait for AprilTag detection to be ready
        if (aprilTagManager == null)
        {
            Debug.LogError("AprilTagPassthroughManager is not available. Calibration aborted.");
            isCalibrating = false;
            yield break;
        }

        // Wait an initial frame to make sure everything is initialized
        yield return null;

        // Set up data structures to collect samples for all tags
        Dictionary<int, List<Vector3>> positionSamplesMap = new Dictionary<int, List<Vector3>>();
        Dictionary<int, List<Quaternion>> rotationSamplesMap = new Dictionary<int, List<Quaternion>>();

        // Initialize collections for each tag we need to calibrate
        foreach (var pair in tagToAnchorMap)
        {
            positionSamplesMap[pair.Key] = new List<Vector3>();
            rotationSamplesMap[pair.Key] = new List<Quaternion>();
        }

        // Collect multiple samples for all tags simultaneously
        Debug.Log($"Collecting {samplesToCollect} samples for all visible tags...");
        for (int i = 0; i < samplesToCollect; i++)
        {
            // Get poses for all tags in a single call
            Dictionary<int, TagPose> allPoses = aprilTagManager.DetectTags(false);
            if (allPoses != null)
            {
                // Process all visible tags we're interested in
                foreach (var tagId in tagToAnchorMap.Keys)
                {
                    if (allPoses.TryGetValue(tagId, out TagPose pose))
                    {
                        // Add sample to appropriate collections
                        positionSamplesMap[tagId].Add(pose.Position);
                        rotationSamplesMap[tagId].Add(pose.Rotation);
                    }
                }
            }
        }

        // Process the collected samples for each tag
        foreach (var pair in tagToAnchorMap)
        {
            int tagId = pair.Key;
            AnchorData anchorData = pair.Value;

            List<Vector3> positionSamples = positionSamplesMap[tagId];
            List<Quaternion> rotationSamples = rotationSamplesMap[tagId];

            // Check if we have sufficient samples for this tag
            if (positionSamples.Count > 0)
            {
                // Calculate average position
                Vector3 avgPosition = Vector3.zero;
                foreach (var position in positionSamples)
                {
                    avgPosition += position;
                }
                avgPosition /= positionSamples.Count;

                // Calculate average rotation (simple approach)
                Quaternion avgRotation = rotationSamples[0];
                for (int i = 1; i < rotationSamples.Count; i++)
                {
                    avgRotation = Quaternion.Slerp(avgRotation, rotationSamples[i], 1.0f / (i + 1));
                }

                // Update the anchor's transform
                UpdateAnchorTransform(anchorData, avgPosition, avgRotation);

                Debug.Log($"Calibrated anchor for tag {tagId} at position {avgPosition} with {positionSamples.Count} samples");
            }
            else
            {
                Debug.LogWarning($"No samples collected for tag {tagId}. Tag may not be visible during calibration.");
            }
        }

        Debug.Log("Anchor calibration completed.");
        isCalibrating = false;
    }

    /// <summary>
    /// Updates the transform of the spatial anchor
    /// </summary>
    private void UpdateAnchorTransform(AnchorData anchorData, Vector3 position, Quaternion rotation)
    {
        if (anchorData == null || anchorData.gameObject == null)
            return;

        // Set the new transform
        anchorData.gameObject.transform.position = position;
        anchorData.gameObject.transform.rotation = rotation;
    }

    /// <summary>
    /// Saves all anchors to persistent storage
    /// </summary>
    public async void SaveAllAnchors()
    {
        List<OVRSpatialAnchor> anchorsToSave = new List<OVRSpatialAnchor>();

        foreach (var pair in tagToAnchorMap)
        {
            if (pair.Value?.anchor != null && pair.Value.anchor.Created)
            {
                anchorsToSave.Add(pair.Value.anchor);
            }
        }

        if (anchorsToSave.Count > 0)
        {
            var result = await OVRSpatialAnchor.SaveAnchorsAsync(anchorsToSave);
            if (result.Success)
            {
                Debug.Log($"Successfully saved {anchorsToSave.Count} anchors");
            }
            else
            {
                Debug.LogError($"Failed to save anchors: {result.Status}");
            }
        }
        else
        {
            Debug.LogWarning("No valid anchors to save");
        }
    }

    /// <summary>
    /// Calibrates a specific anchor by tag ID
    /// </summary>
    public void CalibrateAnchorByTagId(int tagId)
    {
        if (!tagToAnchorMap.ContainsKey(tagId))
        {
            Debug.LogError($"No anchor configured for tag ID {tagId}");
            return;
        }

        if (!isCalibrating)
        {
            StartCoroutine(CalibrateSpecificAnchor(tagId));
        }
        else
        {
            Debug.LogWarning("Calibration already in progress. Please wait.");
        }
    }

    /// <summary>
    /// Calibrates a specific anchor
    /// </summary>
    private IEnumerator CalibrateSpecificAnchor(int tagId)
    {
        isCalibrating = true;
        Debug.Log($"Starting calibration for tag {tagId}...");

        if (!tagToAnchorMap.TryGetValue(tagId, out AnchorData anchorData))
        {
            Debug.LogError($"No anchor data found for tag {tagId}");
            isCalibrating = false;
            yield break;
        }

        List<Vector3> positionSamples = new List<Vector3>();
        List<Quaternion> rotationSamples = new List<Quaternion>();

        // Collect multiple samples
        for (int i = 0; i < samplesToCollect; i++)
        {
            var allPoses = aprilTagManager.DetectTags(false);
            if (allPoses != null && allPoses.TryGetValue(tagId, out TagPose pose))
            {
                // Add sample
                positionSamples.Add(pose.Position);
                rotationSamples.Add(pose.Rotation);
            }
            else
            {
                i--; // Retry this sample
                yield return null;
            }
        }

        // Calculate averages and update
        if (positionSamples.Count > 0)
        {
            Vector3 avgPosition = Vector3.zero;
            foreach (var position in positionSamples)
            {
                avgPosition += position;
            }
            avgPosition /= positionSamples.Count;

            // Calculate average rotation
            Quaternion avgRotation = rotationSamples[0];
            for (int i = 1; i < rotationSamples.Count; i++)
            {
                avgRotation = Quaternion.Slerp(avgRotation, rotationSamples[i], 1.0f / (i + 1));
            }

            UpdateAnchorTransform(anchorData, avgPosition, avgRotation);
            Debug.Log($"Calibration completed for tag {tagId}");
        }
        else
        {
            Debug.LogWarning($"No samples collected for tag {tagId}. Calibration failed.");
        }

        isCalibrating = false;
    }
}