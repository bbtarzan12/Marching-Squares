using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class Chunk : MonoBehaviour
{
    Vector2Int chunkPosition;
    Vector2Int chunkSize;
    Vector2 chunkScale;
    Voxel[,] voxels;
    bool dirty;
    
    // Mesh
    Mesh mesh;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;

    // For Avoid GC
    List<Vector3> verticies = new List<Vector3>();
    List<int> triangles = new List<int>();

    public Voxel[,] Voxels => voxels;
    public bool Dirty => dirty;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mesh = new Mesh();
    }

    void Start()
    {
        meshFilter.mesh = mesh;
    }
    
    public void Init(Vector2Int chunkPosition, Vector2Int chunkSize, Vector2 chunkScale)
    {
        this.chunkPosition = chunkPosition;
        this.chunkSize = chunkSize;
        this.chunkScale = chunkScale;

        voxels = new Voxel[chunkSize.x + 1, chunkSize.y + 1];
        
        GenerateVoxelData();

        dirty = true;
    }

    public void SetMaterial(Material material)
    {
        if (material is null)
        {
            meshRenderer.material = new Material(Shader.Find("Diffuse"));
        }
        else
        {
            meshRenderer.material = material;
        }
    }

    void GenerateVoxelData()
    {
        FastNoise noise = new FastNoise();
        noise.SetNoiseType(FastNoise.NoiseType.SimplexFractal);
        noise.SetFractalOctaves(5);
        noise.SetFrequency(0.01f);
        
        for (int x = 0; x < voxels.GetLength(0); x++)
        {
            for (int y = 0; y < voxels.GetLength(1); y++)
            {
                Vector2Int gridPosition = new Vector2Int(x, y) + chunkPosition * chunkSize;
                voxels[x,y] = new Voxel
                {
                    Density = noise.GetSimplexFractal(gridPosition.x, gridPosition.y)
                };
            }
        }
    }

    public void SetVoxel(Vector2Int worldGridPosition, float value)
    {
        Vector2Int gridPosition = new Vector2Int
        {
            x = worldGridPosition.x - chunkPosition.x * chunkSize.x,
            y = worldGridPosition.y - chunkPosition.y * chunkSize.y
        };

        if (gridPosition.x < 0 || gridPosition.y < 0)
            return;
        
        voxels[gridPosition.x, gridPosition.y].Density = value;
        dirty = true;
    }

    public void AddVoxel(Vector2Int worldGridPosition, float value)
    {
        Vector2Int gridPosition = new Vector2Int
        {
            x = worldGridPosition.x - chunkPosition.x * chunkSize.x,
            y = worldGridPosition.y - chunkPosition.y * chunkSize.y
        };

        if (gridPosition.x < 0 || gridPosition.y < 0)
            return;
        
        voxels[gridPosition.x, gridPosition.y].Density += value;
        dirty = true;
    }

    public Voxel GetVoxel(Vector2Int gridPosition)
    {
        return voxels[gridPosition.x + 1, gridPosition.y + 1];
    }

    public void UpdateMesh()
    {
        MarchingSquares.GenerateMarchingSquares(voxels, chunkSize, chunkScale, false, true, true, verticies, triangles);

        mesh.Clear();
        mesh.SetVertices(verticies);
        mesh.SetTriangles(triangles, 0);
        
        dirty = false;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 boxSize = chunkSize * chunkScale;
        Gizmos.DrawWireCube(transform.position + boxSize / 2.0f, boxSize);
    }
}