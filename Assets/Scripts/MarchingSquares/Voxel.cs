
using UnityEngine;

public struct Voxel
{
    float density;

    public float Density
    {
        get => density;
        set => density = value; //= Mathf.Clamp(value, -1f, 1f);
    }
}