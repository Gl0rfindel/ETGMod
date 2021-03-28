using System;
using System.IO;
using System.Reflection;
using Ionic.Zip;
using IOFile = System.IO.File;

/// <summary>
/// ETGMod asset metadata.
/// </summary>
public class AssetMetadata {
    public ContainerType Container;
    public Type AssetType = null;

    public string File;

    public string Zip;

    public byte[] RawData;

    public Assembly Assembly;
    public string AssemblyName;

    public long Offset;
    public int Length;

    /// <summary>
    /// Returns a new stream to read the data from.
    /// In case of limited data (Length is set), LimitedStream is used.
    /// </summary>
    public Stream Stream {
        get {
            if (!HasData)
                return null;

            Stream stream = null;
            if (Container == ContainerType.Filesystem) {
                stream = IOFile.OpenRead(File);
            } else if (Container == ContainerType.Zip) {
                if (RawData != null) {
                    stream = new MemoryStream(RawData);
                } else {
                    using (ZipFile zip = ZipFile.Read(Zip)) {
                        var entry = zip[File];
                        if (entry != null) {
                            var ms = new MemoryStream((int) entry.UncompressedSize);
                            entry.Extract(ms);
                            ms.Seek(0, SeekOrigin.Begin);
                            stream = ms;
                        }
                    }
                }
            } else if (Container == ContainerType.Assembly) {
                stream = Assembly.GetManifestResourceStream(File);
            }

            if (stream == null || Length == 0) {
                return stream;
            }

            return new LimitedStream(stream, Offset, Length);
        }
    }

    /// <summary>
    /// Returns the files contents.
    /// </summary>
    public byte[] Data {
        get {
            if (!HasData)
                return null;

            if (RawData != null) {
                var data = new byte[RawData.Length];
                Buffer.BlockCopy(RawData, 0, data, 0, RawData.Length);
                return data;
            }

            using (Stream stream = Stream) {
                if (stream is LimitedStream) {
                    return ((LimitedStream) stream).GetBuffer();
                }

                using (MemoryStream ms = new MemoryStream()) {
                    byte[] buffer = new byte[2048];
                    int read;
                    while (0 < (read = stream.Read(buffer, 0, buffer.Length))) {
                        ms.Write(buffer, 0, read);
                    }
                    return ms.ToArray();
                }
            }
        }
    }

    public bool HasData {
        get {
            return AssetType != ETGMod.Assets.t_AssetDirectory;
        }
    }

    public AssetMetadata() {
        Container = ContainerType.Filesystem;
    }

    public AssetMetadata(string file)
        : this(file, 0, 0) {
    }

    public AssetMetadata(string file, long offset, int length)
        : this() {
        File = file;
        Offset = offset;
        Length = length;
    }

    public AssetMetadata(string zip, string file, byte[] rawData)
        : this(file) {
        Container = ContainerType.Zip;
        Zip = zip;
        File = file;
        RawData = rawData;
    }

    public AssetMetadata(Assembly assembly, string file)
        : this(file) {
        Container = ContainerType.Assembly;
        Assembly = assembly;
        AssemblyName = assembly.GetName().Name;
    }

    public enum ContainerType {
        Filesystem,
        Zip,
        Assembly
    }
}
