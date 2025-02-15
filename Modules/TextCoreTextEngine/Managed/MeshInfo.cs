// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Runtime.InteropServices;
using UnityEngine.Bindings;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.Scripting;

namespace UnityEngine.TextCore.Text
{
    enum VertexSortingOrder { Normal, Reverse }

    [VisibleToOtherModules("UnityEngine.UIElementsModule")]
    internal enum VertexDataLayout { Mesh, VBO }

    /// <summary>
    /// Structure which contains the vertex attributes (geometry) of the text object, as well as the material to be used.
    /// </summary>
    [VisibleToOtherModules("UnityEngine.IMGUIModule", "UnityEngine.UIElementsModule")]
    internal struct MeshInfo
    {
        public int vertexCount;
        public TextCoreVertex[] vertexData;
        public Material material;

        [Ignore] static readonly Color32 k_DefaultColor = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
        [Ignore] static readonly Vector3 k_DefaultNormal = new Vector3(0.0f, 0.0f, -1f);
        [Ignore] static readonly Vector4 k_DefaultTangent = new Vector4(-1f, 0.0f, 0.0f, 1f);

        [Ignore] public Vector3[] vertices;
        [Ignore] public Vector3[] normals;
        [Ignore] public Vector4[] tangents;
        [Ignore] public int vertexBufferSize;

        /// <summary>
        /// UV0 contains the following information
        /// X, Y are the UV coordinates of the glyph in the atlas texture.
        /// Z is the texture index in the texture atlas array
        /// W is the SDF Scale where a negative value represents bold text
        /// </summary>
        [Ignore] public Vector4[] uvs0;
        [Ignore] public Vector2[] uvs2;
        [Ignore] public Color32[] colors32;
        [Ignore] public int[] triangles;

        [Ignore] public VertexDataLayout vertexDataLayout;
        
        [VisibleToOtherModules("UnityEngine.UIElementsModule")]
        internal GlyphRenderMode glyphRenderMode;

        /// <summary>
        /// Function to pre-allocate vertex attributes for a mesh of size X.
        /// </summary>
        /// <param name="size"></param>
        public MeshInfo(int size, VertexDataLayout layout) : this()
        {
            vertexDataLayout = layout;
            material = null;

            // Limit the mesh to less than 65535 vertices which is the limit for Unity's Mesh.
            size = Mathf.Min(size, 16383);

            int sizeX4 = size * 4;
            int sizeX6 = size * 6;

            vertexCount = 0;
            vertexBufferSize = sizeX4;

            if (layout == VertexDataLayout.VBO)
            {
                vertexData = new TextCoreVertex[sizeX4];
            }
            else
            {
                vertices = new Vector3[sizeX4];
                uvs0 = new Vector4[sizeX4];
                uvs2 = new Vector2[sizeX4];
                colors32 = new Color32[sizeX4];
                normals = new Vector3[sizeX4];
                tangents = new Vector4[sizeX4];
                triangles = new int[sizeX6];

                int indexX6 = 0;
                int indexX4 = 0;
                while (indexX4 / 4 < size)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        vertices[indexX4 + i] = Vector3.zero;
                        uvs0[indexX4 + i] = Vector2.zero;
                        uvs2[indexX4 + i] = Vector2.zero;
                        colors32[indexX4 + i] = k_DefaultColor;
                        normals[indexX4 + i] = k_DefaultNormal;
                        tangents[indexX4 + i] = k_DefaultTangent;
                    }

                    triangles[indexX6 + 0] = indexX4 + 0;
                    triangles[indexX6 + 1] = indexX4 + 1;
                    triangles[indexX6 + 2] = indexX4 + 2;
                    triangles[indexX6 + 3] = indexX4 + 2;
                    triangles[indexX6 + 4] = indexX4 + 3;
                    triangles[indexX6 + 5] = indexX4 + 0;

                    indexX4 += 4;
                    indexX6 += 6;
                }
            }
            material = null;
            glyphRenderMode = 0;
        }

        /// <summary>
        /// Function to resized the content of MeshData and re-assign normals, tangents and triangles.
        /// </summary>
        /// <param name="size"></param>
        internal void ResizeMeshInfo(int size)
        {
            // Limit the mesh to less than 65535 vertices which is the limit for Unity's Mesh.
            size = Mathf.Min(size, 16383);

            int sizeX4 = size * 4;
            int sizeX6 = size * 6;

            vertexBufferSize = sizeX4;

            if (vertexDataLayout == VertexDataLayout.VBO)
            {
                Array.Resize(ref vertexData, sizeX4);
            }
            else
            {
                int previousSize = vertices.Length / 4;
                Array.Resize(ref vertices, sizeX4);

                Array.Resize(ref uvs0, sizeX4);
                Array.Resize(ref uvs2, sizeX4);

                Array.Resize(ref colors32, sizeX4);

                Array.Resize(ref triangles, sizeX6);

                for (int i = previousSize; i < size; i++)
                {
                    int indexX4 = i * 4;
                    int indexX6 = i * 6;

                    // Setup Triangles
                    triangles[0 + indexX6] = 0 + indexX4;
                    triangles[1 + indexX6] = 1 + indexX4;
                    triangles[2 + indexX6] = 2 + indexX4;
                    triangles[3 + indexX6] = 2 + indexX4;
                    triangles[4 + indexX6] = 3 + indexX4;
                    triangles[5 + indexX6] = 0 + indexX4;
                }
            }
        }

        /// <summary>
        /// Function to clear the vertices while preserving the Triangles, Normals and Tangents.
        /// </summary>
        internal void Clear(bool uploadChanges)
        {
            if (vertexDataLayout == VertexDataLayout.VBO)
            {
                if (vertexData == null) return;
                Array.Clear(vertexData, 0, vertexData.Length);
                vertexBufferSize = vertexData.Length;
            }
            else
            {
                if (vertices == null) return;
                Array.Clear(vertices, 0, vertices.Length);
                vertexBufferSize = vertices.Length;
            }
            
            vertexCount = 0;
        }

        /// <summary>
        /// Function to clear the vertices while preserving the Triangles, Normals and Tangents.
        /// </summary>
        internal void ClearUnusedVertices()
        {
            if (vertexDataLayout == VertexDataLayout.VBO)
            {
                int length = vertexData.Length - vertexCount;

                if (length > 0)
                    Array.Clear(vertexData, vertexCount, length);

                vertexBufferSize = vertexData.Length;
            }
            else
            {
                int length = vertices.Length - vertexCount;

                if (length > 0)
                    Array.Clear(vertices, vertexCount, length);

                vertexBufferSize = vertices.Length;
            }
        }

        /// <summary>
        /// Function used to mark unused vertices as degenerate an upload resulting data to the mesh.
        /// </summary>
        /// <param name="startIndex"></param>
        public void ClearUnusedVertices(int startIndex, bool updateMesh)
        {
            if (vertexDataLayout == VertexDataLayout.VBO)
            {
                int length = vertexData.Length - startIndex;

                if (length > 0)
                    Array.Clear(vertexData, startIndex, length);

                vertexBufferSize = vertexData.Length;
            }
            else
            {
                int length = vertices.Length - startIndex;

                if (length > 0)
                    Array.Clear(vertices, startIndex, length);

                vertexBufferSize = vertices.Length;
            }
        }

        /// <summary>
        /// Function used to mark unused vertices as degenerate.
        /// </summary>
        /// <param name="startIndex"></param>
        internal void ClearUnusedVertices(int startIndex)
        {
            if (vertexDataLayout == VertexDataLayout.VBO)
            {
                int length = vertexData.Length - startIndex;

                if (length > 0)
                    Array.Clear(vertexData, startIndex, length);

                  vertexBufferSize = vertexData.Length;
            }
            else
            {
                int length = vertices.Length - startIndex;

                if (length > 0)
                    Array.Clear(vertices, startIndex, length);

                vertexBufferSize = vertices.Length;
            }
        }

        internal void SortGeometry(VertexSortingOrder order)
        {
            switch (order)
            {
                case VertexSortingOrder.Normal:
                    // Do nothing
                    break;
                case VertexSortingOrder.Reverse:
                    int size = vertexCount / 4;
                    for (int i = 0; i < size; i++)
                    {
                        int src = i * 4;
                        int dst = (size - i - 1) * 4;

                        if (src < dst)
                            SwapVertexData(src, dst);
                    }
                    break;
            }
        }

        /// <summary>
        /// Method to swap the vertex attributes between src and dst quads.
        /// </summary>
        /// <param name="src">Index of the first vertex attribute of the source character / quad.</param>
        /// <param name="dst">Index of the first vertex attribute of the destination character / quad.</param>
        internal void SwapVertexData(int src, int dst)
        {
            int srcIndex = src;
            int dstIndex = dst;

            if(vertexDataLayout == VertexDataLayout.VBO)
            {
                var vertex = vertexData[dstIndex + 0];
                vertexData[dstIndex + 0] = vertexData[srcIndex + 0];
                vertexData[srcIndex + 0] = vertex;

                vertex = vertexData[dstIndex + 1];
                vertexData[dstIndex + 1] = vertexData[srcIndex + 1];
                vertexData[srcIndex + 1] = vertex;

                vertex = vertexData[dstIndex + 2];
                vertexData[dstIndex + 2] = vertexData[srcIndex + 2];
                vertexData[srcIndex + 2] = vertex;

                vertex = vertexData[dstIndex + 3];
                vertexData[dstIndex + 3] = vertexData[srcIndex + 3];
                vertexData[srcIndex + 3] = vertex;
            }
            else
            {
                // Swap vertices
                Vector3 vertex;
                vertex = vertices[dstIndex + 0];
                vertices[dstIndex + 0] = vertices[srcIndex + 0];
                vertices[srcIndex + 0] = vertex;

                vertex = vertices[dstIndex + 1];
                vertices[dstIndex + 1] = vertices[srcIndex + 1];
                vertices[srcIndex + 1] = vertex;

                vertex = vertices[dstIndex + 2];
                vertices[dstIndex + 2] = vertices[srcIndex + 2];
                vertices[srcIndex + 2] = vertex;

                vertex = vertices[dstIndex + 3];
                vertices[dstIndex + 3] = vertices[srcIndex + 3];
                vertices[srcIndex + 3] = vertex;

                //Swap UVs0
                Vector4 uvs;
                uvs = uvs0[dstIndex + 0];
                uvs0[dstIndex + 0] = uvs0[srcIndex + 0];
                uvs0[srcIndex + 0] = uvs;

                uvs = uvs0[dstIndex + 1];
                uvs0[dstIndex + 1] = uvs0[srcIndex + 1];
                uvs0[srcIndex + 1] = uvs;

                uvs = uvs0[dstIndex + 2];
                uvs0[dstIndex + 2] = uvs0[srcIndex + 2];
                uvs0[srcIndex + 2] = uvs;

                uvs = uvs0[dstIndex + 3];
                uvs0[dstIndex + 3] = uvs0[srcIndex + 3];
                uvs0[srcIndex + 3] = uvs;

                // Swap UVs2
                uvs = uvs2[dstIndex + 0];
                uvs2[dstIndex + 0] = uvs2[srcIndex + 0];
                uvs2[srcIndex + 0] = uvs;

                uvs = uvs2[dstIndex + 1];
                uvs2[dstIndex + 1] = uvs2[srcIndex + 1];
                uvs2[srcIndex + 1] = uvs;

                uvs = uvs2[dstIndex + 2];
                uvs2[dstIndex + 2] = uvs2[srcIndex + 2];
                uvs2[srcIndex + 2] = uvs;

                uvs = uvs2[dstIndex + 3];
                uvs2[dstIndex + 3] = uvs2[srcIndex + 3];
                uvs2[srcIndex + 3] = uvs;

                // Vertex Colors
                Color32 color;
                color = colors32[dstIndex + 0];
                colors32[dstIndex + 0] = colors32[srcIndex + 0];
                colors32[srcIndex + 0] = color;

                color = colors32[dstIndex + 1];
                colors32[dstIndex + 1] = colors32[srcIndex + 1];
                colors32[srcIndex + 1] = color;

                color = colors32[dstIndex + 2];
                colors32[dstIndex + 2] = colors32[srcIndex + 2];
                colors32[srcIndex + 2] = color;

                color = colors32[dstIndex + 3];
                colors32[dstIndex + 3] = colors32[srcIndex + 3];
                colors32[srcIndex + 3] = color;
            }
        }
    }
}
