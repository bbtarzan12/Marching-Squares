﻿using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

public static class MarchingSquares
{
    
    static unsafe void NativeAddRange<T>(this List<T> list, NativeSlice<T> nativeSlice) where T : struct
    {
        var index = list.Count;
        var newLength = index + nativeSlice.Length;
 
        // Resize our list if we require
        if (list.Capacity < newLength)
        {
            list.Capacity = newLength;
        }
 
        var items = NoAllocHelpers.ExtractArrayFromListT(list);
        var size = UnsafeUtility.SizeOf<T>();
 
        // Get the pointer to the end of the list
        var bufferStart = (IntPtr) UnsafeUtility.AddressOf(ref items[0]);
        var buffer = (byte*)(bufferStart + (size * index));
 
        UnsafeUtility.MemCpy(buffer, nativeSlice.GetUnsafePtr(), nativeSlice.Length * (long) size);
 
        NoAllocHelpers.ResizeList(list, newLength);
    }

    [BurstCompile]
    struct MarchingSquaresJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Voxel> voxels;

        [ReadOnly] public Vector2Int chunkSize;
        [ReadOnly] public Vector2 chunkScale;
        [ReadOnly] public bool interpolate;
        [ReadOnly] public bool triangleIndexing;
        [ReadOnly] public bool greedyMeshing;


        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<Vector3> vertices;
        [WriteOnly] public NativeArray<bool> quadMap;
        [WriteOnly] public NativeCounter.Concurrent counter;
        
        public unsafe void Execute(int idx)
        {
            int x = idx % chunkSize.x;
            int y = idx / chunkSize.x;

            float* densities = stackalloc float[4];

            // 사각형의 각 꼭짓점을 순회하면서 Density를 받아온다
            for (int i = 0; i < 4; i++)
            {
                densities[i] = voxels[(x + CornerTable[i].x) + (y + CornerTable[i].y) * (chunkSize.x + 1)].Density;
            }
            
            Vector2Int gridPosition = new Vector2Int(x, y);

            // 교차하는 지점을 찾는다
            // IsoLevel은 0이라고 가정
            int intersectionBits = 0;
            for (int i = 0; i < 4; i++)
            {
                if (densities[i] > 0)
                {
                    intersectionBits |= 1 << (3 - i);
                }
            }

            // 교차하는 지점이 없다면 끝낸다.
            if (intersectionBits == 0)
            {
                return;
            }


            // 교차하는 변 찾기
            int edgeBits = EdgeTable[intersectionBits];


            // 교차하는 변의 교차점 보간하여 찾기
            Vector2* interpolatedPoints = stackalloc Vector2[4];
            for (int i = 0; i < 4; i++) // 각 변을 순회
            {
                if ((edgeBits & (1 << (3 - i))) != 0) // 만약 변이 교차되었다면
                {
                    Vector2 edge0 = (gridPosition + CornerTable[CornerofEdgeTable[i].x]);
                    Vector2 edge1 = (gridPosition + CornerTable[CornerofEdgeTable[i].y]);

                    if (interpolate) // 보간을 사용하는가?
                    {
                        float v0 = densities[CornerofEdgeTable[i].x];
                        float v1 = densities[CornerofEdgeTable[i].y];
                        interpolatedPoints[i] = VectorInterp(edge0, edge1, v0, v1);
                    }
                    else
                    {
                        interpolatedPoints[i] = (edge0 + edge1) / 2.0f; // 변의 중심점 사용
                    }
                }
            }

            // 모서리와 변의 교차점을 합친 배열
            Vector2* finalPoints = stackalloc Vector2[8];
            for (int i = 0; i < 4; i++)
            {
                finalPoints[i] = gridPosition + CornerTable[i];
            }

            for (int i = 4; i < 8; i++)
            {
                finalPoints[i] = interpolatedPoints[i - 4];
            }

            if (intersectionBits == 15 && (triangleIndexing || greedyMeshing))
            {
                quadMap[idx] = true;
            }
            else
            {
                quadMap[idx] = false;
                
                // 삼각형 만들기
                for (int i = 0; i < 9; i += 3)
                {
                    int index0 = TriangleTable[intersectionBits][i];
                    int index1 = TriangleTable[intersectionBits][i + 1];
                    int index2 = TriangleTable[intersectionBits][i + 2];

                    if (index0 == -1 || index1 == -1 || index2 == -1)
                        break;
                
                    Vector3 vertex0 = finalPoints[index0] * chunkScale;
                    Vector3 vertex1 = finalPoints[index1] * chunkScale;
                    Vector3 vertex2 = finalPoints[index2] * chunkScale;

                    int triangleIndex = counter.Increment() * 3;
                    vertices[triangleIndex] = vertex0;
                    vertices[triangleIndex + 1] = vertex1;
                    vertices[triangleIndex + 2] = vertex2;
                }   
            }
        }
    }

    [BurstCompile]
    struct AddTriangleIndexForNoIndexing : IJobParallelFor
    {
        [WriteOnly] public NativeArray<int> triangles;
        
        public void Execute(int index)
        {
            triangles[index] = index;
        }
    }

    [BurstCompile]
    struct MakeQuadJob : IJobParallelFor
    {
        [ReadOnly] public Vector2Int chunkSize;
        [ReadOnly] public Vector2 chunkScale;
        [ReadOnly] public NativeArray<bool> quadMap;

        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<Vector3> vertices;
        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<int> triangles;
        [WriteOnly] public NativeCounter.Concurrent counter;
        
        public void Execute(int idx)
        {
            int x = idx % chunkSize.x;
            int y = idx / chunkSize.x;

            if (!quadMap[idx])
                return;

            int triangleIndex = counter.Increment() * 3;
            for (int i = 0; i < 3; i++)
            {
                 Vector3 vertex = new Vector3(CornerTable[TriangleTable[15][i]].x + x, CornerTable[TriangleTable[15][i]].y + y, 0) * chunkScale;
                 vertices[triangleIndex + i] = vertex;
                 triangles[triangleIndex + i] = triangleIndex + i;
            }
            
            triangleIndex = counter.Increment() * 3;
            for (int i = 0; i < 3; i++)
            {
                Vector3 vertex = new Vector3(CornerTable[TriangleTable[15][i + 3]].x + x, CornerTable[TriangleTable[15][i + 3]].y + y, 0) * chunkScale;
                vertices[triangleIndex + i] = vertex;
                triangles[triangleIndex + i] = triangleIndex + i;
            }
        }
    }

    public static void GenerateMarchingSquaresWithJob(NativeArray<Voxel> nativeVoxels, Vector2Int chunkSize, Vector2 chunkScale, bool interpolate, bool triangleIndexing, bool greedyMeshing, List<Vector3> vertices, List<int> triangles)
    {
        NativeArray<Vector3> nativeVertices = new NativeArray<Vector3>(9 * chunkSize.x * chunkSize.y , Allocator.TempJob);
        NativeArray<int> nativeTriangles = new NativeArray<int>(9 * chunkSize.x * chunkSize.y , Allocator.TempJob);
        NativeArray<bool> nativeQuadMap = new NativeArray<bool> (chunkSize.x * chunkSize.y, Allocator.TempJob);
        NativeCounter counter = new NativeCounter(Allocator.TempJob);
        
        MarchingSquaresJob marchingSquaresJob = new MarchingSquaresJob
        {
            vertices = nativeVertices,
            quadMap = nativeQuadMap,
            counter = counter.ToConcurrent(),
            voxels = nativeVoxels,
            chunkSize = chunkSize,
            chunkScale = chunkScale,
            interpolate = interpolate,
            triangleIndexing = triangleIndexing,
            greedyMeshing = greedyMeshing
        };
        
        JobHandle marchingSquaresJobHandle = marchingSquaresJob.Schedule(chunkSize.x * chunkSize.y, 32);
        marchingSquaresJobHandle.Complete();

        if (greedyMeshing)
        {
            GreedyMeshingForJob(chunkSize, chunkScale, nativeVertices, counter, nativeQuadMap);
        }
        else
        {
            MakeQuadJob quadJob = new MakeQuadJob
            {
                chunkSize = chunkSize,
                chunkScale = chunkScale,
                vertices = nativeVertices,
                triangles = nativeTriangles,
                quadMap = nativeQuadMap,
                counter = counter.ToConcurrent()
            };

            JobHandle quadJobHandle = quadJob.Schedule(chunkSize.x * chunkSize.y, 32);
            quadJobHandle.Complete();
        }

        int verticeSize = counter.Count * 3;
        int triangleSize = verticeSize;

        if (triangleIndexing)
        {
            verticeSize = TriangleIndexingForJob(nativeVertices, nativeTriangles, verticeSize);
        }
        else
        {
            AddTriangleIndexForNoIndexing triangleJob = new AddTriangleIndexForNoIndexing
            {
                triangles = nativeTriangles
            };

            JobHandle triangleJobHandle = triangleJob.Schedule(verticeSize, 32);
            triangleJobHandle.Complete();
        }

        NativeSlice<Vector3> nativeSliceVertices = new NativeSlice<Vector3>(nativeVertices, 0, verticeSize);
        NativeSlice<int> nativeSliceTriangles = new NativeSlice<int>(nativeTriangles, 0, triangleSize);

        vertices.Clear();
        triangles.Clear();
        
        vertices.NativeAddRange(nativeSliceVertices);
        triangles.NativeAddRange(nativeSliceTriangles);

        counter.Dispose();
        nativeQuadMap.Dispose();
        nativeVertices.Dispose();
        nativeTriangles.Dispose();
    }

    static unsafe void GreedyMeshingForJob(Vector2Int chunkSize, Vector2 chunkScale, NativeArray<Vector3> vertices, NativeCounter counter, NativeArray<bool> quadSet)
    {
        for (int x = 0; x < chunkSize.x; x++)
        {
            for (int y = 0; y < chunkSize.y;)
            {
                int idx = x + y * chunkSize.x;
                Vector2Int gridPosition = new Vector2Int(x, y);
                if (!quadSet[idx])
                {
                    y++;
                    continue;
                }

                // 높이 계산
                int height = 0;
                for (int dy = gridPosition.y; dy < chunkSize.y; dy++)
                {
                    Vector2Int subGridPosition = new Vector2Int(gridPosition.x, dy);
                    if (!quadSet[subGridPosition.x + subGridPosition.y * chunkSize.x])
                    {
                        break;
                    }

                    height++;
                }

                // 넓이 계산
                int width = 1;
                bool done = false;
                for (int dx = gridPosition.x + 1; dx < chunkSize.x; dx++)
                {
                    for (int dy = gridPosition.y; dy < gridPosition.y + height && dy < chunkSize.y; dy++)
                    {
                        Vector2Int subGridPosition = new Vector2Int(dx, dy);
                        if (!quadSet[subGridPosition.x + subGridPosition.y * chunkSize.x])
                        {
                            done = true;
                            break;
                        }
                    }

                    if (done)
                    {
                        break;
                    }

                    width++;
                }

                // 사용한 quadMap 삭제하기
                for (int dx = gridPosition.x; dx < gridPosition.x + width; dx++)
                {
                    for (int dy = gridPosition.y; dy < gridPosition.y + height; dy++)
                    {
                        quadSet[dx + dy * chunkSize.x] = false;
                    }
                }

                // width height 만큼 mesh 만들기
                Vector2Int size = new Vector2Int(width, height);
                Vector2* quad = stackalloc[] {(gridPosition + CornerTable[0] * size) * chunkScale, (gridPosition + CornerTable[1] * size) * chunkScale, (gridPosition + CornerTable[2] * size) * chunkScale, (gridPosition + CornerTable[3] * size) * chunkScale};

                for (int i = 0; i < 6; i += 3)
                {
                    int index0 = TriangleTable[15][i];
                    int index1 = TriangleTable[15][i + 1];
                    int index2 = TriangleTable[15][i + 2];

                    Vector3 vertex0 = quad[index0];
                    Vector3 vertex1 = quad[index1];
                    Vector3 vertex2 = quad[index2];

                    int triangleIndex = counter.Increment() * 3;
                    
                    vertices[triangleIndex] = vertex0;
                    vertices[triangleIndex + 1] = vertex1;
                    vertices[triangleIndex + 2] = vertex2;
                }

                y += height;
            }
        }
    }

    static int TriangleIndexingForJob(NativeArray<Vector3> vertices, NativeArray<int> triangles, int arraySize)
    {
        // Fix IL2CPP
        if (points == null)
        {
            points = new Dictionary<Vector3, int>();
        }

        points.Clear();

        for (int i = 0; i < arraySize; i++)
        {
            if (points.ContainsKey(vertices[i]))
            {
                triangles[i] = points[vertices[i]];
            }
            else
            {
                int numTriangles = points.Count;
                points.Add(vertices[i], numTriangles);
                triangles[i] = numTriangles;
                vertices[numTriangles] = vertices[i];
            }
        }

        return points.Count;
    }

    // For avoid GC
    static Dictionary<Vector3, int> points;
    static bool[,] quadMap;

    public static unsafe void GenerateMarchingSquares(Voxel[,] voxels, Vector2Int chunkSize, Vector2 chunkScale, bool interpolate, bool triangleIndexing, bool greedyMeshing, List<Vector3> vertices, List<int> triangles)
    {
        vertices.Clear();
        triangles.Clear();

        // Fix IL2CPP
        if (points == null)
        {
            points = new Dictionary<Vector3, int>();
        }

        if (quadMap == null)
        {
            quadMap = new bool[voxels.GetLength(0), voxels.GetLength(1)];
        }
        
        points.Clear();

        // For avoid GC
        float* densities = stackalloc float[4];
        Vector2* interpolatedPoints = stackalloc Vector2[4];
        Vector2* finalPoints = stackalloc Vector2[8];

        for (int x = 0; x < chunkSize.x; x++)
        {
            for (int y = 0; y < chunkSize.y; y++)
            {
                // 사각형의 각 꼭짓점을 순회하면서 Density를 받아온다
                for (int i = 0; i < 4; i++)
                {
                    densities[i] = voxels[x + CornerTable[i].x, y + CornerTable[i].y].Density;
                }

                Vector2Int gridPosition = new Vector2Int(x, y);

                // 교차하는 지점을 찾는다
                // IsoLevel은 0이라고 가정
                int intersectionBits = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (densities[i] > 0)
                    {
                        intersectionBits |= 1 << (3 - i);
                    }
                }
                
                // 교차하는 지점이 없다면 끝낸다.
                if (intersectionBits == 0)
                {
                    quadMap[x, y] = false;
                    continue;
                }

                // 교차하는 변 찾기
                int edgeBits = EdgeTable[intersectionBits];
                
                // 교차하는 변의 교차점 보간하여 찾기
                for (int i = 0; i < 4; i++) // 각 변을 순회
                {
                    if ((edgeBits & (1 << (3 - i))) != 0) // 만약 변이 교차되었다면
                    {
                        Vector2 edge0 = (gridPosition + CornerTable[CornerofEdgeTable[i].x]);
                        Vector2 edge1 = (gridPosition + CornerTable[CornerofEdgeTable[i].y]);

                        if (interpolate) // 보간을 사용하는가?
                        {
                            float v0 = densities[CornerofEdgeTable[i].x];
                            float v1 = densities[CornerofEdgeTable[i].y];
                            interpolatedPoints[i] = VectorInterp(edge0, edge1, v0, v1);   
                        }
                        else
                        {
                            interpolatedPoints[i] = (edge0 + edge1) / 2.0f; // 변의 중심점 사용
                        }
                    }
                }
                
                // 모서리와 변의 교차점을 합친 배열
                for (int i = 0; i < 4; i++)
                {
                    finalPoints[i] = gridPosition + CornerTable[i];
                }

                for (int i = 4; i < 8; i++)
                {
                    finalPoints[i] = interpolatedPoints[i - 4];
                }

                if (intersectionBits == 15 && (triangleIndexing || greedyMeshing))
                {
                    quadMap[x, y] = true;
                }
                else
                {
                    quadMap[x, y] = false;
                    
                    // 삼각형 만들기
                    for (int i = 0; i < 9; i += 3)
                    {
                        int index0 = TriangleTable[intersectionBits][i];
                        int index1 = TriangleTable[intersectionBits][i + 1];
                        int index2 = TriangleTable[intersectionBits][i + 2];

                        if (index0 == -1 || index1 == -1 || index2 == -1)
                            break;

                        Vector3 vertex0 = finalPoints[index0] * chunkScale;
                        Vector3 vertex1 = finalPoints[index1] * chunkScale;
                        Vector3 vertex2 = finalPoints[index2] * chunkScale;

                        if (triangleIndexing)
                        {
                            if (points.ContainsKey(vertex0))
                            {
                                triangles.Add(points[vertex0]);
                            }
                            else
                            {
                                int index = points.Count;
                                points.Add(vertex0, index);
                                triangles.Add(index);
                            }

                            if (points.ContainsKey(vertex1))
                            {
                                triangles.Add(points[vertex1]);
                            }
                            else
                            {
                                int index = points.Count;
                                points.Add(vertex1, index);
                                triangles.Add(index);
                            }

                            if (points.ContainsKey(vertex2))
                            {
                                triangles.Add(points[vertex2]);
                            }
                            else
                            {
                                int index = points.Count;
                                points.Add(vertex2, index);
                                triangles.Add(index);
                            }
                        }
                        else
                        {
                            vertices.Add(vertex0);
                            vertices.Add(vertex1);
                            vertices.Add(vertex2);

                            int numTriangles = triangles.Count;
                            triangles.Add(numTriangles++);
                            triangles.Add(numTriangles++);
                            triangles.Add(numTriangles);
                        }
                    }
                }
            }
        }
        
        if (greedyMeshing)
        {
            for (int x = 0; x < chunkSize.x; x++)
            {
                for (int y = 0; y < chunkSize.y;)
                {
                    Vector2Int gridPosition = new Vector2Int(x, y);
                    if (!quadMap[x,y])
                    {
                        y++;
                        continue;
                    }

                    // 높이 계산
                    int height = 0;
                    for (int dy = gridPosition.y; dy < chunkSize.y; dy++)
                    {
                        Vector2Int subGridPosition = new Vector2Int(gridPosition.x, dy);
                        if (!quadMap[subGridPosition.x, subGridPosition.y])
                        {
                            break;
                        }

                        height++;
                    }
                    
                    // 넓이 계산
                    int width = 1;
                    bool done = false;
                    for (int dx = gridPosition.x + 1; dx < chunkSize.x; dx++)
                    {
                        for (int dy = gridPosition.y; dy < gridPosition.y + height && dy < chunkSize.y; dy++)
                        {
                            Vector2Int subGridPosition = new Vector2Int(dx, dy);
                            if (!quadMap[subGridPosition.x, subGridPosition.y])
                            {
                                done = true;
                                break;
                            }
                        }
                        
                        if (done)
                        {
                            break;
                        }

                        width++;
                    }

                    // 사용한 quadMap 삭제하기
                    for (int dx = gridPosition.x; dx < gridPosition.x + width; dx++)
                    {
                        for (int dy = gridPosition.y; dy < gridPosition.y + height; dy++)
                        {
                            quadMap[dx, dy] = false;
                        }
                    }
                    
                    // width height 만큼 mesh 만들기
                    Vector2Int size = new Vector2Int(width, height);
                    Vector3* quad = stackalloc Vector3[]
                    {
                        (gridPosition + CornerTable[0] * size) * chunkScale,
                        (gridPosition + CornerTable[1] * size) * chunkScale,
                        (gridPosition + CornerTable[2] * size) * chunkScale,
                        (gridPosition + CornerTable[3] * size) * chunkScale
                    };

                    for (int i = 0; i < 6; i+=3)
                    {
                        int index0 = TriangleTable[15][i];
                        int index1 = TriangleTable[15][i + 1];
                        int index2 = TriangleTable[15][i + 2];

                        Vector3 vertex0 = quad[index0];
                        Vector3 vertex1 = quad[index1];
                        Vector3 vertex2 = quad[index2];
                        
                        if (triangleIndexing)
                        {
                            if (points.ContainsKey(vertex0))
                            {
                                triangles.Add(points[vertex0]);
                            }
                            else
                            {
                                int index = points.Count;
                                points.Add(vertex0, index);
                                triangles.Add(index);
                            }
                            
                            if (points.ContainsKey(vertex1))
                            {
                                triangles.Add(points[vertex1]);
                            }
                            else
                            {
                                int index = points.Count;
                                points.Add(vertex1, index);
                                triangles.Add(index);
                            }
                            
                            if (points.ContainsKey(vertex2))
                            {
                                triangles.Add(points[vertex2]);
                            }
                            else
                            {
                                int index = points.Count;
                                points.Add(vertex2, index);
                                triangles.Add(index);
                            }
                        }
                        else
                        {
                            vertices.Add(vertex0);
                            vertices.Add(vertex1);
                            vertices.Add(vertex2);
                        
                            int numTriangle = triangles.Count;
                            triangles.Add(numTriangle++);
                            triangles.Add(numTriangle++);
                            triangles.Add(numTriangle);   
                        }
                    }
                    
                    
                    y += height;
                }
            }
        }
        else
        {
            // 앞서 추가하지 않은 꽉찬 사각형(1111) 추가
            for (int x = 0; x < quadMap.GetLength(0); x++)
            {
                for (int y = 0; y < quadMap.GetLength(1); y++)
                {
                    if(!quadMap[x,y])
                        continue;
                    
                    for (int vertexIndex = 0; vertexIndex < 6; vertexIndex++)
                    {
                        Vector3 vertex = new Vector3(CornerTable[TriangleTable[15][vertexIndex]].x + x, CornerTable[TriangleTable[15][vertexIndex]].y + y, 0) * chunkScale;
                        if (points.ContainsKey(vertex))
                        {
                            triangles.Add(points[vertex]);
                        }
                        else
                        {
                            int index = points.Count;
                            points.Add(vertex, index);
                            triangles.Add(index);
                        }
                    }
                }
            }
        }

        if (triangleIndexing)
        {
            vertices.AddRange(points.Keys);
        }
    }

    static Vector2 VectorInterp(Vector2 p0, Vector2 p1, float v0, float v1)
    {
        // IsoLevel은 0이라 가정

        if (Mathf.Abs(v0) < 0.00001f)
            return p0;

        if (Mathf.Abs(v1) < 0.00001f)
            return p1;

        if (Mathf.Abs(v0 - v1) < 0.00001f)
            return p0;
        
        float mu = (0 - v0) / (v1 - v0);
        return new Vector2{x = p0.x + mu * (p1.x - p0.x), y = p0.y + mu * (p1.y - p0.y)};
    }

    /*
     *    0        1
     *    ┌─────────┐
     *    │    4    │        0~3 : 모서리
     *    │7       5│        4~7 : 변
     *    │    6    │        
     *    └─────────┘        
     *    3        2
     */
    public static readonly Vector2Int[] CornerTable =
    {
        new Vector2Int(0, 1), 
        new Vector2Int(1, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, 0),
    };

    public static readonly Vector2Int[] CornerofEdgeTable =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 2),
        new Vector2Int(2, 3),
        new Vector2Int(3, 0)
    };

    public static readonly int[] EdgeTable =
    {
        0,    //{0, 0, 0, 0},    // 0000
        3,    //{0, 0, 1, 1},    // 0001
        6,    //{0, 1, 1, 0},    // 0010
        5,    //{0, 1, 0, 1},    // 0011
        12,   //{1, 1, 0, 0},    // 0100
        15,   //{1, 1, 1, 1},    // 0101
        10,   //{1, 0, 1, 0},    // 0110
        9,    //{1, 0, 0, 1},    // 0111
        9,    //{1, 0, 0, 1},    // 1000
        10,   //{1, 0, 1, 0},    // 1001
        15,   //{1, 1, 1, 1},    // 1010
        12,   //{1, 1, 0, 0},    // 1011
        5,    //{0, 1, 0, 1},    // 1100
        6,    //{0, 1, 1, 0},    // 1101
        3,    //{0, 0, 1, 1},    // 1110
        0,    //{0, 0, 0, 0},    // 1111
    };

    public static readonly TriangleTableEntry[] TriangleTable =
    {
    //  0~3 : 사각형의 꼭짓점, 4~7 : 변의 보간된 점 // intersectionBits
        new TriangleTableEntry(-1, -1, -1, -1, -1, -1, -1, -1, -1),     // 0000
        new TriangleTableEntry(3, 7, 6, -1, -1, -1, -1, -1, -1),        // 0001
        new TriangleTableEntry(2, 6, 5, -1, -1, -1, -1, -1, -1),        // 0010
        new TriangleTableEntry(3, 7, 5, 3, 5, 2, -1, -1, -1),           // 0011
        new TriangleTableEntry(4, 1, 5, -1, -1, -1, -1, -1, -1),        // 0100
        new TriangleTableEntry(4, 1, 5, 3, 7, 6, -1, -1, -1),           // 0101
        new TriangleTableEntry(6, 4, 1, 6, 1, 2, -1, -1, -1),           // 0110
        new TriangleTableEntry(2, 3, 7, 2, 7, 4, 2, 4, 1),              // 0111
        new TriangleTableEntry(7, 0, 4, -1, -1, -1, -1, -1, -1),        // 1000
        new TriangleTableEntry(3, 0, 4, 3, 4, 6, -1, -1, -1),           // 1001
        new TriangleTableEntry(7, 0, 4, 2, 6, 5, -1, -1, -1),           // 1010
        new TriangleTableEntry(3, 0, 4, 3, 4, 5, 3, 5, 2),              // 1011
        new TriangleTableEntry(7, 0, 1, 7, 1, 5, -1, -1, -1),           // 1100
        new TriangleTableEntry(0, 1, 5, 0, 5, 6, 0, 6, 3),              // 1101
        new TriangleTableEntry(1, 2, 6, 1, 6, 7, 1, 7, 0),              // 1110
        new TriangleTableEntry(3, 0, 2, 2, 0, 1, -1, -1, -1)            // 1111
    };
    
    public struct TriangleTableEntry
    {
        public TriangleTableEntry(int value0, int value1, int value2, int value3, int value4, int value5, int value6, int value7, int value8)
        {
            this.value0 = value0;
            this.value1 = value1;
            this.value2 = value2;
            this.value3 = value3;
            this.value4 = value4;
            this.value5 = value5;
            this.value6 = value6;
            this.value7 = value7;
            this.value8 = value8;
        }

        public int this[int key]
        {
            get
            {
                switch (key)
                {
                    case 0: return value0;
                    case 1: return value1;
                    case 2: return value2;
                    case 3: return value3;
                    case 4: return value4;
                    case 5: return value5;
                    case 6: return value6;
                    case 7: return value7;
                    case 8: return value8;
                    default: return -1;
                }
            }
        }
        
        readonly int value0;
        readonly int value1;
        readonly int value2;
        readonly int value3;
        readonly int value4;
        readonly int value5;
        readonly int value6;
        readonly int value7;
        readonly int value8;
    }
}