using Chronofoil.CaptureFile.Binary.Packet;

namespace Chronofoil.CaptureFile.Binary.Capture;

public class CaptureFrameHeader {
    public PacketProtocol Protocol;
    public Direction Direction;
    
    public CaptureFrameHeader(BinaryReader br) {
        Protocol = (PacketProtocol) br.ReadByte();
        Direction = (Direction) br.ReadByte();
    }
}