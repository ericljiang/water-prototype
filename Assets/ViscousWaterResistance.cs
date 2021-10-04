using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ViscousWaterResistance : HydrodynamicForce
{
    private const float WaterDensity = 1029f;
    private const float WaterViscosity = 1e-6f; // m^2/s
    private readonly Vector3 _velocity;
    private readonly Vector3 _angularVelocity;
    private readonly Vector3 _centerOfGravity;

    public ViscousWaterResistance(Vector3 velocity, Vector3 angularVelocity, Vector3 centerOfGravity)
    {
        _velocity = velocity;
        _angularVelocity = angularVelocity;
        _centerOfGravity = centerOfGravity;
    }
    
    public IEnumerable<(Vector3 force, Vector3 origin)> CalculateForce(ISet<(Vector3, Vector3, Vector3)> submergedTriangles)
    {
        var speed = _velocity.magnitude;
        var length = 1f;
        var reynoldsNumber = ReynoldsNumber(speed, length, WaterViscosity);
        var referenceResistanceCoefficient = ReferenceResistanceCoefficient(reynoldsNumber);

        return submergedTriangles.Select(triangle =>
        {
            var (a, b, c) = triangle;
            // TODO calculate center, normal, and area outside of this class and reuse for all hydrodynamic forces
            var center = (a + b + c) / 3;
            var pointVelocity = _velocity + Vector3.Cross(_angularVelocity, center - _centerOfGravity);
            var pointSpeed = pointVelocity.magnitude;
            var crossProduct = Vector3.Cross(b - a, c - a);
            var normal = crossProduct.normalized;
            var flowDirection =
                Vector3.Cross(Vector3.Cross(pointVelocity, normal), normal)
                    .normalized; // could be written as dot products
            var flowVelocity = pointSpeed * flowDirection;
            var surfaceArea = crossProduct.magnitude / 2;
            // TODO multiply by local (1 + k) factor
            var drag = 0.5f * WaterDensity * referenceResistanceCoefficient * surfaceArea * pointSpeed * flowVelocity;
            return (drag, center);
        });
    }

    private static float ReynoldsNumber(float speed, float length, float viscosity)
    {
        return speed * length / viscosity;
    }

    /// <summary>
    /// C_F
    /// </summary>
    /// <returns></returns>
    private static float ReferenceResistanceCoefficient(float reynoldsNumber)
    {
        return 0.075f / Mathf.Pow(Mathf.Log(reynoldsNumber, 10) - 2, 2);
    }
}