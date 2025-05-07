using PassthroughCameraSamples;
using UnityEngine;

public class KeepInFrontOfEyes : MonoBehaviour
{
    [SerializeField] private WebCamTextureManager m_webCamTextureManager;
    private PassthroughCameraEye CameraEye => m_webCamTextureManager.Eye;
    [SerializeField] private GameObject targetObject;
    [SerializeField] private float m_distanceFromEyes = 0.5f;
    [SerializeField] private bool m_isEnabled = true;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (m_webCamTextureManager == null)
        {
            Debug.LogError("WebCamTextureManager reference is not set!");
            enabled = false;
            return;
        }

        if (targetObject == null)
        {
            Debug.LogError("Anchor transform is not set!");
            enabled = false;
            return;
        }

        // Set initial state
        SetEnabled(m_isEnabled);
    }

    // Update is called once per frame
    void Update()
    {
        if (m_isEnabled)
        {
            UpdateCameraCanvas();
        }
    }


    private void UpdateCameraCanvas()
    {
        var cameraPose = PassthroughCameraUtils.GetCameraPoseInWorld(CameraEye);
        targetObject.transform.SetPositionAndRotation(
            cameraPose.position + cameraPose.rotation * Vector3.forward * m_distanceFromEyes,
            cameraPose.rotation);
    }
    public void ToggleEnabled()
    {
        SetEnabled(!m_isEnabled);
    }

    public void SetEnabled(bool isEnabled)
    {
        m_isEnabled = isEnabled;
        targetObject.SetActive(m_isEnabled);
    }
}
