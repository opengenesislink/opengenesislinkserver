using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NexVerse.RegionModules.Archives;

internal static class Program
{
    private record Entry(string Path, byte[] Payload, byte Type = (byte)'0');

    private static void Assert(bool test, string reason)
    {
        if (!test) throw new InvalidOperationException("IAR preflight regression: " + reason);
    }

    private static void WriteOctal(byte[] header, int offset, int width, long value)
    {
        string oct = Convert.ToString(value, 8) ?? "0";
        Assert(oct.Length <= width - 1, "test octal field overflow");
        byte[] bytes = Encoding.ASCII.GetBytes(oct.PadLeft(width - 1, '0') + '\0');
        Array.Copy(bytes, 0, header, offset, width);
    }

    private static void WriteEntry(Stream tar, Entry e, bool corruptChecksum = false)
    {
        byte[] header = new byte[512];
        byte[] name = Encoding.ASCII.GetBytes(e.Path);
        Assert(name.Length <= 100, "test name too long");
        Array.Copy(name, header, name.Length);
        WriteOctal(header, 100, 8, 420);
        WriteOctal(header, 108, 8, 0);
        WriteOctal(header, 116, 8, 0);
        WriteOctal(header, 124, 12, e.Payload.LongLength);
        WriteOctal(header, 136, 12, 0);
        for (int i = 148; i < 156; i++) header[i] = 32;
        header[156] = e.Type;
        Array.Copy(Encoding.ASCII.GetBytes("ustar\0"), 0, header, 257, 6);
        long sum = 0;
        foreach (byte x in header) sum += x;
        WriteOctal(header, 148, 8, sum + (corruptChecksum ? 1 : 0));
        tar.Write(header);
        tar.Write(e.Payload);
        int pad = (int)((512 - e.Payload.LongLength % 512) % 512);
        if (pad > 0) tar.Write(new byte[pad]);
    }

    private static string MakeArchive(Entry[] entries, bool corruptChecksum = false)
    {
        string destination = Path.Combine(Path.GetTempPath(), "ogl-iar-test-" + Guid.NewGuid().ToString("N") + ".iar");
        using (FileStream file = File.Create(destination))
        using (GZipStream gz = new(file, CompressionLevel.Fastest))
        {
            for (int i = 0; i < entries.Length; i++)
            {
                Entry item = entries[i];
                if (item.Path.Length > 100)
                {
                    // Reproduce OpenSim TarArchiveWriter.WriteFile:
                    // GNU LongName metadata, followed by the first 100
                    // ASCII bytes of the real header's filename.
                    WriteEntry(gz, new Entry(
                        "././@LongLink", Encoding.ASCII.GetBytes(item.Path), (byte)'L'));
                    WriteEntry(gz, new Entry(
                        item.Path.Substring(0, 100), item.Payload, item.Type),
                        corruptChecksum && i == 0);
                }
                else
                {
                    WriteEntry(gz, item, corruptChecksum && i == 0);
                }
            }
            gz.Write(new byte[1024]);
        }
        return destination;
    }

    private static void Evaluate(bool expected, string reason, Entry[] entries, bool badChecksum = false,
        int? logicalEntries = null)
    {
        string file = MakeArchive(entries, badChecksum);
        try
        {
            OglIarInspection result = OglIarInspector.Inspect(file);
            Assert(result.Valid == expected, reason + ": " + result.Error);
            if (logicalEntries.HasValue)
                Assert(result.Entries == logicalEntries.Value,
                    reason + ": GNU metadata must not inflate logical import progress");
        }
        finally { File.Delete(file); }
    }

    private static int Main()
    {
        var xml = new Entry("archive.xml", Encoding.UTF8.GetBytes("<archive major_version=\"1\" minor_version=\"0\"/>"));
        var inventory = new Entry("inventory/Test/item.xml", Encoding.UTF8.GetBytes("<InventoryItem/>"));
        var asset = new Entry("assets/01234567-89ab-cdef-0123-456789abcdef_texture.jp2", new byte[] { 1, 2, 3 });
        Evaluate(true, "valid IAR", new[] { xml, inventory, asset });

        // Regression for the production incident: long inventory paths
        // produced by OpenSim are introduced with GNU TAR type 'L'.
        string longPath = "inventory/" + new string('n', 145) + "/item.xml";
        var longInventory = new Entry(longPath, Encoding.UTF8.GetBytes("<InventoryItem/>"));
        Evaluate(true, "OpenSim GNU LongName inventory path",
            new[] { xml, longInventory, asset }, logicalEntries: 3);
        var longDirectory = new Entry("inventory/" + new string('d', 139) + "/", Array.Empty<byte>(), (byte)'5');
        Evaluate(true, "OpenSim GNU LongName directory",
            new[] { xml, longDirectory, longInventory, asset }, logicalEntries: 4);
        Evaluate(true, "several GNU LongName paths",
            new[] { xml, longInventory,
                new Entry("inventory/" + new string('q', 137) + "/second.xml", new byte[] { 5 }),
                asset }, logicalEntries: 4);

        // Real links, PAX, sparse formats and malformed GNU long-name
        // metadata must remain prohibited; no import path traversal.
        Evaluate(false, "GNU long link target K is not LongName",
            new[] { xml, new Entry("././@LongLink", Encoding.ASCII.GetBytes(longPath), (byte)'K'), inventory });
        Evaluate(false, "PAX extended header is not permitted",
            new[] { xml, new Entry("paxhdr", new byte[] { 0 }, (byte)'x'), inventory });
        Evaluate(false, "GNU sparse entry is not permitted",
            new[] { xml, new Entry("inventory/sparse", Array.Empty<byte>(), (byte)'S'), inventory });
        Evaluate(false, "hard link entry is not permitted",
            new[] { xml, new Entry("inventory/hardlink", Array.Empty<byte>(), (byte)'1'), inventory });
        Evaluate(false, "GNU LongName wrong metadata header path",
            new[] { xml, new Entry("fake-longname", Encoding.ASCII.GetBytes(longPath), (byte)'L'), inventory });
        Evaluate(false, "GNU LongName without following header",
            new[] { xml, inventory, new Entry("././@LongLink", Encoding.ASCII.GetBytes(longPath), (byte)'L') });
        Evaluate(false, "GNU LongName truncated path mismatch",
            new[] { xml,
                new Entry("././@LongLink", Encoding.ASCII.GetBytes(longPath), (byte)'L'),
                new Entry("inventory/mismatched.xml", new byte[] { 1 }), inventory });
        Evaluate(false, "GNU LongName traversal",
            new[] { xml, new Entry("././@LongLink",
                Encoding.ASCII.GetBytes("inventory/../" + new string('r', 130)), (byte)'L'), inventory });
        Evaluate(false, "GNU LongName too long",
            new[] { xml, new Entry("././@LongLink",
                Encoding.ASCII.GetBytes("inventory/" + new string('r', 510)), (byte)'L'), inventory });
        Evaluate(false, "GNU LongName NUL byte",
            new[] { xml, new Entry("././@LongLink",
                Encoding.ASCII.GetBytes(longPath + "\0"), (byte)'L'), inventory });
        Evaluate(false, "GNU LongName double metadata",
            new[] { xml,
                new Entry("././@LongLink", Encoding.ASCII.GetBytes(longPath), (byte)'L'),
                new Entry("././@LongLink", Encoding.ASCII.GetBytes(longPath), (byte)'L'),
                inventory });
        Evaluate(false, "GNU LongName may not hide symlink",
            new[] { xml, new Entry("././@LongLink", Encoding.ASCII.GetBytes(longPath), (byte)'L'),
                new Entry(longPath.Substring(0, 100), Array.Empty<byte>(), (byte)'2'), inventory });

        // Legacy OpenSim wrote UTF-8 bytes under an XML declaration that
        // incorrectly said UTF-16 (StringWriter + TarArchiveWriter).
        string legacyXml = "<?xml version=\"1.0\" encoding=\"utf-16\"?><archive major_version=\"0\" minor_version=\"3\"><assets_included>True</assets_included></archive>";
        var legacyControl = new Entry("archive.xml", Encoding.UTF8.GetBytes(legacyXml));
        Evaluate(true, "legacy UTF-8 data with UTF-16 declaration", new[] { legacyControl, inventory, asset });

        string correctedXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><archive major_version=\"0\" minor_version=\"3\"/>";
        Evaluate(true, "correct UTF-8 XML declaration",
            new[] { new Entry("archive.xml", Encoding.UTF8.GetBytes(correctedXml)), inventory, asset });

        // Genuine UTF-16 with BOM still uses the normal byte-stream parser.
        byte[] utf16Body = Encoding.Unicode.GetBytes(legacyXml);
        byte[] utf16WithBom = new byte[utf16Body.Length + 2];
        utf16WithBom[0] = 0xff; utf16WithBom[1] = 0xfe;
        Buffer.BlockCopy(utf16Body, 0, utf16WithBom, 2, utf16Body.Length);
        Evaluate(true, "genuine UTF-16 with BOM",
            new[] { new Entry("archive.xml", utf16WithBom), inventory });

        string legacyDtd = "<?xml version=\"1.0\" encoding=\"utf-16\"?><!DOCTYPE archive [<!ENTITY payload SYSTEM \"file:///etc/passwd\">]><archive major_version=\"0\" minor_version=\"3\"/>";
        Evaluate(false, "legacy declaration must not permit DTD",
            new[] { new Entry("archive.xml", Encoding.UTF8.GetBytes(legacyDtd)), inventory });

        // Strict UTF-8 decoder must reject illegal bytes; no replacement chars.
        byte[] malformedUtf8 = Encoding.UTF8.GetBytes(legacyXml);
        malformedUtf8[malformedUtf8.Length - 12] = 0xff;
        Evaluate(false, "legacy declaration rejects malformed UTF-8",
            new[] { new Entry("archive.xml", malformedUtf8), inventory });
        Evaluate(false, "traversal", new[] { xml, new Entry("inventory/../evil.xml", new byte[] { 1 }) });
        Evaluate(false, "absolute path", new[] { xml, new Entry("/inventory/evil.xml", new byte[] { 1 }) });
        Evaluate(false, "duplicate inventory path", new[] { xml, inventory, inventory });
        Evaluate(false, "unsupported link type", new[] { xml, new Entry("inventory/evil", Array.Empty<byte>(), (byte)'2') });
        Evaluate(false, "wrong ordering", new[] { xml, asset, inventory });
        Evaluate(false, "invalid major version", new[] { new Entry("archive.xml", Encoding.UTF8.GetBytes("<archive major_version=\"9\" minor_version=\"0\"/>")), inventory });
        Evaluate(false, "XXE/DTD", new[] { new Entry("archive.xml", Encoding.UTF8.GetBytes("<!DOCTYPE archive [ <!ENTITY leak SYSTEM \"file:///etc/passwd\"> ]><archive major_version=\"1\" minor_version=\"0\"/>")), inventory });
        Evaluate(false, "no control file", new[] { inventory });
        Evaluate(false, "checksum mismatch", new[] { xml, inventory }, badChecksum: true);
        Evaluate(false, "overlarge single payload", new[] { xml, new Entry("assets/big.bin", new byte[OglIarInspector.MaxFileBytes > int.MaxValue ? 1 : (int)OglIarInspector.MaxFileBytes + 1]), inventory });
        // Validate the 64-bit quota boundary without allocating a giant
        // fixture. Content validation is still tested with TAR/GZip fixtures.
        long tenGiB = 10L * 1024 * 1024 * 1024;
        Assert(OglIarInspector.MaxCompressedBytes == tenGiB,
            "compressed limit matches configured 10 GiB");
        Assert(OglIarInspector.MaxExpandedBytes == 40L * 1024 * 1024 * 1024,
            "expanded TAR limit is bounded");
        Assert(!OglIarInspector.IsAllowedCompressedSize(127),
            "too-short archive rejected");
        Assert(OglIarInspector.IsAllowedCompressedSize(128),
            "minimum archive length accepted");
        Assert(OglIarInspector.IsAllowedCompressedSize(2L * 1024 * 1024 * 1024 + 1),
            "compressed IAR over 2 GiB is permitted");
        Assert(OglIarInspector.IsAllowedCompressedSize(tenGiB),
            "10 GiB compressed archive permitted");
        Assert(!OglIarInspector.IsAllowedCompressedSize(tenGiB + 1),
            "compressed IAR beyond 10 GiB rejected");
        Console.WriteLine("OGL IAR bounded preflight: PASS (GNU LongName acceptance, unsafe TAR rejection, size boundaries)");
        return 0;
    }
}
