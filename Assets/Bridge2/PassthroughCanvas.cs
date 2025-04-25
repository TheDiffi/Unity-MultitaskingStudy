using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using PassthroughCameraSamples;


public class PassthroughCanvas : MonoBehaviour
{
    [Header("Camera Setup")]
    [SerializeField] private WebCamTextureManager m_webCamTextureManager;
    private PassthroughCameraEye CameraEye => m_webCamTextureManager.Eye;
    private Vector2Int CameraResolution => m_webCamTextureManager.RequestedResolution;
    [SerializeField] private Transform m_cameraAnchor;

    [Header("Visualization")]
    [SerializeField] private Canvas m_cameraCanvas;
    [SerializeField] private RawImage m_resultRawImage;
    [SerializeField] private float m_canvasDistance = 1f;

    // State flags
    private bool m_isReady = false;
    public bool findMarkers = true;

    private IEnumerator Start()
    {
        Debug.Log("[AprilTag] Starting initialization process...");

        if (m_webCamTextureManager == null)
        {
            Debug.LogError($"[AprilTag] {nameof(m_webCamTextureManager)} is required");
            enabled = false;
            yield break;
        }

        while (!(PassthroughCameraPermissions.HasCameraPermission ?? false))
            yield return null;

        ScaleCameraCanvas();
        m_isReady = true;
    }

    private void ScaleCameraCanvas()
    {
        var cameraCanvasRectTransform = m_cameraCanvas.GetComponentInChildren<RectTransform>();

        // Calculate field of view
        var leftPoint = PassthroughCameraUtils.ScreenPointToRayInCamera(
            CameraEye, new Vector2Int(0, CameraResolution.y / 2));
        var rightPoint = PassthroughCameraUtils.ScreenPointToRayInCamera(
            CameraEye, new Vector2Int(CameraResolution.x, CameraResolution.y / 2));

        var horizontalFoVDegrees = Vector3.Angle(leftPoint.direction, rightPoint.direction);
        var horizontalFoVRadians = horizontalFoVDegrees * Mathf.Deg2Rad;

        // Calculate and set canvas scale
        var canvasWidth = 2 * m_canvasDistance * Math.Tan(horizontalFoVRadians / 2);
        var scale = (float)(canvasWidth / cameraCanvasRectTransform.sizeDelta.x);
        cameraCanvasRectTransform.localScale = new Vector3(scale, scale, scale);

        var intrinsics = PassthroughCameraUtils.GetCameraIntrinsics(CameraEye);
        var width = intrinsics.Resolution.x;
        var height = intrinsics.Resolution.y;
        m_resultRawImage.texture = new Texture2D(width, height, TextureFormat.RGB24, false);

    }

    private void LateUpdate()
    {
        if (m_webCamTextureManager.WebCamTexture == null || !m_isReady)
            return;

        UpdateCameraCanvas();
    }

    private void UpdateCameraCanvas()
    {
        var cameraPose = PassthroughCameraUtils.GetCameraPoseInWorld(CameraEye);

        // Update camera anchor and canvas
        m_cameraAnchor.SetPositionAndRotation(cameraPose.position, cameraPose.rotation);
        m_cameraCanvas.transform.SetPositionAndRotation(
            cameraPose.position + cameraPose.rotation * Vector3.forward * m_canvasDistance,
            cameraPose.rotation);

        m_resultRawImage.texture = m_webCamTextureManager.WebCamTexture;
    }
}
