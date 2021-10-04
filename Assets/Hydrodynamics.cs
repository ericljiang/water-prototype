using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Hydrodynamics : MonoBehaviour
{
    [SerializeField] private GameObject water;
    
    [Header("Debug")]
    [SerializeField] private bool applyForce;
    [SerializeField] private bool drawForces;
    [SerializeField] private bool drawForceComponents;
    [SerializeField] private bool fakeWaveHeight;
    
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
        Debug.Log("========= Calculating hydrodynamic forces =========");
        Debug.Log("Creating sample patch");
        var bounds = _renderer.bounds;
        var patch = Patch.SampleCollider(_waterSurfaceCollider, bounds.min, bounds.max, fakeWaveHeight);
        Debug.Log($"Created sample patch width={patch.Width} length={patch.Length}");

        // calculate heights above water
        var vertexHeights = new float[_mesh.vertices.Length];
        for (var i = 0; i < _mesh.vertices.Length; i++)
        {
            var vertex = transform.TransformPoint(_mesh.vertices[i]);
            vertexHeights[i] = vertex.y - patch.HeightAt(vertex);
        }

        var submergedTriangles = CalculateSubmergedTriangles(vertexHeights);

        var buoyancy = new Buoyancy(patch, _rigidbody.mass);
        var viscousWaterResistance = new ViscousWaterResistance(
            _rigidbody.velocity, _rigidbody.angularVelocity, _rigidbody.worldCenterOfMass);
        ApplyForces(buoyancy.CalculateForce(submergedTriangles), Color.green);
        ApplyForces(viscousWaterResistance.CalculateForce(submergedTriangles), Color.red);

    }

    private void ApplyForces(IEnumerable<(Vector3 force, Vector3 origin)> forces, Color color)
    {
        foreach (var (force, origin) in forces)
        {
            if (applyForce)
            {
                _rigidbody.AddForceAtPosition(force, origin);
            }
            if (drawForces)
            {
                Debug.DrawRay(origin, force / _rigidbody.mass, color);
            }
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
