using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Hydrodynamics : MonoBehaviour
{
    [SerializeField] private GameObject water;
    
    [Header("Pressure drag force parameters")]
    [SerializeField] private float linearPressureCoefficient;
    [SerializeField] private float quadraticPressureCoefficient;
    [SerializeField] private float linearSuctionCoefficient;
    [SerializeField] private float quadraticSuctionCoefficient;
    [SerializeField] private float pressureFallOffPower;
    [SerializeField] private float suctionFallOffPower;
    
    [Header("Debug")]
    [SerializeField] private bool applyForce;
    [SerializeField] private bool drawForces;
    [SerializeField] private bool drawForceComponents;
    [SerializeField] private bool fakeWaveHeight;
    
    private Rigidbody _rigidbody;
    private Mesh _mesh;
    private Renderer _renderer;
    private MeshCollider _waterSurfaceCollider;
    
    private int[] _meshTriangles;
    private Vector3[] _meshVertices;

    // Start is called before the first frame update
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _mesh = GetComponent<MeshFilter>().mesh;
        _renderer = GetComponent<Renderer>();
        _waterSurfaceCollider = water.GetComponent<MeshCollider>();
            
        _meshTriangles = _mesh.triangles;
        _meshVertices = _mesh.vertices;
        Debug.Log($"Mesh has {_meshTriangles.Length / 3} triangles");
    }

    private void FixedUpdate()
    {
        Debug.Log("========= Calculating hydrodynamic forces =========");
        var startTime = Time.realtimeSinceStartup;
        Debug.Log("Creating sample patch");
        var bounds = _renderer.bounds;
        var patch = Patch.SampleCollider(_waterSurfaceCollider, bounds.min, bounds.max, fakeWaveHeight);
        Debug.Log($"Created sample patch width={patch.Width} length={patch.Length}");

        // calculate heights above water
        var vertexHeights = new float[_meshVertices.Length];
        for (var i = 0; i < _meshVertices.Length; i++)
        {
            var vertex = transform.TransformPoint(_meshVertices[i]);
            vertexHeights[i] = vertex.y - patch.HeightAt(vertex);
        }

        var submergedTriangles = CalculateSubmergedTriangles(vertexHeights);
        Debug.Log($"Found {submergedTriangles.Count} submerged triangles.");

        var buoyancy = new Buoyancy(patch, _rigidbody.mass);
        var viscousWaterResistance = new ViscousWaterResistance(
            _rigidbody.velocity, _rigidbody.angularVelocity, _rigidbody.worldCenterOfMass);
        var pressureDrag = new PressureDrag(_rigidbody.velocity, _rigidbody.angularVelocity,
            _rigidbody.worldCenterOfMass, linearPressureCoefficient, quadraticPressureCoefficient,
            linearSuctionCoefficient, quadraticSuctionCoefficient, pressureFallOffPower, suctionFallOffPower);
        ApplyForces(buoyancy.CalculateForce(submergedTriangles), Color.green);
        ApplyForces(viscousWaterResistance.CalculateForce(submergedTriangles), Color.red);
        ApplyForces(pressureDrag.CalculateForce(submergedTriangles), Color.blue);
        var endTime = Time.realtimeSinceStartup;
        Debug.Log($"Finished calculating hydrodynamic forces in {(endTime - startTime) * 1000} ms.");
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
        for (int i = 0; i < _meshTriangles.Length; i += 3)
        {
            var orderByDescending = new[] {0, 1, 2};
            Array.Sort(orderByDescending, (i1, i2) => vertexHeights[_meshTriangles[i + i2]].CompareTo(vertexHeights[_meshTriangles[i + i1]]));
            // fully submerged
            if (vertexHeights[_meshTriangles[i + orderByDescending[0]]] <= 0)
            {
                var a = transform.TransformPoint(_meshVertices[_meshTriangles[i]]);
                var b = transform.TransformPoint(_meshVertices[_meshTriangles[i + 1]]);
                var c = transform.TransformPoint(_meshVertices[_meshTriangles[i + 2]]);
                triangles.Add((a, b, c));
            }
            // 2 of 3 submerged
            else if (vertexHeights[_meshTriangles[i + orderByDescending[1]]] <= 0)
            {
                //   0
                //  / \
                // 2---1
                var triVertexHeights = new float[3];
                var vertices = new Vector3[3];
                for (int j = 0; j < 3; j++)
                {
                    var vertexIndex = _meshTriangles[i + (orderByDescending[0] + j) % 3];
                    triVertexHeights[j] = vertexHeights[vertexIndex];
                    vertices[j] = transform.TransformPoint(_meshVertices[vertexIndex]);
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
            else if (vertexHeights[_meshTriangles[i + orderByDescending[2]]] <= 0)
            {
                // 1---2
                //  \ /
                //   0
                var triVertexHeights = new float[3];
                var vertices = new Vector3[3];
                for (int j = 0; j < 3; j++)
                {
                    var vertexIndex = _meshTriangles[i + (orderByDescending[2] + j) % 3];
                    triVertexHeights[j] = vertexHeights[vertexIndex];
                    vertices[j] = transform.TransformPoint(_meshVertices[vertexIndex]);
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
