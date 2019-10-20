using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(EdgeCollider2D))]
public class MetaballGenerator : MonoBehaviour
{

    [SerializeField] Vector2Int chunkSize = new Vector2Int(75, 40);
    [SerializeField] Material material;
    [SerializeField] int numCircles = 5;
    [SerializeField] Vector2 chunkScale = new Vector2(1f, 1f);
    [SerializeField] bool enableJob;

    [SerializeField] bool enableInterpolation;
    [SerializeField] bool enableTriangleIndexing;
    [SerializeField] bool enableGreedyMeshing;

    Vector2Int gridSize;

    Voxel[,] voxels; // For Managed Version
    Circle[] circles;

    // Mesh
    Mesh mesh;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    
    // For Avoid GC
    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();

    EdgeCollider2D edgeCollider;

    System.TimeSpan lastTimespan;

    public System.TimeSpan GetLastCalculateTime => lastTimespan;
    public int GetNumVertices => mesh.vertexCount;
    public int GetNumTriangles => mesh.triangles.Length;

    public bool EnableInterpolation => enableInterpolation;
    public bool EnableTriangleIndexing => enableTriangleIndexing;
    public bool EnableGreedyMeshing => enableGreedyMeshing;
    public bool EnableJob => enableJob;


    void Awake()
    {
        gridSize = chunkSize + Vector2Int.one;
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
    
    [BurstCompile]
    struct MetaballDensityJob : IJobParallelFor
    {
        [ReadOnly] public Vector2Int gridSize;
        [ReadOnly] public NativeArray<float> radiuses;
        [ReadOnly] public NativeArray<Vector3> positions;
        [ReadOnly] public int numCircles;
        
        [WriteOnly] public NativeArray<Voxel> voxels;
        
        public void Execute(int index)
        {
            int x = index % gridSize.x;
            int y = index / gridSize.x;
            
            float density = -1.0f;
            Vector2Int gridPosition = new Vector2Int(x, y);

            for (int i = 0; i < numCircles; i++)
            {
                float distance = Vector2.Distance(positions[i], gridPosition);
                density += Mathf.Max(0, radiuses[i] - distance);
            }

            Voxel voxel = new Voxel {Density = density};
            voxels[index] = voxel;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            enableJob = !enableJob;
        }
        
        if (Input.GetKeyDown(KeyCode.I))
        {
            enableInterpolation = !enableInterpolation;
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            enableTriangleIndexing = !enableTriangleIndexing;
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            enableGreedyMeshing = !enableGreedyMeshing;
        }

        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        if (enableJob)
        {
            MarchingSquaresWithJob();
        }
        else
        {
            MarchingSquaresWithManagedArray();
        }
        stopwatch.Stop();
        lastTimespan = stopwatch.Elapsed;
        stopwatch.Reset();
    }

    unsafe void MarchingSquaresWithJob()
    {
        int arraySize = gridSize.x * gridSize.y;
        
        NativeArray<Vector3> nativePositions = new NativeArray<Vector3>(circles.Length, Allocator.TempJob);
        NativeArray<float> nativeRadiuses = new NativeArray<float>(circles.Length, Allocator.TempJob);
        NativeArray<Voxel> nativeVoxels = new NativeArray<Voxel>(arraySize, Allocator.TempJob);

        for (int i = 0; i < circles.Length; i++)
        {
            UnsafeUtility.WriteArrayElement(nativeRadiuses.GetUnsafePtr(), i, circles[i].Radius);
            UnsafeUtility.WriteArrayElement(nativePositions.GetUnsafePtr(), i, circles[i].transform.position);
        }

        MetaballDensityJob job = new MetaballDensityJob
        {
            voxels = nativeVoxels,
            gridSize = gridSize,
            positions = nativePositions,
            radiuses = nativeRadiuses,
            numCircles = circles.Length
        };

        JobHandle handle = job.Schedule(arraySize, 32);
        handle.Complete();
        
        MarchingSquares.GenerateMarchingSquaresWithJob(nativeVoxels, chunkSize, chunkScale, enableInterpolation, enableTriangleIndexing, enableGreedyMeshing, ref vertices, ref triangles);
        
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        
        nativePositions.Dispose();
        nativeRadiuses.Dispose();
        nativeVoxels.Dispose();
    }

    void MarchingSquaresWithManagedArray()
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

        MarchingSquares.GenerateMarchingSquares(voxels, chunkSize, chunkScale, enableInterpolation, enableTriangleIndexing, enableGreedyMeshing, ref vertices, ref triangles);
        
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
    }
    
}


