using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class Patch
{
    public readonly Vector3[] Vertices;
    public readonly Vector3Int BottomLeft;
    public readonly int Width;
    public readonly int Length;
    private readonly IDictionary<(float, float), float> _vertexHeightCache;

    public Patch(Vector3[] vertices, int width, int length, Vector3Int bottomLeft)
    {
        Vertices = vertices;
        Width = width;
        Length = length;
        BottomLeft = bottomLeft;
        _vertexHeightCache = new ConcurrentDictionary<(float, float), float>();
    }
    
    /// <summary>
    /// Samples the given surface to create a patch that encapsulates this GO's vertical projection.
    /// </summary>
    /// <returns></returns>
    public static Patch SampleCollider(Collider collider, Vector3 min, Vector3 max)
    {
        var roundedMin = Vector3Int.FloorToInt(min);
        var roundedMax = Vector3Int.CeilToInt(max);
        var width = (roundedMax - roundedMin).x;
        var length = (roundedMax - roundedMin).z;
        
        var vertices = new Vector3[(width + 1) * (length + 1)];
        for (int i = 0; i < width + 1; i++)
        {
            for (int j = 0; j < length + 1; j++)
            {
                var x = roundedMin.x + i;
                var z = roundedMin.z + j;
                var maxHeight = roundedMax.y;
                float y = maxHeight;
                var ray = new Ray(new Vector3(x, maxHeight, z), Vector3.down);
                if (collider.Raycast(ray, out var hit, 2 * maxHeight - roundedMin.y)) {
                    Debug.Log("Hit point: " + hit.point);
                    y = hit.point.y;
                    // y = 0;
                }
                vertices[i + j * (width + 1)] = new Vector3(x, y, z);
            }
        }
        // Debug.Log(string.Join(",", vertices));

        var triangles = new int[width * length * 6]; // 1 vert -> 1 square -> 2 tris -> 6 verts
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < length; j++)
            {
                var baseIndex = i + j * width * 6;
                // upper left triangle
                triangles[baseIndex] = i + j * width;               // .____.
                triangles[baseIndex + 1] = i + (j + 1) * width;     // |  /
                triangles[baseIndex + 2] = i + 1 + (j + 1) * width; // ./
                // lower right triangle
                triangles[baseIndex + 3] = i + j * width;           //      .
                triangles[baseIndex + 4] = i + 1 + (j + 1) * width; //    / |
                triangles[baseIndex + 5] = i + 1 + j * width;       // ./___.
            }
        }

        return new Patch(vertices, width, length, roundedMin);
    }

    public float HeightAt(Vector3 vertex)
    {
        if (!_vertexHeightCache.ContainsKey((vertex.x, vertex.y)))
        {
            // determine supporting square
            var flooredVertex = Vector3Int.FloorToInt(vertex);
            var offset = flooredVertex - BottomLeft;
            // this is the index of the bottom left corner vertex of the square that supports the current vertex
            var indexInPatch = Convert(offset.x, offset.z);
            // determine supporting triangle
            var localCoordinates = vertex - Vertices[indexInPatch];
            Vector3 a;
            Vector3 b;
            Vector3 c;
            if (localCoordinates.z > localCoordinates.x) // upper left triangle
            {
                a = Vertices[indexInPatch];
                b = Vertices[Convert(offset.x, offset.z + 1)];
                c = Vertices[Convert(offset.x + 1, offset.z + 1)];
            }
            else // lower right triangle
            {
                a = Vertices[indexInPatch];
                b = Vertices[Convert(offset.x + 1, offset.z + 1)];
                c = Vertices[Convert(offset.x + 1, offset.z)];
            }

            // find surface height
            var abc = Vector3.Cross(b - a, c - a);
            var d = Vector3.Dot(abc, a);
            _vertexHeightCache[(vertex.x, vertex.y)] = (d - abc.x * vertex.x - abc.z * vertex.z) / abc.y;
        }
        return _vertexHeightCache[(vertex.x, vertex.y)];
    }

    private int Convert(int x, int z)
    {
        return x + z * (Width + 1);
    }

    private (int x, int z) Convert(int i)
    {
        var x = i % (Width + 1);
        var y = i / (Width + 1);
        return (x, y);
    }
}