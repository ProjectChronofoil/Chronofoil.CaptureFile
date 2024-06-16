namespace Chronofoil.CaptureFile.Binary.Capture;

public class ReadablePersistentCaptureData
{
    private const ushort HeaderSize = 240;
    private const ushort HeaderSizeWithPadding = 256;
	
    public int Size { get; init; }
    public int CaptureVersion { get; init; }

    public ulong Dx9GameRev { get; init; }
    public ulong Dx11GameRev { get; init; }
	
    public byte[] Dx9Hash { get; init; }
    public byte[] Dx11Hash { get; init; }
	
    public string FfxivGameVer { get; init; }
    public string Ex1GameVer { get; init; }
    public string Ex2GameVer { get; init; }
    public string Ex3GameVer { get; init; }
    public string Ex4GameVer { get; init; }
	
    public string PluginVersion { get; init; }

    public ReadablePersistentCaptureData(BinaryReader br) {
        Size = br.ReadInt32();
        CaptureVersion = br.ReadInt32();
        Dx9GameRev = br.ReadUInt64();
        Dx11GameRev = br.ReadUInt64();
        Dx9Hash = br.ReadBytes(20);
        Dx11Hash = br.ReadBytes(20);

        FfxivGameVer = br.ReadAscii(32);
        Ex1GameVer = br.ReadAscii(32);
        Ex2GameVer = br.ReadAscii(32);
        Ex3GameVer = br.ReadAscii(32);
        Ex4GameVer = br.ReadAscii(32);

        PluginVersion = br.ReadAscii(16);

        var padding = Size - HeaderSize;
        br.ReadBytes(padding);
    }

    public override string ToString()
    {
	    return $"DX9 {Dx9GameRev} DX11 {Dx11GameRev}, FFXIV {FfxivGameVer} EX1 {Ex1GameVer} EX2 {Ex2GameVer} EX3 {Ex3GameVer} EX4 {Ex4GameVer} Plugin {PluginVersion}";
    }
}