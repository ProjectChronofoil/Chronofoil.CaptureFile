using Chronofoil.CaptureFile.Generated;

namespace Chronofoil.CaptureFile.Censor;

public enum KnownCensoredOpcode
{
    ZoneChatUp,
    ZoneLetterUp,
    ZoneChatDown,
    ZoneLetterListDown,
    ZoneLetterDown,
}

public static class CensorRegistry
{
    public static Dictionary<KnownCensoredOpcode, CensorTarget> CensorTargets { get; private set; }

    static CensorRegistry()
    {
        var targets = new List<CensorTarget>
        {
            new() { Descriptor = KnownCensoredOpcode.ZoneChatUp, Protocol = Protocol.Zone, Direction = Direction.Tx },
            new() { Descriptor = KnownCensoredOpcode.ZoneLetterUp, Protocol = Protocol.Zone, Direction = Direction.Tx },
            new() { Descriptor = KnownCensoredOpcode.ZoneChatDown, Protocol = Protocol.Zone, Direction = Direction.Rx },
            new() { Descriptor = KnownCensoredOpcode.ZoneLetterListDown, Protocol = Protocol.Zone, Direction = Direction.Rx },
            new() { Descriptor = KnownCensoredOpcode.ZoneLetterDown, Protocol = Protocol.Zone, Direction = Direction.Rx }
        };
        CensorTargets = targets.ToDictionary(t => t.Descriptor, t => t);
    }

    public static CensorTarget GetCensorTarget(KnownCensoredOpcode opcode)
    {
        return CensorTargets[opcode];
    }
}