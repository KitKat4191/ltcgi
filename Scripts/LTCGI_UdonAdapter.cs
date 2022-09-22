﻿using System;
using UnityEngine;

#if VRC_SDK_VRCSDK3 && UDONSHARP
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;

using LTCGIShader = VRC.SDKBase.VRCShader;
#else
using LTCGIShader = UnityEngine.Shader;
#endif

#if VRC_SDK_VRCSDK3 && UDONSHARP
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LTCGI_UdonAdapter : UdonSharpBehaviour
#else
// FIXME: This makes the filename mismatch the class name
// - I think Unity doesn't like that?
public class LTCGI_RuntimeAdapter : MonoBehaviour
#endif

{
    // perhaps fixes some lightmap issues with static batching?
    public bool DEBUG_ReverseUnityLightmapST = false;

    [Header("Internal Data (auto-generated, do not edit!)")]
    public Renderer[] _Renderers;
    public Texture2D[] _LTCGI_Lightmaps;
    public Vector4[] _LTCGI_LightmapST;
    public float[] _LTCGI_Mask;
    public Vector4 _LTCGI_LightmapMult;
    public GameObject[] _Screens;
    public Texture2D _LTCGI_lut1, _LTCGI_lut2;
    public Texture[] _LTCGI_LODs;
    public Texture2DArray _LTCGI_Static_LODs_0;
    public Texture2DArray _LTCGI_Static_LODs_1;
    public Texture2DArray _LTCGI_Static_LODs_2;
    public Texture2DArray _LTCGI_Static_LODs_3;
    public Vector4[] _LTCGI_Vertices_0, _LTCGI_Vertices_1, _LTCGI_Vertices_2, _LTCGI_Vertices_3;
    public Vector4[] _LTCGI_Vertices_0t, _LTCGI_Vertices_1t, _LTCGI_Vertices_2t, _LTCGI_Vertices_3t;
    public Vector4[] _LTCGI_ExtraData;
    public Texture2D _LTCGI_static_uniforms;
    public Transform[] _LTCGI_ScreenTransforms;
    public int _LTCGI_ScreenCount;
    public int[] _LTCGI_ScreenCountMasked;
    public int _LTCGI_ScreenCountDynamic;
    public CustomRenderTexture BlurCRTInput;

    private bool disabled = false;

    private int extraDataId;
    private int vert0Id, vert1Id, vert2Id, vert3Id;
    private int textureLod0Id;
    private int globalDisableId = -1;

    void Start()
    {
        Debug.Log("LTCGI adapter start");

        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        if (DEBUG_ReverseUnityLightmapST)
        {
            Debug.LogWarning("WARNING: LTCGI DEBUG_ReverseUnityLightmapST is active! This is probably not what you want!");
        }

        if (_LTCGI_ScreenCount == 0)
        {
            Debug.LogError("LTCGI Adapter: No screens found! Try deleting the LTCGI_UdonAdapter component from the controller object and clicking 'Force Update' on the controller if this is unexpected.");
            this.enabled = false;
            return;
        }

        _LTCGI_Vertices_0t = new Vector4[_LTCGI_Vertices_0.Length];
        _LTCGI_Vertices_1t = new Vector4[_LTCGI_Vertices_1.Length];
        _LTCGI_Vertices_2t = new Vector4[_LTCGI_Vertices_2.Length];
        _LTCGI_Vertices_3t = new Vector4[_LTCGI_Vertices_3.Length];

        for (int i = 0; i < _LTCGI_ScreenCount; i++)
        {
            var transform = _LTCGI_ScreenTransforms[i];
            _LTCGI_Vertices_0t[i] = CalcTransform(_LTCGI_Vertices_0[i], transform);
            _LTCGI_Vertices_1t[i] = CalcTransform(_LTCGI_Vertices_1[i], transform);
            _LTCGI_Vertices_2t[i] = CalcTransform(_LTCGI_Vertices_2[i], transform);
            _LTCGI_Vertices_3t[i] = CalcTransform(_LTCGI_Vertices_3[i], transform);
        }

        for (int j = 0; j < _LTCGI_LODs.Length; j++)
        {
            if (_LTCGI_LODs[j] != null)
            {
                var id = LTCGIShader.PropertyToID("_Udon_LTCGI_Texture_LOD" + j);
                LTCGIShader.SetGlobalTexture(id, _LTCGI_LODs[j]);
                if (j == 0)
                    textureLod0Id = id;
            }
        }
        if (_LTCGI_Static_LODs_0 != null)
        {
            LTCGIShader.SetGlobalTexture(LTCGIShader.PropertyToID("_Udon_LTCGI_Texture_LOD0_arr"), _LTCGI_Static_LODs_0);
            LTCGIShader.SetGlobalTexture(LTCGIShader.PropertyToID("_Udon_LTCGI_Texture_LOD1_arr"), _LTCGI_Static_LODs_1);
            LTCGIShader.SetGlobalTexture(LTCGIShader.PropertyToID("_Udon_LTCGI_Texture_LOD2_arr"), _LTCGI_Static_LODs_2);
            LTCGIShader.SetGlobalTexture(LTCGIShader.PropertyToID("_Udon_LTCGI_Texture_LOD3_arr"), _LTCGI_Static_LODs_3);
        }

        LTCGIShader.SetGlobalTexture(LTCGIShader.PropertyToID("_Udon_LTCGI_lut1"), _LTCGI_lut1);
        LTCGIShader.SetGlobalTexture(LTCGIShader.PropertyToID("_Udon_LTCGI_lut2"), _LTCGI_lut2);
        
        LTCGIShader.SetGlobalInteger(LTCGIShader.PropertyToID("_Udon_LTCGI_ScreenCount"), _LTCGI_ScreenCount);

        if (_LTCGI_static_uniforms != null)
            LTCGIShader.SetGlobalTexture(LTCGIShader.PropertyToID("_Udon_LTCGI_static_uniforms"), _LTCGI_static_uniforms);
        
        // by default, mask allows all
        LTCGIShader.SetGlobalFloatArray(LTCGIShader.PropertyToID("_Udon_LTCGI_Mask"), new float[_LTCGI_ScreenCount]);

        for (int i = 0; i < _Renderers.Length; i++)
        {
            var r = _Renderers[i];
            var block = new MaterialPropertyBlock();
            if (r.HasPropertyBlock())
                r.GetPropertyBlock(block);
            var maskSubset = new float[_LTCGI_ScreenCount];
            Array.Copy(_LTCGI_Mask, i * _LTCGI_ScreenCount, maskSubset, 0, _LTCGI_ScreenCount);
            block.SetFloatArray("_Udon_LTCGI_Mask", maskSubset);
            block.SetInt("_Udon_LTCGI_ScreenCount", _LTCGI_ScreenCountMasked[i]);
            if (_LTCGI_Lightmaps[i] != null)
                block.SetTexture("_Udon_LTCGI_Lightmap", _LTCGI_Lightmaps[i]);
            block.SetVector("_Udon_LTCGI_LightmapMult", _LTCGI_LightmapMult);

            var lmst = _LTCGI_LightmapST[i];
            if (DEBUG_ReverseUnityLightmapST)
            {
                // workaround?
                lmst.x /= r.lightmapScaleOffset.x;
                lmst.y /= r.lightmapScaleOffset.y;
                lmst.z -= r.lightmapScaleOffset.z;
                lmst.w -= r.lightmapScaleOffset.w;
            }
            block.SetVector("_Udon_LTCGI_LightmapST", lmst);

            r.SetPropertyBlock(block);
        }

        // experimental support for LTCGI on avatar shaders!
        LTCGIShader.SetGlobalFloat(LTCGIShader.PropertyToID("_Udon_LTCGI_AvatarEnable"), 1.0f);

        extraDataId = LTCGIShader.PropertyToID("_Udon_LTCGI_ExtraData");
        vert0Id = LTCGIShader.PropertyToID("_Udon_LTCGI_Vertices_0");
        vert1Id = LTCGIShader.PropertyToID("_Udon_LTCGI_Vertices_1");
        vert2Id = LTCGIShader.PropertyToID("_Udon_LTCGI_Vertices_2");
        vert3Id = LTCGIShader.PropertyToID("_Udon_LTCGI_Vertices_3");

        Update();

        stopwatch.Stop();

        Debug.Log($"LTCGI adapter started for {_LTCGI_ScreenCount} ({_LTCGI_ScreenCountDynamic} dynamic) screens, {_Renderers.Length} renderers, LTCGIShader mode, took: {stopwatch.ElapsedMilliseconds}ms");

        if (_LTCGI_ScreenCountDynamic == 0 || _Renderers.Length == 0)
        {
            Debug.Log("LTCGI adapter going to sleep 😴");
            this.enabled = false;
        }
    }

    private Vector4 CalcTransform(Vector4 i, Transform t)
    {
        var ret = (Vector4)t.TransformPoint((Vector3)i);
        ret.w = i.w; // keep UV the same
        return ret;
    }

    void Update()
    {
        if (disabled) return;

        for (int i = 0; i < _LTCGI_ScreenCountDynamic /* only run for dynamic screens */; i++)
        {
            var transform = _LTCGI_ScreenTransforms[i];
            _LTCGI_Vertices_0t[i] = CalcTransform(_LTCGI_Vertices_0[i], transform);
            _LTCGI_Vertices_1t[i] = CalcTransform(_LTCGI_Vertices_1[i], transform);
            _LTCGI_Vertices_2t[i] = CalcTransform(_LTCGI_Vertices_2[i], transform);
            _LTCGI_Vertices_3t[i] = CalcTransform(_LTCGI_Vertices_3[i], transform);
        }

        LTCGIShader.SetGlobalVectorArray(extraDataId, _LTCGI_ExtraData);
        LTCGIShader.SetGlobalVectorArray(vert0Id, _LTCGI_Vertices_0t);
        LTCGIShader.SetGlobalVectorArray(vert1Id, _LTCGI_Vertices_1t);
        LTCGIShader.SetGlobalVectorArray(vert2Id, _LTCGI_Vertices_2t);
        LTCGIShader.SetGlobalVectorArray(vert3Id, _LTCGI_Vertices_3t);
    }

    // See the docs for more info:
    // https://github.com/PiMaker/ltcgi/wiki#udonsharp-api

    public int _GetIndex(GameObject screen)
    {
        var idx = Array.IndexOf(_Screens, screen);
        if (idx != -1)
        {
            // if (idx >= _LTCGI_ScreenCountDynamic)
            // {
            //     Debug.LogError("LTCGI: Cannot index non-dynamic object " + screen.name);
            //     return -1;
            // }

            return idx;
        }
        else
        {
            Debug.LogError("LTCGI: Cannot index unregistered object " + (screen == null ? "<null>" : screen.name));
            return -1;
        }
    }

    public Color _GetColor(int screen)
    {
        if (screen < 0) return Color.black;
        var data = _LTCGI_ExtraData[screen];
        return new Color(data.x, data.y, data.z);
    }

    public void _SetColor(int screen, Color color)
    {
        if (screen < 0) return;
        _LTCGI_ExtraData[screen].x = color.r;
        _LTCGI_ExtraData[screen].y = color.g;
        _LTCGI_ExtraData[screen].z = color.b;

        if (!this.enabled) Update();
    }

    public void _SetVideoTexture(Texture texture)
    {
        BlurCRTInput.material.SetTexture("_MainTex", texture);
        LTCGIShader.SetGlobalTexture(textureLod0Id, texture);
    }

    private uint getFlags(int screen)
    {
        var raw = _LTCGI_ExtraData[screen].w;
        var buffer = new byte[4];
        WriteSingle(raw, buffer, 0);
        return ReadUInt32(buffer, 0);
    }

    private void setFlags(int screen, uint flags)
    {
        var buffer = new byte[4];
        WriteUInt32(flags, buffer, 0);
        var raw = ReadSingle(buffer, 0);
        _LTCGI_ExtraData[screen].w = raw;
    }

    public void _SetTexture(int screen, uint index)
    {
        if (screen < 0) return;
        var flags = getFlags(screen);
        flags &= ~(0xfU << 4);
        flags |= (index & 0xf) << 4;
        setFlags(screen, flags);

        if (!this.enabled) Update();
    }

    public void _SetGlobalState(bool enabled)
    {
        float fstate = enabled ? 0.0f : 1.0f;
        if (globalDisableId == -1)
            globalDisableId = LTCGIShader.PropertyToID("_Udon_LTCGI_GlobalDisable");
        LTCGIShader.SetGlobalFloat(globalDisableId, fstate);
        disabled = !enabled;
    }


    // Below code from: https://github.com/Xytabich/UNet

    private const int BIT8 = 8;
    private const int BIT16 = 16;
    private const int BIT24 = 24;
    private const int BIT32 = 32;
    private const int BIT40 = 40;
    private const int BIT48 = 48;
    private const int BIT56 = 56;

    private const uint FLOAT_SIGN_BIT = 0x80000000;
    private const uint FLOAT_EXP_MASK = 0x7F800000;
    private const uint FLOAT_FRAC_MASK = 0x007FFFFF;

    /// <summary>
    /// Writes unsigned 32-bit integer (<see cref="uint"/>)
    /// </summary>
    /// <remarks>Takes 4 bytes</remarks>
    /// <param name="buffer">Target buffer</param>
    /// <param name="index">Index in the buffer at which to start writing data</param>
    /// <returns>Size in bytes</returns>
    public int WriteUInt32(uint value, byte[] buffer, int index)
    {
        buffer[index] = (byte)((value >> BIT24) & 255u);
        index++;
        buffer[index] = (byte)((value >> BIT16) & 255u);
        index++;
        buffer[index] = (byte)((value >> BIT8) & 255u);
        index++;
        buffer[index] = (byte)(value & 255u);
        return 4;
    }

    /// <summary>
    /// Reads unsigned 32-bit integer (<see cref="uint"/>)
    /// </summary>
    /// <remarks>Takes 4 bytes</remarks>
    /// <param name="buffer">Target buffer</param>
    /// <param name="index">Index in the buffer where to start reading data</param>
    public uint ReadUInt32(byte[] buffer, int index)
    {
        uint value = 0;
        value |= (uint)buffer[index] << BIT24;
        index++;
        value |= (uint)buffer[index] << BIT16;
        index++;
        value |= (uint)buffer[index] << BIT8;
        index++;
        value |= (uint)buffer[index];
        return value;
    }

    /// <summary>
    /// Writes single-precision floating-point number
    /// </summary>
    /// <remarks>Takes 4 bytes</remarks>
    /// <param name="buffer">Target buffer</param>
    /// <param name="index">Index in the buffer at which to start writing data</param>
    /// <returns>Size in bytes</returns>
    public int WriteSingle(float value, byte[] buffer, int index)
    {
        uint tmp = 0;
        if(float.IsNaN(value))
        {
            tmp = FLOAT_EXP_MASK | FLOAT_FRAC_MASK;
        }
        else if(float.IsInfinity(value))
        {
            tmp = FLOAT_EXP_MASK;
            if(float.IsNegativeInfinity(value)) tmp |= FLOAT_SIGN_BIT;
        }
        else if(value != 0f)
        {
            if(value < 0f)
            {
                value = -value;
                tmp |= FLOAT_SIGN_BIT;
            }

            int exp = 0;
            bool normal = true;
            while(value >= 2f)
            {
                value *= 0.5f;
                exp++;
            }
            while(value < 1f)
            {
                if(exp == -126)
                {
                    normal = false;
                    break;
                }
                value *= 2f;
                exp--;
            }

            if(normal)
            {
                value -= 1f;
                exp += 127;
            }
            else exp = 0;

            tmp |= Convert.ToUInt32(exp << 23) & FLOAT_EXP_MASK;
            tmp |= Convert.ToUInt32(value * (2 << 22)) & FLOAT_FRAC_MASK;
        }
        return WriteUInt32(tmp, buffer, index);
    }

    /// <summary>
    /// Reads single-precision floating-point number
    /// </summary>
    /// <remarks>Takes 4 bytes</remarks>
    /// <param name="buffer">Target buffer</param>
    /// <param name="index">Index in the buffer where to start reading data</param>
    public float ReadSingle(byte[] buffer, int index)
    {
        uint value = ReadUInt32(buffer, index);
        if(value == 0 || value == FLOAT_SIGN_BIT) return 0f;

        int exp = (int)((value & FLOAT_EXP_MASK) >> 23);
        int frac = (int)(value & FLOAT_FRAC_MASK);
        bool negate = (value & FLOAT_SIGN_BIT) == FLOAT_SIGN_BIT;
        if(exp == 0xFF)
        {
            if(frac == 0)
            {
                return negate ? float.NegativeInfinity : float.PositiveInfinity;
            }
            return float.NaN;
        }

        bool normal = exp != 0x00;
        if(normal) exp -= 127;
        else exp = -126;

        float result = frac / (float)(2 << 22);
        if(normal) result += 1f;

        result *= Mathf.Pow(2, exp);
        if(negate) result = -result;
        return result;
    }
}