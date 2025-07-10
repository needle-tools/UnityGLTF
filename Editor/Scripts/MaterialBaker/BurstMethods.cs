using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace UnityGLTF
{
    #region Special Types
    public struct Color24RGB : IEquatable<Color24RGB>
    {
        public byte r;
        public byte g;
        public byte b;
        
        public Color ToColor()
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
        
        public Color24RGB(byte r, byte g, byte b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }

        public bool Equals(Color24RGB other)
        {
            return r == other.r && g == other.g && b == other.b;
        }

        public override bool Equals(object obj)
        {
            return obj is Color24RGB other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(r, g, b);
        }
    }
    #endregion
    
    public struct Color32RGBA : IEquatable<Color32RGBA>
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;
        
        public Color ToColor()
        {
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }
        
        public Color32RGBA(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public bool Equals(Color32RGBA other)
        {
            return r == other.r && g == other.g && b == other.b && a == other.a;
        }

        public override bool Equals(object obj)
        {
            return obj is Color32RGBA other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(r, g, b, a);
        }
    }
    
    [BurstCompile(CompileSynchronously = true)]
    public static class BurstMethods
    {
        private const float ProximityThreshold = 0.0001f; // threshold for color proximity check
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float4 LinearToGamma(float4 linear)
        {
            // Simple gamma correction, assuming sRGB
            return new float4(
                math.pow(linear.x, 1f / 2.2f),
                math.pow(linear.y, 1f / 2.2f),
                math.pow(linear.z, 1f / 2.2f),
                linear.w);
        }
    
        public static unsafe void ConvertEmissionPixels(Texture2D texture, out Color emissionColor)
        {
            var pd = texture.GetPixelData<float4>(0);
            ConvertEmissionPixels((float4*)pd.GetUnsafeReadOnlyPtr(), pd.Length, out var emissionColorFloat4);
            emissionColor = new Color(emissionColorFloat4.x, emissionColorFloat4.y, emissionColorFloat4.z, emissionColorFloat4.w);
        }
        
        [BurstCompile(CompileSynchronously = true)]
        public static unsafe void OrmCombine(Color24RGB* ormPixels, Color24RGB* met, Color24RGB* smooth, Color24RGB* occl, int start, int end, 
            bool metallicSingleValueOrEmpty, bool occlusionSingleValueOrEmpty, bool smoothnessSingleValueOrEmpty)
        {
            bool hasMetPixels = met != null;
            bool hasSmoothPixels = smooth != null;
            bool hasOcclPixels = occl != null;
            for (int i = start; i <= end; i++)
            {
                byte metallicValue = metallicSingleValueOrEmpty ? byte.MaxValue : (hasMetPixels ? met[i].r : byte.MaxValue);
                byte occlusionValue = occlusionSingleValueOrEmpty ? byte.MinValue : (hasOcclPixels ? occl[i].r : byte.MinValue);
                byte smoothnessValue = smoothnessSingleValueOrEmpty ? byte.MaxValue : (hasSmoothPixels ? smooth[i].r : byte.MaxValue);
                ormPixels[i] = new Color24RGB(occlusionValue, (byte)(byte.MaxValue - smoothnessValue), metallicValue);
            }
        }

        public static unsafe void AlbedoAlphaCombine(Texture2D result, Texture2D albedo, Texture2D alpha)
        {
            AlbedoAlphaCombine((Color32RGBA*)result.GetPixelData<Color32RGBA>(0).GetUnsafePtr(),
                albedo != null ? (Color24RGB*)albedo.GetPixelData<Color24RGB>(0).GetUnsafeReadOnlyPtr() : null,
                alpha != null ? (Color24RGB*)alpha.GetPixelData<Color24RGB>(0).GetUnsafeReadOnlyPtr() : null,
                result.GetPixelData<Color32RGBA>(0).Length);
        }
        
        [BurstCompile(CompileSynchronously = true)]
        public static unsafe void AlbedoAlphaCombine(Color32RGBA* result, Color24RGB* albedo, Color24RGB* alpha, int length)
        {
            if (albedo == null)
            {
                for (var i = 0; i < length; i++)
                    result[i] = new Color32RGBA(0, 0, 0, alpha[i].r);
            }
            else if (alpha == null)
            {
                for (var i = 0; i < length; i++)
                    result[i] = new Color32RGBA(0, 0, 0, byte.MaxValue);
            }
            else
            {
                for (var i = 0; i < length; i++)
                    result[i] = new Color32RGBA(albedo[i].r, albedo[i].g, albedo[i].b, alpha[i].r);
            }
        }
        
        [BurstCompile(CompileSynchronously = true)]
        public unsafe struct OrmCombineJob:IJobParallelFor, IDisposable
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<Color24RGB> orm;
            [ReadOnly] public NativeArray<Color24RGB> metallic;
            [ReadOnly] public NativeArray<Color24RGB> smoothness;
            [ReadOnly] public NativeArray<Color24RGB> occlusion;
            public int length;
            public bool metallicSingleValueOrEmpty;
            public bool occlusionSingleValueOrEmpty;
            public bool smoothnessSingleValueOrEmpty;
            public NativeArray<int2> ranges;
            public int parallelCount;

            public void CreateRanges()
            {
                var singleRange = length / parallelCount;
                ranges = new NativeArray<int2>(parallelCount, Allocator.TempJob);
                var cStart = 0;
                var cEnd = singleRange;
                for (int i = 0; i < parallelCount; i++)
                {
                    int start = cStart;
                    int end = cEnd;
                    ranges[i] = new int2(start, Mathf.Max(length-1, end));
                        
                    cStart = cEnd+1;
                    cEnd = cStart + singleRange;
                }
            }
            
            public void Execute(int index)
            {
                OrmCombine((Color24RGB*)orm.GetUnsafePtr(),
                    metallic.IsCreated ? (Color24RGB*)metallic.GetUnsafeReadOnlyPtr() : null,
                    smoothness.IsCreated ? (Color24RGB*)smoothness.GetUnsafeReadOnlyPtr() : null,
                    occlusion.IsCreated ? (Color24RGB*)occlusion.GetUnsafeReadOnlyPtr() : null,
                    ranges[index].x, ranges[index].y,
                    metallicSingleValueOrEmpty, occlusionSingleValueOrEmpty, smoothnessSingleValueOrEmpty);
            }

            public void Dispose()
            {
                ranges.Dispose();
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public static unsafe void ConvertEmissionPixels(float4* emissionPixels, int length, out float4 emissionColor)
        {
            bool isHdr = false;
            float highestValue = 0f;
            for (int i = 0; i < length; i++)
            {
                var linear = emissionPixels[i];
                if (linear.x > highestValue) highestValue = linear.x;
                if (linear.y > highestValue) highestValue = linear.y;
                if (linear.z > highestValue) highestValue = linear.z;

                if (emissionPixels[i].x > 1f || emissionPixels[i].y > 1f || emissionPixels[i].z > 1f)
                {
                    isHdr = true;
                    break;
                }
            }

            if (isHdr)
            {
                // Normalize the emission values to the highest value
                for (int i = 0; i < length; i++)
                {
                    var a = emissionPixels[i].w;
                    var l = emissionPixels[i] / highestValue;
                    l = LinearToGamma(l);
                    l.w = a; // preserve alpha
                    if (l.x < 0) l.x = 0;
                    if (l.y < 0) l.y = 0;
                    if (l.z < 0) l.z = 0;
                    emissionPixels[i] = l;
                }

                emissionColor = new float4(1,1,1,1) * highestValue;
            }
            else
            {
                emissionColor = new float4(1,1,1,1);
                for (int i = 0; i < length; i++)
                {
                    var a = emissionPixels[i].w;
                    var l = emissionPixels[i];
                    l = LinearToGamma(l);
                    l.w = a; // preserve alpha
                    if (l.x < 0) l.x = 0;
                    if (l.y < 0) l.y = 0;
                    if (l.z < 0) l.z = 0;
                    emissionPixels[i] = l;
                }
            }

        }
        
        
        [BurstCompile(CompileSynchronously = true)]
        public static unsafe void IsTextureEmptyRGBA(Color32RGBA* textureData, Color24RGB* mask, int length, bool ignoreAlpha, out bool isEmpty)
        {
            isEmpty = false;
            bool hasData = false;
            
            Color24RGB blackColor = new Color24RGB(0, 0, 0);

            if (mask == null)
            {
                for (int i = 0; i < length; i++)
                {
                    hasData |= (!ignoreAlpha && textureData[i].a > ProximityThreshold) || textureData[i].r > ProximityThreshold || textureData[i].g > ProximityThreshold || textureData[i].b > ProximityThreshold;
                    if (hasData)
                        break;
                }

                isEmpty = !hasData;
                return;
            }
            
            for (int i = 0; i < length; i++)
            {
                if (mask[i].Equals(blackColor))
                    continue; // skip masked pixels

                hasData |= (!ignoreAlpha && textureData[i].a > ProximityThreshold) || textureData[i].r > ProximityThreshold || textureData[i].g > ProximityThreshold || textureData[i].b > ProximityThreshold;
                if (hasData)
                    break;
            }
            isEmpty = !hasData;
        }
        
        [BurstCompile(CompileSynchronously = true)]
        public static unsafe void IsTextureEmptyRGBAFloat(float4* textureData, Color24RGB* mask, int length, bool ignoreAlpha, out bool isEmpty)
        {
            isEmpty = false;
            bool hasData = false;
            Color24RGB blackColor = new Color24RGB(0, 0, 0);

            if (mask == null)
            {
                for (int i = 0; i < length; i++)
                {
                    hasData |= (!ignoreAlpha && textureData[i].w > ProximityThreshold) || textureData[i].x > ProximityThreshold || textureData[i].y > ProximityThreshold || textureData[i].z > ProximityThreshold;
                    if (hasData)
                        break;
                }

                isEmpty = !hasData;
                return;
            }
            
            for (int i = 0; i < length; i++)
            {
                if (mask[i].Equals(blackColor))
                    continue; // skip masked pixels

                hasData |= (!ignoreAlpha && textureData[i].w > ProximityThreshold) || textureData[i].x > ProximityThreshold || textureData[i].y > ProximityThreshold || textureData[i].z > ProximityThreshold;
                if (hasData)
                    break;
            }
            isEmpty = !hasData;
        }
        
        
        [BurstCompile(CompileSynchronously = true)]
        public static unsafe void IsTextureEmptyRGB(Color24RGB* textureData, Color24RGB* mask, int length, out bool isEmpty)
        {
            isEmpty = false;
            bool hasData = false;
            Color24RGB blackColor = new Color24RGB(0, 0, 0);

            if (mask == null)
            {
                for (int i = 0; i < length; i++)
                {
                    hasData |= textureData[i].r > ProximityThreshold || textureData[i].g > ProximityThreshold || textureData[i].b > ProximityThreshold;
                    if (hasData)
                        break;
                }

                isEmpty = !hasData;
                return;
            }
            
            for (int i = 0; i < length; i++)
            {
                if (mask[i].Equals(blackColor))
                    continue; // skip masked pixels

                hasData |= textureData[i].r > ProximityThreshold || textureData[i].g > ProximityThreshold || textureData[i].b > ProximityThreshold;
                if (hasData)
                    break;
            }
            isEmpty = !hasData;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ColorProximityRGBAFloat(float4 a, float4 b, float threshold = ProximityThreshold)
        {
            return Math.Abs(a.x - b.x) < threshold &&
                   Math.Abs(a.y - b.y) < threshold &&
                   Math.Abs(a.z - b.z) < threshold &&
                   Math.Abs(a.w - b.w) < threshold;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ColorProximityRGBA(Color32RGBA a, Color32RGBA b, float threshold = ProximityThreshold)
        {
            return Math.Abs(a.r - b.r) < threshold &&
                   Math.Abs(a.g - b.g) < threshold &&
                   Math.Abs(a.b - b.b) < threshold &&
                   Math.Abs(a.a - b.a) < threshold;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ColorProximityRGB(Color24RGB a, Color24RGB b, float threshold = ProximityThreshold)
        {
            return Math.Abs(a.r - b.r) < threshold &&
                   Math.Abs(a.g - b.g) < threshold &&
                   Math.Abs(a.b - b.b) < threshold;
        }
        
        [BurstCompile(CompileSynchronously = true)]
        public static unsafe void HasSingleValueRGBA(Color32RGBA* textureData, Color24RGB* mask, int length, out bool hasSingleValue, out Color32RGBA singleColor)
        {
            Color32RGBA lastColor = default;
            bool lastColorHasValue = false;
            Color24RGB blackColor = new Color24RGB(0, 0, 0);

            for (int i = 0; i < length; i++)
            {
                if (mask != null && mask[i].Equals(blackColor))
                    continue;
                
                if (lastColorHasValue && !ColorProximityRGBA(textureData[i], lastColor))
                {
                    singleColor = new Color32RGBA(0,0,0,0);
                    hasSingleValue = false;
                    return;
                }
   
                lastColor = textureData[i];
                lastColorHasValue = true;

            }
            if (!lastColorHasValue)
            {
                singleColor = new Color32RGBA(0,0,0,0);
                hasSingleValue = false;
                return;
            }

            hasSingleValue = true;
            singleColor = lastColor;
        }
        
        [BurstCompile(CompileSynchronously = true)]
        public static unsafe void HasSingleValueRGBAFloat(float4* textureData, Color24RGB* mask, int length, out bool hasSingleValue, out float4 singleColor)
        {
            float4 lastColor = default;
            bool lastColorHasValue = false;
            Color24RGB blackColor = new Color24RGB(0, 0, 0);

            for (int i = 0; i < length; i++)
            {
                if (mask != null && mask[i].Equals(blackColor))
                    continue;
                
                if (lastColorHasValue && !ColorProximityRGBAFloat(textureData[i], lastColor))
                {
                    singleColor = new float4(0,0,0,0);
                    hasSingleValue = false;
                    return;
                }
   
                lastColor = textureData[i];
                lastColorHasValue = true;

            }
            if (!lastColorHasValue)
            {
                singleColor = new float4(0,0,0,0);
                hasSingleValue = false;
                return;
            }

            hasSingleValue = true;
            singleColor = lastColor;
        }
        
        [BurstCompile(CompileSynchronously = true)]
        public static unsafe void HasSingleValueRGB(Color24RGB* textureData, Color24RGB* mask, int length, out bool hasSingleValue, out Color24RGB singleColor)
        {
            Color24RGB lastColor = default;
            bool lastColorHasValue = false;
            Color24RGB blackColor = new Color24RGB(0, 0, 0);

            for (int i = 0; i < length; i++)
            {
                if (mask != null && mask[i].Equals(blackColor))
                    continue;
                
                if (lastColorHasValue && !ColorProximityRGB(textureData[i], lastColor))
                {
                    singleColor = new Color24RGB(0,0,0);
                    hasSingleValue = false;
                    return;
                }
   
                lastColor = textureData[i];
                lastColorHasValue = true;

            }
            if (!lastColorHasValue)
            {
                singleColor = new Color24RGB(0,0,0);
                hasSingleValue = false;
                return;
            }

            hasSingleValue = true;
            singleColor = lastColor;
        }
        
    }
}