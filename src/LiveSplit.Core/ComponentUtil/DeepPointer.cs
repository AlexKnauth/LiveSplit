﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using OffsetT = System.Int32;

// Note: Please be careful when modifying this because it could break existing components!

namespace LiveSplit.ComponentUtil;
public class DeepPointer
{
    public enum DerefType { Auto, Bit32, Bit64 }

    private readonly IntPtr _absoluteBase;
    private readonly bool _usingAbsoluteBase;
    private readonly DerefType _derefType;

    private readonly OffsetT _base;
    private List<OffsetT> _offsets;
    private readonly string _module;

    public DeepPointer(IntPtr absoluteBase, params OffsetT[] offsets)
        : this(absoluteBase, DerefType.Auto, offsets) { }

    public DeepPointer(IntPtr absoluteBase, DerefType derefType, params OffsetT[] offsets)
    {
        _absoluteBase = absoluteBase;
        _usingAbsoluteBase = true;
        _derefType = derefType;

        InitializeOffsets(offsets);
    }

    public DeepPointer(string module, OffsetT base_, params OffsetT[] offsets)
        : this(module, base_, DerefType.Auto, offsets) { }

    public DeepPointer(string module, OffsetT base_, DerefType derefType, params OffsetT[] offsets)
        : this(base_, derefType, offsets)
    {
        _module = module.ToLower();
    }

    public DeepPointer(OffsetT base_, params OffsetT[] offsets)
        : this(base_, DerefType.Auto, offsets) { }

    public DeepPointer(OffsetT base_, DerefType derefType, params OffsetT[] offsets)
    {
        _base = base_;
        _derefType = derefType;
        InitializeOffsets(offsets);
    }

    public T Deref<T>(Process process, T default_ = default) where T : struct // all value types including structs
    {
        if (!Deref(process, out T val))
        {
            val = default_;
        }

        return val;
    }

    public bool Deref<T>(Process process, out T value) where T : struct
    {
        if (!DerefOffsets(process, out IntPtr ptr)
            || !process.ReadValue(ptr, out value))
        {
            value = default;
            return false;
        }

        return true;
    }

    public byte[] DerefBytes(Process process, int count)
    {
        if (!DerefBytes(process, count, out byte[] bytes))
        {
            bytes = null;
        }

        return bytes;
    }

    public bool DerefBytes(Process process, int count, out byte[] value)
    {
        if (!DerefOffsets(process, out IntPtr ptr)
            || !process.ReadBytes(ptr, count, out value))
        {
            value = null;
            return false;
        }

        return true;
    }

    public string DerefString(Process process, int numBytes, string default_ = null)
    {
        if (!DerefString(process, ReadStringType.AutoDetect, numBytes, out string str))
        {
            str = default_;
        }

        return str;
    }

    public string DerefString(Process process, ReadStringType type, int numBytes, string default_ = null)
    {
        if (!DerefString(process, type, numBytes, out string str))
        {
            str = default_;
        }

        return str;
    }

    public bool DerefString(Process process, int numBytes, out string str)
    {
        return DerefString(process, ReadStringType.AutoDetect, numBytes, out str);
    }

    public bool DerefString(Process process, ReadStringType type, int numBytes, out string str)
    {
        var sb = new StringBuilder(numBytes);
        if (!DerefString(process, type, sb))
        {
            str = null;
            return false;
        }

        str = sb.ToString();
        return true;
    }

    public bool DerefString(Process process, StringBuilder sb)
    {
        return DerefString(process, ReadStringType.AutoDetect, sb);
    }

    public bool DerefString(Process process, ReadStringType type, StringBuilder sb)
    {
        if (!DerefOffsets(process, out IntPtr ptr)
            || !process.ReadString(ptr, type, sb))
        {
            return false;
        }

        return true;
    }

    public bool DerefOffsets(Process process, out IntPtr ptr)
    {
        bool is64Bit;
        if (_derefType == DerefType.Auto)
        {
            is64Bit = process.Is64Bit();
        }
        else
        {
            is64Bit = _derefType == DerefType.Bit64;
        }

        if (!string.IsNullOrEmpty(_module))
        {
            ProcessModuleWow64Safe module = process.ModulesWow64Safe()
                .FirstOrDefault(m => m.ModuleName.ToLower() == _module);
            if (module == null)
            {
                ptr = IntPtr.Zero;
                return false;
            }

            ptr = module.BaseAddress + _base;
        }
        else if (_usingAbsoluteBase)
        {
            ptr = _absoluteBase;
        }
        else
        {
            ptr = process.MainModuleWow64Safe().BaseAddress + _base;
        }

        for (int i = 0; i < _offsets.Count - 1; i++)
        {
            if (!process.ReadPointer(ptr + _offsets[i], is64Bit, out ptr)
                || ptr == IntPtr.Zero)
            {
                return false;
            }
        }

        ptr += _offsets[^1];
        return true;
    }

    private void InitializeOffsets(params OffsetT[] offsets)
    {
        _offsets =
        [
            0, // deref base first
            .. offsets,
        ];
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct Vector3f
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public readonly int IX => (int)X;
    public readonly int IY => (int)Y;
    public readonly int IZ => (int)Z;

    public Vector3f(float x, float y, float z) : this()
    {
        X = x;
        Y = y;
        Z = z;
    }

    public readonly float Distance(Vector3f other)
    {
        float result = ((X - other.X) * (X - other.X)) +
            ((Y - other.Y) * (Y - other.Y)) +
            ((Z - other.Z) * (Z - other.Z));
        return (float)Math.Sqrt(result);
    }

    public readonly float DistanceXY(Vector3f other)
    {
        float result = ((X - other.X) * (X - other.X)) +
            ((Y - other.Y) * (Y - other.Y));
        return (float)Math.Sqrt(result);
    }

    public readonly bool BitEquals(Vector3f other)
    {
        return X.BitEquals(other.X)
               && Y.BitEquals(other.Y)
               && Z.BitEquals(other.Z);
    }

    public readonly bool BitEqualsXY(Vector3f other)
    {
        return X.BitEquals(other.X)
               && Y.BitEquals(other.Y);
    }

    public override readonly string ToString()
    {
        return X + " " + Y + " " + Z;
    }
}
