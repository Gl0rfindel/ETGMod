using System;
using System.Collections.Generic;
using System.IO;
using Ionic.Zip;

internal static class ZipExtensions {
    public static byte[] ReadAllBytes(this ZipEntry entry) {
        using (var ms = new MemoryStream((int) entry.UncompressedSize)) {
            entry.Extract(ms);
            // getting the buffer doesn't allocate and
            // since we explicitly set the size there is no excess
            return ms.GetBuffer();
        }
    }
}
