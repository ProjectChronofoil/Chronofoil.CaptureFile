using System.IO.Compression;
using Chronofoil.CaptureFile.Generated;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using ZstdSharp;

namespace Chronofoil.CaptureFile;

public class CaptureReader
{
    public VersionInfo VersionInfo { get; private set; }
    public CaptureInfo CaptureInfo { get; private set; }
    
    private ZipArchive _archive;
    private string _path;
    private bool _isPacked;
    
    private FileInfo VersionInfoPath => new(Path.Combine(_path, "VersionInfo"));
    private FileInfo CaptureInfoPath => new(Path.Combine(_path, "CaptureInfo"));
    private FileInfo DataPath => new(Path.Combine(_path, "Data"));

    public CaptureReader(string path)
    { 
        _path = path;
        _isPacked = (path.EndsWith(".cfcap") || path.EndsWith(".ccfcap")) && File.Exists(path);
        FinalizeCapture();
        Init();
    }

    private void Init()
    {
        if (_isPacked) _archive = new ZipArchive(File.OpenRead(_path));
        ReadCaptureFileHeader();
        ReadCaptureInfo();
        // ReadCaptureHeader();
        // ReadCaptureFooter();
    }

    private void FinalizeCapture()
    {
        if (_isPacked) return;
        
        // Fix frames
        var validFrames = GetValidFrames().ToList();
        var newDataPath = new FileInfo(DataPath.FullName + "2");
        using (var newDataStream = newDataPath.OpenWrite())
            foreach (var frame in validFrames)
                frame.WriteDelimitedTo(newDataStream);
        DataPath.Delete();
        newDataPath.MoveTo(DataPath.FullName);
        
        // Fix end time
        DateTime? endTime = null; //Timestamp.FromDateTime(DateTime.UnixEpoch);
        using var infoStream = CaptureInfoPath.OpenRead();
        try
        {
            var tmpInfo = CaptureInfo.Parser.ParseDelimitedFrom(infoStream);
            endTime = tmpInfo.CaptureEndTime.ToDateTime();
        }
        catch (Exception e)
        {
            // failed to read for whatever reason
        }

        if (endTime == null)
        {
            // TODO: defend against the timestamp position changing, i guess. that would be annoying
            var endTimeLong = GetFrames().Select(f => BitConverter.ToInt64(f.Frame.Span[16..24])).Last(t => t > 100000);
            endTime = DateTimeOffset.FromUnixTimeMilliseconds(endTimeLong).UtcDateTime;
        }
        
        // Write dirty because we had to finalize this ourselves
        new CaptureWriter(_path).WriteCaptureEnd(endTime.Value, true);
        _path = $"{_path}.cfcap";
        _isPacked = true;
    }

    private void ReadCaptureFileHeader()
    {
        using var os = _isPacked ? _archive.GetEntry("VersionInfo").Open() : VersionInfoPath.OpenRead();
        VersionInfo = VersionInfo.Parser.ParseDelimitedFrom(os);
    }
    
    private void ReadCaptureInfo()
    {
        using var os = _isPacked ? _archive.GetEntry("CaptureInfo").Open() : CaptureInfoPath.OpenRead();
        CaptureInfo = CaptureInfo.Parser.ParseDelimitedFrom(os);
    }
    
    private IEnumerable<CaptureFrame> GetValidFrames()
    {
        // Only called if we're unpacked, so just immediately try the file
        using var stream = DataPath.OpenRead();
        
        while (stream.Position != stream.Length)
        {
            CaptureFrame? frame = null;
            try
            {
                frame = CaptureFrame.Parser.ParseDelimitedFrom(stream);
            }
            catch
            {
                // ignored
            }

            if (frame == null)
                break;
            
            yield return CaptureFrame.Parser.ParseDelimitedFrom(stream);
        }    
    }

    public IEnumerable<CaptureFrame> GetFrames()
    {
        using var baseStream = _isPacked ? _archive.GetEntry("Data").Open() : DataPath.OpenRead();
        using var stream = _isPacked ? new DecompressionStream(baseStream, 10240) : baseStream;
        
        while (true)
        {
            CaptureFrame? frame = null;
            try
            {
                frame = CaptureFrame.Parser.ParseDelimitedFrom(stream);
            }
            catch (InvalidProtocolBufferException e)
            {
                // End of stream
            }

            if (frame == null)
                break;
            yield return frame;
        }
    }
}