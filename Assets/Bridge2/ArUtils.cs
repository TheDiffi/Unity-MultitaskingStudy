using UnityEngine;

namespace TryAR.MarkerTracking
{
    /// <summary>
    /// Utility functions for AR applications, particularly for coordinate system transformations.
    /// </summary>
    public static class ARUtils
    {
        /// <summary>
        /// Converts rotation and translation vectors to PoseData.
        /// </summary>
        /// <param name="rvec">Rotation vector</param>
        /// <param name="tvec">Translation vector</param>
        /// <returns>PoseData with position and rotation</returns>
        public static PoseData ConvertRvecTvecToPoseData(double[] rvec, double[] tvec)
        {
            PoseData poseData = new PoseData();

            // Convert OpenCV tvec to Unity position
            // OpenCV: right-handed (X right, Y down, Z forward)
            // Unity: left-handed (X right, Y up, Z forward)
            poseData.pos = new Vector3(
                (float)tvec[0],
                -(float)tvec[1],  // Flip Y axis
                (float)tvec[2]
            );

            // Convert OpenCV rvec to Unity quaternion
            // This is a simplified conversion for illustration purposes
            // For actual implementation, may need more accurate conversion
            float angle = Mathf.Sqrt((float)(rvec[0] * rvec[0] + rvec[1] * rvec[1] + rvec[2] * rvec[2]));
            if (angle > 0.0001f)
            {
                Vector3 axis = new Vector3(
                    (float)rvec[0] / angle,
                    -(float)rvec[1] / angle,  // Flip Y axis
                    (float)rvec[2] / angle
                );
                poseData.rot = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, axis);
            }
            else
            {
                poseData.rot = Quaternion.identity;
            }

            return poseData;
        }

        /// <summary>
        /// Converts PoseData to a transformation matrix.
        /// </summary>
        /// <param name="poseData">Pose data with position and rotation</param>
        /// <param name="flipZ">Whether to flip the Z axis (for AR coordinate system conversion)</param>
        /// <returns>Transformation matrix</returns>
        public static Matrix4x4 ConvertPoseDataToMatrix(ref PoseData poseData, bool flipZ = false)
        {
            Vector3 position = poseData.pos;
            Quaternion rotation = poseData.rot;

            // Flip the Z axis if needed (used for certain AR transformations)
            if (flipZ)
            {
                position.z = -position.z;
                rotation = new Quaternion(-rotation.x, -rotation.y, rotation.z, rotation.w);
            }

            return Matrix4x4.TRS(position, rotation, Vector3.one);
        }

        /// <summary>
        /// Sets a transform from a transformation matrix.
        /// </summary>
        /// <param name="transform">Transform to update</param>
        /// <param name="matrix">Source transformation matrix</param>
        public static void SetTransformFromMatrix(Transform transform, ref Matrix4x4 matrix)
        {
            // Extract position
            transform.localPosition = matrix.GetColumn(3);

            // Extract rotation (convert matrix to quaternion)
            transform.localRotation = Quaternion.LookRotation(
                matrix.GetColumn(2),
                matrix.GetColumn(1)
            );

            // Extract scale (if needed)
            // This is usually uniform scale for AR applications
            // transform.localScale = new Vector3(
            //     matrix.GetColumn(0).magnitude,
            //     matrix.GetColumn(1).magnitude,
            //     matrix.GetColumn(2).magnitude
            // );
        }
    }
}