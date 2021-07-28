using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour
{
    const float Scale = 3;
    const float ViewerMoveTresholdForChunkUpdate = 25f;
    const float SqrtViewerMoveTresholdForChunkUpdate = ViewerMoveTresholdForChunkUpdate * ViewerMoveTresholdForChunkUpdate;

    public static float MaxViewDist;
    public LODInfo[] DetailLevels;
    public Transform Viewer;
    public Material mapMaterial;

    public static Vector2 ViewerPosition;
    Vector2 ViewerPositionOld;
    public static MapGenerator mapGenerator;
    int ChunkSize;
    int ChunksVisibleInViewDst;

    Dictionary<Vector2, TerrainChunk> TerrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> TerrainChunkVisibleLastUpdate = new List<TerrainChunk>();

    private void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();

        MaxViewDist = DetailLevels[DetailLevels.Length - 1].visibleDstTreshold;
        ChunkSize = MapGenerator.MapChunkSize - 1;
        ChunksVisibleInViewDst = Mathf.RoundToInt(MaxViewDist) / ChunkSize;

        UpdateVisibleChunks();
    }

    private void Update()
    {
        ViewerPosition = new Vector2(Viewer.position.x, Viewer.position.z)/ Scale;

        if ((ViewerPositionOld-ViewerPosition).sqrMagnitude>SqrtViewerMoveTresholdForChunkUpdate)
        {
            ViewerPositionOld = ViewerPosition;
            UpdateVisibleChunks();
        }     
    }

    void UpdateVisibleChunks()
    {

        for (int i = 0; i < TerrainChunkVisibleLastUpdate.Count; i++)
        {
            TerrainChunkVisibleLastUpdate[i].SetVisible(false);
        }

        TerrainChunkVisibleLastUpdate.Clear();
        int currentChunkCoordX = Mathf.RoundToInt(ViewerPosition.x) / ChunkSize;
        int currentChunkCoordY = Mathf.RoundToInt(ViewerPosition.y) / ChunkSize;

        for (int yOffset = -ChunksVisibleInViewDst; yOffset <= ChunksVisibleInViewDst; yOffset++)
        {
            for (int xOffset = -ChunksVisibleInViewDst; xOffset <= ChunksVisibleInViewDst; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (TerrainChunkDictionary.ContainsKey(viewedChunkCoord))
                {
                    TerrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                   
                }
                else
                {
                    TerrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, ChunkSize, DetailLevels, transform, mapMaterial));
                }
            }
        }
    }

    public class TerrainChunk
    {
        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;

        LODInfo[] detailLevels;
        LODMesh[] lODMeshes;
        LODMesh collisionLODMesh;

        MapData mapData;
        bool mapDataReceived;
        int previousLODIndex=-1;

        public TerrainChunk(Vector2 coord, int size,LODInfo[] _detailLevels, Transform parent, Material material)
        {
            this.detailLevels = _detailLevels;
            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("Terrain Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();
            meshRenderer.material = material;

            meshObject.transform.position = positionV3*Scale;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale = Vector3.one * Scale;
            SetVisible(false);

            lODMeshes = new LODMesh[detailLevels.Length];

            for (int i = 0; i < detailLevels.Length; i++)
            {
                lODMeshes[i] = new LODMesh(detailLevels[i].LOD, UpdateTerrainChunk);
                if (detailLevels[i].useForCollider)
                {
                    collisionLODMesh = lODMeshes[i];
                }
            }

            mapGenerator.RequestMapData(position, OnMapDataReceived);
        }

        void OnMapDataReceived(MapData _mapData)
        {
            this.mapData = _mapData;
            mapDataReceived = true;

            Texture2D texture = TextureGenerator.TextureFromColourMap(mapData.colourMap, MapGenerator.MapChunkSize, MapGenerator.MapChunkSize);
            meshRenderer.material.mainTexture = texture;

            UpdateTerrainChunk();
        }

        public void UpdateTerrainChunk()
        {
            if (mapDataReceived)
            {            
                float viewerDistanceFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(ViewerPosition));
                bool visible = viewerDistanceFromNearestEdge <= MaxViewDist;

                if (visible)
                {
                    int lodIndex = 0;

                    for (int i = 0; i < detailLevels.Length-1; i++)
                    {
                        if (viewerDistanceFromNearestEdge>detailLevels[i].visibleDstTreshold)
                        {
                            lodIndex = i + 1;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (lodIndex!= previousLODIndex)
                    {
                        LODMesh lodMesh = lODMeshes[lodIndex];
                        if (lodMesh.hasMesh)
                        {
                            previousLODIndex = lodIndex;
                            meshFilter.mesh = lodMesh.mesh;
                        }
                        else if(!lodMesh.hasRequestedMesh)
                        {
                            lodMesh.RequestMesh(mapData);
                        }
                    }

                    if (lodIndex==0)
                    {
                        if (collisionLODMesh.hasMesh)
                        {

                            meshCollider.sharedMesh = collisionLODMesh.mesh;
                        }
                        else if(!collisionLODMesh.hasRequestedMesh)
                        {
                            collisionLODMesh.RequestMesh(mapData);
                        }
                    }
                   
                   TerrainChunkVisibleLastUpdate.Add(this);                 
                }

                SetVisible(visible);
            }
        }

        public void SetVisible(bool _visible)
        {
            meshObject.SetActive(_visible);
        }

        public bool IsVisible()
        {
            return meshObject.activeSelf;
        }
    }

    class LODMesh
    {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        int lod;
        System.Action updateCallback;

        public LODMesh(int _lod, System.Action _updateCallback)
        {
            this.lod = _lod;
            this.updateCallback = _updateCallback;
        }

        void OnMeshDataReceived(MeshData _meshData)
        {
            mesh = _meshData.CreateMesh();
            hasMesh = true;

            updateCallback();
        }
        public void RequestMesh(MapData _mapData)
        {
            hasRequestedMesh = true;
            mapGenerator.RequestMeshData(_mapData, lod, OnMeshDataReceived);
        }
    }
    [System.Serializable]
    public struct LODInfo
    {
        public int LOD;
        public float visibleDstTreshold;
        public bool useForCollider;
    }
}
