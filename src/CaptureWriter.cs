using System.IO.Compression;
using Chronofoil.CaptureFile.Generated;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using ZstdSharp;

namespace Chronofoil.CaptureFile;

public class CaptureWriter
{
    private readonly string _path;
    private readonly CaptureFrame _frame;
    private Stream? _frameStream;
    private readonly string _extension;

    private long _frameCount;

    private FileInfo VersionInfoPath => new(Path.Combine(_path, "VersionInfo"));
    private FileInfo CaptureInfoPath => new(Path.Combine(_path, "CaptureInfo"));
    private FileInfo DataPath => new(Path.Combine(_path, "Data"));

    private FileInfo OutputFile => new($"{_path}{_extension}");
    private DirectoryInfo OutputPath => new(_path);

    public CaptureWriter(string path, bool isCensored = false)
    {
        _path = path.Replace(".cfcap", "");
        _extension = isCensored ? ".ccfcap" : ".cfcap";
        
        _frame = new CaptureFrame { Header = new CaptureFrameHeader() };

        if (OutputPath.Exists)
        {
            throw new IOException("Capture file exists for provided path");
        }
        
        OutputPath.Create();
    }
    
    public void WriteVersionInfo(VersionInfo versionInfo)
    {
        using var vis = VersionInfoPath.OpenWrite();
        versionInfo.WriteDelimitedTo(vis);
    }
    
    public void WriteCaptureStart(Guid guid, DateTime startTime)
    {
        // Create a Capture Info to store the guid and start time in
        var tmpCaptureInfo = new CaptureInfo
        {
            CaptureId = guid.ToString(),
            CaptureStartTime = Timestamp.FromDateTime(startTime),
            CaptureEndTime = Timestamp.FromDateTime(DateTime.UnixEpoch),
            IsCensored = false,
            IsDirty = true
        };
        
        using var infoOut = CaptureInfoPath.OpenWrite();
        tmpCaptureInfo.WriteDelimitedTo(infoOut);

        // Open the frame stream
        _frameStream = DataPath.OpenWrite();
    }

    public void WriteCaptureEnd(DateTime endTime, bool dirty = false, bool censored = false)
    {
        // End the frame stream
        _frameStream?.Flush();
        _frameStream?.Close();
            
        // Read in, update, and rewrite the Capture Info file
        {
            CaptureInfo tmpCaptureInfo;
            using (var infoIn = CaptureInfoPath.OpenRead())
            {
                tmpCaptureInfo = CaptureInfo.Parser.ParseDelimitedFrom(infoIn);
                tmpCaptureInfo.CaptureEndTime = Timestamp.FromDateTime(endTime);
                tmpCaptureInfo.IsDirty = dirty;
                tmpCaptureInfo.IsCensored = censored;
            }
            
            using (var infoOut = CaptureInfoPath.OpenWrite())
                tmpCaptureInfo.WriteDelimitedTo(infoOut);
        }
        
        // Create the zip and copy the already serialized files into it
        var capOut = OutputFile.OpenWrite();
        var archive = new ZipArchive(capOut, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(VersionInfoPath.FullName, "VersionInfo", CompressionLevel.NoCompression);
        archive.CreateEntryFromFile(CaptureInfoPath.FullName, "CaptureInfo", CompressionLevel.NoCompression);

        // Copy the Data file into the archive using a ZStd compression stream
        // Note that ZStd level 8 was chosen for a combination of size, compression speed, and decompression speed
        var dataEntry = archive.CreateEntry("Data", CompressionLevel.NoCompression);
        {
            using var dataIn = DataPath.OpenRead();
            using var dataOut = new CompressionStream(dataEntry.Open(), 8, 0, false);
            dataIn.CopyTo(dataOut);
        }

        archive.Dispose();
        OutputPath.Delete(true);
    }
    
    public void AppendCaptureFrame(Protocol protocol, Direction direction, ReadOnlySpan<byte> data)
    {
        _frame.Header.Protocol = protocol;
        _frame.Header.Direction = direction;
        _frame.Frame = ByteString.CopyFrom(data);
        _frame.WriteDelimitedTo(_frameStream);
        _frameCount++;
        
        // Flush only occasionally
        // Note: this is not restroom advice
        if (_frameCount % 10 == 0)
            _frameStream!.Flush();
    }
}