namespace Chronofoil.CaptureFile.Binary.Packet;

public class Frame {
    public FrameHeader Header;
    public Packet[] Packets;

    public Frame(ReadOnlySpan<byte> data)
    {
        // var headerSize = Marshal.SizeOf<FrameHeader>();
        // var headerBytes = data[0..headerSize].ToArray();
        // Header = new FrameHeader(headerBytes);
        // data = data[headerSize..];
        //
        // Packets = new Packet[Header.Count];
        // for (int i = 0; i < Packets.Length; i++)
        // {
        //     Packets[i] = new Packet(data);
        //     data = data[(int)Packets[i].Header.Size..];
        // }
    }
    
    public Frame(BinaryReader br) {
        Header = new FrameHeader(br);
        Packets = new Packet[Header.Count];
        for (int i = 0; i < Packets.Length; i++)
            Packets[i] = new Packet(br);
    }
}