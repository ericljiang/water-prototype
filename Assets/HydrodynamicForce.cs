using System.Collections.Generic;
using UnityEngine;

public interface HydrodynamicForce
{
    public abstract (float force, Vector3 origin) CalculateForce(
        IEnumerable<(Vector3, Vector3, Vector3)> submergedTriangles);
}