using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PressureDrag : HydrodynamicForce
{
    private readonly Vector3 _velocity;
    private readonly Vector3 _angularVelocity;
    private readonly Vector3 _centerOfGravity;
    private readonly float _linearPressureCoefficient;
    private readonly float _quadraticPressureCoefficient;
    private readonly float _linearSuctionCoefficient;
    private readonly float _quadraticSuctionCoefficient;
    private readonly float _pressureFallOffPower;
    private readonly float _suctionFallOffPower;

    public PressureDrag(Vector3 velocity, Vector3 angularVelocity, Vector3 centerOfGravity, float linearPressureCoefficient, float quadraticPressureCoefficient, float linearSuctionCoefficient, float quadraticSuctionCoefficient, float pressureFallOffPower, float suctionFallOffPower)
    {
        _velocity = velocity;
        _angularVelocity = angularVelocity;
        _centerOfGravity = centerOfGravity;
        _linearPressureCoefficient = linearPressureCoefficient;
        _quadraticPressureCoefficient = quadraticPressureCoefficient;
        _linearSuctionCoefficient = linearSuctionCoefficient;
        _quadraticSuctionCoefficient = quadraticSuctionCoefficient;
        _pressureFallOffPower = pressureFallOffPower;
        _suctionFallOffPower = suctionFallOffPower;
    }
    public IEnumerable<(Vector3 force, Vector3 origin)> CalculateForce(ISet<(Vector3, Vector3, Vector3)> submergedTriangles)
    {
        return submergedTriangles.Select(triangle =>
        {
            var (a, b, c) = triangle;
            var center = (a + b + c) / 3;
            var pointVelocity = _velocity + Vector3.Cross(_angularVelocity, center - _centerOfGravity);
            var pointSpeed = pointVelocity.magnitude;
            var left = b - a;
            var right = c - a;
            var cosine = Vector3.Dot(left, right) / left.magnitude / right.magnitude;
            var crossProduct = Vector3.Cross(left, right);
            var area = crossProduct.magnitude / 2;
            var normal = crossProduct.normalized;
            if (cosine > 0)
            {
                var force = -1 * (_linearPressureCoefficient * pointSpeed + _quadraticPressureCoefficient * Mathf.Pow(pointSpeed, 2)) * area * Mathf.Pow(area, _pressureFallOffPower) * normal;
                return (force, center);
            }
            else if (cosine < 0)
            {
                var force = (_linearSuctionCoefficient * pointSpeed + _quadraticSuctionCoefficient * Mathf.Pow(pointSpeed, 2)) * area * Mathf.Pow(area, _suctionFallOffPower) * normal;
                return (force, center);
            }
            else
            {
                return (Vector3.zero, center);
            }
        });
    }
}