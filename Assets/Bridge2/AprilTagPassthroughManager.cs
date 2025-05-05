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
        public int markerId;
        public GameObject gameObject;
    }

    [Header("Camera Setup")]
    [SerializeField] private WebCamTextureManager m_webCamTextureManager;
    [SerializeField] private Transform m_cameraAnchor;
    private PassthroughCameraEye CameraEye => m_webCamTextureManager.Eye;
    private Vector2Int CameraResolution => m_webCamTextureManager.RequestedResolution;


    [Header("Visualizing")]
    // AprilTag detection components
    [SerializeField] private bool m_drawBoundingBox = true;
    [SerializeField] Material m_boundingBoxMaterial = null;
    TagDrawer m_drawer;


    [Header("Tag Detection")]
    [SerializeField] private int m_decimation = 4;
    [SerializeField]
    [DebugMember(Tweakable = true, Min = 24f, Max = 28f, Category = "AprilTag")]
    private float m_tagSize_mm = 26.5f;

    [Header("Object Placement")]
    [DebugMember(Tweakable = true, Min = 0.1f, Max = 2.0f, Category = "AprilTag")]
    [SerializeField] private bool m_enableSmoothing = false;
    [SerializeField] private float m_smoothingFactor = 0.8f;
    public bool m_autoplaceObjects = true;
    [SerializeField, Tooltip("List of marker IDs mapped to their corresponding GameObjects")]
    private List<MarkerGameObjectPair> m_markerGameObjectPairs = new List<MarkerGameObjectPair>();

    private TagDetector m_tagDetector;
    private Dictionary<int, GameObject> m_markerGameObjectDictionary = new Dictionary<int, GameObject>();
    private Dictionary<int, TagPose> m_prevPoseDataDictionary = new Dictionary<int, TagPose>();
    private Dictionary<int, bool> m_tagVisibilityStatus = new Dictionary<int, bool>();

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

        if (m_autoplaceObjects)
        {
            PlaceObjectsAtTags();
        }
        else if (m_isDetecting)
        {
            _ = DetectTags(m_enableSmoothing);
        }
    }

    /// <summary>
    /// Processes marker detection and updates the position of 3D objects.
    /// </summary>
    public void PlaceObjectsAtTags()
    {
        var poses = DetectTags(false);
        Debug.Log($"[AprilTag] Detected {poses.Count} tags");
        foreach (var objectPair in m_markerGameObjectPairs)
        {
            if (!poses.TryGetValue(objectPair.markerId, out TagPose foundPose) || objectPair == null)
            {
                objectPair.gameObject.SetActive(false);
                continue;
            }

            var targetObject = objectPair.gameObject;
            // Ensure object is active
            targetObject.SetActive(true);
            targetObject.transform.position = foundPose.Position;
            targetObject.transform.rotation = foundPose.Rotation;
        }
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

