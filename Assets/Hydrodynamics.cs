using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Hydrodynamics : MonoBehaviour
{
    [SerializeField] private GameObject water;
    [SerializeField] private bool applyForce;
    private Rigidbody _rigidbody;
    private Mesh _mesh;
    private Renderer _renderer;
    private MeshCollider _waterSurfaceCollider;

    // Start is called before the first frame update
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _mesh = GetComponent<MeshFilter>().mesh;
        _renderer = GetComponent<Renderer>();
        _waterSurfaceCollider = water.GetComponent<MeshCollider>();
    }

    private void FixedUpdate()
    {
        Debug.Log("=========Calculating hydrodynamic forces========");
        Debug.Log("Creating sample patch");
        var bounds = _renderer.bounds;
        var patch = Patch.SampleCollider(_waterSurfaceCollider, bounds.min, bounds.max);
        Debug.Log(
            $"Created sample patch width={patch.Width} length={patch.Length} vertices={string.Join(",", patch.Vertices)}");

        // calculate heights above water
        Debug.Log("Calculating heights relative to surface");
        Debug.Log("vertices=" + string.Join(",", _mesh.vertices));
        Debug.Log("triangles" + string.Join(",", _mesh.triangles));
        var vertexHeights = new float[_mesh.vertices.Length];
        for (var i = 0; i < _mesh.vertices.Length; i++)
        {
            var vertex = transform.TransformPoint(_mesh.vertices[i]);
            vertexHeights[i] = vertex.y - patch.HeightAt(vertex);
        }

        Debug.Log("vertexHeights=" + string.Join(",", vertexHeights));

        var submergedTriangles = CalculateSubmergedTriangles(vertexHeights);

        var hydrodynamicForces = new HashSet<HydrodynamicForce>
        {
            new Buoyancy(patch, _rigidbody.mass)
        };
        foreach (var hydrodynamicForce in hydrodynamicForces)
        {
            var (force, origin) = hydrodynamicForce.CalculateForce(submergedTriangles);
            Debug.Log($"Buoyancy calculated: force={force}, origin={origin}");
            if (applyForce)
            {
                _rigidbody.AddForceAtPosition(Vector3.up * force, origin);
            }

            Debug.DrawRay(origin, Vector3.up * force / _rigidbody.mass, Color.green);
        }
    }

    private ISet<(Vector3 a, Vector3 b, Vector3 c)> CalculateSubmergedTriangles(float[] vertexHeights)
    {
        var triangles = new HashSet<(Vector3 a, Vector3 b, Vector3 c)>();
        for (int i = 0; i < _mesh.triangles.Length; i += 3)
        {
            var orderByDescending = Enumerable.Range(0, 3)
                .OrderByDescending(offset => vertexHeights[_mesh.triangles[i + offset]])
                .ToArray();
            // fully submerged
            if (vertexHeights[_mesh.triangles[i + orderByDescending[0]]] <= 0)
            {
                Debug.Log("Fully submerged");
                var a = transform.TransformPoint(_mesh.vertices[_mesh.triangles[i]]);
                var b = transform.TransformPoint(_mesh.vertices[_mesh.triangles[i + 1]]);
                var c = transform.TransformPoint(_mesh.vertices[_mesh.triangles[i + 2]]);
                triangles.Add((a, b, c));
            }
            // 2 of 3 submerged
            else if (vertexHeights[_mesh.triangles[i + orderByDescending[1]]] <= 0)
            {
                //   0
                //  / \
                // 2---1
                var triVertexHeights = new float[3];
                var vertices = new Vector3[3];
                for (int j = 0; j < 3; j++)
                {
                    var vertexIndex = _mesh.triangles[i + (orderByDescending[0] + j) % 3];
                    triVertexHeights[j] = vertexHeights[vertexIndex];
                    vertices[j] = transform.TransformPoint(_mesh.vertices[vertexIndex]);
                }

                var highHeight = triVertexHeights[0];
                var rightHeight = triVertexHeights[1];
                var leftHeight = triVertexHeights[2];
                var high = vertices[0];
                var right = vertices[1];
                var left = vertices[2];
                var newRight = -rightHeight / (highHeight - rightHeight) * (high - right) + right;
                var newLeft = -leftHeight / (highHeight - leftHeight) * (high - left) + left;
                triangles.Add((right, newLeft, newRight));
                triangles.Add((left, newLeft, right));
                Debug.DrawLine(newLeft, newRight, Color.blue);
            }
            // 1 of 3 submerged
            else if (vertexHeights[_mesh.triangles[i + orderByDescending[2]]] <= 0)
            {
                // 1---2
                //  \ /
                //   0
                var triVertexHeights = new float[3];
                var vertices = new Vector3[3];
                for (int j = 0; j < 3; j++)
                {
                    var vertexIndex = _mesh.triangles[i + (orderByDescending[2] + j) % 3];
                    triVertexHeights[j] = vertexHeights[vertexIndex];
                    vertices[j] = transform.TransformPoint(_mesh.vertices[vertexIndex]);
                }

                var lowHeight = triVertexHeights[0];
                var leftHeight = triVertexHeights[1];
                var rightHeight = triVertexHeights[2];
                var low = vertices[0];
                var left = vertices[1];
                var right = vertices[2];
                var newRight = -leftHeight / (lowHeight - leftHeight) * (low - left) + left;
                var newLeft = -rightHeight / (lowHeight - rightHeight) * (low - right) + right;
                triangles.Add((left, newLeft, newRight));
                triangles.Add((right, newLeft, right));
                Debug.DrawLine(newLeft, newRight, Color.blue);
            }
        }

        return triangles;
    }
}
