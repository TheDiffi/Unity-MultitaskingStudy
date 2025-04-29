/* using System;
using System.Collections;
using System.Collections.Generic;
using AprilTag;
using UnityEngine;

/// <summary>
/// Manages the creation and calibration of spatial anchors based on AprilTag positions.
/// Uses OVRSpatialAnchor for Meta Quest devices following best practices.
/// </summary>
public class TagSpatialAnchorManager : MonoBehaviour
{
    [SerializeField] private AprilTagPassthroughManager aprilTagManager;

    [Header("Tag Configuration")]
    [SerializeField] private List<TagAnchorPair> tagAnchorPairs = new List<TagAnchorPair>();

    [Header("Calibration Settings")]
    [SerializeField] private int samplesForCalibration = 10;
    [SerializeField] private float sampleDelaySeconds = 0.05f;
    [SerializeField] private float localizationTimeoutSeconds = 10.0f;

    private Dictionary<int, OVRSpatialAnchor> tagAnchorMap = new Dictionary<int, OVRSpatialAnchor>();
    private HashSet<Guid> persistedAnchorUuids = new HashSet<Guid>();
    private bool isCalibrating = false;

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
                OVRSpatialAnchor anchor = pair.anchorObject.GetComponent<OVRSpatialAnchor>();
                if (anchor != null)
                {
                    tagAnchorMap[pair.tagId] = anchor;
                }
            }
        }

        // Auto-load existing anchors or calibrate if none exist
        if (persistedAnchorUuids.Count > 0)
        {
            LoadAllSavedAnchors();
        }
        else
        {
            Invoke("CalibrateAllAnchors", 1.0f);
        }
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
                if (Guid.TryParse(uuidStr, out Guid uuid))
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

        StartCoroutine(CalibrateAllAnchorsCoroutine());
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

        StartCoroutine(CalibrateAnchorCoroutine(tagId, anchorObject));
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

        // Ensure tags are being detected
        aprilTagManager.DetectTags(false);

        List<Vector3> positionSamples = new List<Vector3>();
        List<Quaternion> rotationSamples = new List<Quaternion>();

        // Collect samples
        int sampleCount = 0;
        float timeout = 5.0f; // 5 seconds timeout
        float elapsedTime = 0f;

        while (sampleCount < samplesForCalibration && elapsedTime < timeout)
        {
            if (aprilTagManager.TryGetLastTagTransform(tagId, out TagPose tag))
            {
                positionSamples.Add(tag.Position);
                rotationSamples.Add(tag.Rotation);
                sampleCount++;
                Debug.Log($"Collected sample {sampleCount}/{samplesForCalibration} for tag ID {tagId}");
                elapsedTime = 0; // Reset timeout when we get a valid sample
            }
            else
            {
                elapsedTime += sampleDelaySeconds;
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

            // For rotation, we use the last sample as averaging quaternions is non-trivial
            Quaternion averageRotation = rotationSamples[rotationSamples.Count - 1];

            // Create or update the spatial anchor
            yield return CreateOrUpdateSpatialAnchorAsync(tagId, anchorObject, averagePosition, averageRotation);
        }
        else
        {
            Debug.LogWarning($"Failed to collect any samples for tag ID {tagId}. Please ensure the tag is visible.");
        }
    }

    private IEnumerator CreateOrUpdateSpatialAnchorAsync(int tagId, GameObject anchorObject, Vector3 position, Quaternion rotation)
    {
        // If there's an existing anchor component, we need to erase it first
        OVRSpatialAnchor existingAnchor = anchorObject.GetComponent<OVRSpatialAnchor>();
        if (existingAnchor != null)
        {
            // Remove from persisted UUIDs if present
            if (persistedAnchorUuids.Contains(existingAnchor.Uuid))
            {
                _ = persistedAnchorUuids.Remove(existingAnchor.Uuid);
            }

            // Erase from persistent storage
            if (existingAnchor.Localized)
            {
                var eraseTask = existingAnchor.EraseAnchorAsync();
                yield return new WaitUntil(() => eraseTask.IsCompleted);
            }

            // Remove from our dictionary
            if (tagAnchorMap.ContainsKey(tagId))
            {
                _ = tagAnchorMap.Remove(tagId);
            }

            Destroy(existingAnchor);
        }

        // Set the position and rotation of the object
        anchorObject.transform.position = position;
        anchorObject.transform.rotation = rotation;

        // Create a new spatial anchor
        OVRSpatialAnchor newAnchor = anchorObject.AddComponent<OVRSpatialAnchor>();

        // Wait for anchor to be created and localized
        var localizeTask = newAnchor.WhenLocalizedAsync();
        yield return new WaitUntil(() => localizeTask.IsCompleted || Time.time > localizationTimeoutSeconds);

        if (localizeTask.IsCompleted && localizeTask.Result)
        {
            tagAnchorMap[tagId] = newAnchor;

            // Save the anchor
            var saveTask = newAnchor.SaveAnchorAsync();
            yield return new WaitUntil(() => saveTask.IsCompleted);

            var saveResult = saveTask.Result;
            if (saveResult.Success)
            {
                persistedAnchorUuids.Add(newAnchor.Uuid);
                SavePersistedAnchorUuids();
                Debug.Log($"Successfully created and saved spatial anchor for tag ID {tagId} with UUID {newAnchor.Uuid}");
            }
            else
            {
                Debug.LogError($"Failed to save anchor for tag ID {tagId} with error {saveResult.Status}");
            }
        }
        else
        {
            Debug.LogError($"Failed to create or localize spatial anchor for tag ID {tagId}");
            // Clean up the failed anchor
            if (newAnchor != null)
            {
                Destroy(newAnchor);
            }
        }
    }

    /// <summary>
    /// Loads all saved anchors following the proper three-step process
    /// </summary>
    public async void LoadAllSavedAnchors()
    {
        if (persistedAnchorUuids.Count == 0)
        {
            Debug.Log("No saved anchors to load");
            return;
        }

        Debug.Log($"Loading {persistedAnchorUuids.Count} saved anchors...");

        try
        {
            // Step 1: Load unbound anchors
            List<OVRSpatialAnchor.UnboundAnchor> unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
            var result = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(persistedAnchorUuids, unboundAnchors);

            if (!result.Success)
            {
                Debug.LogError($"Failed to load anchors with error {result.Status}");
                return;
            }

            Debug.Log($"Successfully loaded {unboundAnchors.Count} unbound anchors");

            // Step 2 & 3: Localize and bind each anchor
            foreach (var unboundAnchor in unboundAnchors)
            {
                // Step 2: Try to localize with a timeout
                var localizeTask = unboundAnchor.LocalizeAsync(localizationTimeoutSeconds);
                await localizeTask;

                if (localizeTask.Result)
                {
                    // Step 3: Find matching tag or create new GameObject and bind
                    await BindAnchorToGameObject(unboundAnchor);
                }
                else
                {
                    Debug.LogWarning($"Failed to localize anchor {unboundAnchor.Uuid}. User should look around to improve mapping.");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading anchors: {ex.Message}");
        }
    }

    /// <summary>
    /// Binds an unbound anchor to the appropriate GameObject
    /// </summary>
    private async System.Threading.Tasks.Task BindAnchorToGameObject(OVRSpatialAnchor.UnboundAnchor unboundAnchor)
    {
        // Find if the anchor UUID matches any of our known anchor UUIDs
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

        // Find the correct GameObject for this tag ID
        if (matchingTagId.HasValue)
        {
            foreach (var pair in tagAnchorPairs)
            {
                if (pair.tagId == matchingTagId.Value && pair.anchorObject != null)
                {
                    targetObject = pair.anchorObject;
                    break;
                }
            }
        }

        // If no matching GameObject found, create a new one
        if (targetObject == null)
        {
            targetObject = new GameObject($"Anchor_{unboundAnchor.Uuid.ToString().Substring(0, 8)}");
        }

        // Make sure we have a valid Pose before binding
        if (!unboundAnchor.TryGetPose(out var pose))
        {
            Debug.LogError($"Failed to get pose for anchor {unboundAnchor.Uuid}");
            return;
        }

        // Position the GameObject at the anchor's position
        targetObject.transform.position = pose.position;
        targetObject.transform.rotation = pose.rotation;

        // Clear any existing anchor component
        var existingAnchor = targetObject.GetComponent<OVRSpatialAnchor>();
        if (existingAnchor != null)
        {
            Destroy(existingAnchor);
        }

        // Create a new spatial anchor component and bind the unbound anchor to it
        var spatialAnchor = targetObject.AddComponent<OVRSpatialAnchor>();
        unboundAnchor.BindTo(spatialAnchor);

        Debug.Log($"Successfully bound anchor {unboundAnchor.Uuid} to GameObject {targetObject.name}");

        // Update our dictionary if this is for a known tag
        if (matchingTagId.HasValue)
        {
            tagAnchorMap[matchingTagId.Value] = spatialAnchor;
        }
    }

    /// <summary>
    /// Erases all saved anchors from persistent storage using batch operation
    /// </summary>
    public async void EraseAllSavedAnchors()
    {
        if (persistedAnchorUuids.Count == 0)
        {
            Debug.Log("No saved anchors to erase");
            return;
        }

        Debug.Log($"Erasing {persistedAnchorUuids.Count} saved anchors...");

        try
        {
            // Use batch operation to erase all anchors at once
            var result = await OVRSpatialAnchor.EraseAnchorsAsync(null, persistedAnchorUuids);

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
        catch (Exception ex)
        {
            Debug.LogError($"Error erasing anchors: {ex.Message}");
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
} */