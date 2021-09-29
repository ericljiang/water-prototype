using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Water : MonoBehaviour
{
    [SerializeField] private int width, length;
    
    private int vertWidth, vertLength;

    private Mesh _mesh;

    private MeshCollider _meshCollider;

    // Start is called before the first frame update
    void Start()
    {
        vertWidth = width + 1;
        vertLength = length + 1;
        
        var meshFilter = GetComponent<MeshFilter>();
        _mesh = new Mesh
        {
            vertices =
            (
                from x in Enumerable.Range(0, vertWidth)
                from z in Enumerable.Range(0, vertLength)
                select new Vector3(x - width / 2f, 0, z - length / 2f)
            ).ToArray(),
            triangles =
            (
                from x in Enumerable.Range(0, vertWidth - 1)
                from z in Enumerable.Range(0, vertLength - 1)
                from index in new List<int>
                {
                    x + z * vertWidth,
                    (x + 1) + (z + 1) * vertWidth,
                    x + (z + 1) * vertWidth,
                    x + z * vertWidth,
                    (x + 1) + z * vertWidth,
                    (x + 1) + (z + 1) * vertWidth
                }
                select index
            ).ToArray()
        };
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
        meshFilter.mesh = _mesh;
        _meshCollider = GetComponent<MeshCollider>();
        _meshCollider.sharedMesh = _mesh;
    }

    void FixedUpdate()
    {
        _mesh.vertices = (
            from x in Enumerable.Range(0, vertWidth)
            from z in Enumerable.Range(0, vertLength)
            select new Vector3(x - width / 2f, Mathf.Sin(Time.fixedTime + x - z / 2f) / 2, z - length / 2f)
        ).ToArray();
        _mesh.RecalculateNormals();
        _meshCollider.sharedMesh = _mesh;
    }
}
