using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Buoyancy : HydrodynamicForce
{
    private const float WaterDensity = 1029f;
    
    private readonly Patch _patch;
    private readonly float _mass;
    
    public Buoyancy(Patch patch, float mass)
    {
        _patch = patch;
        _mass = mass;
    }
    
    public IEnumerable<(Vector3 force, Vector3 origin)> CalculateForce(ISet<(Vector3, Vector3, Vector3)> submergedTriangles)
    {
        Debug.Log("Calculating buoyancy");
        float buoyantForce = 0;
        var centers = new HashSet<Vector3>();
        foreach (var (a, b, c) in submergedTriangles)
        {
            var (hydrostaticForce, origin) = CalculateHydrostaticForce(a, b, c);
            buoyantForce += hydrostaticForce.y;
            centers.Add(origin);
            Debug.DrawRay(origin, hydrostaticForce / _mass);
        }
        var count = centers.Count;
        Debug.Log($"Calculated {count} hydrostatic forces");
        var averageCenter = count > 0 ? centers.Aggregate(Vector3.zero, (a, b) => a + b) / centers.Count : Vector3.zero;
        return new [] { (buoyantForce * Vector3.up, averageCenter) };
    }

    /// <summary>
    /// Calculates the hydrostatic force on a fully submerged triangle.
    /// Points must be given in clockwise order.
    /// </summary>
    private (Vector3 force, Vector3 origin) CalculateHydrostaticForce(Vector3 a, Vector3 b, Vector3 c)
    {
        var center = (a + b + c) / 3;
        var centerHeight = center.y - _patch.HeightAt(center);
        var crossProduct = Vector3.Cross(b - a, c - a);
        var area = crossProduct.magnitude / 2;
        var triangleNormal = crossProduct.normalized;
        var hydrostaticForce = -WaterDensity * Physics.gravity.y * centerHeight * area * triangleNormal;
        return (hydrostaticForce, center);
    }
}