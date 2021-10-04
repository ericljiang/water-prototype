using System.Collections.Generic;
using UnityEngine;

public interface HydrodynamicForce
{
    public abstract IEnumerable<(Vector3 force, Vector3 origin)> CalculateForce(
        ISet<(Vector3, Vector3, Vector3)> submergedTriangles);
}