using Chronofoil.CaptureFile.Generated;

namespace Chronofoil.CaptureFile.Censor;

public record CensorTarget
{
    public KnownCensoredOpcode Descriptor { get; init; }
    
    public Protocol Protocol { get; init; }
    public Direction Direction { get; init; }
    public int Opcode { get; init; }
}

