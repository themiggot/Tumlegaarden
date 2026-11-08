using System.Buffers.Binary;
using System.Net;

namespace Tumlegaarden.Web.Security;

/// <summary>
/// Hurtigt opslag i en liste af IP-intervaller (IPv4 og IPv6). IPv4 gemmes som IPv4-mappede IPv6-adresser,
/// så begge typer ligger i samme sorterede tabel og slås op med binær søgning.
/// </summary>
public sealed class CountryIpFilter
{
    private readonly UInt128[] _starts;
    private readonly UInt128[] _ends;

    private CountryIpFilter(UInt128[] starts, UInt128[] ends) { _starts = starts; _ends = ends; }

    public int RangeCount => _starts.Length;

    public static CountryIpFilter Empty { get; } = new([], []);

    /// <summary>Indlæser linjer på formen "start,slut". Linjer der starter med # ignoreres.</summary>
    public static CountryIpFilter Load(string path) => Parse(File.ReadLines(path));

    public static CountryIpFilter Parse(IEnumerable<string> lines)
    {
        var ranges = new List<(UInt128 Start, UInt128 End)>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split(',');
            if (parts.Length < 2 || !IPAddress.TryParse(parts[0], out var a) || !IPAddress.TryParse(parts[1], out var b)) continue;
            var (start, end) = (ToNumber(a), ToNumber(b));
            if (start <= end) ranges.Add((start, end));
        }

        ranges.Sort((x, y) => x.Start.CompareTo(y.Start));

        // Flet overlappende og tilstødende intervaller.
        var starts = new List<UInt128>(ranges.Count);
        var ends = new List<UInt128>(ranges.Count);
        foreach (var (s, e) in ranges)
        {
            if (ends.Count > 0 && (s <= ends[^1] || s == ends[^1] + 1))
            {
                if (e > ends[^1]) ends[^1] = e;
            }
            else
            {
                starts.Add(s);
                ends.Add(e);
            }
        }
        return new CountryIpFilter([.. starts], [.. ends]);
    }

    public bool Contains(IPAddress ip)
    {
        var value = ToNumber(ip);
        int lo = 0, hi = _starts.Length - 1, found = -1;
        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (_starts[mid] <= value) { found = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return found >= 0 && value <= _ends[found];
    }

    private static UInt128 ToNumber(IPAddress ip)
    {
        Span<byte> bytes = stackalloc byte[16];
        ip.MapToIPv6().TryWriteBytes(bytes, out _);
        return new UInt128(BinaryPrimitives.ReadUInt64BigEndian(bytes[..8]), BinaryPrimitives.ReadUInt64BigEndian(bytes[8..]));
    }
}
