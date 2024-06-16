namespace Chronofoil.CaptureFile.Binary.Capture;

public class RawCaptureFrame {
    public CaptureFrameHeader CaptureHeader;
    public byte[] Data;
    
    public RawCaptureFrame(BinaryReader br) {
        CaptureHeader = new CaptureFrameHeader(br);
        var currentPosition = br.BaseStream.Position;
        br.BaseStream.Seek(24, SeekOrigin.Current);
        var frameSize = br.ReadUInt32();
        br.BaseStream.Seek(currentPosition, SeekOrigin.Begin);
        Data = br.ReadBytes((int)frameSize);
        if (Data.Length != frameSize)
            throw new IOException("Failed to read full frame.");
    }
}