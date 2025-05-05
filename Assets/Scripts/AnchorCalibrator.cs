using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using AprilTag;
using Meta.XR.BuildingBlocks;
using Meta.XR.ImmersiveDebugger;

public class AnchorCalibrator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AprilTagPassthroughManager aprilTagManager;
    [SerializeField] private SpatialAnchorCoreBuildingBlock anchorBuildingBlock = null;

    [Header("Anchor Configuration")]
    [SerializeField] private List<AnchorTagPair> anchorTagPairs = new List<AnchorTagPair>();

    [Header("Calibration Settings")]
    [SerializeField] private int samplesToCollect = 10;
    [SerializeField] private bool autoCalibrate = false;
    [DebugMember(Tweakable = true, Min = 0.1f, Max = 2.0f, Category = "Anchors")]

    // Keep track of active anchors and their mapping to tag IDs
    private Dictionary<int, AnchorData> tagToAnchorMap = new Dictionary<int, AnchorData>();
    private Dictionary<int, Guid> tagToAnchorUuidMap = new Dictionary<int, Guid>();
    private bool isCalibrating = false;

    [Serializable]
    public class AnchorTagPair
    {
        public int tagId;
        public GameObject anchorPrefab;
    }

    private class AnchorData
    {
        public GameObject gameObject;
        public Guid anchorUuid;
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

        // Find SpatialAnchorCoreBuildingBlock if not assigned
        if (anchorBuildingBlock == null)
        {
            anchorBuildingBlock = FindAnyObjectByType<SpatialAnchorCoreBuildingBlock>();
            if (anchorBuildingBlock == null)
            {
                Debug.LogError("AnchorCalibrator: SpatialAnchorCoreBuildingBlock not found!");
                enabled = false;
                return;
            }
        }

        // Subscribe to anchor events
        SubscribeToAnchorEvents();

        // Auto-calibrate if enabled
        if (autoCalibrate)
        {
            CalibrateAllAnchors();
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events when this component is destroyed
        UnsubscribeFromAnchorEvents();
    }

    /// <summary>
    /// Subscribe to the SpatialAnchorCoreBuildingBlock events using UnityEvent's AddListener method
    /// </summary>
    private void SubscribeToAnchorEvents()
    {
        if (anchorBuildingBlock != null)
        {
            anchorBuildingBlock.OnAnchorCreateCompleted.AddListener(HandleAnchorCreateCompleted);
            anchorBuildingBlock.OnAnchorsLoadCompleted.AddListener(HandleAnchorsLoadCompleted);
            anchorBuildingBlock.OnAnchorsEraseAllCompleted.AddListener(HandleAnchorsEraseAllCompleted);
            anchorBuildingBlock.OnAnchorEraseCompleted.AddListener(HandleAnchorEraseCompleted);
        }
    }

    /// <summary>
    /// Unsubscribe from the SpatialAnchorCoreBuildingBlock events
    /// </summary>
    private void UnsubscribeFromAnchorEvents()
    {
        if (anchorBuildingBlock != null)
        {
            anchorBuildingBlock.OnAnchorCreateCompleted.RemoveListener(HandleAnchorCreateCompleted);
            anchorBuildingBlock.OnAnchorsLoadCompleted.RemoveListener(HandleAnchorsLoadCompleted);
            anchorBuildingBlock.OnAnchorsEraseAllCompleted.RemoveListener(HandleAnchorsEraseAllCompleted);
            anchorBuildingBlock.OnAnchorEraseCompleted.RemoveListener(HandleAnchorEraseCompleted);
        }
    }

    /// <summary>
    /// Called when an anchor is created successfully - matches the UnityEvent delegate signature
    /// </summary>
    private void HandleAnchorCreateCompleted(OVRSpatialAnchor anchor, OVRSpatialAnchor.OperationResult result)
    {
        if (result == OVRSpatialAnchor.OperationResult.Success)
        {
            Debug.Log($"Anchor created successfully with UUID: {anchor.Uuid}");

            // Find which tag this anchor belongs to based on the GameObject
            int associatedTagId = -1;

            // First, try to find the tag ID based on pending anchors
            foreach (var pair in tagToAnchorMap)
            {
                if ((pair.Value.gameObject == null || pair.Value.gameObject == anchor.gameObject) &&
                    pair.Value.anchorUuid == Guid.Empty)
                {
                    pair.Value.gameObject = anchor.gameObject;
                    pair.Value.anchorUuid = anchor.Uuid;
                    tagToAnchorUuidMap[pair.Key] = anchor.Uuid;
                    associatedTagId = pair.Key;
                    break;
                }
            }

            if (associatedTagId != -1)
            {
                Debug.Log($"Associated anchor with tag ID: {associatedTagId}");
            }
            else
            {
                Debug.LogWarning($"Could not determine which tag ID is associated with this anchor: {anchor.Uuid}");
            }
        }
        else
        {
            Debug.LogError($"Failed to create anchor: {result}");
        }
    }

    /// <summary>
    /// Called when anchors are loaded successfully - matches the UnityEvent delegate signature
    /// </summary>
    private void HandleAnchorsLoadCompleted(List<OVRSpatialAnchor> anchors)
    {
        Debug.Log($"Loaded {anchors.Count} anchors successfully");

        // Update our local object references for the loaded anchors
        foreach (var anchor in anchors)
        {
            // Find the tag ID for this UUID
            int tagId = -1;
            foreach (var pair in tagToAnchorUuidMap)
            {
                if (pair.Value == anchor.Uuid)
                {
                    tagId = pair.Key;
                    break;
                }
            }

            if (tagId != -1)
            {
                // Update the reference in our mapping
                if (tagToAnchorMap.TryGetValue(tagId, out AnchorData anchorData))
                {
                    anchorData.gameObject = anchor.gameObject;
                }
                else
                {
                    // Create a new mapping entry
                    tagToAnchorMap[tagId] = new AnchorData
                    {
                        gameObject = anchor.gameObject,
                        anchorUuid = anchor.Uuid
                    };
                }

                Debug.Log($"Loaded anchor for tag {tagId} with UUID: {anchor.Uuid}");
            }
        }
    }

    /// <summary>
    /// Called when all anchors are erased - matches the UnityEvent delegate signature
    /// </summary>
    private void HandleAnchorsEraseAllCompleted(OVRSpatialAnchor.OperationResult result)
    {
        if (result == OVRSpatialAnchor.OperationResult.Success)
        {
            Debug.Log("All anchors erased successfully");
            tagToAnchorUuidMap.Clear();

            // Clear UUID references but keep game object references
            foreach (var pair in tagToAnchorMap)
            {
                pair.Value.anchorUuid = Guid.Empty;
            }
        }
        else
        {
            Debug.LogError($"Failed to erase all anchors: {result}");
        }
    }

    /// <summary>
    /// Called when a specific anchor is erased - matches the UnityEvent delegate signature
    /// </summary>
    private void HandleAnchorEraseCompleted(OVRSpatialAnchor anchor, OVRSpatialAnchor.OperationResult result)
    {
        if (result == OVRSpatialAnchor.OperationResult.Success)
        {
            Debug.Log($"Anchor {anchor.Uuid} erased successfully");

            // Remove this UUID from our maps
            int tagId = -1;
            foreach (var pair in tagToAnchorUuidMap)
            {
                if (pair.Value == anchor.Uuid)
                {
                    tagId = pair.Key;
                    break;
                }
            }

            if (tagId != -1)
            {
                _ = tagToAnchorUuidMap.Remove(tagId);

                // Clear UUID reference but keep game object reference
                if (tagToAnchorMap.TryGetValue(tagId, out AnchorData anchorData))
                {
                    anchorData.anchorUuid = Guid.Empty;
                }
            }
        }
        else
        {
            Debug.LogError($"Failed to erase anchor {anchor.Uuid}: {result}");
        }
    }

    /// <summary>
    /// Starts the calibration process for all anchors
    /// </summary>
    public void CalibrateAllAnchors()
    {
        if (!isCalibrating)
        {
            Debug.Log("Starting calibration for all anchors...");
            _ = StartCoroutine(CalibrateAnchors());
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
        foreach (var pair in anchorTagPairs)
        {
            positionSamplesMap[pair.tagId] = new List<Vector3>();
            rotationSamplesMap[pair.tagId] = new List<Quaternion>();
        }

        // Collect multiple samples for all tags simultaneously
        Debug.Log($"Collecting {samplesToCollect} samples for all visible tags...");
        for (int i = 0; i < samplesToCollect; i++)
        {
            // Get poses for all tags in a single call
            Dictionary<int, TagPose> allPoses = aprilTagManager.DetectTags(true);

            if (allPoses != null)
            {
                // Process all visible tags we're interested in
                foreach (var pair in anchorTagPairs)
                {
                    int tagId = pair.tagId;
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
        foreach (var pair in anchorTagPairs)
        {
            int tagId = pair.tagId;
            GameObject prefab = pair.anchorPrefab;

            if (prefab == null)
            {
                Debug.LogWarning($"No prefab specified for tag ID {tagId}. Skipping calibration.");
                continue;
            }

            List<Vector3> positionSamples = positionSamplesMap[tagId];
            List<Quaternion> rotationSamples = rotationSamplesMap[tagId];

            // Check if we have sufficient samples for this tag
            if (positionSamples.Count > 0)
            {
                /*  // Calculate average position
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
                 } */

                // Use the building block to create/update the anchor
                CreateOrUpdateSpatialAnchor(tagId, prefab, positionSamples[1], rotationSamples[1]);

                //Debug.Log($"Calibrated anchor for tag {tagId} at position {avgPosition} with {positionSamples.Count} samples");
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
    /// Creates or updates a spatial anchor using the building block
    /// </summary>
    public void CreateOrUpdateSpatialAnchor(int tagId, GameObject prefab, Vector3 position, Quaternion rotation)
    {
        // Check if we already have an instance for this tag
        GameObject existingAnchorObject = null;
        OVRSpatialAnchor existingAnchor = null;

        // Check if we already have an anchor for this tag ID in our tracking dictionary
        if (tagToAnchorMap.TryGetValue(tagId, out AnchorData anchorData) && anchorData.gameObject != null)
        {
            existingAnchorObject = anchorData.gameObject;
            existingAnchor = existingAnchorObject.GetComponent<OVRSpatialAnchor>();
        }

        // If there's an existing anchor, destroy it first
        if (existingAnchorObject != null && existingAnchor != null)
        {
            Debug.Log($"Destroying existing anchor for tag {tagId} with UUID {existingAnchor.Uuid}");

            // Remove from our maps
            Guid anchorUuid = existingAnchor.Uuid;
            if (tagToAnchorUuidMap.ContainsKey(tagId))
            {
                tagToAnchorUuidMap.Remove(tagId);
            }

            // Make sure we have valid objects before destroying
            if (existingAnchorObject != null)
            {
                // Destroy the GameObject with the anchor
                Destroy(existingAnchorObject);
            }

            // Also erase from storage if it was saved - only if we have a valid UUID
            if (anchorUuid != Guid.Empty)
            {
                StartCoroutine(EraseAnchorAsync(anchorUuid));
            }
        }

        // Create a new anchor at the desired position and rotation
        Debug.Log($"Creating new anchor for tag {tagId} at position {position}");
        anchorBuildingBlock.InstantiateSpatialAnchor(prefab, position, rotation);

        // Store a placeholder until the create complete event fires
        tagToAnchorMap[tagId] = new AnchorData
        {
            gameObject = null,  // Will be set in the callback
            anchorUuid = Guid.Empty // Will be set in the callback
        };
    }

    /// <summary>
    /// Erases a specific anchor by UUID asynchronously
    /// </summary>
    private IEnumerator EraseAnchorAsync(Guid uuid)
    {
        if (uuid != Guid.Empty)
        {
            bool eraseSuccessful = false;
            try
            {
                // Use the building block to erase the anchor instead of direct API
                anchorBuildingBlock.EraseAnchorByUuid(uuid);
                eraseSuccessful = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error erasing anchor with UUID {uuid}: {ex.Message}");
            }

            // Wait a short time to allow the erase operation to complete
            if (eraseSuccessful)
            {
                yield return new WaitForSeconds(0.1f);
                Debug.Log($"Erased anchor with UUID: {uuid}");
            }
        }
    }

    /// <summary>
    /// Loads all saved anchors that are mapped to tags
    /// </summary>
    public void LoadAllAnchors()
    {
        List<Guid> uuidsToLoad = new List<Guid>();
        Dictionary<Guid, int> uuidToTagMap = new Dictionary<Guid, int>();
        Dictionary<int, GameObject> tagToPrefabMap = new Dictionary<int, GameObject>();

        // Gather all UUIDs to load and map prefabs to UUIDs
        foreach (var pair in tagToAnchorUuidMap)
        {
            int tagId = pair.Key;
            Guid uuid = pair.Value;

            if (uuid != Guid.Empty)
            {
                uuidsToLoad.Add(uuid);
                uuidToTagMap[uuid] = tagId;

                // Find the prefab for this tag
                foreach (var anchorPair in anchorTagPairs)
                {
                    if (anchorPair.tagId == tagId && anchorPair.anchorPrefab != null)
                    {
                        tagToPrefabMap[tagId] = anchorPair.anchorPrefab;
                        break;
                    }
                }
            }
        }

        if (uuidsToLoad.Count > 0)
        {
            // Use the building block to load all the anchors at once
            foreach (int tagId in tagToPrefabMap.Keys)
            {
                if (tagToAnchorUuidMap.TryGetValue(tagId, out Guid uuid) &&
                    tagToPrefabMap.TryGetValue(tagId, out GameObject prefab))
                {
                    // Use the building block to load and instantiate this anchor
                    List<Guid> singleUuidList = new List<Guid> { uuid };
                    anchorBuildingBlock.LoadAndInstantiateAnchors(prefab, singleUuidList);
                }
            }
        }
        else
        {
            Debug.LogWarning("No saved anchor UUIDs to load");
        }
    }

    /// <summary>
    /// Erases all saved spatial anchors
    /// </summary>
    public void EraseAllAnchors()
    {
        anchorBuildingBlock.EraseAllAnchors();
        // The tagToAnchorUuidMap will be cleared in the HandleAnchorsEraseAllCompleted callback
    }

    /// <summary>
    /// Calibrates a specific anchor by tag ID
    /// </summary>
    public void CalibrateAnchorByTagId(int tagId)
    {
        // Check if this tag ID exists in our configuration
        bool tagFound = false;
        foreach (var pair in anchorTagPairs)
        {
            if (pair.tagId == tagId)
            {
                tagFound = true;
                break;
            }
        }

        if (!tagFound)
        {
            Debug.LogError($"No anchor configured for tag ID {tagId}");
            return;
        }

        if (!isCalibrating)
        {
            _ = StartCoroutine(CalibrateSpecificAnchor(tagId));
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

        // Find the prefab for this tag
        GameObject prefab = null;
        foreach (var pair in anchorTagPairs)
        {
            if (pair.tagId == tagId)
            {
                prefab = pair.anchorPrefab;
                break;
            }
        }

        if (prefab == null)
        {
            Debug.LogError($"No prefab found for tag {tagId}");
            isCalibrating = false;
            yield break;
        }

        List<Vector3> positionSamples = new List<Vector3>();
        List<Quaternion> rotationSamples = new List<Quaternion>();

        // Collect multiple samples
        int sampleCount = 0;
        while (sampleCount < samplesToCollect)
        {
            var allPoses = aprilTagManager.DetectTags(false);
            if (allPoses != null && allPoses.TryGetValue(tagId, out TagPose pose))
            {
                // Add sample
                positionSamples.Add(pose.Position);
                rotationSamples.Add(pose.Rotation);
                sampleCount++;
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

            // Create or update the spatial anchor using the building block
            CreateOrUpdateSpatialAnchor(tagId, prefab, avgPosition, avgRotation);

            Debug.Log($"Calibration completed for tag {tagId}");
        }
        else
        {
            Debug.LogWarning($"No samples collected for tag {tagId}. Calibration failed.");
        }

        isCalibrating = false;
    }
}