using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(EdgeCollider2D))]
public class MetaballGenerator : MonoBehaviour
{

    [SerializeField] Vector2Int chunkSize = new Vector2Int(75, 40);
    [SerializeField] Vector2 chunkScale = new Vector2(1f, 1f);
    [SerializeField] Material material;
    [SerializeField] int numCircles = 5;

    Vector2Int gridSize => chunkSize + Vector2Int.one;
    
    Circle[] circles;
    Voxel[,] voxels;

    // Mesh
    Mesh mesh;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    
    // For Avoid GC
    List<Vector3> verticies = new List<Vector3>();
    List<int> triangles = new List<int>();

    EdgeCollider2D edgeCollider;
    
    void Awake()
    {
        voxels = new Voxel[gridSize.x, gridSize.y];
        
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mesh = new Mesh();

        edgeCollider = GetComponent<EdgeCollider2D>();
        
        circles = new Circle[numCircles]; 
    }

    void Start()
    {
        meshFilter.mesh = mesh;
        meshRenderer.material = material;

        Vector3 circlePosition = (Vector2) gridSize / 2.0f;
        for (int i = 0; i < numCircles; i++)
        {
            GameObject circleObject = new GameObject($"Circle {i}");
            Circle circle = circleObject.AddComponent<Circle>();
            circle.transform.position = circlePosition;
            
            circles[i] = circle;
        }

        edgeCollider.points = new[] {new Vector2(0, 0), new Vector2(0, gridSize.y), new Vector2(gridSize.x, gridSize.y), new Vector2(gridSize.x, 0), new Vector2(0, 0)};
        edgeCollider.edgeRadius = 1.0f;
    }

    void Update()
    {
        for (int x = 0; x < chunkSize.x; x++)
        {
            for (int y = 0; y < chunkSize.y; y++)
            {
                float density = -1.0f;
                Vector2Int gridPosition = new Vector2Int(x, y);

                foreach (Circle circle in circles)
                {
                    float distance = Vector2.Distance(circle.transform.position, gridPosition);
                    density += Mathf.Clamp(circle.Radius - distance, 0, float.MaxValue);
                }

                voxels[x, y].Density = density;
            }
        }
        
        verticies.Clear();
        triangles.Clear();
        
        MarchingSquares.GenerateMarchingSquares(voxels, chunkSize, chunkScale, true, true, false, ref verticies, ref triangles);
        
        mesh.Clear();
        mesh.SetVertices(verticies);
        mesh.SetTriangles(triangles, 0);
    }

}