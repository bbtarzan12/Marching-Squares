
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MarchingSquares
{
    public static void GenerateMarchingSquares(Voxel[,] voxels, Vector2Int chunkSize, Vector2 chunkScale, bool interpolate, bool triangleIndexing, bool greedyMeshing, ref List<Vector3> verticies, ref List<int> triangles)
    {
        Dictionary<Vector3, int> points = new Dictionary<Vector3, int>();
        Dictionary<Vector2Int, Vector3[]> quadTriangles = new Dictionary<Vector2Int, Vector3[]>();

        for (int x = 0; x < chunkSize.x; x++)
        {
            for (int y = 0; y < chunkSize.y; y++)
            {
                float[] densities = new float[4];

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
                if(intersectionBits == 0)
                    continue;

                
                // 교차하는 변 찾기
                int edgeBits = EdgeTable[intersectionBits];

                
                // 교차하는 변의 교차점 보간하여 찾기
                Vector2[] interpolatedPoints = new Vector2[4];
                for (int i = 0; i < 4; i++) // 각 변을 순회
                {
                    if ((edgeBits & (1 << (3 - i))) != 0) // 만약 변이 교차되었다면
                    {
                        Vector2 edge0 = (gridPosition + CornerTable[CornerofEdgeTable[i, 0]]);
                        Vector2 edge1 = (gridPosition + CornerTable[CornerofEdgeTable[i, 1]]);

                        if (interpolate) // 보간을 사용하는가?
                        {
                            float v0 = densities[CornerofEdgeTable[i, 0]];
                            float v1 = densities[CornerofEdgeTable[i, 1]];
                            interpolatedPoints[i] = VectorInterp(edge0, edge1, v0, v1);   
                        }
                        else
                        {
                            interpolatedPoints[i] = (edge0 + edge1) / 2.0f; // 변의 중심점 사용
                        }
                    }
                }
                
                // 모서리와 변의 교차점을 합친 배열
                Vector2[] finalPoints = new Vector2[8];
                for (int i = 0; i < 4; i++)
                {
                    finalPoints[i] = gridPosition + CornerTable[i];
                }

                for (int i = 4; i < 8; i++)
                {
                    finalPoints[i] = interpolatedPoints[i - 4];
                }

                if (intersectionBits == 15)
                {
                    quadTriangles.Add(gridPosition, new Vector3[6]);
                }

                // 삼각형 만들기
                for (int i = 0; i < 9; i += 3)
                {
                    int index0 = TriangleTable[intersectionBits, i];
                    int index1 = TriangleTable[intersectionBits, i + 1];
                    int index2 = TriangleTable[intersectionBits, i + 2];
                    
                    if(index0 == -1 || index1 == -1 || index2 == -1)
                        break;

                    Vector3 vertex0 = finalPoints[index0] * chunkScale;
                    Vector3 vertex1 = finalPoints[index1] * chunkScale;
                    Vector3 vertex2 = finalPoints[index2] * chunkScale;
                    
                    // 꽉찬 사각형은 나중에 쓸일이 따로 있으므로 따로 추가하고 넘긴다
                    if (intersectionBits == 15)
                    {
                        quadTriangles[gridPosition][i] = vertex0;
                        quadTriangles[gridPosition][i + 1] = vertex1;
                        quadTriangles[gridPosition][i + 2] = vertex2;
                        continue;
                    }
                    
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
                        verticies.Add(vertex0);
                        verticies.Add(vertex1);
                        verticies.Add(vertex2);

                        int numTriangles = triangles.Count;
                        triangles.Add(numTriangles++);
                        triangles.Add(numTriangles++);
                        triangles.Add(numTriangles);
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
                    if (!quadTriangles.ContainsKey(gridPosition))
                    {
                        y++;
                        continue;
                    }

                    // 높이 계산
                    int height = 0;
                    for (int dy = gridPosition.y; dy < chunkSize.y; dy++)
                    {
                        Vector2Int subGridPosition = new Vector2Int(gridPosition.x, dy);
                        if (!quadTriangles.ContainsKey(subGridPosition))
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
                            if (!quadTriangles.ContainsKey(subGridPosition))
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

                    // 사용한 quadTriangles 삭제하기
                    for (int dx = gridPosition.x; dx < gridPosition.x + width; dx++)
                    {
                        for (int dy = gridPosition.y; dy < gridPosition.y + height; dy++)
                        {
                            quadTriangles.Remove(new Vector2Int(dx, dy));
                        }
                    }
                    
                    // width height 만큼 mesh 만들기
                    Vector2Int size = new Vector2Int(width, height);
                    Vector3[] quad = new Vector3[4]
                    {
                        (gridPosition + CornerTable[0] * size) * chunkScale,
                        (gridPosition + CornerTable[1] * size) * chunkScale,
                        (gridPosition + CornerTable[2] * size) * chunkScale,
                        (gridPosition + CornerTable[3] * size) * chunkScale
                    };

                    for (int i = 0; i < 6; i+=3)
                    {
                        int index0 = TriangleTable[15, i];
                        int index1 = TriangleTable[15, i + 1];
                        int index2 = TriangleTable[15, i + 2];

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
                            verticies.Add(vertex0);
                            verticies.Add(vertex1);
                            verticies.Add(vertex2);
                        
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
            foreach (Vector3[] quad in quadTriangles.Values)
            {
                if (triangleIndexing)
                {
                    foreach (Vector3 vertex in quad)
                    {
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
                else
                {
                    verticies.AddRange(quad);

                    int numTriangle = triangles.Count;
                    triangles.Add(numTriangle++);
                    triangles.Add(numTriangle++);
                    triangles.Add(numTriangle++);
                    triangles.Add(numTriangle++);
                    triangles.Add(numTriangle++);
                    triangles.Add(numTriangle);
                }
            }   
        }

        if (triangleIndexing)
        {
            verticies = new List<Vector3>(points.Keys);
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
    public static Vector2Int[] CornerTable =
    {
        new Vector2Int(0, 1), 
        new Vector2Int(1, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, 0),
    };

    public static int[,] CornerofEdgeTable =
    {
        {0, 1},
        {1, 2},
        {2, 3},
        {3, 0}
    };

    public static int[] EdgeTable =
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

    public static int[,] TriangleTable =
    {
    //  0~3 : 사각형의 꼭짓점, 4~7 : 변의 보간된 점 // intersectionBits
        {-1, -1, -1, -1, -1, -1, -1, -1, -1},     // 0000
        {3, 7, 6, -1, -1, -1, -1, -1, -1},        // 0001
        {2, 6, 5, -1, -1, -1, -1, -1, -1},        // 0010
        {3, 7, 5, 3, 5, 2, -1, -1, -1},           // 0011
        {4, 1, 5, -1, -1, -1, -1, -1, -1},        // 0100
        {4, 1, 5, 3, 7, 6, -1, -1, -1},           // 0101
        {6, 4, 1, 6, 1, 2, -1, -1, -1},           // 0110
        {2, 3, 7, 2, 7, 4, 2, 4, 1},              // 0111
        {7, 0, 4, -1, -1, -1, -1, -1, -1},        // 1000
        {3, 0, 4, 3, 4, 6, -1, -1, -1},           // 1001
        {7, 0, 4, 2, 6, 5, -1, -1, -1},           // 1010
        {3, 0, 4, 3, 4, 5, 3, 5, 2},              // 1011
        {7, 0, 1, 7, 1, 5, -1, -1, -1},           // 1100
        {0, 1, 5, 0, 5, 6, 0, 6, 3},              // 1101
        {1, 2, 6, 1, 6, 7, 1, 7, 0},              // 1110
        {3, 0, 2, 2, 0, 1, -1, -1, -1}            // 1111
    };
}