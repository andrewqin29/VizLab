using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Diagnostics;
namespace PointCloud
{

    public class PointCloudRenderer : MonoBehaviour
    {
        // Public fields
        // (interface with these!)
        private float[] pointData;
        public float[] PointData
        {
            get { return pointData; }
            set
            {
                pointData = value;
                numPoints = (int)(pointData.Length / 3.0f);
                SetPointAndScaleBuffer();
            }
        }

        private float[] scaleData;
        public float[] ScaleData
        {
            get { return scaleData; }
            set
            {
                scaleData = value;
                SetPointAndScaleBuffer();
            }
        }

        private float[] colorData;
        public float[] ColorData
        {
            get { return colorData; }
            set
            {
                colorData = value;
                SetColorFromArray();
            }
        }

        private Color uniformColor;
        public Color UniformColor
        {
            get { return uniformColor; }
            set
            {
                uniformColor = value;
                SetUniformColor();
            }
        }

        private float uniformScale = 0.03f;
        public float UniformScale
        {
            get { return uniformScale; }
            set
            {
                uniformScale = value;
                SetUniformScale();
            }
        }

        // Internal state
        private int numPoints;
        [SerializeField, HideInInspector]
        private Mesh mesh;

        [SerializeField, HideInInspector]
        private ComputeBuffer frameBuffer;

        [SerializeField, HideInInspector]
        private ComputeBuffer colorBuffer;

        [SerializeField, HideInInspector]
        private ComputeBuffer argsBuffer;

        [SerializeField]
        private Material material;
        private Collider collider;

        private void Awake()
        {

            mesh = CreateQuad();
            collider = GetComponent<BoxCollider>();
        }

        /// <summary>
        /// Set point cloud color based on a uniform color value;
        /// </summary>
        public void SetUniformColor()
        {
            float4[] cmapArray = new float4[numPoints];
            for (int i = 0; i < numPoints; i++)
            {
                cmapArray[i] = new float4(uniformColor.r, uniformColor.g, uniformColor.b, uniformColor.a);
            }
            SetColorBuffer(cmapArray);
        }

        /// <summary>
        /// Set point cloud color based on array data.
        /// </summary>
        public void SetColorFromArray()
        {
            if (colorData.Length != numPoints)
            {
                return;
            }

            float4[] cmapArray = new float4[numPoints];
            for (int i = 0; i < numPoints; i++)
            {
                cmapArray[i] = new float4(colorData[3 * i], colorData[3 * i + 1], colorData[3 * i + 2], 1.0f);
            }

            SetColorBuffer(cmapArray);
        }

        /// <summary>
        /// Set point cloud positions and scale in shader buffer.
        /// </summary>
        public void SetPointAndScaleBuffer()
        {

            // these args tell the renderer that each quad has 6 triangle points (2 tris) and we'll need one for each point in the cloud
            uint[] args = new uint[] {
                6, (uint)numPoints, 0, 0, 0
            };
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            argsBuffer.SetData(args);

            // figure out if we have valid scale data, update shader keyword if so
            var scaleInArray = scaleData != null && scaleData.Length == numPoints;
            if (scaleInArray)
            {
                material.DisableKeyword("CONSTANT_SCALE");
            }

            // set float4 array to prep for compute buffer
            float4[] positions = new float4[numPoints];
            for (int i = 0; i < numPoints; i++)
            {
                float x = pointData[3 * i];
                float y = pointData[3 * i + 1];
                float z = pointData[3 * i + 2];
                float scale = scaleInArray ? scaleData[i] : uniformScale;
                positions[i] = new float4(x, y, z, scale);
            }

            // pass positions/scale into compute buffer
            frameBuffer = new ComputeBuffer(numPoints, 16);
            frameBuffer.SetData(positions);
            int matrixBufferId = Shader.PropertyToID("frameBuffer");
            material.SetBuffer(matrixBufferId, frameBuffer);
        }

        /// <summary>
        /// Set point cloud scale from a fixe value.
        /// </summary>
        public void SetUniformScale()
        {
            material.SetFloat("diskSize", uniformScale);
            material.EnableKeyword("CONSTANT_SCALE");
        }

        /// <summary>
        /// Set point cloud color buffer using an array of color values.
        /// </summary>
        /// <param name="colors">Float4 array of color values (RGBA)</param>
        public void SetColorBuffer(float4[] colors)
        {
            colorBuffer = new ComputeBuffer(colors.Length, 16);
            colorBuffer.SetData(colors);
            int colorsBufferId = Shader.PropertyToID("colorsBuffer");
            material.SetBuffer(colorsBufferId, colorBuffer);
        }

        private void Update()
        {
            if (pointData != null)
            {
                material.SetMatrix("transform", this.gameObject.transform.localToWorldMatrix);
                Graphics.DrawMeshInstancedIndirect(mesh, 0, material, GetBounds(), argsBuffer);
            }
        }

        /// <summary>
        /// Generate point cloud mesh to be rendered by point cloud unlit shader.
        /// </summary>
        /// <returns></returns>
        private static Mesh CreateQuad()
        {
            Mesh mesh = new Mesh();
            Vector3[] vertices = new Vector3[4];
            vertices[0] = new Vector3(-0.5f, -0.5f, 0);
            vertices[1] = new Vector3(0.5f, -0.5f, 0);
            vertices[2] = new Vector3(-0.5f, 0.5f, 0);
            vertices[3] = new Vector3(0.5f, 0.5f, 0);
            mesh.vertices = vertices;

            int[] tri = new int[6];
            tri[0] = 0;
            tri[1] = 2;
            tri[2] = 1;
            tri[3] = 2;
            tri[4] = 3;
            tri[5] = 1;
            mesh.triangles = tri;

            Vector3[] normals = new Vector3[4];
            normals[0] = -Vector3.forward;
            normals[1] = -Vector3.forward;
            normals[2] = -Vector3.forward;
            normals[3] = -Vector3.forward;
            mesh.normals = normals;

            Vector2[] uv = new Vector2[4];
            uv[0] = new Vector2(0, 0);
            uv[1] = new Vector2(1, 0);
            uv[2] = new Vector2(0, 1);
            uv[3] = new Vector2(1, 1);

            mesh.uv = uv;

            return mesh;
        }

        private Bounds GetBounds()
        {
            return collider.bounds;
        }

        private void OnDestroy()
        {
            frameBuffer.Release();
            colorBuffer.Release();
            argsBuffer.Release();
        }
    }
}