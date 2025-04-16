using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PassthroughCameraSamples;
using System.Linq;

namespace TryAR.MarkerTracking
{
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
        [Header("Camera Setup")]
        [SerializeField] private WebCamTextureManager m_webCamTextureManager;
        private PassthroughCameraEye CameraEye => m_webCamTextureManager.Eye;
        private Vector2Int CameraResolution => m_webCamTextureManager.RequestedResolution;
        [SerializeField] private Transform m_cameraAnchor;

        [Header("Tag Detection")]
        [SerializeField] private int m_decimation = 4;
        [SerializeField] private float m_tagSize = 0.5f;
        [SerializeField, Tooltip("List of marker IDs mapped to their corresponding GameObjects")]
        private List<MarkerGameObjectPair> m_markerGameObjectPairs = new List<MarkerGameObjectPair>();

        [Header("Visualization")]
        [SerializeField] private Canvas m_cameraCanvas;
        [SerializeField] private RawImage m_resultRawImage;
        [SerializeField] private float m_canvasDistance = 1f;
        [SerializeField] private float m_smoothingFactor = 0.5f;
        [SerializeField] private bool m_enableSmoothing = true;

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
            Debug.Log("[AprilTag] Camera permissions granted");

            Debug.Log("[AprilTag] Initializing camera...");
            yield return InitializeCamera();
            Debug.Log("[AprilTag] Camera initialized successfully");

            Debug.Log("[AprilTag] Configuring UI and detection components...");
            ScaleCameraCanvas();

            Debug.Log("[AprilTag] Initializing tag detection system...");
            InitializeTagDetection();
            Debug.Log("[AprilTag] Tag detection system initialized");

            // Set initial visibility states
            m_cameraCanvas.gameObject.SetActive(true);
            m_webCamTextureManager.enabled = true;

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
            Debug.Log($"[AprilTag] Setting up camera with resolution: {PassthroughCameraUtils.GetCameraIntrinsics(CameraEye).Resolution}");
            m_webCamTextureManager.RequestedResolution = PassthroughCameraUtils.GetCameraIntrinsics(CameraEye).Resolution;
            m_webCamTextureManager.enabled = true;

            Debug.Log("[AprilTag] Waiting for camera texture to become available...");
            while (m_webCamTextureManager.WebCamTexture == null)
            {
                yield return null;
            }
            Debug.Log("[AprilTag] Camera texture is now available");
        }


        /// <summary>
        /// Initializes the marker detection system with camera parameters and builds the marker dictionary.
        /// </summary>
        private void InitializeTagDetection()
        {
            Debug.Log("[AprilTag] Getting camera intrinsics...");
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

            Debug.Log("[AprilTag] Building marker dictionary...");
            BuildMarkerDictionary();
            Debug.Log($"[AprilTag] Marker dictionary built with {m_markerGameObjectDictionary.Count} entries");

            Debug.Log("[AprilTag] Configuring result texture...");
            ConfigureResultTexture(width, height);

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
        /// Configures the texture for displaying camera and tracking results.
        /// </summary>
        private void ConfigureResultTexture(int width, int height)
        {
            // Calculate reduced size based on decimation factor
            int reducedWidth = width / m_decimation;
            int reducedHeight = height / m_decimation;

            // Create texture for visualization
            Texture2D resultTexture = new Texture2D(reducedWidth, reducedHeight, TextureFormat.RGB24, false);
            m_resultRawImage.texture = resultTexture;
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

            LogSlowed($"[AprilTag] Applied smoothing to tag {tagId} with factor {t}: " +
                      $"Current Pose: {currentPose.pos}, Previous Pose: {previousPose.pos}, Smoothed Pose: {smoothedPose.pos}");
            return smoothedPose;
        }
        /// <summary>
        /// Updates camera poses, detects markers, and handles input for toggling visualization mode.
        /// </summary>
        private void Update()
        {
            // Skip if camera isn't ready
            if (m_webCamTextureManager.WebCamTexture == null || !m_isReady)
                return;

            // Update tracking and visualization
            UpdateCameraPoses();

            // Process tag detection and position 3D objects
            ProcessTagDetection();
        }

        /// <summary>
        /// Processes marker detection and updates the position of 3D objects.
        /// </summary>
        private void ProcessTagDetection()
        {
            if (!m_isReady)
            {
                Debug.LogWarning("[AprilTag] Tag detection system not ready yet");
                return;
            }

            if (m_webCamTextureManager.WebCamTexture == null)
            {
                Debug.LogWarning("[AprilTag] WebCamTexture is null");
                return;
            }

            // Set the webcam texture directly to the RawImage (like in DetectionTest.cs)
            m_resultRawImage.texture = m_webCamTextureManager.WebCamTexture;

            // Get the webcam texture as span
            var image = m_webCamTextureManager.WebCamTexture.AsSpan();
            if (image.IsEmpty)
            {
                Debug.LogWarning("[AprilTag] Image span is empty");
                return;
            }

            // Calculate field of view for detection
            var fov = Mathf.Atan2(m_cy, m_focalLengthY) * 2f;
            LogSlowed($"[AprilTag] Processing image with FOV: {fov} radians");

            // Process image to detect AprilTags
            m_tagDetector.ProcessImage(image, fov, m_tagSize);
            LogSlowed($"[AprilTag] Detected {m_tagDetector.DetectedTags.Count()} tags");

            // Update GameObject positions based on detected tags
            foreach (var tag in m_tagDetector.DetectedTags)
            {
                LogSlowed($"[AprilTag] Processing tag ID: {tag.ID}");
                m_drawer.Draw(tag.ID, tag.Position, tag.Rotation, m_tagSize);

                if (!m_markerGameObjectDictionary.TryGetValue(tag.ID, out GameObject targetObject) || targetObject == null)
                {
                    Debug.LogWarning($"[AprilTag] No GameObject registered for tag ID: {tag.ID}");
                    continue;
                }

                // Convert AprilTag pose to Unity pose
                Vector3 position = tag.Position;
                Quaternion rotation = tag.Rotation;
                LogSlowed($"[AprilTag] Tag {tag.ID} position: {position}, rotation: {rotation}");

                // Create pose data from AprilTag detection
                PoseData currentPoseData = new PoseData
                {
                    pos = position,
                    rot = rotation
                };

                // Apply smoothing if we have previous pose data
                PoseData smoothedPoseData;
                if (m_enableSmoothing && m_prevPoseDataDictionary.TryGetValue(tag.ID, out PoseData prevPose))
                {
                    smoothedPoseData = ApplySmoothing(currentPoseData, prevPose, tag.ID);
                }
                else
                {
                    smoothedPoseData = currentPoseData;
                }

                // Store current pose for next frame
                m_prevPoseDataDictionary[tag.ID] = smoothedPoseData;

                // Convert pose to matrix and apply to game object
                var arMatrix = ARUtils.ConvertPoseDataToMatrix(ref smoothedPoseData, true);
                arMatrix = m_cameraAnchor.localToWorldMatrix * arMatrix;
                ARUtils.SetTransformFromMatrix(targetObject.transform, ref arMatrix);

                // Ensure object is active
                targetObject.SetActive(true);
                Debug.Log($"[AprilTag] Successfully updated GameObject for tag {tag.ID}");
            }
        }

        /// <summary>
        /// Updates the positions and rotations of camera-related transforms based on head and camera poses.
        /// </summary>
        private void UpdateCameraPoses()
        {
            // Get camera pose in world space
            var cameraPose = PassthroughCameraUtils.GetCameraPoseInWorld(CameraEye);

            // Update camera anchor position and rotation
            m_cameraAnchor.position = cameraPose.position;
            m_cameraAnchor.rotation = cameraPose.rotation;

            // Position the canvas in front of the camera
            m_cameraCanvas.transform.position = cameraPose.position + cameraPose.rotation * Vector3.forward * m_canvasDistance;
            m_cameraCanvas.transform.rotation = cameraPose.rotation;
        }

        private void LogSlowed(string message)
        {
            //Log with 30 frame interval
            if (Time.frameCount % 30 == 0)
                Debug.Log(message);
        }

        /// <summary>
        /// Calculates the dimensions of the canvas based on the distance from the camera origin and the camera resolution.
        /// </summary>
        private void ScaleCameraCanvas()
        {
            var cameraCanvasRectTransform = m_cameraCanvas.GetComponentInChildren<RectTransform>();

            // Calculate field of view based on camera parameters
            var leftSidePointInCamera = PassthroughCameraUtils.ScreenPointToRayInCamera(CameraEye, new Vector2Int(0, CameraResolution.y / 2));
            var rightSidePointInCamera = PassthroughCameraUtils.ScreenPointToRayInCamera(CameraEye, new Vector2Int(CameraResolution.x, CameraResolution.y / 2));
            var horizontalFoVDegrees = Vector3.Angle(leftSidePointInCamera.direction, rightSidePointInCamera.direction);
            var horizontalFoVRadians = horizontalFoVDegrees / 180 * Math.PI;

            // Calculate canvas size to match camera view
            var newCanvasWidthInMeters = 2 * m_canvasDistance * Math.Tan(horizontalFoVRadians / 2);
            var localScale = (float)(newCanvasWidthInMeters / cameraCanvasRectTransform.sizeDelta.x);
            cameraCanvasRectTransform.localScale = new Vector3(localScale, localScale, localScale);
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
}