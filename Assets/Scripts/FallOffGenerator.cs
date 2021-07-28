using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class FallOffGenerator
{ 
    public static float[,] GenerateFallOffMap(int _size)
    {
        float[,] map = new float[_size, _size];

        for (int i = 0; i < _size; i++)
        {
            for (int j = 0; j < _size; j++)
            {
                float x = i / (float)_size * 2 - 1;
                float y = j / (float)_size * 2 - 1;

                float value = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                map[i, j] = Evaluate(value);
            }
        }
        return map;
    }

    static float Evaluate(float _value)
    {
        float a = 3f;
        float b = 2.2f;

        return Mathf.Pow(_value, a) / (Mathf.Pow(_value, a) + Mathf.Pow(b - b * _value, a));
    }
}

