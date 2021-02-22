using System;
using System.Collections.Generic;
using System.IO;
using Ionic.Zip;

internal static class ZipExtensions
{
    public static MemoryStream ExtractToMemoryStream(this ZipEntry entry)
    {
        var ms = new MemoryStream((int)entry.UncompressedSize);
        entry.Extract(ms);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    public static byte[] ExtractToArray(this ZipEntry entry)
    {
        var ms = new MemoryStream((int)entry.UncompressedSize);
        entry.Extract(ms);
        return ms.GetBuffer();
    }
}
