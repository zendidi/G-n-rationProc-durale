using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Noise
{
    public enum NormalizeMode { Local, Global};
   
    public static float[,] GenerateNoiseMap(int _mapWidth , int _mapHeigth, int _seed,  float _scale, int _octaves, float _persistance, float _lacunarity, Vector2 _offset, NormalizeMode _normalizeMode)
    {
        float[,] noiseMap = new float[_mapWidth, _mapHeigth];

        System.Random prng = new System.Random(_seed);
        Vector2[] octaveOffsets = new Vector2[_octaves];

        float maxPossibleHeight = 0;
        float amplitude = 1;
        float frequency = 1;

        for (int i = 0; i < _octaves; i++)
        {
            float offsetX = prng.Next(-100000,100000)+_offset.x;
            float offsetY = prng.Next(-100000, 100000) - _offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);

            maxPossibleHeight+= amplitude;
            amplitude *= _persistance;
        }

        if (_scale <= 0)
        {
            _scale = 0.0001f;
        }
        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;

        float halfWidth = _mapWidth / 2;
        float halfHeight = _mapHeigth / 2;

        for (int y = 0; y < _mapHeigth; y++)
        {          
            for (int x = 0; x < _mapWidth; x++)
            {
                amplitude = 1;
                frequency = 1;
                float noiseHeigt = 0;

                for (int i = 0; i < _octaves; i++)
                {
                    float sampleX = (x- halfWidth + octaveOffsets[i].x) / _scale * frequency ;
                    float sampleY = (y- halfHeight + octaveOffsets[i].y) / _scale * frequency ; 

                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeigt += perlinValue * amplitude;
                    amplitude *=  _persistance;
                    frequency *= _lacunarity;
                }
                if (noiseHeigt>maxLocalNoiseHeight)
                {
                    maxLocalNoiseHeight = noiseHeigt;
                }
                else if (noiseHeigt<minLocalNoiseHeight)
                {
                    minLocalNoiseHeight = noiseHeigt;
                }

                noiseMap[x, y] = noiseHeigt;
            }
        }

        for (int y = 0; y < _mapHeigth; y++)
        {
            for (int x = 0; x < _mapWidth; x++)
            {
                if (_normalizeMode==NormalizeMode.Local)
                {
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);
                }
                else
                {
                    float normalizedHeight = (noiseMap[x, y] + 1) / (maxPossibleHeight);
                    noiseMap[x, y] = Mathf.Clamp(normalizedHeight,0, int.MaxValue);
                }
            }
        }
                return noiseMap;
    }
}
