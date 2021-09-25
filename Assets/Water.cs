using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Water : MonoBehaviour
{
    [SerializeField] private int width, length;
    
    private int vertWidth, vertLength;

    private Mesh mesh;

    // Start is called before the first frame update
    void Start()
    {
        vertWidth = width + 1;
        vertLength = length + 1;
        
        var meshFilter = gameObject.AddComponent<MeshFilter>();
        mesh = new Mesh
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
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
    }

    void FixedUpdate()
    {
        mesh.vertices = (
            from x in Enumerable.Range(0, vertWidth)
            from z in Enumerable.Range(0, vertLength)
            select new Vector3(x - width / 2f, Mathf.Sin(Time.fixedTime + x - z / 2f) / 2, z - length / 2f)
        ).ToArray();
        mesh.RecalculateNormals();
    }
}
