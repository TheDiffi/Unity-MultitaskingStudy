using UnityEngine;

public class HorizontalPlane : MonoBehaviour
{
    [Tooltip("If true, the plane will maintain horizontal orientation relative to world space")]
    public bool maintainHorizontal = true;

    private void LateUpdate()
    {
        if (maintainHorizontal)
        {
            // Get the current rotation
            Vector3 currentRotation = transform.rotation.eulerAngles;

            // Create a new rotation that zeroes out X and Z rotation (keeps only Y rotation)
            // This preserves rotation around the vertical axis but makes the plane stay horizontal
            transform.rotation = Quaternion.Euler(0f, currentRotation.y, 0f);
        }
    }
}