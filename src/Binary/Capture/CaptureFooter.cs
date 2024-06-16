namespace Chronofoil.CaptureFile.Binary.Capture;

public class CaptureFooter
{
    public short Sentinel; // -1
    public int Size;
    public ulong CaptureEndTime;

    public CaptureFooter(BinaryReader br)
    {
        Sentinel = br.ReadInt16();
        Size = br.ReadInt32();
        CaptureEndTime = br.ReadUInt64();
        br.BaseStream.Seek(18, SeekOrigin.Current);
    }
}