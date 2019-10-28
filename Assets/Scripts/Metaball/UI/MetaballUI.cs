using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MetaballUI : MonoBehaviour
{

    MetaballGenerator generator;
    
    [SerializeField] Text ms;
    [SerializeField] Text triangles;
    [SerializeField] Text vertices;
    [SerializeField] Text interpolation;
    [SerializeField] Text triangleIndexing;
    [SerializeField] Text greedyMeshing;
    [SerializeField] Text job;

    float lastMS;

    void Awake()
    {
        generator = FindObjectOfType<MetaballGenerator>();
    }
    
    void Update()
    {
        if(generator == null)
            return;
        
        if (ms != null)
        {
            lastMS = 0.3f * (generator.GetLastCalculateTime.Ticks / 10000f) + 0.7f * lastMS;
            ms.text = $"Marching Squares Time : {lastMS} ms";
        }

        if (triangles != null)
        {
            triangles.text = $"Num of Triangles : {generator.GetNumTriangles}";
        }

        if (vertices != null)
        {
            vertices.text = $"Num of Vertices : {generator.GetNumVertices}";
        }

        if (interpolation != null)
        {
            interpolation.text = $" <color=#3EBBFF>I</color>nterpolation : {generator.EnableInterpolation}";
        }
        
        if (triangleIndexing != null)
        {
            triangleIndexing.text = $"<color=#3EBBFF>T</color>riangleIndexing : {generator.EnableTriangleIndexing}";
        }
        
        if (greedyMeshing != null)
        {
            greedyMeshing.text = $"<color=#3EBBFF>G</color>reedyMeshing : {generator.EnableGreedyMeshing}";
        }
        
        if (job != null)
        {
            job.text = $"<color=#3EBBFF>J</color>ob : {generator.EnableJob}";
        }
    }
}
