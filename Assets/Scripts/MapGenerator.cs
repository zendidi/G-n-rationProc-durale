using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode { NoiseMap,ColourMap,Mesh, FallOffMap};
    public DrawMode E_DrawMode;

    public Noise.NormalizeMode NormalizeMode;

    [Range(0,6)]
    public int EditorPreviewOfDetail;
    public float NoiseScale;

    public int Octave;
    [Range(0,1)]
    public float Persistance;
    public float Lacunarity;

    public int Seed;
    public Vector2 Offset;

    public bool UseFallOff;
    public float meshHeightMultiplier;
    public AnimationCurve meshHeightCurve;

    public bool AutoUpdate;
    public bool UseFlatShading;

    public TerrainType[] Region;

    float[,] FallOffMap;
    static MapGenerator instance;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();

    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    private void Awake()
    {
        FallOffMap = FallOffGenerator.GenerateFallOffMap(MapChunkSize);
    }

    public static int MapChunkSize
    {
        get {
            if (instance== null)
            {
                instance = FindObjectOfType<MapGenerator>();
            }
            if (instance.UseFlatShading)
            {
                return 95;
            }
            else
            {
                return 239;
            }
        }
    }
    public void DrawMapInEditor()
    {
        MapData mapData = GenerateMapData(Vector2.zero);
        MapDisplay display = FindObjectOfType<MapDisplay>();
        if (E_DrawMode == DrawMode.NoiseMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if (E_DrawMode == DrawMode.ColourMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromColourMap(mapData.colourMap, MapChunkSize, MapChunkSize));
        }
        else if (E_DrawMode == DrawMode.Mesh)
        {
            display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, EditorPreviewOfDetail, UseFlatShading), TextureGenerator.TextureFromColourMap(mapData.colourMap, MapChunkSize, MapChunkSize));
        }
        else if (E_DrawMode == DrawMode.FallOffMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(FallOffGenerator.GenerateFallOffMap(MapChunkSize)));
        }
    }

    public void RequestMapData(Vector2 _center, Action<MapData> _callBack)
    { 
        ThreadStart threadStart = delegate
         {
             MapDataThread(_center,_callBack);
         };

        new Thread(threadStart).Start();
    }

    public void MapDataThread(Vector2 _center, Action<MapData> _callBack)
    {
        MapData mapData = GenerateMapData(_center);
        lock (mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(_callBack, mapData));
        }    
    }

    public void RequestMeshData( MapData _mapData, int _LOD, Action<MeshData> _callback)
    {
        ThreadStart threadStart = delegate
        {
            MeshDataThread(_mapData, _LOD,_callback);
        };

        new Thread(threadStart).Start();
    }

    void MeshDataThread(MapData _mapData, int _LOD,  Action<MeshData> _callback)
    {
        MeshData meshData=MeshGenerator.GenerateTerrainMesh(_mapData.heightMap, meshHeightMultiplier,meshHeightCurve,_LOD, UseFlatShading);

        lock (meshDataThreadInfoQueue)
        {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(_callback, meshData));
        }
    }

    private void Update()
    {
        if (mapDataThreadInfoQueue.Count>0)
        {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }

        if (meshDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }
    MapData GenerateMapData(Vector2 _center)
    {
        float[,] noiseMap = Noise.GenerateNoiseMap(MapChunkSize + 2, MapChunkSize + 2,Seed, NoiseScale, Octave,Persistance,Lacunarity, _center+Offset, NormalizeMode);

        Color[] colourMap = new Color[MapChunkSize*MapChunkSize];
        for (int y = 0; y < MapChunkSize; y++)
        {
            for (int x = 0; x < MapChunkSize; x++)
            {
                if (UseFallOff)
                {
                    noiseMap[x, y] =Mathf.Clamp01(noiseMap[x,y] - FallOffMap[x, y]);
                }

                float currentHeight = noiseMap[x, y];
                for (int i = 0; i < Region.Length; i++)
                {
                    if (currentHeight>=Region[i].height)
                    {
                        colourMap[y * MapChunkSize + x] = Region[i].colour;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        return new MapData(noiseMap, colourMap);
    }

    private void OnValidate()
    {      
        if (Lacunarity < 1)
        {
            Lacunarity = 1;
        }
        if (Octave<0)
        {
            Octave = 0;
        }
        FallOffMap = FallOffGenerator.GenerateFallOffMap(MapChunkSize);
    }

     struct MapThreadInfo<T>
    {
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo(Action<T> _callback, T _parameter)
        {
            this.callback = _callback;
            this.parameter = _parameter;
        }
    }
}

[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color colour;
}

public struct MapData
{
    public readonly float[,] heightMap;
    public readonly Color[] colourMap;

    public MapData(float[,] _heightMap, Color[] _colourMap)
    {
        this.heightMap = _heightMap;
        this.colourMap = _colourMap;
    }
}
