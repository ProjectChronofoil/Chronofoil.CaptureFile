using Chronofoil.CaptureFile.Binary.Packet;

namespace Chronofoil.CaptureFile.Binary.Capture;

public class CaptureFrame {
    public CaptureFrameHeader CaptureHeader;
    public FrameHeader Header;
    public Packet.Packet[] Packets;
    
    public CaptureFrame(BinaryReader br) {
        CaptureHeader = new CaptureFrameHeader(br);
        Header = new FrameHeader(br);
        Packets = new Packet.Packet[Header.Count];
        for (int i = 0; i < Packets.Length; i++)
            Packets[i] = new Packet.Packet(br);
    }
}