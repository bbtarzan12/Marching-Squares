using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
    [SerializeField] Vector2Int chunkSize;
    [SerializeField] Vector2 chunkScale; 
    [SerializeField] Vector2Int chunkSpawnSize;

    [SerializeField] Material mapMaterial;
    [SerializeField] Transform target;

    Vector2Int lastTargetChunkPosition;

    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int gridPosition = WorldToGrid(worldPosition);

            for (int x = gridPosition.x - 1; x <= gridPosition.x + 1; x++)
            {
                for (int y = gridPosition.y - 1; y <= gridPosition.y + 1; y++)
                {
                    AddVoxel(new Vector2Int(x, y), 0.1f);
                } 
            }
        }
        
        if (Input.GetMouseButton(1))
        {
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int gridPosition = WorldToGrid(worldPosition);
            
            for (int x = gridPosition.x - 1; x <= gridPosition.x + 1; x++)
            {
                for (int y = gridPosition.y - 1; y <= gridPosition.y + 1; y++)
                {
                    AddVoxel(new Vector2Int(x, y), -0.1f);
                } 
            }
        }

        GenerateChunkByTargetPosition();
        UpdateChunkMesh();
    }

    void GenerateChunkByTargetPosition()
    {
        if (target is null)
            return;
        
        Vector2Int chunkPosition = WorldToChunk(target.position);

        if (lastTargetChunkPosition == chunkPosition)
            return;

        for (int x = chunkPosition.x - chunkSpawnSize.x; x <= chunkPosition.x + chunkSpawnSize.x; x++)
        {
            for (int y = chunkPosition.y - chunkSpawnSize.y; y <= chunkPosition.y + chunkSpawnSize.y; y++)
            {
                GenerateChunk(new Vector2Int(x, y));
            }
        }

        lastTargetChunkPosition = chunkPosition;
    }

    void UpdateChunkMesh()
    {
        foreach (Chunk chunk in chunks.Values)
        {
            if (!chunk.Dirty)
                continue;

            chunk.UpdateMesh();
        }
    }

    void SetVoxel(Vector2Int gridPosition, float density)
    {
        Vector2Int chunkPosition = GridtoChunk(gridPosition);
        if (GetChunk(chunkPosition, out Chunk chunk, true))
        {
            chunk.SetVoxel(gridPosition, density);
        }
        
        Vector2Int gridRemainder = new Vector2Int {x = Mod(gridPosition.x, chunkSize.x), y = Mod(gridPosition.y,  chunkSize.y)};

        Chunk neighborChunk;
        // Left Right
        if (gridRemainder.x == 0)
        {
            if (GetChunk(new Vector2Int(chunkPosition.x - 1, chunkPosition.y), out neighborChunk, true))
            {
                neighborChunk.SetVoxel(gridPosition, density);
            }
            
            if (GetChunk(new Vector2Int(chunkPosition.x + 1, chunkPosition.y), out neighborChunk, true))
            {
                neighborChunk.SetVoxel(gridPosition, density);
            }
        }

        // Top Bottom
        if (gridRemainder.y == 0)
        {
            if (GetChunk(new Vector2Int(chunkPosition.x, chunkPosition.y + 1), out neighborChunk, true))
            {
                neighborChunk.SetVoxel(gridPosition, density);
            }
            
            if (GetChunk(new Vector2Int(chunkPosition.x, chunkPosition.y - 1), out neighborChunk, true))
            {
                neighborChunk.SetVoxel(gridPosition, density);
            }
        }

        
        // Corners
        if (gridRemainder.x == 0 && gridRemainder.y == 0)
        {
            if (GetChunk(new Vector2Int(chunkPosition.x - 1, chunkPosition.y - 1), out neighborChunk, true))
            {
                neighborChunk.SetVoxel(gridPosition, density);
            }
            
            if (GetChunk(new Vector2Int(chunkPosition.x + 1, chunkPosition.y - 1), out neighborChunk, true))
            {
                neighborChunk.SetVoxel(gridPosition, density);
            }
            
            if (GetChunk(new Vector2Int(chunkPosition.x - 1, chunkPosition.y + 1), out neighborChunk, true))
            {
                neighborChunk.SetVoxel(gridPosition, density);
            }
            
            if (GetChunk(new Vector2Int(chunkPosition.x + 1, chunkPosition.y + 1), out neighborChunk, true))
            {
                neighborChunk.SetVoxel(gridPosition, density);
            }
        }
    }
    
    void AddVoxel(Vector2Int gridPosition, float density)
    {
        Vector2Int chunkPosition = GridtoChunk(gridPosition);
        if (GetChunk(chunkPosition, out Chunk chunk, true))
        {
            chunk.AddVoxel(gridPosition, density);
        }
        
        Vector2Int gridRemainder = new Vector2Int {x = Mod(gridPosition.x, chunkSize.x), y = Mod(gridPosition.y,  chunkSize.y)};

        Chunk neighborChunk;
        // Left Right
        if (gridRemainder.x == 0)
        {
            if (GetChunk(new Vector2Int(chunkPosition.x - 1, chunkPosition.y), out neighborChunk, true))
            {
                neighborChunk.AddVoxel(gridPosition, density);
            }
            
            if (GetChunk(new Vector2Int(chunkPosition.x + 1, chunkPosition.y), out neighborChunk, true))
            {
                neighborChunk.AddVoxel(gridPosition, density);
            }
        }

        // Top Bottom
        if (gridRemainder.y == 0)
        {
            if (GetChunk(new Vector2Int(chunkPosition.x, chunkPosition.y + 1), out neighborChunk, true))
            {
                neighborChunk.AddVoxel(gridPosition, density);
            }
            
            if (GetChunk(new Vector2Int(chunkPosition.x, chunkPosition.y - 1), out neighborChunk, true))
            {
                neighborChunk.AddVoxel(gridPosition, density);
            }
        }

        
        // Corners
        if (gridRemainder.x == 0 && gridRemainder.y == 0)
        {
            if (GetChunk(new Vector2Int(chunkPosition.x - 1, chunkPosition.y - 1), out neighborChunk, true))
            {
                neighborChunk.AddVoxel(gridPosition, density);
            }
            
            if (GetChunk(new Vector2Int(chunkPosition.x + 1, chunkPosition.y - 1), out neighborChunk, true))
            {
                neighborChunk.AddVoxel(gridPosition, density);
            }
            
            if (GetChunk(new Vector2Int(chunkPosition.x - 1, chunkPosition.y + 1), out neighborChunk, true))
            {
                neighborChunk.AddVoxel(gridPosition, density);
            }
            
            if (GetChunk(new Vector2Int(chunkPosition.x + 1, chunkPosition.y + 1), out neighborChunk, true))
            {
                neighborChunk.AddVoxel(gridPosition, density);
            }
        }
    }
    
    Chunk GenerateChunk(Vector2Int chunkPosition)
    {
        if (chunks.ContainsKey(chunkPosition))
            return chunks[chunkPosition];

        GameObject chunkGameObject = new GameObject(chunkPosition.ToString());
        chunkGameObject.transform.SetParent(transform);
        chunkGameObject.transform.position = ChunkToWorld(chunkPosition);

        Chunk newChunk = chunkGameObject.AddComponent<Chunk>();
        newChunk.Init(chunkPosition, chunkSize, chunkScale);
        newChunk.SetMaterial(mapMaterial);

        chunks.Add(chunkPosition, newChunk);
        return newChunk;
    }

    public bool GetChunk(Vector2Int chunkPosition, out Chunk chunk, bool createNotExist = false)
    {
        if (chunks.ContainsKey(chunkPosition))
        {
            chunk = chunks[chunkPosition];
            return true;
        }

        if (createNotExist)
        {
            chunk = GenerateChunk(chunkPosition);
            return true;
        }

        chunk = null;
        return false;
    }

    public Vector3 ChunkToWorld(Vector2Int chunkPosition)
    {
        return chunkPosition * chunkSize * chunkScale;
    }

    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        return gridPosition * chunkScale;
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        return new Vector2Int
        {
            x = Mathf.RoundToInt(worldPosition.x  / chunkScale.x),
            y = Mathf.RoundToInt(worldPosition.y / chunkScale.y)
        };
    }

    public Vector2Int WorldToChunk(Vector3 worldPosition)
    {
        return new Vector2Int(Mathf.FloorToInt(worldPosition.x / chunkSize.x / chunkScale.x), Mathf.FloorToInt(worldPosition.y / chunkSize.y / chunkScale.y));
    }

    public Vector2Int GridtoChunk(Vector2Int gridPosition)
    {
        return new Vector2Int {x = Mathf.FloorToInt((float) gridPosition.x / chunkSize.x), y = Mathf.FloorToInt((float) gridPosition.y / chunkSize.y)};
    }
    
    int Mod(int x, int m) {
        int r = x%m;
        return r<0 ? r+m : r;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int gridPosition = WorldToGrid(worldPosition);
        Gizmos.DrawSphere(GridToWorld(gridPosition), 0.1f);
        Handles.Label(GridToWorld(gridPosition) + Vector3.up * 0.25f, gridPosition.ToString());
    }
}