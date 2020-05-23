﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace FreeMote.Psb
{
    public static class PsbExtension
    {
        /// <summary>
        /// If this spec uses RL
        /// </summary>
        /// <param name="spec"></param>
        /// <returns></returns>
        public static PsbCompressType CompressType(this PsbSpec spec)
        {
            switch (spec)
            {
                case PsbSpec.krkr:
                    return PsbCompressType.RL;
                case PsbSpec.ems:
                case PsbSpec.common:
                case PsbSpec.win:
                case PsbSpec.other:
                default:
                    return PsbCompressType.None;
            }
        }

        /// <summary>
        /// Try to measure EMT PSB Canvas Size
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns>True: The canvas size can be measured; False: can not get canvas size</returns>
        public static bool TryGetCanvasSize(this PSB psb, out int width, out int height)
        {
            //Get from CharProfile
            if (psb.Objects["metadata"] is PsbDictionary md && md["charaProfile"] is PsbDictionary cp &&
                cp["pixelMarker"] is PsbDictionary pm
                && pm["boundsBottom"] is PsbNumber b && pm["boundsTop"] is PsbNumber t &&
                pm["boundsLeft"] is PsbNumber l && pm["boundsRight"] is PsbNumber r)
            {
                height = (int) Math.Abs(b.AsFloat - t.AsFloat);
                width = (int) Math.Abs(r.AsFloat - l.AsFloat);
                return true;
            }

            //not really useful
            var resList = psb.CollectResources<ImageMetadata>();
            width = resList.Max(data => data.Width);
            height = resList.Max(data => data.Height);
            return false;
        }

        /// <summary>
        /// Get name in <see cref="PsbDictionary"/>
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static string GetName(this IPsbChild c)
        {
            if (c?.Parent is PsbDictionary dic)
            {
                var result = dic.FirstOrDefault(pair => Equals(pair.Value, c));
                return result.Value == null ? null : result.Key;
            }

            if (c?.Parent is PsbList col)
            {
                var result = col.Value.IndexOf(c);
                if (result < 0)
                {
                    return null;
                }

                return $"[{result}]";
            }

            return null;
        }

        /// <summary>
        /// Get name
        /// </summary>
        /// <param name="c"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static string GetName(this IPsbSingleton c, PsbDictionary parent = null)
        {
            var source = parent ?? c?.Parents.FirstOrDefault(p => p is PsbDictionary) as PsbDictionary;
            var result = source?.FirstOrDefault(pair => Equals(pair.Value, c));
            return result?.Value == null ? null : result.Value.Key;
        }

        public static PsbString ToPsbString(this string s)
        {
            return s == null ? PsbString.Empty : new PsbString(s);
        }

        public static PsbNumber ToPsbNumber(this int i)
        {
            return new PsbNumber(i);
        }

        internal static void WriteUTF8(this BinaryWriter bw, string value)
        {
            bw.Write(Encoding.UTF8.GetBytes(value));
        }

        /// <summary>
        /// Set archData value to archData object
        /// </summary>
        /// <param name="archData"></param>
        /// <param name="val"></param>
        public static void SetPsbArchData(this IArchData archData, IPsbValue val)
        {
            archData.PsbArchData["archData"] = val;
        }

        #region Object Finding

        /// <summary>
        /// Quickly fetch children (use at your own risk)
        /// </summary>
        /// <param name="col"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IPsbValue Children(this IPsbValue col, string name)
        {
            while (true)
            {
                switch (col)
                {
                    case PsbDictionary dictionary:
                        return dictionary[name];
                    case PsbList collection:
                        col = collection.FirstOrDefault(c => c is PsbDictionary d && d.ContainsKey(name));
                        continue;
                }

                throw new ArgumentException($"{col} doesn't have children.");
            }
        }

        /// <summary>
        /// Quickly fetch number (use at your own risk)
        /// </summary>
        /// <param name="col"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static int GetInt(this IPsbValue col)
        {
            return ((PsbNumber) col).AsInt;
        }

        /// <summary>
        /// Quickly fetch number (use at your own risk)
        /// </summary>
        /// <param name="col"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static float GetFloat(this IPsbValue col)
        {
            return ((PsbNumber)col).AsFloat;
        }

        /// <summary>
        /// Quickly fetch number (use at your own risk)
        /// </summary>
        /// <param name="col"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static double GetDouble(this IPsbValue col)
        {
            return ((PsbNumber)col).AsDouble;
        }

        /// <summary>
        /// Quickly fetch children (use at your own risk)
        /// </summary>
        /// <param name="col"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static IPsbValue Children(this IPsbValue col, int index)
        {
            while (true)
            {
                switch (col)
                {
                    case PsbDictionary dictionary:
                        return dictionary.Values.ElementAt(index);
                    case PsbList collection:
                        col = collection[index];
                        continue;
                }

                throw new ArgumentException($"{col} doesn't have children.");
            }
        }

        public static IEnumerable<IPsbValue> FindAllByPath(this PsbDictionary psbObj, string path)
        {
            if (psbObj == null)
                yield break;
            if (path.StartsWith("/"))
            {
                path = new string(path.SkipWhile(c => c == '/').ToArray());
            }

            if (path.Contains("/"))
            {
                var pos = path.IndexOf('/');
                var current = path.Substring(0, pos);
                if (current == "*")
                {
                    if (pos == path.Length - 1) //end
                    {
                        if (psbObj is PsbDictionary dic)
                        {
                            foreach (var dicValue in dic.Values)
                            {
                                yield return dicValue;
                            }
                        }
                    }

                    path = new string(path.SkipWhile(c => c == '*').ToArray());
                    foreach (var val in psbObj.Values)
                    {
                        if (val is PsbDictionary dic)
                        {
                            foreach (var dicValue in dic.FindAllByPath(path))
                            {
                                yield return dicValue;
                            }
                        }
                    }
                }

                if (pos == path.Length - 1 && psbObj[current] != null)
                {
                    yield return psbObj[current];
                }

                var currentObj = psbObj[current];
                if (currentObj is PsbDictionary collection)
                {
                    path = path.Substring(pos);
                    foreach (var dicValue in collection.FindAllByPath(path))
                    {
                        yield return dicValue;
                    }
                }
            }

            if (path == "*")
            {
                foreach (var value in psbObj.Values)
                {
                    yield return value;
                }
            }
            else if (psbObj[path] != null)
            {
                yield return psbObj[path];
            }
        }

        /// <summary>
        /// Find object by path (use index [n] for collection)
        /// <example>e.g. "/object/all_parts/motion/全体構造/layer/[0]"</example>
        /// </summary>
        /// <param name="psbObj"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static IPsbValue FindByPath(this PsbDictionary psbObj, string path)
        {
            if (psbObj == null)
                return null;
            if (path.StartsWith("/"))
            {
                path = new string(path.SkipWhile(c => c == '/').ToArray());
            }

            if (path.Contains("/"))
            {
                var pos = path.IndexOf('/');
                var current = path.Substring(0, pos);
                if (pos == path.Length - 1)
                {
                    return psbObj[current];
                }

                var currentObj = psbObj[current];
                if (currentObj is PsbDictionary dictionary)
                {
                    path = path.Substring(pos);
                    return dictionary.FindByPath(path);
                }

                if (currentObj is PsbList collection)
                {
                    path = path.Substring(pos);
                    return collection.FindByPath(path);
                }
            }

            return psbObj[path];
        }

        /// <inheritdoc cref="FindByPath(FreeMote.Psb.PsbDictionary,string)"/>
        public static IPsbValue FindByPath(this PsbList psbObj, string path)
        {
            if (psbObj == null)
                return null;
            if (path.StartsWith("/"))
            {
                path = new string(path.SkipWhile(c => c == '/').ToArray());
            }

            if (path.Contains("/"))
            {
                var pos = path.IndexOf('/');
                var current = path.Substring(0, pos);
                IPsbValue currentObj = null;
                if (current == "*")
                {
                    currentObj = psbObj.FirstOrDefault();
                }

                if (current.StartsWith("[") && current.EndsWith("]") &&
                    Int32.TryParse(current.Substring(1, current.Length - 2), out var id))
                {
                    currentObj = psbObj[id];
                }

                if (pos == path.Length - 1)
                {
                    return currentObj;
                }

                if (currentObj is PsbDictionary dictionary)
                {
                    path = path.Substring(pos);
                    return dictionary.FindByPath(path);
                }

                if (currentObj is PsbList collection)
                {
                    path = path.Substring(pos);
                    return collection.FindByPath(path);
                }
            }

            return null;
        }

        /// <summary>
        /// Find object by MMO style path (based on label)
        /// <example>e.g. "all_parts/全体構造/■全体レイアウト"</example>
        /// </summary> 
        /// <param name="psbObj"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static IPsbValue FindByMmoPath(this IPsbCollection psbObj, string path)
        {
            if (psbObj == null)
                return null;
            if (path.StartsWith("/"))
            {
                path = new string(path.SkipWhile(c => c == '/').ToArray());
            }

            string current = null;
            int pos = -1;
            if (path.Contains("/"))
            {
                pos = path.IndexOf('/');
                current = path.Substring(0, pos);
            }
            else
            {
                current = path;
            }

            IPsbValue currentObj = null;
            if (psbObj is PsbList col)
            {
                currentObj = col.FirstOrDefault(c =>
                    c is PsbDictionary d && d.ContainsKey("label") && d["label"] is PsbString s &&
                    s.Value == current);
            }
            else if (psbObj is PsbDictionary dic)
            {
                //var dd = dic.Value.FirstOrDefault();
                var children =
                    (PsbList) (dic.ContainsKey("layerChildren") ? dic["layerChildren"] : dic["children"]);
                currentObj = children.FirstOrDefault(c =>
                    c is PsbDictionary d && d.ContainsKey("label") && d["label"] is PsbString s &&
                    s.Value == current);
            }

            if (pos == path.Length - 1 || pos < 0)
            {
                return currentObj;
            }

            if (currentObj is IPsbCollection psbCol)
            {
                path = path.Substring(pos);
                return psbCol.FindByMmoPath(path);
            }

            return psbObj[path];
        }

        /// <summary>
        /// Get MMO style path (based on label)
        /// </summary>
        /// <param name="child"></param>
        /// <returns></returns>
        public static string GetMmoPath(this IPsbChild child)
        {
            if (child?.Parent == null)
            {
                if (child is PsbDictionary dic)
                {
                    return dic["label"].ToString();
                }

                return "";
            }

            List<string> paths = new List<string>();

            while (child != null)
            {
                if (child is PsbDictionary current)
                {
                    if (current.ContainsKey("label"))
                    {
                        paths.Add(current["label"].ToString());
                    }
                    else
                    {
                        paths.Add(current.GetName());
                    }
                }

                child = child.Parent;
            }

            paths.Reverse();
            return string.Join("/", paths);
        }

        #endregion

        #region MDF

        /// <summary>
        /// Save PSB as pure MDF file
        /// </summary>
        /// <remarks>can not save as impure MDF (such as MT19937 MDF)</remarks>
        /// <param name="psb"></param>
        /// <param name="path"></param>
        /// <param name="key"></param>
        public static void SaveAsMdfFile(this PSB psb, string path, uint? key = null)
        {
            psb.Merge();
            var bytes = psb.Build();
            Adler32 adler = new Adler32();
            uint checksum = 0;
            if (key == null)
            {
                adler.Update(bytes);
                checksum = (uint) adler.Checksum;
            }

            MemoryStream ms = new MemoryStream(bytes);
            using (Stream fs = new FileStream(path, FileMode.Create))
            {
                if (key != null)
                {
                    MemoryStream nms = new MemoryStream((int) ms.Length);
                    PsbFile.Encode(key.Value, EncodeMode.Encrypt, EncodePosition.Auto, ms, nms);
                    ms.Dispose();
                    ms = nms;
                    var pos = ms.Position;
                    adler.Update(ms);
                    checksum = (uint) adler.Checksum;
                    ms.Position = pos;
                }

                BinaryWriter bw = new BinaryWriter(fs);
                bw.WriteStringZeroTrim(MdfFile.Signature);
                bw.Write((uint) ms.Length);
                bw.Write(ZlibCompress.Compress(ms));
                bw.WriteBE(checksum);
                ms.Dispose();
                bw.Flush();
            }
        }

        /// <summary>
        /// Save as pure MDF
        /// </summary>
        /// <param name="psb"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static byte[] SaveAsMdf(this PSB psb, uint? key = null)
        {
            psb.Merge();
            var bytes = psb.Build();
            Adler32 adler = new Adler32();
            uint checksum = 0;
            if (key == null)
            {
                adler.Update(bytes);
                checksum = (uint) adler.Checksum;
            }

            MemoryStream ms = new MemoryStream(bytes);
            using (MemoryStream fs = new MemoryStream())
            {
                if (key != null)
                {
                    MemoryStream nms = new MemoryStream((int) ms.Length);
                    PsbFile.Encode(key.Value, EncodeMode.Encrypt, EncodePosition.Auto, ms, nms);
                    ms.Dispose();
                    ms = nms;
                    var pos = ms.Position;
                    adler.Update(ms);
                    checksum = (uint) adler.Checksum;
                    ms.Position = pos;
                }

                BinaryWriter bw = new BinaryWriter(fs);
                bw.WriteStringZeroTrim(MdfFile.Signature);
                bw.Write((uint) ms.Length);
                bw.Write(ZlibCompress.Compress(ms));
                bw.WriteBE(checksum);
                ms.Dispose();
                bw.Flush();
                return fs.ToArray();
            }
        }

        #endregion

        #region PSB Parser

        public static bool ByteArrayEqual(this byte[] a1, byte[] a2)
        {
            if (a1 == null && a2 == null)
            {
                return true;
            }
            if (a1 == null || a2 == null)
            {
                return false;
            }
            if (a1.Length != a2.Length)
            {
                return false;
            }
            return ByteSpanEqual(a1, a2);
        }

        /// <summary>
        /// Fast compare byte array
        /// </summary>
        /// <param name="a1"></param>
        /// <param name="a2"></param>
        /// <returns></returns>
        public static bool ByteSpanEqual(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2)
        {
            return a1.SequenceEqual(a2);
        }

        /// <summary>
        /// Check if number is not NaN
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static bool IsFinite(this float num)
        {
            return !Single.IsNaN(num) && !Single.IsInfinity(num);
        }

        /// <summary>
        /// Check if number is not NaN
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static bool IsFinite(this double num)
        {
            return !Double.IsNaN(num) && !Double.IsInfinity(num);
        }

        //WARN: GetSize should not return 0
        /// <summary>
        /// Black magic to get size hehehe...
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public static int GetSize(this int i)
        {
            bool neg = false;
            if (i < 0)
            {
                neg = true;
                i = Math.Abs(i);
            }

            var hex = i.ToString("X");
            var l = hex.Length;
            bool firstBitOne =
                hex[0] >= '8' &&
                hex.Length % 2 == 0; //FIXED: Extend size if first bit is 1 //FIXED: 0x0F is +, 0xFF is -, 0x0FFF is +

            if (l % 2 != 0)
            {
                l++;
            }

            l = l / 2;
            if (neg || firstBitOne)
            {
                l++;
            }

            if (l > 4)
            {
                l = 4;
            }

            return l;
        }

        /// <summary>
        /// Black magic to get size hehehe...
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public static int GetSize(this uint i)
        {
            //FIXED: Treat uint as int to prevent overconfidence
            if (i <= Int32.MaxValue)
            {
                return GetSize((int) i);
            }

            var l = i.ToString("X").Length;
            if (l % 2 != 0)
            {
                l++;
            }

            l = l / 2;
            if (l > 4)
            {
                l = 4;
            }

            return l;
        }

        /// <summary>
        /// Black magic... hehehe...
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public static int GetSize(this long i)
        {
            bool neg = false;
            if (i < 0)
            {
                neg = true;
                i = Math.Abs(i);
            }

            var hex = i.ToString("X");
            var l = hex.Length;
            bool firstBitOne =
                hex[0] >= '8' &&
                hex.Length % 2 == 0; //FIXED: Extend size if first bit is 1 //FIXED: 0x0F is +, 0xFF is -, 0x0FFF is +

            if (l % 2 != 0)
            {
                l++;
            }

            l = l / 2;
            if (neg || firstBitOne)
            {
                l++;
            }

            if (l > 8)
            {
                l = 8;
            }

            return l;
        }

        public static uint ReadCompactUInt(this BinaryReader br, byte size)
        {
            return br.ReadBytes(size).UnzipUInt();
        }

        public static void ReadAndUnzip(this BinaryReader br, byte size, byte[] data, bool unsigned = false)
        {
            br.Read(data, 0, size);

            byte fill = 0x0;
            if (!unsigned && (data[size - 1] >= 0b10000000)) //negative
            {
                fill = 0xFF;
            }

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = i < size ? data[i] : fill;
            }
        }

        /// <summary>
        /// Shorten number bytes
        /// </summary>
        /// <param name="i"></param>
        /// <param name="size">Fix size</param>
        /// <returns></returns>
        public static byte[] ZipNumberBytes(this int i, int size = 0)
        {
            return BitConverter.GetBytes(i).Take(size <= 0 ? i.GetSize() : size).ToArray();
        }

        /// <summary>
        /// Shorten number bytes
        /// </summary>
        /// <param name="i"></param>
        /// <param name="size">Fix size</param>
        /// <returns></returns>
        public static byte[] ZipNumberBytes(this long i, int size = 0)
        {
            return BitConverter.GetBytes(i).Take(size <= 0 ? i.GetSize() : size).ToArray();
        }

        /// <summary>
        /// Shorten number bytes
        /// </summary>
        /// <param name="i"></param>
        /// <param name="size">Fix size</param>
        /// <returns></returns>
        public static byte[] ZipNumberBytes(this uint i, int size = 0)
        {
            //FIXED: Treat uint as int to prevent overconfidence
            //if (i <= int.MaxValue)
            //{
            //    return ZipNumberBytes((int) i, size);
            //}
            var span = BitConverter.GetBytes(i);

            return span.Take(size <= 0 ? i.GetSize() : size).ToArray();
        }

        public static byte[] UnzipNumberBytes(this byte[] b, int size = 8, bool unsigned = false)
        {
            byte[] r = new byte[size];
            if (!unsigned && (b[b.Length - 1] >= 0b10000000)) //negative
            {
                for (int i = 0; i < r.Length; i++)
                {
                    r[i] = (0xFF);
                }
            }

            b.CopyTo(r, 0);

            return r;
        }

        public static void UnzipNumberBytes(this byte[] b, byte[] data, bool unsigned = false)
        {
            if (!unsigned && (b[b.Length - 1] >= 0b10000000)) //negative
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = (0xFF);
                }
            }

            b.CopyTo(data, 0);
        }

        public static long UnzipNumber(this byte[] b)
        {
            return BitConverter.ToInt64(b.UnzipNumberBytes(), 0);
        }

        public static uint UnzipUInt(this byte[] b)
        {
            //return BitConverter.ToUInt32(b.UnzipNumberBytes(4, true), 0);

            //optimized with Span<T>
            Span<byte> span = stackalloc byte[4];
            for (int i = 0; i < Math.Min(b.Length, 4); i++)
            {
                span[i] = b[i];
            }

            return MemoryMarshal.Read<uint>(span);
        }

        public static uint UnzipUInt(this byte[] b, int start, byte size)
        {
            //return BitConverter.ToUInt32(b.UnzipNumberBytes(4, true), 0);

            //optimized with Span<T>
            Span<byte> span = stackalloc byte[4];
            for (int i = 0; i < Math.Min(size, (byte) 4); i++)
            {
                span[i] = b[start + i];
            }

            return MemoryMarshal.Read<uint>(span);
        }

        #endregion
    }

    public class ByteListComparer : IComparer<IList<byte>>
    {
        public int Compare(IList<byte> x, IList<byte> y)
        {
            int result;
            int min = Math.Min(x.Count, y.Count);
            for (int index = 0; index < min; index++)
            {
                result = x[index].CompareTo(y[index]);
                if (result != 0) return result;
            }

            return x.Count.CompareTo(y.Count);
        }
    }

    public class StringListComparer : IComparer<IList<string>>
    {
        public int Compare(IList<string> x, IList<string> y)
        {
            int result;
            int min = Math.Min(x.Count, y.Count);
            for (int index = 0; index < min; index++)
            {
                result = String.Compare(x[index], y[index], StringComparison.Ordinal);
                if (result != 0) return result;
            }

            return x.Count.CompareTo(y.Count);
        }
    }
}