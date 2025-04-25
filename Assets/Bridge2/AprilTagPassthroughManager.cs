using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PassthroughCameraSamples;
using Meta.XR.ImmersiveDebugger;

/// <summary>
/// Connects the Meta Quest passthrough camera API with AprilTag detection.
/// Replaces the OpenCV ArUco detection with AprilTag detection.
/// </summary>
[Serializable]
public class MarkerGameObjectPair
{
    /// <summary>
    /// The unique ID of the AR marker to track.
    /// </summary>
    public int markerId;

    /// <summary>
    /// The GameObject to associate with this marker.
    /// </summary>
    public GameObject gameObject;
}

public class AprilTagPassthroughManager : MonoBehaviour
{
    public bool findMarkers = true;

    [Header("Camera Setup")]
    [SerializeField] private WebCamTextureManager m_webCamTextureManager;
    private PassthroughCameraEye CameraEye => m_webCamTextureManager.Eye;
    private Vector2Int CameraResolution => m_webCamTextureManager.RequestedResolution;
    [SerializeField] private Transform m_cameraAnchor;

    [Header("Tag Detection")]
    [SerializeField] private int m_decimation = 4;
    [SerializeField]
    [DebugMember(Tweakable = true, Min = 23f, Max = 27f, Category = "AprilTag")]
    private float m_tagSize_mm = 25.5f;
    [SerializeField, Tooltip("List of marker IDs mapped to their corresponding GameObjects")]
    private List<MarkerGameObjectPair> m_markerGameObjectPairs = new List<MarkerGameObjectPair>();

    [DebugMember(Tweakable = true, Min = 0.1f, Max = 2.0f, Category = "AprilTag")]

    [SerializeField] private float m_smoothingFactor = 0.8f;
    [SerializeField] private bool m_enableSmoothing = false;


    [Header("Camera Positioning")]
    // AprilTag detection components
    private AprilTag.TagDetector m_tagDetector;
    private Dictionary<int, GameObject> m_markerGameObjectDictionary = new Dictionary<int, GameObject>();
    private Dictionary<int, PoseData> m_prevPoseDataDictionary = new Dictionary<int, PoseData>();
    TagDrawer m_drawer;
    [SerializeField] Material m_tagMaterial = null;

    // Camera parameters
    private float m_focalLengthX, m_focalLengthY;
    private float m_cx, m_cy;

    // State flags
    private bool m_isReady = false;


    /// <summary>
    /// Initializes the camera, permissions, and tag detection system.
    /// </summary>
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
        m_tagDetector = new AprilTag.TagDetector(width, height, m_decimation);
        m_drawer = new TagDrawer(m_tagMaterial);

        BuildMarkerDictionary();

        m_isReady = true;
        Debug.Log("[AprilTag] Tag detection system is ready");
    }

    /// <summary>
    /// Builds the dictionary mapping marker IDs to GameObjects.
    /// </summary>
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

    /// <summary>
    /// Updates camera poses, detects markers, and handles input for toggling visualization mode.
    /// </summary>
    private void LateUpdate()
    {
        if (m_webCamTextureManager.WebCamTexture == null || !m_isReady)
            return;

        if (findMarkers)
        {
            DetectTags();
        }
    }

    /// <summary>
    /// Processes marker detection and updates the position of 3D objects.
    /// </summary>
    private void DetectTags()
    {
        if (m_webCamTextureManager.WebCamTexture == null || !m_isReady)
            return;



        // Get the webcam texture as span
        var image = m_webCamTextureManager.WebCamTexture.AsSpan();
        if (image.IsEmpty)
        {
            Debug.LogWarning("[AprilTag] Image span is empty");
            return;
        }

        // Calculate field of view for detection and process the image
        var fov_y = Mathf.Atan2(m_cy, m_focalLengthY) * 2f;
        var tagSizeInMeters = m_tagSize_mm / 1000f;
        m_tagDetector.ProcessImage(image, fov_y, tagSizeInMeters);

        // Update GameObject positions based on detected tags
        foreach (var tag in m_tagDetector.DetectedTags)
        {
            var (drawerPos, drawerRot) = MovePoseToCameraCoordiantes(tag.Position, tag.Rotation);
            m_drawer.Draw(tag.ID, drawerPos, drawerRot, tagSizeInMeters);

            if (!m_markerGameObjectDictionary.TryGetValue(tag.ID, out GameObject targetObject) || targetObject == null)
            {
                continue;
            }

            // Convert AprilTag pose to Unity pose
            Vector3 position = tag.Position;
            Quaternion rotation = tag.Rotation;

            // Create pose data from AprilTag detection
            PoseData currentPoseData = new PoseData
            {
                pos = position,
                rot = rotation
            };

            // Apply smoothing if we have previous pose data
            PoseData smoothedPoseData = currentPoseData;
            if (m_enableSmoothing && m_prevPoseDataDictionary.TryGetValue(tag.ID, out PoseData prevPose))
            {
                smoothedPoseData = ApplySmoothing(currentPoseData, prevPose, tag.ID);
            }

            // Store current pose for next frame
            m_prevPoseDataDictionary[tag.ID] = smoothedPoseData;

            // Convert pose to matrix and apply to game object
            var (gameObjPos, gameObjRot) = MovePoseToCameraCoordiantes(smoothedPoseData.pos, smoothedPoseData.rot);
            targetObject.transform.position = gameObjPos;
            targetObject.transform.rotation = gameObjRot;

            // Ensure object is active
            targetObject.SetActive(true);
        }
    }

    private (Vector3 position, Quaternion rotation) MovePoseToCameraCoordiantes(Vector3 tagPosition, Quaternion tagRotation)
    {
        var cameraPose = PassthroughCameraUtils.GetCameraPoseInWorld(CameraEye);
        m_cameraAnchor.position = cameraPose.position;
        m_cameraAnchor.rotation = cameraPose.rotation;

        var pos = m_cameraAnchor.TransformPoint(tagPosition);
        var rot = m_cameraAnchor.rotation * tagRotation;

        return (pos, rot);
    }



    /// <summary>
    /// Applies smoothing between current and previous pose data.
    /// </summary>
    /// <param name="currentPose">The current detected pose</param>
    /// <param name="previousPose">The previous frame's pose</param>
    /// <param name="tagId">Tag ID for logging purposes</param>
    /// <returns>Smoothed pose data</returns>
    private PoseData ApplySmoothing(PoseData currentPose, PoseData previousPose, int tagId)
    {
        if (previousPose.pos == Vector3.zero)
            return currentPose;

        float t = m_smoothingFactor;
        PoseData smoothedPose = new PoseData
        {
            pos = Vector3.Lerp(currentPose.pos, previousPose.pos, t),
            rot = Quaternion.Slerp(currentPose.rot, previousPose.rot, t)
        };

        return smoothedPose;
    }

    private void LogSlowed(string message)
    {
        //Log with 30 frame interval
        if (Time.frameCount % 30 == 0)
            Debug.Log(message);
    }



    /// <summary>
    /// Clean up when object is destroyed.
    /// </summary>
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

/// <summary>
/// Simple struct to store pose data for smoothing.
/// </summary>
public struct PoseData
{
    public Vector3 pos;
    public Quaternion rot;
}
