using System.Net;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Result of SRV resolution for a Game Master instance.
/// </summary>
public record ResolvedGameMaster
{
    /// <summary>HTTP endpoint for Lobby→GM API calls (e.g., /create-race).</summary>
    public required Uri HttpEndpoint { get; init; }

    /// <summary>ENet UDP address clients use to connect for races.</summary>
    public required IpAndPort GameAddress { get; init; }

    /// <summary>Domain name this GM was resolved from.</summary>
    public required string Domain { get; init; }

    /// <summary>Whether this GM is currently considered healthy.</summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>Number of consecutive failed requests.</summary>
    public int ConsecutiveFailures { get; set; }
}

/// <summary>
/// Discovers Game Masters via DNS SRV records and selects one per race.
/// 
/// Configured via GAME_MASTER_DOMAINS env var (comma-separated domain names).
/// Each domain is queried with SRV record _nfmw-game._udp.{domain}.
/// The SRV response provides the UDP game port; HTTP is on standard port 80/443
/// on the same target host.
/// 
/// SRV records are re-resolved periodically (default 60s) for DNS-level changes.
/// </summary>
public class GameMasterRegistry : IDisposable
{
    private readonly List<ResolvedGameMaster> _masters = new();
    private readonly IReadOnlyDictionary<string, (string Host, ushort? Port)>? _publicAddresses;
    private readonly int _maxConsecutiveFailures;
    private readonly Timer _refreshTimer;
    private readonly object _lock = new();
    private int _roundRobinIndex;

    /// <param name="publicAddresses">
    /// Where players reach a game master, keyed by the domain the lobby reaches it at. Absent,
    /// players are told the same address the lobby uses.
    /// </param>
    public GameMasterRegistry(
        IEnumerable<string> domains,
        int maxConsecutiveFailures = 3,
        int refreshIntervalSeconds = 60,
        IReadOnlyDictionary<string, (string Host, ushort? Port)>? publicAddresses = null
    )
    {
        _maxConsecutiveFailures = maxConsecutiveFailures;
        _publicAddresses = publicAddresses;

        foreach (var domain in domains)
            ResolveAndAdd(domain.Trim());

        if (_masters.Count == 0)
            throw new InvalidOperationException(
                "No Game Masters could be resolved. Check GAME_MASTER_DOMAINS configuration."
            );

        _refreshTimer = new Timer(
            _ => RefreshAll(),
            null,
            TimeSpan.FromSeconds(refreshIntervalSeconds),
            TimeSpan.FromSeconds(refreshIntervalSeconds)
        );
    }

    /// <summary>Creates a registry from the GAME_MASTER_DOMAINS env var.</summary>
    public static GameMasterRegistry FromEnvironment(
        int maxConsecutiveFailures = 3,
        int refreshIntervalSeconds = 60
    )
    {
        var domainsStr = Environment.GetEnvironmentVariable("GAME_MASTER_DOMAINS")
                         ?? "localhost";

        var domains = domainsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

        // Where the lobby reaches a game master is rarely where players reach it: behind a
        // tunnel or on a private network those are different names. Players are told the
        // public one, which is resolved to an address here because that is what goes on the
        // wire - a player who got the name back would still have to resolve it themselves.
        var publicAddress = Environment.GetEnvironmentVariable("GM_PUBLIC_ADDRESS");
        var map = string.IsNullOrWhiteSpace(publicAddress)
            ? null
            : ParseAddressMap(publicAddress);

        return new GameMasterRegistry(domains, maxConsecutiveFailures, refreshIntervalSeconds, map);
    }

    /// <summary>
    /// Parses <c>privateHost=publicHost[:port][,privateHost=publicHost...]</c>. The port is the
    /// game's UDP port and defaults to the private side's.
    /// </summary>
    public static Dictionary<string, (string Host, ushort? Port)> ParseAddressMap(string value)
    {
        var map = new Dictionary<string, (string Host, ushort? Port)>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = entry.IndexOf('=');
            if (split <= 0)
                continue;

            var from = entry[..split].Trim();
            var to = entry[(split + 1)..].Trim();
            if (from.Length == 0 || to.Length == 0)
                continue;

            // Only split a port off when the tail is numeric, so an IPv6 literal survives.
            ushort? port = null;
            var colon = to.LastIndexOf(':');
            if (colon > 0 && ushort.TryParse(to[(colon + 1)..], out var parsedPort))
            {
                port = parsedPort;
                to = to[..colon];
            }

            map[from] = (to, port);
        }

        return map;
    }

    /// <summary>Selects a healthy Game Master using round-robin.</summary>
    public ResolvedGameMaster SelectGameMaster()
    {
        lock (_lock)
        {
            var healthy = _masters.Where(m => m.IsHealthy).ToList();
            if (healthy.Count == 0)
                throw new InvalidOperationException("No healthy Game Masters available.");

            _roundRobinIndex = (_roundRobinIndex + 1) % healthy.Count;
            return healthy[_roundRobinIndex];
        }
    }

    /// <summary>Marks a GM as failed (increments failure counter, marks unhealthy if threshold reached).</summary>
    public void MarkFailure(ResolvedGameMaster master)
    {
        lock (_lock)
        {
            master.ConsecutiveFailures++;
            if (master.ConsecutiveFailures >= _maxConsecutiveFailures)
                master.IsHealthy = false;
        }
    }

    /// <summary>Marks a GM as healthy (resets failure counter).</summary>
    public void MarkSuccess(ResolvedGameMaster master)
    {
        lock (_lock)
        {
            master.ConsecutiveFailures = 0;
            master.IsHealthy = true;
        }
    }

    private void ResolveAndAdd(string domain)
    {
        var gm = ResolveDomain(domain);
        if (gm is not null)
        {
            lock (_lock)
            {
                _masters.Add(gm);
            }
        }
    }

    private void RefreshAll()
    {
        // Snapshot the domains, resolve without the lock (a slow DNS lookup would
        // otherwise block every SelectGameMaster caller for its duration), then apply.
        string[] domains;
        bool anyUnhealthy;
        lock (_lock)
        {
            domains = _masters.Select(m => m.Domain).ToArray();
            anyUnhealthy = _masters.Any(m => !m.IsHealthy);
        }

        var resolved = new List<(string Domain, ResolvedGameMaster Gm)>(domains.Length);
        foreach (var domain in domains)
        {
            if (ResolveDomain(domain) is { } updated)
                resolved.Add((domain, updated));
        }

        lock (_lock)
        {
            foreach (var (domain, updated) in resolved)
            {
                for (var i = 0; i < _masters.Count; i++)
                {
                    if (!string.Equals(_masters[i].Domain, domain, StringComparison.Ordinal))
                        continue;

                    // Preserve health state
                    updated.IsHealthy = _masters[i].IsHealthy;
                    updated.ConsecutiveFailures = _masters[i].ConsecutiveFailures;
                    _masters[i] = updated;
                    break;
                }
            }
        }

        // A GM that failed three races is taken out of rotation, but a successful race is the
        // only thing that used to restore it - and it cannot race while excluded. Without this
        // it stays dead for the process lifetime. Probe outside the lock.
        if (anyUnhealthy)
            _ = ProbeUnhealthyAsync();
    }

    /// <summary>
    /// Asks every excluded Game Master whether it is back. A reachable one is returned to
    /// rotation; one that is still down stays out until the next probe.
    /// </summary>
    private async Task ProbeUnhealthyAsync()
    {
        ResolvedGameMaster[] unhealthy;
        lock (_lock)
            unhealthy = _masters.Where(m => !m.IsHealthy).ToArray();

        foreach (var master in unhealthy)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                using var response = await http.GetAsync(new Uri(master.HttpEndpoint, "/health"));
                if (!response.IsSuccessStatusCode)
                    continue;

                lock (_lock)
                {
                    master.ConsecutiveFailures = 0;
                    master.IsHealthy = true;
                }

                Console.WriteLine($"[GameMasterRegistry] {master.Domain} is healthy again");
            }
            catch
            {
                // Still down; next refresh probes again.
            }
        }
    }

    private ResolvedGameMaster? ResolveDomain(string domain)
    {
        // Dev mode: host:udpPort+httpPort (e.g. localhost:7000+7002)
        var plusIdx = domain.IndexOf('+');
        if (plusIdx > 0)
        {
            return ResolveDevEntry(domain, plusIdx);
        }

        // Production: SRV lookup via _nfmw-game._udp.{domain}
        return ResolveSrvDomain(domain);
    }

    private ResolvedGameMaster? ResolveDevEntry(string entry, int plusIdx)
    {
        try
        {
            var httpPortStr = entry[(plusIdx + 1)..];
            var hostAndUdp = entry[..plusIdx];

            var colonIdx = hostAndUdp.LastIndexOf(':');
            var host = colonIdx > 0 ? hostAndUdp[..colonIdx] : hostAndUdp;
            var udpPort = colonIdx > 0 ? ushort.Parse(hostAndUdp[(colonIdx + 1)..]) : (ushort)7000;
            var httpPort = ushort.Parse(httpPortStr);

            // The lobby may reach the game master somewhere players cannot - a tunnel, a
            // private network - so players are given the public name resolved here instead.
            var publicHost = host;
            var publicPort = udpPort;
            if (_publicAddresses is not null && _publicAddresses.TryGetValue(host, out var mapped))
            {
                publicHost = mapped.Host;
                publicPort = mapped.Port ?? udpPort;
            }

            var addresses = HostName(publicHost);
            if (addresses.Length == 0) return null;
            var ip = addresses[0];

            Console.WriteLine(
                $"[GameMasterRegistry] GM: {host} UDP:{udpPort} HTTP:{httpPort}, "
                + $"players reach it at {publicHost}:{publicPort} ({ip})"
            );

            return new ResolvedGameMaster
            {
                // Use hostname, not resolved IP — HttpListener matches by host prefix
                HttpEndpoint = new Uri($"http://{host}:{httpPort}"),
                GameAddress = new IpAndPort(
                    new CompactIpAddress(ip.GetAddressBytes()),
                    publicPort
                ),
                Domain = entry
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameMasterRegistry] Failed to parse '{entry}': {ex.Message}");
            return null;
        }
    }

    private static IPAddress[] HostName(string host)
        => host is "localhost" ? [IPAddress.Loopback] : Dns.GetHostAddresses(host);

    private ResolvedGameMaster? ResolveSrvDomain(string domain)
    {
        try
        {
            // The domain resolves to whichever name the lobby can reach; players are given
            // the mapped public one when there is one.
            var publicHost = domain;
            var publicPort = (ushort)7000; // TODO: use actual SRV port when DnsClient is added
            if (_publicAddresses is not null && _publicAddresses.TryGetValue(domain, out var mapped))
            {
                publicHost = mapped.Host;
                publicPort = mapped.Port ?? publicPort;
            }

            var addresses = HostName(publicHost);
            if (addresses.Length == 0) return null;

            var ip = addresses[0];

            return new ResolvedGameMaster
            {
                HttpEndpoint = new Uri($"https://{domain}:443"),
                GameAddress = new IpAndPort(
                    new CompactIpAddress(ip.GetAddressBytes()),
                    publicPort
                ),
                Domain = domain
            };
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _refreshTimer.Dispose();
    }
}
