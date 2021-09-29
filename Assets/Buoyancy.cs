using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public class Buoyancy : MonoBehaviour
{
    [SerializeField] private GameObject water;
    private Rigidbody _rigidbody;
    private Mesh _mesh;
    private Renderer _renderer;
    private Mesh _waterSurface;
    private MeshCollider _waterSurfaceCollider;

    // Start is called before the first frame update
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _mesh = GetComponent<MeshFilter>().mesh;
        _renderer = GetComponent<Renderer>();
        _waterSurface = water.GetComponent<MeshFilter>().mesh;
        _waterSurfaceCollider = water.GetComponent<MeshCollider>();
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void FixedUpdate()
    {
        // if (transform.position.y < 0)
        // {
        //     var force = Vector3.up * 9.81f * 2;
        //     _rigidbody.AddForce(force);
        //     Debug.DrawRay(transform.position, force, Color.yellow);
        // }

        var (force, origin) = CalculateBuoyantForce(_waterSurface);
        Debug.Log($"Buoyancy calculated: force={force}, origin={origin}");
        _rigidbody.AddForceAtPosition(Vector3.up * force, origin);
        Debug.DrawRay(origin, Vector3.up * force, Color.green);
    }

    private (float force, Vector3 origin) CalculateBuoyantForce(Mesh waterSurface)
    {
        Debug.Log("=========Calculating buoyancy========");
        Debug.Log("Creating sample patch");
        var patch = CreateSamplePatch(waterSurface);
        Debug.Log($"Created sample patch width={patch.Width} length={patch.Length} vertices={string.Join(",", patch.Mesh.vertices)}");
        
        // calculate heights above water
        Debug.Log("Calculating heights relative to surface");
        Debug.Log(string.Join(",", _mesh.vertices));
        Debug.Log(string.Join(",", _mesh.triangles));
        var vertexHeights = new float[_mesh.vertices.Length];
        for (var i = 0; i < _mesh.vertices.Length; i++)
        {
            var vertex = transform.TransformPoint(_mesh.vertices[i]);
            vertexHeights[i] = vertex.y - patch.HeightAt(vertex);
        }
        Debug.Log("vertexHeights=" + string.Join(",", vertexHeights));

        float buoyantForce = 0;
        var centers = new HashSet<Vector3>();
        for (int i = 0; i < _mesh.triangles.Length; i += 3)
        {
            Vector3 a = transform.TransformPoint(_mesh.vertices[_mesh.triangles[i]]);
            Vector3 b = transform.TransformPoint(_mesh.vertices[_mesh.triangles[i + 1]]);
            Vector3 c = transform.TransformPoint(_mesh.vertices[_mesh.triangles[i + 2]]);
            if (vertexHeights[_mesh.triangles[i]] <= 0 && vertexHeights[_mesh.triangles[i + 1]] <= 0 && vertexHeights[_mesh.triangles[i + 2]] <= 0)
            {
                var center = (a + b + c) / 3;
                Debug.Log($"Found submerged triangle centered at {center}");
                var centerHeight = center.y - patch.HeightAt(center);
                Debug.Log($"Triangle's relative height is {centerHeight}");
                var triangleNormal = Vector3.Cross(b - a, c - a).normalized;
                const float waterPressure = 1029f;
                var hydrostaticForce = -waterPressure * Physics.gravity.y * centerHeight * triangleNormal / 2;
                Debug.Log($"Resulting in a hydrostatic force of {hydrostaticForce}");
                buoyantForce += hydrostaticForce.y;
                centers.Add(center);
                Debug.DrawRay(center, hydrostaticForce);
            }
        }

        var count = centers.Count;
        Debug.Log($"Calculated {count} hydrostatic forces");
        var averageCenter = count > 0 ? centers.Aggregate(Vector3.zero, (a, b) => a + b) / centers.Count : Vector3.zero;
        return (buoyantForce, averageCenter);
    }

    /// <summary>
    /// Samples the given surface to create a patch that encapsulates this GO's vertical projection.
    /// </summary>
    /// <param name="surface">Surface to sample</param>
    /// <returns></returns>
    private Patch CreateSamplePatch(Mesh surface)
    {
        var roundedMin = Vector3Int.FloorToInt(_renderer.bounds.min);
        var roundedMax = Vector3Int.CeilToInt(_renderer.bounds.max);
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
                if (_waterSurfaceCollider.Raycast(ray, out var hit, 2 * maxHeight - roundedMin.y)) {
                    Debug.Log("Hit point: " + hit.point);
                    y = hit.point.y;
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

        return new Patch(new Mesh {vertices = vertices, triangles = triangles}, width, length, roundedMin);
    }

    private class Patch
    {
        public readonly Mesh Mesh; // TODO array of vertices instead
        public readonly Vector3Int BottomLeft;
        public readonly int Width;
        public readonly int Length;

        public Patch(Mesh mesh, int width, int length, Vector3Int bottomLeft)
        {
            Mesh = mesh;
            Width = width;
            Length = length;
            BottomLeft = bottomLeft;
        }

        public float HeightAt(Vector3 vertex)
        {
            // determine supporting square
            var flooredVertex = Vector3Int.FloorToInt(vertex);
            var offset = flooredVertex - BottomLeft;
            // this is the index of the bottom left corner vertex of the square that supports the current vertex
            var indexInPatch = Convert(offset.x, offset.z);
            // determine supporting triangle
            var localCoordinates = vertex - Mesh.vertices[indexInPatch];
            Vector3 a;
            Vector3 b;
            Vector3 c;
            if (localCoordinates.z > localCoordinates.x) // upper left triangle
            {
                a = Mesh.vertices[indexInPatch];
                b = Mesh.vertices[Convert(offset.x, offset.z + 1)];
                c = Mesh.vertices[Convert(offset.x + 1, offset.z + 1)];
            }
            else // lower right triangle
            {
                a = Mesh.vertices[indexInPatch];
                b = Mesh.vertices[Convert(offset.x + 1, offset.z + 1)];
                c = Mesh.vertices[Convert(offset.x + 1, offset.z)];
            }

            // find surface height
            var abc = Vector3.Cross(b - a, c - a);
            var d = Vector3.Dot(abc, a);
            return (d - abc.x * vertex.x - abc.z * vertex.z) / abc.y;
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
}
