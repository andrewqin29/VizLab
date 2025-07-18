using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PointCloud;

namespace PointCloud
{
    public class PointCloudData : MonoBehaviour
    {

        [SerializeField, Tooltip("Number of points in the point cloud.")]
        private int numPoints = 100;

        [SerializeField, Tooltip("Size of the points in the point cloud.")]
        private float pointScale = 0.05f;

        [SerializeField, Tooltip("Color of the points in the point cloud")]
        private Color pointColor = Color.red;

        [SerializeField, Tooltip("Time to complete a full 360deg rotation in seconds.")]
        private float rotationSpeed = 5f;

        private PointCloudRenderer renderer;

        /// <summary>
        /// Retrieve renderer and run functions that initialize point cloud data.
        /// </summary>
        void Start()
        {
            renderer = GetComponent<PointCloudRenderer>();
            InitPoints();
            SetScale();
            SetColor();
        }

        /// <summary>
        /// Initialize the point cloud with a random set of points along the surface of a sphere.
        /// </summary>
        private void InitPoints()
        {
            var pointData = new float[numPoints * 3];

            for (int i = 0; i < numPoints; i++)
            {
                var x = Random.Range(-0.5f, 0.5f);
                var y = Random.Range(-0.5f, 0.5f);
                var z = Random.Range(-0.5f, 0.5f);

                if (x == 0f && y == 0f && z == 0f)
                {
                    x += 0.01f;
                }

                var normFactor = 1f / Mathf.Sqrt(x * x + y * y + z * z);
                var sphereRadius = 0.35f;

                pointData[3 * i] = x * normFactor * sphereRadius;
                pointData[3 * i + 1] = y * normFactor * sphereRadius;
                pointData[3 * i + 2] = z * normFactor * sphereRadius;
            }

            renderer.PointData = pointData;
        }

        /// <summary>
        /// Apply a rotation along the y-axis to all points in the point cloud.
        /// </summary>
        private void StepPoints()
        {
            var pointData = renderer.PointData;

            for (int i = 0; i < numPoints; i++)
            {
                // pack x y z data into Vector3 to make rotation easier
                var point = new Vector3(pointData[3 * i], pointData[3 * i + 1], pointData[3 * i + 2]);

                // use Time.deltaTime to make rotation frame-rate independent
                var rot = Quaternion.Euler(0, 360f * Time.deltaTime / rotationSpeed, 0);

                // apply rotation and pack point data back into array
                var rotatedPoint = rot * point;
                pointData[3 * i] = rotatedPoint.x;
                pointData[3 * i + 1] = rotatedPoint.y;
                pointData[3 * i + 2] = rotatedPoint.z;
            }

            renderer.PointData = pointData;
        }

        /// <summary>
        /// Set the point scale according to the private field exposed through the Editor.
        /// </summary>
        private void SetScale()
        {
            var scaleData = new float[numPoints];

            for (int i = 0; i < numPoints; i++)
            {
                scaleData[i] = pointScale;
            }

            renderer.ScaleData = scaleData;
        }

        /// <summary>
        /// Set the uniform point color according to the private field exposed through the editor.
        /// </summary>
        private void SetColor()
        {
            renderer.UniformColor = pointColor;
        }

        // Update is called once per frame
        void Update()
        {
            StepPoints();
        }
    }
}
