using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PassthroughCameraSamples;
using Meta.XR.ImmersiveDebugger;
using AprilTag;


public class AprilTagPassthroughManager : MonoBehaviour
{
    [Serializable]
    public class MarkerGameObjectPair
    {
        [Tooltip("The unique ID of the AprilTag marker")]
        public int markerId;

        [Tooltip("The GameObject to position at the detected tag location")]
        public GameObject gameObject;
    }

    [Header("Camera Setup")]
    [Tooltip("Reference to the WebCamTextureManager component")]
    [SerializeField] private WebCamTextureManager m_webCamTextureManager;
    [Tooltip("Transform that represents the camera position in the scene")]
    [SerializeField] private Transform m_cameraAnchor;
    private PassthroughCameraEye CameraEye => m_webCamTextureManager.Eye;
    private Vector2Int CameraResolution => m_webCamTextureManager.RequestedResolution;

    [Space(10)]
    [Header("Visual Debugging")]
    [Tooltip("Whether to draw debug visuals for detected tags")]
    [SerializeField] private bool m_drawBoundingBox = true;
    [Tooltip("Material used for drawing tag bounding boxes")]
    [SerializeField] Material m_boundingBoxMaterial = null;
    TagDrawer m_drawer;

    [Space(10)]
    [Header("Tag Detection Settings")]
    [Tooltip("Decimation factor for the detector (higher = faster but less accurate)")]
    [SerializeField] private int m_decimation = 4;
    [Tooltip("Physical size of the tag in millimeters")]
    [SerializeField]
    [DebugMember(Tweakable = true, Min = 24f, Max = 28f, Category = "AprilTag")]
    private float m_tagSize_mm = 26.5f;

    [Space(10)]
    [Header("Object Placement - Basic")]
    [Tooltip("Enable automatic placement of objects at detected tag positions")]
    public bool m_autoplaceObjects = true;
    [Tooltip("List of marker IDs mapped to their corresponding GameObjects")]
    [SerializeField] private List<MarkerGameObjectPair> m_markerGameObjectPairs = new List<MarkerGameObjectPair>();

    [Space(10)]
    [Header("Object Placement - Smoothing")]
    [Tooltip("Enable position smoothing to reduce jitter")]
    [DebugMember(Tweakable = true, Min = 0.1f, Max = 2.0f, Category = "AprilTag")]
    [SerializeField] private bool m_enableSmoothing = false;
    [Tooltip("Smoothing factor (higher = more smoothing but more latency)")]
    [SerializeField, Range(0.0f, 0.95f)] private float m_smoothingFactor = 0.8f;

    [Space(10)]
    [Header("Object Placement - Slow Mode")]
    [Tooltip("Enable slow-placing mode that updates positions at a reduced rate")]
    [SerializeField] private bool m_enableSlowPlacing = false;
    [Tooltip("How often to update positions in seconds (0.5 = twice per second)")]
    [SerializeField, Range(0.1f, 2.0f)] private float m_slowPlacingUpdateRate = 0.5f;
    [Tooltip("How quickly objects move to their target positions")]
    [SerializeField, Range(1.0f, 20.0f)] private float m_slowPlacingInterpolationSpeed = 5f;

    private TagDetector m_tagDetector;
    private Dictionary<int, GameObject> m_markerGameObjectDictionary = new Dictionary<int, GameObject>();
    private Dictionary<int, TagPose> m_prevPoseDataDictionary = new Dictionary<int, TagPose>();
    private Dictionary<int, bool> m_tagVisibilityStatus = new Dictionary<int, bool>();

    // Slow-placing target positions and rotations
    private Dictionary<int, Vector3> m_targetPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Quaternion> m_targetRotations = new Dictionary<int, Quaternion>();
    private Coroutine m_slowPlacingCoroutine;

    // Camera parameters
    private float m_focalLengthX, m_focalLengthY;
    private float m_cx, m_cy;

    // State flags
    private bool m_isReady = false;
    private bool m_isDetecting { get; set; } = false;

    public bool IsTagVisible(int tagId)
    {
        return m_tagVisibilityStatus.ContainsKey(tagId) && m_tagVisibilityStatus[tagId];
    }

    public bool TryGetLastTagTransform(int tagId, out TagPose pose)
    {
        if (m_prevPoseDataDictionary.TryGetValue(tagId, out TagPose tagPose) && IsTagVisible(tagId))
        {
            pose = MovePoseToCameraCoordiantes(tagPose);
            return true;
        }

        pose = new TagPose(tagId, Vector3.zero, Quaternion.identity);
        return false;
    }


    private IEnumerator Start()
    {
        Debug.Log("[AprilTag] Starting initialization process...");

        // Validate required components
        if (m_webCamTextureManager == null)
        {
            Debug.LogError($"[AprilTag] {nameof(m_webCamTextureManager)} field is required for this component to operate properly");
            enabled = false;
            yield break;
        }

        Debug.Log("[AprilTag] Waiting for camera permissions...");
        yield return WaitForCameraPermission();

        Debug.Log("[AprilTag] Initializing camera...");
        yield return InitializeCamera();

        Debug.Log("[AprilTag] Initializing tag detection system...");
        InitializeTagDetection();
        Debug.Log("[AprilTag] Initialization complete. System is ready.");
    }

    /// <summary>
    /// Waits until camera permission is granted.
    /// </summary>
    private IEnumerator WaitForCameraPermission()
    {
        while (PassthroughCameraPermissions.HasCameraPermission != true)
        {
            yield return null;
        }
    }

    /// <summary>
    /// Initializes the camera with appropriate resolution and waits until ready.
    /// </summary>
    private IEnumerator InitializeCamera()
    {
        var possibleResolutions = PassthroughCameraUtils.GetOutputSizes(CameraEye);
        m_webCamTextureManager.RequestedResolution = possibleResolutions[1];
        m_webCamTextureManager.enabled = true;

        while (m_webCamTextureManager.WebCamTexture == null)
        {
            yield return null;
        }
    }

    /// <summary>
    /// Initializes the marker detection system with camera parameters and builds the marker dictionary.
    /// </summary>
    private void InitializeTagDetection()
    {
        var intrinsics = PassthroughCameraUtils.GetCameraIntrinsics(CameraEye);
        m_cx = intrinsics.PrincipalPoint.x;
        m_cy = intrinsics.PrincipalPoint.y;
        m_focalLengthX = intrinsics.FocalLength.x;
        m_focalLengthY = intrinsics.FocalLength.y;
        var width = intrinsics.Resolution.x;
        var height = intrinsics.Resolution.y;

        Debug.Log($"[AprilTag] Camera parameters - Focal Length: ({m_focalLengthX}, {m_focalLengthY}), Principal Point: ({m_cx}, {m_cy}), Resolution: ({width}, {height})");
        Debug.Log($"[AprilTag] Initializing AprilTag detector with dimensions: {width}x{height}, decimation: {m_decimation}");
        m_tagDetector = new TagDetector(width, height, m_decimation);
        m_drawer = new TagDrawer(m_boundingBoxMaterial);

        BuildMarkerDictionary();

        m_isReady = true;
        Debug.Log("[AprilTag] Tag detection system is ready");
    }

    private void BuildMarkerDictionary()
    {
        m_markerGameObjectDictionary.Clear();
        foreach (var pair in m_markerGameObjectPairs)
        {
            if (pair.gameObject != null)
            {
                m_markerGameObjectDictionary[pair.markerId] = pair.gameObject;
            }
        }
    }

    private void LateUpdate()
    {
        if (m_webCamTextureManager.WebCamTexture == null || !m_isReady)
            return;

        // Handle coroutine management for slow-placing option
        if (m_enableSlowPlacing)
        {
            // Start coroutine if it's not running
            if (m_slowPlacingCoroutine == null)
            {
                m_slowPlacingCoroutine = StartCoroutine(SlowPlacingCoroutine());
            }

            // Update object positions with smooth interpolation
            UpdateSlowPlacingObjects();

            // Disable autoplace when slow-placing is on
            if (m_autoplaceObjects)
            {
                Debug.Log("[AprilTag] Auto-place disabled when slow-placing is enabled");
                m_autoplaceObjects = false;
            }
        }
        else
        {
            // Stop coroutine if slow-placing is disabled
            if (m_slowPlacingCoroutine != null)
            {
                StopCoroutine(m_slowPlacingCoroutine);
                m_slowPlacingCoroutine = null;
            }

            // Regular autoplace handling
            if (m_autoplaceObjects)
            {
                PlaceObjectsAtTags();
            }
            else if (m_isDetecting)
            {
                _ = DetectTags(m_enableSmoothing);
            }
        }
    }

    /// <summary>
    /// Processes marker detection and updates the position of 3D objects.
    /// </summary>
    public bool PlaceObjectsAtTags(List<int> includedIds, bool deactivateNonFound = true)
    {
        var poses = DetectTags(false);
        var allPosesDetected = false;
        Debug.Log($"[AprilTag] Detected {poses.Count} tags");
        foreach (var objectPair in m_markerGameObjectPairs)
        {
            if (!includedIds.Contains(objectPair.markerId))
            {
                if (deactivateNonFound) objectPair.gameObject.SetActive(false);
                continue;
            }

            if (!poses.TryGetValue(objectPair.markerId, out TagPose foundPose) || objectPair == null)
            {
                if (deactivateNonFound) objectPair.gameObject.SetActive(false);
                allPosesDetected = false;
                Debug.LogWarning($"[AprilTag] Tag {objectPair.markerId} not detected or object pair is null");
                continue;
            }

            var targetObject = objectPair.gameObject;
            // Ensure object is active
            targetObject.SetActive(true);
            targetObject.transform.position = foundPose.Position;
            targetObject.transform.rotation = foundPose.Rotation;
        }

        return allPosesDetected;
    }

    public bool PlaceObjectsAtTags()
    {
        return PlaceObjectsAtTags(m_markerGameObjectPairs.ConvertAll(pair => pair.markerId));
    }

    public void ClearTags()
    {
        foreach (var objectPair in m_markerGameObjectPairs)
        {
            if (objectPair.gameObject != null)
            {
                objectPair.gameObject.SetActive(false);
            }
        }

        m_prevPoseDataDictionary.Clear();
        m_tagVisibilityStatus.Clear();
    }

    public Dictionary<int, TagPose> DetectTags(bool enableSmoothing)
    {
        if (m_webCamTextureManager.WebCamTexture == null || !m_isReady)
            return null;

        var image = m_webCamTextureManager.WebCamTexture.AsSpan();
        if (image.IsEmpty)
        {
            Debug.LogWarning("[AprilTag] Image span is empty");
            return null;
        }

        return DetectTagsInImage(image, enableSmoothing);
    }

    private Dictionary<int, TagPose> DetectTagsInImage(ReadOnlySpan<Color32> image, bool enableSmoothing)
    {
        // Calculate field of view for detection and process the image
        var fov_y = Mathf.Atan2(m_cy, m_focalLengthY) * 2f;
        var tagSizeInMeters = m_tagSize_mm / 1000f;

        // Process the image with the AprilTag detector
        m_tagDetector.ProcessImage(image, fov_y, tagSizeInMeters);

        Dictionary<int, TagPose> detectedPoses = new Dictionary<int, TagPose>();
        var tagVisibilityStatus = new Dictionary<int, bool>();

        // Process the PoseData for each detected tag
        foreach (var tag in m_tagDetector.DetectedTags)
        {
            if (m_drawBoundingBox)
            {
                var pose = MovePoseToCameraCoordiantes(tag);
                m_drawer.Draw(tag.ID, pose.Position, pose.Rotation, tagSizeInMeters * 2);
            }

            // Apply smoothing if we have previous pose data
            var smoothedPoseData = tag;
            if (enableSmoothing && m_prevPoseDataDictionary.TryGetValue(tag.ID, out TagPose prevPose))
            {
                smoothedPoseData = ApplySmoothing(tag, prevPose);
            }

            // Store current pose for next frame
            m_prevPoseDataDictionary[tag.ID] = smoothedPoseData;
            tagVisibilityStatus[tag.ID] = true;

            // Convert pose 
            var movedPose = MovePoseToCameraCoordiantes(smoothedPoseData);

            // save the detected pose
            detectedPoses[tag.ID] = movedPose;
        }

        m_tagVisibilityStatus = tagVisibilityStatus;
        return detectedPoses;
    }

    private TagPose MovePoseToCameraCoordiantes(TagPose pose)
    {
        var cameraPose = PassthroughCameraUtils.GetCameraPoseInWorld(CameraEye);
        m_cameraAnchor.position = cameraPose.position;
        m_cameraAnchor.rotation = cameraPose.rotation;

        var pos = m_cameraAnchor.TransformPoint(pose.Position);
        var rot = m_cameraAnchor.rotation * pose.Rotation;

        return new TagPose(pose.ID, pos, rot);
    }

    private TagPose ApplySmoothing(TagPose currentPose, TagPose previousPose)
    {
        if (previousPose.Position == Vector3.zero)
            return currentPose;

        float t = m_smoothingFactor;
        return new TagPose(
            currentPose.ID,
            Vector3.Lerp(currentPose.Position, previousPose.Position, t),
            Quaternion.Slerp(currentPose.Rotation, previousPose.Rotation, t)
        );
    }

    private void LogSlowed(string message)
    {
        //Log with 30 frame interval
        if (Time.frameCount % 30 == 0)
            Debug.Log(message);
    }

    private void OnEnable()
    {
        if (m_enableSlowPlacing && m_slowPlacingCoroutine == null)
        {
            m_slowPlacingCoroutine = StartCoroutine(SlowPlacingCoroutine());
        }
    }

    private void OnDisable()
    {
        if (m_slowPlacingCoroutine != null)
        {
            StopCoroutine(m_slowPlacingCoroutine);
            m_slowPlacingCoroutine = null;
        }
    }

    /// <summary>
    /// Coroutine that runs the tag detection at a fixed rate for slow-placing mode
    /// </summary>
    private IEnumerator SlowPlacingCoroutine()
    {
        Debug.Log("[AprilTag] Starting slow-placing mode");

        while (true)
        {
            if (m_webCamTextureManager.WebCamTexture != null && m_isReady)
            {
                var poses = DetectTags(false);
                if (poses != null)
                {
                    // Update target positions and rotations for interpolation
                    foreach (var pair in poses)
                    {
                        int tagId = pair.Key;
                        TagPose tagPose = pair.Value;

                        // Store target positions and rotations for interpolation
                        m_targetPositions[tagId] = tagPose.Position;
                        m_targetRotations[tagId] = tagPose.Rotation;
                    }

                    // Set all detected tags as visible
                    foreach (var pair in poses)
                    {
                        m_tagVisibilityStatus[pair.Key] = true;
                    }
                }
            }

            // Wait for the next update cycle (twice per second)
            yield return new WaitForSeconds(m_slowPlacingUpdateRate);
        }
    }

    /// <summary>
    /// Update object positions and rotations with smooth interpolation for slow-placing mode
    /// </summary>
    private void UpdateSlowPlacingObjects()
    {
        float deltaTime = Time.deltaTime;
        float interpolationStep = m_slowPlacingInterpolationSpeed * deltaTime;

        foreach (var objectPair in m_markerGameObjectPairs)
        {
            int tagId = objectPair.markerId;
            GameObject targetObject = objectPair.gameObject;

            if (targetObject == null)
                continue;

            // If we have a target position for this tag
            if (m_targetPositions.ContainsKey(tagId))
            {
                // Ensure the object is active
                targetObject.SetActive(true);

                // Smoothly interpolate position and rotation
                targetObject.transform.position = Vector3.Lerp(
                    targetObject.transform.position,
                    m_targetPositions[tagId],
                    interpolationStep);

                targetObject.transform.rotation = Quaternion.Slerp(
                    targetObject.transform.rotation,
                    m_targetRotations[tagId],
                    interpolationStep);
            }
            // If tag isn't visible, just leave the object where it is
        }
    }

    private void OnDestroy()
    {
        if (m_tagDetector != null)
        {
            m_tagDetector.Dispose();
            m_tagDetector = null;
        }

        m_drawer.Dispose();
    }
}

