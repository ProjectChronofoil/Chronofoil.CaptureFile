using System.Diagnostics;
using System.Runtime.CompilerServices;
using Chronofoil.Capture.IO;
using Chronofoil.CaptureFile.Binary;
using Chronofoil.CaptureFile.Binary.Packet;
using Chronofoil.CaptureFile.Generated;
using Google.Protobuf;
using Direction = Chronofoil.CaptureFile.Generated.Direction;

namespace Chronofoil.CaptureFile.Censor;

public class CaptureRedactor
{
    private readonly string _path;
    private Dictionary<Protocol, Dictionary<Direction, HashSet<int>>> _targets;
    private SimpleBuffer _buffer;

    public CaptureRedactor(string path, List<CensorTarget> targets)
    {
        if (!File.Exists(path)) throw new FileNotFoundException();
        if (targets.Count == 0) throw new ArgumentException("Must have at least one censor target.");
        
        _path = path;
        _targets = new Dictionary<Protocol, Dictionary<Direction, HashSet<int>>>();
        foreach (var target in targets)
        {
            if (!_targets.TryGetValue(target.Protocol, out var protoDict))
            {
                protoDict = new Dictionary<Direction, HashSet<int>>();
                _targets.Add(target.Protocol, protoDict);
            }

            if (!protoDict.TryGetValue(target.Direction, out var set))
            {
                protoDict[target.Direction] = [];
            }

            _targets[target.Protocol][target.Direction].Add(target.Opcode);
        }
    }

    public FileInfo Censor()
    {
        var reader = new CaptureReader(_path);
        var writer = new CaptureWriter(_path, isCensored: true);
        _buffer = new SimpleBuffer(1024 * 32);

        // The writer API was not designed for this but it works fine...
        var existingInfo = reader.CaptureInfo;
        writer.WriteVersionInfo(reader.VersionInfo);
        writer.WriteCaptureStart(Guid.Parse(existingInfo.CaptureId), existingInfo.CaptureStartTime.ToDateTime());

        int i = 0;
        var oldFrames = reader.GetFrames().ToList();
        foreach (var frame in oldFrames)
        {
            var censoredFrameData = CensorFrame(frame);
            if (censoredFrameData.Length != frame.Frame.Length)
                throw new Exception("Censor failed; censored frame was shorter than provided frame.");
            frame.Frame = ByteString.CopyFrom(censoredFrameData);
            writer.AppendCaptureFrame(frame.Header.Protocol, frame.Header.Direction, frame.Frame.Span);
            i++;
        }

        writer.WriteCaptureEnd(existingInfo.CaptureEndTime.ToDateTime(), existingInfo.IsDirty, true);

        var newFile = new FileInfo(_path.Replace(".cfcap", ".ccfcap"));
        var censorReader = new CaptureReader(newFile.FullName);

        var censorFrames = censorReader.GetFrames().ToList();
        if (censorFrames.Count != oldFrames.Count)
            throw new Exception("Censored capture did not contain the same number of frames as the original capture.");
        
        foreach (var framePair in oldFrames.Zip(censorFrames))
        {
            var frame1 = framePair.First;
            var frame2 = framePair.Second;
            if (frame1.Header.Protocol != frame2.Header.Protocol)
                throw new Exception("Frame protocol did not match after censoring!");
            if (frame1.Header.Direction != frame2.Header.Direction)
                throw new Exception("Frame direction did not match after censoring!");
            if (frame1.Frame.Length != frame2.Frame.Length)
                throw new Exception("Frame length did not match after censoring!");

            var frameHeaderSize = Unsafe.SizeOf<FrameHeader>();
            var frameHeader1 = frame1.Frame.Span[..frameHeaderSize]; 
            var frameHeader2 = frame2.Frame.Span[..frameHeaderSize]; 
            if (!frameHeader1.SequenceEqual(frameHeader2))
                throw new Exception("Frame header did not match after censoring!");

            // TODO: do research on this check, it might be possible that the packet is blank anyways? idk
            // var restOfFrame1 = frame1.Frame.Span[frameHeaderSize..];
            // var restOfFrame2 = frame2.Frame.Span[frameHeaderSize..];
            // if (restOfFrame1.SequenceEqual(restOfFrame2))
                // throw new Exception("Frame data somehow matched after censoring!");
        }

        return newFile;
    }

    private ReadOnlySpan<byte> CensorFrame(CaptureFrame frame)
    {
        _buffer.Clear();
        var framePtr = frame.Frame.Span;
        
        // Get the frame header
        var headerSize = Unsafe.SizeOf<FrameHeader>();
        var headerSpan = framePtr[..headerSize];
        _buffer.Write(headerSpan);
        
        // Get the data of the frame
        var header = headerSpan.CastTo<FrameHeader>();
        var frameSpan = framePtr[..(int)header.TotalSize];
        var data = frameSpan.Slice(headerSize, (int)header.TotalSize - headerSize);
        
        var offset = 0;
        for (int i = 0; i < header.Count; i++)
        {
            // Get this packet's PacketElementHeader. It tells us the size
	        var pktHdrSize = Unsafe.SizeOf<PacketElementHeader>();
            var pktHdrSlice = data.Slice(offset, pktHdrSize);
            _buffer.Write(pktHdrSlice);
            var pktHdr = pktHdrSlice.CastTo<PacketElementHeader>();
            
            // This span contains all packet data, excluding the element header, including the IPC header
            var pktData = data.Slice(offset + pktHdrSize, (int)pktHdr.Size - pktHdrSize);

            // We don't censor non-IPC packets
            if (pktHdr.Type != PacketType.Ipc)
            {
                _buffer.Write(pktData);
            }
            else
            {
                // Get the IPC header
                var ipcHdrSize = Unsafe.SizeOf<PacketIpcHeader>();
                var ipcHdrSlice = pktData[..ipcHdrSize];
                var ipcHdr = ipcHdrSlice.CastTo<PacketIpcHeader>();
                
                // Console.WriteLine($"Read {frame.Header.Protocol} {frame.Header.Direction} opcode {ipcHdr.Type}");

                var isCensorableLobby = frame.Header.Protocol == Protocol.Lobby && ipcHdr is { Type: 5 or 15 };
                
                // We censor all chat IPC
                var isCensorableChat = frame.Header.Protocol == Protocol.Chat;
                
                // Censor all opcodes provided
                var isCensorableZone = _targets.TryGetValue(frame.Header.Protocol, out var dirDict)
                                       && dirDict.TryGetValue(frame.Header.Direction, out var opcodes)
                                       && opcodes.Contains(ipcHdr.Type);
                
                if (isCensorableChat || isCensorableZone)
                {
                    // Console.WriteLine($"Censoring {frame.Header.Protocol} {frame.Header.Direction} opcode {ipcHdr.Type}");
                    _buffer.Write(ipcHdrSlice);
                    
                    // Censor by replacing relevant packet bytes with null
                    var bytesLeft = pktHdr.Size - pktHdrSize - ipcHdrSize;
                    _buffer.WriteNull((int)bytesLeft);
                }
                else if (isCensorableLobby)
                {
                    var startPos = 0;
                    var endPos = 0;
                    if (ipcHdr.Type == 5)
                    {
                        startPos = ipcHdrSize + 18;     // Session ID starts 18 bytes in
                        endPos = ipcHdrSize + 18 + 64;  // 64 bytes long    
                    }
                    else if (ipcHdr.Type == 15)
                    {
                        startPos = ipcHdrSize + 28;     // Thingy starts 28 bytes in
                        endPos = ipcHdrSize + 28 + 32;  // 32 bytes long (I think, that's all that's filled in
                    }
                    
                    _buffer.Write(pktData[..startPos]);
                    _buffer.WriteNull(endPos - startPos);
                    _buffer.Write(pktData[endPos..]);
                }
                else
                {
                    _buffer.Write(pktData);
                }
            }
            
            offset += (int)pktHdr.Size;
        }

        return _buffer.GetBuffer();
    }
}