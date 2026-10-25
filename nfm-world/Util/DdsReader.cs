using NFMWorld.Graphics;

namespace NFMWorld.Util;

/// <summary>
/// Parses a DDS (DirectDraw Surface) file header and loads its base mip level into an
/// <see cref="ITexture"/> - the compressed-texture counterpart to <see cref="TextureLoader"/>
/// (which only handles stb_image formats; stb_image has no DDS decoder). Ported from FNA's own
/// hand-written parser (<c>FNA/src/Graphics/Texture.cs</c>'s <c>ParseDDS</c>/<c>CalculateDDSLevelSize</c>,
/// "based on MojoDDS") rather than re-deriving the header layout.
/// </summary>
/// <remarks>
/// Only DXT1/DXT3/DXT5 are supported (the three <see cref="TextureFormat"/> variants the graphics
/// abstraction currently exposes) - the RGB/RGBA-uncompressed and DX10-extended-header branches
/// FNA's parser also handles are not ported, since no in-repo asset exercises them today (confirmed:
/// no <c>.dds</c> file is tracked anywhere under <c>data/</c>). Only the base mip level is uploaded -
/// <see cref="IGraphicsDevice.CreateTexture"/>/<see cref="ICommandBuffer.UpdateTexture"/> don't take a
/// mip-level parameter yet, so a DDS with a full mip chain loads only its largest level (the texture
/// itself is still created non-mipmapped). Cube maps are rejected, matching FNA's own DDSFromStreamEXT.
/// </remarks>
public static class DdsReader
{
    private const uint DdsMagic = 0x20534444; // 'DDS '
    private const uint DdsHeaderSize = 124;
    private const uint DdsPixelFormatSize = 32;
    private const uint DdsdHeight = 0x2;
    private const uint DdsdWidth = 0x4;
    private const uint DdsdPitch = 0x8;
    private const uint DdsdLinearSize = 0x80000;
    private const uint DdsdRequired = DdsdHeight | DdsdWidth;
    private const uint DdsCapsMipmap = 0x400000;
    private const uint DdsCapsTexture = 0x1000;
    private const uint DdsCaps2Cubemap = 0x200;
    private const uint DdpfFourCC = 0x4;
    private const uint FourCcDxt1 = 0x31545844;
    private const uint FourCcDxt3 = 0x33545844;
    private const uint FourCcDxt5 = 0x35545844;

    public static ITexture LoadFromStream(IGraphicsDevice device, Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        if (reader.ReadUInt32() != DdsMagic)
            throw new NotSupportedException("Not a DDS file (bad magic).");

        if (reader.ReadUInt32() != DdsHeaderSize)
            throw new NotSupportedException("Invalid DDS header.");

        var flags = reader.ReadUInt32();
        if ((flags & DdsdRequired) != DdsdRequired)
            throw new NotSupportedException("Invalid DDS flags.");

        var height = reader.ReadInt32();
        var width = reader.ReadInt32();
        reader.ReadUInt32(); // dwPitchOrLinearSize, unused
        reader.ReadUInt32(); // dwDepth, unused
        reader.ReadInt32(); // dwMipMapCount - only the base level is loaded, see remarks above

        reader.ReadBytes(4 * 11); // "reserved"

        if (reader.ReadUInt32() != DdsPixelFormatSize)
            throw new NotSupportedException("Invalid DDS pixel format header.");

        var formatFlags = reader.ReadUInt32();
        var formatFourCc = reader.ReadUInt32();
        reader.ReadUInt32(); // dwRGBBitCount
        reader.ReadUInt32(); // dwRBitMask
        reader.ReadUInt32(); // dwGBitMask
        reader.ReadUInt32(); // dwBBitMask
        reader.ReadUInt32(); // dwABitMask

        var caps = reader.ReadUInt32();
        if ((caps & DdsCapsTexture) == 0)
            throw new NotSupportedException("DDS file does not contain a texture.");

        var caps2 = reader.ReadUInt32();
        if (caps2 != 0 && (caps2 & DdsCaps2Cubemap) != 0)
            throw new NotSupportedException("DDS cube maps are not supported.");

        reader.ReadUInt32(); // dwCaps3, unused
        reader.ReadUInt32(); // dwCaps4, unused
        reader.ReadUInt32(); // "reserved"

        if ((formatFlags & DdpfFourCC) == 0)
            throw new NotSupportedException("Only DDS files with a FourCC (DXT1/DXT3/DXT5) pixel format are supported.");

        var format = formatFourCc switch
        {
            FourCcDxt1 => TextureFormat.Dxt1,
            FourCcDxt3 => TextureFormat.Dxt3,
            FourCcDxt5 => TextureFormat.Dxt5,
            _ => throw new NotSupportedException($"Unsupported DDS FourCC 0x{formatFourCc:X8} (only DXT1/DXT3/DXT5 are supported)."),
        };

        var levelSize = CalculateLevelSize(width, height, format);
        var data = reader.ReadBytes(levelSize);
        if (data.Length != levelSize)
            throw new EndOfStreamException("DDS file is truncated - could not read the base mip level.");

        return device.CreateTexture(new TextureDesc(width, height, format), data);
    }

    private static int CalculateLevelSize(int width, int height, TextureFormat format)
    {
        var blockSize = format == TextureFormat.Dxt1 ? 8 : 16;
        width = Math.Max(width, 1);
        height = Math.Max(height, 1);
        return ((width + 3) / 4) * ((height + 3) / 4) * blockSize;
    }
}
