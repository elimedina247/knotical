using System;
using UnityEngine;

namespace Knotical
{
    [Serializable]
    public struct FloatRange
    {
        public float Min;
        public float Max;

        public FloatRange(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public FloatRange(float fixedValue) : this(fixedValue, fixedValue) { }

        public float Pick(System.Random rng) => Mathf.Lerp(Min, Max, (float)rng.NextDouble());
    }

    [Serializable]
    public struct IntRange
    {
        public int Min;
        public int Max;

        public IntRange(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public IntRange(int fixedValue) : this(fixedValue, fixedValue) { }

        public int Pick(System.Random rng) => rng.Next(Min, Max + 1);
    }
}
