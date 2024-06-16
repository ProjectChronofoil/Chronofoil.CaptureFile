using Chronofoil.CaptureFile.Binary.Capture;
using Chronofoil.CaptureFile.Binary.Packet;
using Chronofoil.CaptureFile.Generated;
using Google.Protobuf;
using Direction = Chronofoil.CaptureFile.Binary.Packet.Direction;

namespace Chronofoil.CaptureFile;

public class CaptureConverter
{
    private readonly string _path;

    private FileInfo RawCaptureFile => new(_path);
    private FileInfo OutFile => new(_path.Replace(".cfcap", ""));
    
    // given a single old capture file, produce 1..n captures based on number of detected sessions
    public CaptureConverter(string path)
    {
        throw new NotImplementedException();
        
        _path = path;
        if (!RawCaptureFile.Exists)
            throw new FileNotFoundException();
        
        if (OutFile.Exists)
            OutFile.Delete();
    }

    public void Convert()
    {
        var reader = new RawCaptureReader(RawCaptureFile.OpenRead());
        var writer = new CaptureWriter(OutFile.FullName);

        writer.WriteVersionInfo(MakeVersionInfo(reader.FileHeader));

        var start = DateTimeOffset.FromUnixTimeMilliseconds((long)reader.CaptureHeader.CaptureTime).UtcDateTime;
        writer.WriteCaptureStart(reader.CaptureHeader.CaptureId, start);

        var rawFrames = reader.GetRawFrames().ToList();
        
        foreach (var frame in rawFrames)
        {
            var newDirection = frame.CaptureHeader.Direction == Direction.Tx ? Generated.Direction.Tx : Generated.Direction.Rx;
            var newProtocol = frame.CaptureHeader.Protocol switch
            {
                PacketProtocol.None => Protocol.None,
                PacketProtocol.Zone => Protocol.Zone,
                PacketProtocol.Chat => Protocol.Chat,
                PacketProtocol.Lobby => Protocol.Lobby,
                _ => throw new ArgumentOutOfRangeException()
            };
            
            writer.AppendCaptureFrame(newProtocol, newDirection, frame.Data.AsSpan());
        }

        long endTimeLong = 0;
        if (reader.IsFinalized)
            endTimeLong = (long)reader.CaptureFooter.CaptureEndTime;
        else
            endTimeLong = rawFrames.Select(f => BitConverter.ToInt64(f.Data.AsSpan()[16..24])).Last(t => t > 100000);
        var endTime = DateTimeOffset.FromUnixTimeMilliseconds(endTimeLong).UtcDateTime;

        writer.WriteCaptureEnd(endTime, dirty: true, censored: false);
    }

    private VersionInfo MakeVersionInfo(ReadablePersistentCaptureData data)
    {
        var ret = new VersionInfo();
        ret.CaptureVersion = data.CaptureVersion;
        ret.Dx9Hash = ByteString.CopyFrom(data.Dx9Hash);
        ret.Dx11Hash = ByteString.CopyFrom(data.Dx9Hash);

        if (data.Dx9GameRev != uint.MaxValue)
        {
            ret.Dx9Revision = (long)data.Dx9GameRev;
            ret.Dx9Hash = ByteString.CopyFrom(data.Dx9Hash);
        }
        
        ret.Dx11Revision = (long)data.Dx11GameRev;
        ret.Dx11Hash = ByteString.CopyFrom(data.Dx11Hash);
        
        ret.GameVer.Add(data.FfxivGameVer);
        ret.GameVer.Add(data.Ex1GameVer);
        ret.GameVer.Add(data.Ex2GameVer);
        ret.GameVer.Add(data.Ex3GameVer);
        ret.GameVer.Add(data.Ex4GameVer);
        
        ret.WriterIdentifier = "Chronofoil";
        ret.WriterVersion = "1.0.0.0";
        return ret;
    }
}