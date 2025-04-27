using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

/// <summary>
/// Manages the creation and calibration of spatial anchors based on AprilTag positions.
/// Uses OVRSpatialAnchor for Meta Quest devices according to best practices.
/// </summary>
public class TagSpatialAnchorManager : MonoBehaviour
{
    [SerializeField] private AprilTagPassthroughManager aprilTagManager;

    [Header("Tag Configuration")]
    [SerializeField] private List<TagAnchorPair> tagAnchorPairs = new List<TagAnchorPair>();

    [Header("Calibration Settings")]
    [SerializeField] private int samplesForCalibration = 10;
    [SerializeField] private float sampleDelaySeconds = 0.05f;

    private bool isCalibrating = false;
    private Dictionary<int, OVRSpatialAnchor> tagAnchorMap = new Dictionary<int, OVRSpatialAnchor>();
    private HashSet<System.Guid> persistedAnchorUuids = new HashSet<System.Guid>();
    private System.Action<bool, OVRSpatialAnchor.UnboundAnchor> onLocalizedDelegate;

    /// <summary>
    /// Stores the mapping between an AprilTag ID and its GameObject with a spatial anchor
    /// </summary>
    [System.Serializable]
    public class TagAnchorPair
    {
        public int tagId;
        public GameObject anchorObject;
    }

    private void Awake()
    {
        onLocalizedDelegate = OnAnchorLocalized;
        LoadPersistedAnchorUuids();
    }

    private void Start()
    {
        if (aprilTagManager == null)
        {
            aprilTagManager = FindAnyObjectByType<AprilTagPassthroughManager>();
            if (aprilTagManager == null)
            {
                Debug.LogError("TagSpatialAnchorManager: AprilTagPassthroughManager not found!");
                enabled = false;
                return;
            }
        }

        // Initialize anchor dictionary
        foreach (var pair in tagAnchorPairs)
        {
            if (pair.anchorObject != null)
            {
                // Check if there's already an anchor component
                OVRSpatialAnchor anchor = pair.anchorObject.GetComponent<OVRSpatialAnchor>();
                if (anchor != null)
                {
                    tagAnchorMap[pair.tagId] = anchor;
                }
            }
        }

        // Auto-calibrate on startup after a short delay
        Invoke("CalibrateAllAnchors", 1.0f);
    }

    /// <summary>
    /// Loads persisted anchor UUIDs from PlayerPrefs
    /// </summary>
    private void LoadPersistedAnchorUuids()
    {
        persistedAnchorUuids.Clear();

        string savedUuids = PlayerPrefs.GetString("SavedAnchorUuids", "");
        if (!string.IsNullOrEmpty(savedUuids))
        {
            string[] uuidStrings = savedUuids.Split(',');
            foreach (string uuidStr in uuidStrings)
            {
                if (System.Guid.TryParse(uuidStr, out System.Guid uuid))
                {
                    persistedAnchorUuids.Add(uuid);
                }
            }
        }

        Debug.Log($"Loaded {persistedAnchorUuids.Count} persisted anchor UUIDs");
    }

    /// <summary>
    /// Saves anchor UUIDs to PlayerPrefs for persistence between sessions
    /// </summary>
    private void SavePersistedAnchorUuids()
    {
        string uuidsString = string.Join(",", persistedAnchorUuids);
        PlayerPrefs.SetString("SavedAnchorUuids", uuidsString);
        PlayerPrefs.Save();
        Debug.Log($"Saved {persistedAnchorUuids.Count} anchor UUIDs to PlayerPrefs");
    }

    /// <summary>
    /// Calibrates all configured tag-anchor pairs
    /// </summary>
    public void CalibrateAllAnchors()
    {
        if (isCalibrating)
        {
            Debug.Log("Calibration already in progress. Please wait.");
            return;
        }

        _ = StartCoroutine(CalibrateAllAnchorsCoroutine());
    }

    /// <summary>
    /// Calibrates a specific tag-anchor pair
    /// </summary>
    public void CalibrateAnchor(int tagId)
    {
        if (isCalibrating)
        {
            Debug.Log("Calibration already in progress. Please wait.");
            return;
        }

        // Find the anchor object for this tag ID
        GameObject anchorObject = null;
        foreach (var pair in tagAnchorPairs)
        {
            if (pair.tagId == tagId)
            {
                anchorObject = pair.anchorObject;
                break;
            }
        }

        if (anchorObject == null)
        {
            Debug.LogError($"No anchor object configured for tag ID {tagId}");
            return;
        }

        _ = StartCoroutine(CalibrateAnchorCoroutine(tagId, anchorObject));
    }

    private IEnumerator CalibrateAllAnchorsCoroutine()
    {
        isCalibrating = true;
        Debug.Log("Starting calibration for all anchors...");

        foreach (var pair in tagAnchorPairs)
        {
            if (pair.anchorObject != null)
            {
                yield return CalibrateAnchorCoroutine(pair.tagId, pair.anchorObject);
                // Short delay between calibrating different tags
                yield return new WaitForSeconds(0.2f);
            }
        }

        isCalibrating = false;
        Debug.Log("Calibration complete for all anchors.");
    }

    private IEnumerator CalibrateAnchorCoroutine(int tagId, GameObject anchorObject)
    {
        Debug.Log($"Collecting {samplesForCalibration} samples for tag ID {tagId}...");

        List<Vector3> positionSamples = new List<Vector3>();
        List<Quaternion> rotationSamples = new List<Quaternion>();

        // Collect samples
        int sampleCount = 0;
        while (sampleCount < samplesForCalibration)
        {
            if (aprilTagManager.TryGetTagTransform(tagId, out Vector3 position, out Quaternion rotation))
            {
                positionSamples.Add(position);
                rotationSamples.Add(rotation);
                sampleCount++;
                Debug.Log($"Collected sample {sampleCount}/{samplesForCalibration} for tag ID {tagId}");
            }
            else
            {
                Debug.LogWarning($"Tag ID {tagId} not visible during calibration attempt");
            }

            yield return new WaitForSeconds(sampleDelaySeconds);
        }

        // Calculate average position and rotation
        if (positionSamples.Count > 0)
        {
            Vector3 averagePosition = Vector3.zero;
            foreach (var pos in positionSamples)
            {
                averagePosition += pos;
            }
            averagePosition /= positionSamples.Count;

            // For rotation, we use the last sample as it's difficult to average rotations properly
            Quaternion averageRotation = rotationSamples[^1];

            // Create or update the spatial anchor
            yield return CreateOrUpdateSpatialAnchorCoroutine(tagId, anchorObject, averagePosition, averageRotation);
        }
        else
        {
            Debug.LogError($"Failed to collect any samples for tag ID {tagId}");
        }
    }

    private IEnumerator CreateOrUpdateSpatialAnchorCoroutine(int tagId, GameObject anchorObject, Vector3 position, Quaternion rotation)
    {
        // If there's an existing anchor component, remove it
        OVRSpatialAnchor existingAnchor = anchorObject.GetComponent<OVRSpatialAnchor>();
        if (existingAnchor != null)
        {
            // Remove the UUID from our persisted list if it exists
            if (persistedAnchorUuids.Contains(existingAnchor.Uuid))
            {
                persistedAnchorUuids.Remove(existingAnchor.Uuid);
            }

            // Remove from map
            if (tagAnchorMap.ContainsKey(tagId))
            {
                tagAnchorMap.Remove(tagId);
            }

            Destroy(existingAnchor);
        }

        // Set the position and rotation of the object
        anchorObject.transform.position = position;
        anchorObject.transform.rotation = rotation;

        // Create a new spatial anchor using OVRSpatialAnchor
        OVRSpatialAnchor newAnchor = anchorObject.AddComponent<OVRSpatialAnchor>();

        // Wait for anchor to be created
        yield return new WaitUntil(() => newAnchor.Created);

        if (newAnchor.Created)
        {
            tagAnchorMap[tagId] = newAnchor;

            // Save the anchor asynchronously
            SaveAnchorAsync(newAnchor);

            Debug.Log($"Successfully created spatial anchor for tag ID {tagId} with UUID {newAnchor.Uuid} at position {position}");
        }
        else
        {
            Debug.LogError($"Failed to create spatial anchor for tag ID {tagId}");
        }
    }

    /// <summary>
    /// Saves an anchor asynchronously following best practices
    /// </summary>
    private async void SaveAnchorAsync(OVRSpatialAnchor anchor)
    {
        var result = await anchor.SaveAnchorAsync();
        if (result.Success)
        {
            persistedAnchorUuids.Add(anchor.Uuid);
            SavePersistedAnchorUuids();
            Debug.Log($"Anchor {anchor.Uuid} saved successfully.");
        }
        else
        {
            Debug.LogError($"Anchor {anchor.Uuid} failed to save with error {result.Status}");
        }
    }

    /// <summary>
    /// Loads all saved anchors following the three-step process:
    /// 1. Load unbound anchors
    /// 2. Localize each anchor
    /// 3. Bind each anchor to an OVRSpatialAnchor
    /// </summary>
    public async void LoadAllSavedAnchors()
    {
        if (persistedAnchorUuids.Count == 0)
        {
            Debug.Log("No saved anchors to load");
            return;
        }

        Debug.Log($"Loading {persistedAnchorUuids.Count} saved anchors...");

        // Step 1: Load unbound anchors
        List<OVRSpatialAnchor.UnboundAnchor> unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
        var result = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(persistedAnchorUuids, unboundAnchors);

        if (result.Success)
        {
            Debug.Log($"Successfully loaded {unboundAnchors.Count} unbound anchors");

            // Step 2 & 3: Localize and bind each anchor
            foreach (var unboundAnchor in unboundAnchors)
            {
                // Step 2: Localize the anchor
                unboundAnchor.LocalizeAsync().ContinueWith(onLocalizedDelegate, unboundAnchor);
            }
        }
        else
        {
            Debug.LogError($"Failed to load anchors with error {result.Status}");
        }
    }

    /// <summary>
    /// Called when an unbound anchor is localized
    /// </summary>
    private void OnAnchorLocalized(bool success, OVRSpatialAnchor.UnboundAnchor unboundAnchor)
    {
        if (success)
        {
            Debug.Log($"Successfully localized anchor {unboundAnchor.Uuid}");

            // Find if this anchor belongs to a specific tag
            int? matchingTagId = null;
            foreach (var pair in tagAnchorMap)
            {
                if (pair.Value != null && pair.Value.Uuid == unboundAnchor.Uuid)
                {
                    matchingTagId = pair.Key;
                    break;
                }
            }

            GameObject targetObject = null;

            // Find the matching tag configuration if available
            if (matchingTagId.HasValue)
            {
                foreach (var pair in tagAnchorPairs)
                {
                    if (pair.tagId == matchingTagId.Value)
                    {
                        targetObject = pair.anchorObject;
                        break;
                    }
                }
            }

            // If no matching tag found, create a new GameObject
            if (targetObject == null)
            {
                // Create a new GameObject for this anchor
                targetObject = new GameObject($"Anchor_{unboundAnchor.Uuid}");
            }

            // Step 3: Bind the anchor to an OVRSpatialAnchor component
            var pose = unboundAnchor.Pose;
            targetObject.transform.position = pose.position;
            targetObject.transform.rotation = pose.rotation;

            var spatialAnchor = targetObject.AddComponent<OVRSpatialAnchor>();
            unboundAnchor.BindTo(spatialAnchor);

            Debug.Log($"Bound anchor {unboundAnchor.Uuid} to GameObject {targetObject.name}");

            // Update our dictionary if this is for a known tag
            if (matchingTagId.HasValue)
            {
                tagAnchorMap[matchingTagId.Value] = spatialAnchor;
            }
        }
        else
        {
            Debug.LogError($"Failed to localize anchor {unboundAnchor.Uuid}");
        }
    }

    /// <summary>
    /// Erases all saved anchors from persistent storage
    /// </summary>
    public async void EraseAllSavedAnchors()
    {
        if (persistedAnchorUuids.Count == 0)
        {
            Debug.Log("No saved anchors to erase");
            return;
        }

        Debug.Log($"Erasing {persistedAnchorUuids.Count} saved anchors...");

        var result = await OVRSpatialAnchor.EraseAnchorsAsync(anchors: null, uuids: persistedAnchorUuids);

        if (result.Success)
        {
            persistedAnchorUuids.Clear();
            SavePersistedAnchorUuids();
            Debug.Log("All anchors erased successfully");
        }
        else
        {
            Debug.LogError($"Failed to erase anchors with error {result.Status}");
        }
    }

    /// <summary>
    /// Public method that can be called from UI buttons to trigger calibration
    /// </summary>
    public void OnCalibrateButtonPressed()
    {
        CalibrateAllAnchors();
    }

    /// <summary>
    /// Public method that can be called from UI buttons to load all saved anchors
    /// </summary>
    public void OnLoadAnchorsButtonPressed()
    {
        LoadAllSavedAnchors();
    }

    /// <summary>
    /// Public method that can be called from UI buttons to erase all saved anchors
    /// </summary>
    public void OnEraseAnchorsButtonPressed()
    {
        EraseAllSavedAnchors();
    }
}