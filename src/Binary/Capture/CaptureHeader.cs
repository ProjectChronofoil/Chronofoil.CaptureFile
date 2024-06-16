namespace Chronofoil.CaptureFile.Binary.Capture;

public class CaptureHeader {
    public int Size;
    public Guid CaptureId;
    public ulong CaptureTime;

    public CaptureHeader(BinaryReader br) {
        Size = br.ReadInt32();
        CaptureId = new Guid(br.ReadBytes(16));
        CaptureTime = br.ReadUInt64();
    }
}