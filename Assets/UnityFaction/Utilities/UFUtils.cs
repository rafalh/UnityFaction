﻿using System.Text;
using UnityEngine;
using System;
using UFLevelStructure;
using System.Collections.Generic;

public class UFUtils {

    /// <summary>
    /// True if given string is an absolute path that leads into the assets folder.
    /// </summary>
    public static bool IsAssetPath(string path) {
        return path.StartsWith(Application.dataPath);
    }

    /// <summary>
    /// Converts given absolute path to a relative path, starting with Assets/
    /// </summary>
    public static string GetRelativeUnityPath(string path) {
        return "Assets" + path.Substring(Application.dataPath.Length);
    }

    /* -----------------------------------------------------------------------------------------------
     * -------------------------------------- BINARY READING -----------------------------------------
     * -----------------------------------------------------------------------------------------------
     */

    /// <summary>
    /// Starts reading string encoded in the given bytes until we hit a null byte.
    /// Also moves the pointer passed this null terminator
    /// </summary>
    public static string ReadNullTerminatedString(byte[] bytes, ref int pointer) {
        StringBuilder toReturn = new StringBuilder();
        char nextChar = Encoding.UTF8.GetString(bytes, pointer++, 1).ToCharArray()[0];
        while(nextChar != '\0') {
            toReturn.Append(nextChar);
            nextChar = Encoding.UTF8.GetString(bytes, pointer++, 1).ToCharArray()[0];
        }
        return toReturn.ToString();
    }

    /// <summary>
    /// Starts reading string encoded in the given bytes until we hit a null byte.
    /// </summary>
    public static string ReadNullTerminatedString(byte[] bytes, int pointer) {
        return ReadNullTerminatedString(bytes, ref pointer);
    }

    /// <summary>
    /// Reads and returns string with 2 byte number denoting its length.
    /// Moves the pointer to the first byte after the string.
    /// </summary>
    public static string ReadStringWithLengthHeader(byte[] bytes, ref int pointer) {
        short length = BitConverter.ToInt16(bytes, pointer);
        pointer += 2;
        string toReturn = Encoding.UTF8.GetString(bytes, pointer, length);
        pointer += length;
        return toReturn;
    }

    /// <summary>
    /// Reads 3 bytes and returns the color they would encode (with max alpha)
    /// </summary>
    public static Color GetRGBColor(byte[] bytes, int start) {
        float f = 1f / 255;
        float r = f * bytes[start];
        float g = f * bytes[start + 1];
        float b = f * bytes[start + 2];
        return new Color(r, g, b);
    }

    /// <summary>
    /// Reads 4 bytes and returns the color they would encode (including alpha value)
    /// </summary>
    public static Color GetRGBAColor(byte[] bytes, int start) {
        float f = 1f / 255;
        float r = f * bytes[start];
        float g = f * bytes[start + 1];
        float b = f * bytes[start + 2];
        float a = f * bytes[start + 3];
        return new Color(r, g, b, a);
    }

    /// <summary>
    /// Reads 3 consecutive floats and returns them inside a vector.
    /// </summary>
    public static Vector3 Getvector3(byte[] bytes, int start) {
        float x = BitConverter.ToSingle(bytes, start);
        float y = BitConverter.ToSingle(bytes, start + 4);
        float z = BitConverter.ToSingle(bytes, start + 8);
        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Reads 2 consecutive floats and returns them inside a vector.
    /// </summary>
    public static Vector3 Getvector2(byte[] bytes, int start) {
        float x = BitConverter.ToSingle(bytes, start);
        float y = BitConverter.ToSingle(bytes, start + 4);
        return new Vector2(x, y);
    }

    /// <summary>
    /// Rotations are stored as 3x3 matrices of floats, so they consist of 4x3x3=36 bytes.
    /// the entries of this matrix are stored in an awkard order, so make sure to refer to this method.
    /// </summary>
    public static Quaternion GetRotation(byte[] bytes, int start) {
        Vector3 row3 = Getvector3(bytes, start);
        Vector3 row1 = Getvector3(bytes, start + 12);
        Vector3 row2 = Getvector3(bytes, start + 24);

        Vector4 col1 = new Vector4(row1.x, row2.x, row3.x);
        Vector4 col2 = new Vector4(row1.y, row2.y, row3.y);
        Vector4 col3 = new Vector4(row1.z, row2.z, row3.z);
        Vector4 col4 = new Vector4(0f, 0f, 0f, 1f);

        Matrix4x4 mat = new Matrix4x4(col1, col2, col3, col4);
        return mat.rotation;
    }

    /// <summary>
    /// Reads position followed by rotation: 48 bytes total
    /// </summary>
    public static PosRot GetPosRot(byte[] bytes, int start) {
        Vector3 position = Getvector3(bytes, start);
        Quaternion rotation = GetRotation(bytes, start + 12);
        return new PosRot(position, rotation);
    }

    /// <summary>
    /// True if the given bytes can be reasonably expected to encode
    /// an index for an array with the size 'max'
    /// </summary>
    public static bool IsPlausibleIndex(byte[] bytes, int start, int max) {
        int value = BitConverter.ToInt32(bytes, start);
        return value >= 0 && value < max;
    }

    /// <summary>
    /// True if the given bytes can be reasonably expected to encode
    /// a floating point value such as a coordinate or matrix element.
    /// </summary>
    public static bool IsPlausibleFloat(byte[] bytes, int start) {
        float value = Mathf.Abs(BitConverter.ToSingle(bytes, start));
        return value == 0f || (value > 1e-10f && value < 1e+10f);
    }

    /// <summary>
    /// Return hexadecimal form of the given number
    /// </summary>
    public static string GetHex(int value) {
        return value.ToString("X");
    }

    /// <summary>
    /// Return hexadecimal form of the given number
    /// </summary>
    public static string GetHex(float value) {
        return value.ToString("X");
    }

    /// <summary>
    /// Return given byte as a string containing its binary form
    /// </summary>
    public static string GetBinary(byte[] bytes, int pointer) {
        return Convert.ToString(bytes[pointer], 2).PadLeft(8, '0');
    }

    /// <summary>
    /// Return given length of bytes as a string containg hexadecimal
    /// </summary>
    public static string GetHex(byte[] bytes, int start, int length) {
        StringBuilder toReturn = new StringBuilder();
        for(int i = 0; i < length; i++)
            toReturn.Append(BitConverter.ToString(bytes, start + i, 1));
        return toReturn.ToString();
    }

    /// <summary>
    /// Return value of a specific bit
    /// </summary>
    public static bool GetFlag(byte[] bytes, int pointer, int bit) {
        byte flags = bytes[pointer];
        byte match = (byte)(1 << bit);
        return (flags & match) != 0;
    }

    /// <summary>
    /// Return 4-bit integer encoded as a half of a byte
    /// </summary>
    public static byte GetNibble(byte[] bytes, int pointer, bool first) {
        byte value = bytes[pointer];

        if(first)
            return (byte)(value % 16);
        else
            return (byte)(value / 16);
    }

    /// <summary>
    /// Returns true if given byte encodes a readable character
    /// </summary>
    public static bool IsReadable(Byte b) {
        return b >= 32;
    }

    /// <summary>
    /// Returns true only if all bytes in the given range encode reabable characters
    /// </summary>
    public static bool IsReadable(byte[] bytes, int start, int length) {
        for(int i = start; i < start + length; i++) {
            if(!IsReadable(bytes[i]))
                return false;
        }
        return true;
    }

    /* -----------------------------------------------------------------------------------------------
     * ------------------------------------------ OTHERS ---------------------------------------------
     * -----------------------------------------------------------------------------------------------
     */

    public static int Count(bool[] values) {
        int toReturn = 0;
        for(int i = 0; i < values.Length; i++)
            if(values[i])
                toReturn++;
        return toReturn;
    }

    public static T GetRandom<T>(T[] list) {
        int index = UnityEngine.Random.Range(0, list.Length);
        return list[index];
    }

    public static T GetRandom<T>(List<T> list) {
        int index = UnityEngine.Random.Range(0, list.Count);
        return list[index];
    }

    /// <summary>
    /// Returns lerp factor needed to perform an exponential decay time step, 
    /// using the given half life time and Time.deltaTime.
    /// Use as follows: value = Lerp(value, goalValue, factor);
    /// </summary>
    public static float LerpExpFactor(float halfLife) {
        return LerpExpFactor(halfLife, Time.deltaTime);
    }

    /// <summary>
    /// Returns lerp factor needed to perform an exponential decay time step, 
    /// using the given half life time and time step.
    /// Use as follows: value = Lerp(value, goalValue, factor);
    /// </summary>
    public static float LerpExpFactor(float halfLife, float timeStep) {
        if(halfLife <= 0f)
            return 1f;
        else if(float.IsNaN(halfLife) || float.IsPositiveInfinity(halfLife))
            return 0f;

        float a = timeStep * Mathf.Log(2f) / (2 * halfLife);
        return 2 * a / (1 + a);
    }

    /// <summary>
    /// Moves color from start to target, by at most the given amount of delta.
    /// if delta is 1; it takes 1 second to move from say; black to red.
    /// </summary>
    public static Color MoveTowards(Color startColor, Color targetColor, float delta) {
        Vector4 vecStart = new Vector4(startColor.r, startColor.g, startColor.b, startColor.a);
        Vector4 vecTarget = new Vector4(targetColor.r, targetColor.g, targetColor.b, targetColor.a);
        Vector4 vecReturn = Vector4.MoveTowards(vecStart, vecTarget, delta);
        return new Color(vecReturn.x, vecReturn.y, vecReturn.z, vecReturn.w);
    }

    public static bool Inside(Vector3 point, PosRot center, float radius, Vector3 extents, bool box) {
        if(box)
            return InsideBox(point, new CenteredBox(center, extents));
        else
            return InsideSphere(point, center.position, radius);
    }

    public static bool InsideSphere(Vector3 point, Vector3 center, float radius) {
        return (point - center).sqrMagnitude < radius * radius;
    }

    public static bool InsideBox(Vector3 point, CenteredBox cb) {
        Vector3 relativePoint = point - cb.transform.posRot.position;
        Quaternion rot = Quaternion.Inverse(cb.transform.posRot.rotation);
        relativePoint = rot * relativePoint;

        bool inside = true;
        inside &= Mathf.Abs(relativePoint.x) < cb.extents.x;
        inside &= Mathf.Abs(relativePoint.y) < cb.extents.y;
        inside &= Mathf.Abs(relativePoint.z) < cb.extents.z;

        return inside;
    }

    public static void LocalReset(Transform transform) {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    public static void SetTransform(Transform transform, UFTransform ufTransform) {
        SetTransform(transform, ufTransform.posRot);
    }

    public static void SetTransform(Transform transform, PosRot pr) {
        transform.position = pr.position;
        transform.rotation = pr.rotation;
    }
}