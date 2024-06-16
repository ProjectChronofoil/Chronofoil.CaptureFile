using Chronofoil.CaptureFile.Binary;
using Chronofoil.CaptureFile.Binary.Capture;

namespace Chronofoil.CaptureFile;

public class RawCaptureReader
{
    public ReadablePersistentCaptureData FileHeader { get; private set; }
    public CaptureHeader CaptureHeader { get; private set; }
    public CaptureFooter CaptureFooter { get; private set; }

    public bool IsFinalized => CaptureFooter.Sentinel == -1;

    private readonly BinaryReader _br;

    public Stream BaseStream => _br.BaseStream;

    public RawCaptureReader(string path) : this(new BufferedStream(File.OpenRead(path))) { }

    public RawCaptureReader(Stream stream) : this(new BinaryReader(stream)) { }

    public RawCaptureReader(BinaryReader br)
    {
        FileHeader = new ReadablePersistentCaptureData(br);
        CaptureHeader = new CaptureHeader(br);
        _br = br;
        ReadFooter();
    }

    private void ReadFooter()
    {
        var pos = _br.BaseStream.Position;
        _br.BaseStream.Seek(-32, SeekOrigin.End);
        CaptureFooter = new CaptureFooter(_br);
        _br.BaseStream.Position = pos;
    }

    public IEnumerable<RawCaptureFrame?> GetRawFrames()
    {
        _br.BaseStream.Position = 254 + 28;
        
        while (!_br.IsAtEnd() && _br.PeekInt16() != -1)
        {
            yield return new RawCaptureFrame(_br);
        }
    }
}