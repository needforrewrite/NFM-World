using ENet;

namespace NFMWorld.Library.Test;

/// <summary>
/// Exercises the managed ENet port over real loopback sockets: handshake, reliable delivery,
/// fragmentation and teardown. The transports above it are thin wrappers, so this is the layer
/// that has to stay wire-correct.
/// </summary>
[TestClass]
public class ManagedEnetTests
{
    private static (Host Host, Address Address) StartServer()
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var port = (ushort)Random.Shared.Next(20000, 60000);
            var address = new Address { Port = port };
            var host = new Host();

            try
            {
                host.Create(address, 8);
                return (host, address);
            }
            catch (Exception)
            {
                host.Dispose();
            }
        }

        throw new InvalidOperationException("Could not bind a loopback port for the ENet test.");
    }

    private static void Pump(Host host, int milliseconds, List<Event> events)
    {
        var deadline = Environment.TickCount64 + milliseconds;

        do
        {
            if (host.Service(0, out var ev) > 0)
                events.Add(ev);

            while (host.CheckEvents(out ev) > 0)
                events.Add(ev);

            if (milliseconds > 0)
                Thread.Sleep(1);
        } while (Environment.TickCount64 < deadline);
    }

    private static bool WaitFor(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;

        while (Environment.TickCount64 < deadline)
        {
            if (condition())
                return true;

            Thread.Sleep(1);
        }

        return condition();
    }

    [TestMethod]
    public void ClientAndServer_HandshakeSendAndDisconnect()
    {
        var (server, serverAddress) = StartServer();

        using var serverHost = server;
        using var client = new Host();
        client.Create();

        var serverEvents = new List<Event>();
        var clientEvents = new List<Event>();
        var received = new List<byte[]>();
        var serverConnected = false;

        var connectionAddress = new Address();
        Assert.IsTrue(connectionAddress.SetHost("127.0.0.1"));
        connectionAddress.Port = serverAddress.Port;

        var peer = client.Connect(connectionAddress);

        Assert.IsTrue(
            WaitFor(() =>
                {
                    Pump(client, 1, clientEvents);
                    Pump(server, 1, serverEvents);

                    foreach (var ev in serverEvents)
                    {
                        if (ev.Type == EventType.Connect)
                            serverConnected = true;

                        if (ev.Type == EventType.Receive)
                            received.Add(ev.Packet.Data.ToArray());
                    }

                    serverEvents.Clear();
                    return peer.State == PeerState.Connected && serverConnected;
                }
            ),
            "handshake did not complete"
        );

        var payload = new byte[3000];
        Random.Shared.NextBytes(payload);

        var packet = new Packet();
        packet.Create(payload, PacketFlags.Reliable);
        Assert.IsTrue(peer.Send(0, ref packet));

        Assert.IsTrue(
            WaitFor(() =>
                {
                    Pump(client, 1, clientEvents);
                    Pump(server, 1, serverEvents);

                    foreach (var ev in serverEvents)
                        if (ev.Type == EventType.Receive)
                            received.Add(ev.Packet.Data.ToArray());

                    serverEvents.Clear();
                    return received.Count > 0;
                }
            ),
            "server did not receive the fragmented packet"
        );

        CollectionAssert.AreEqual(payload, received[0]);

        peer.DisconnectNow(0);

        Assert.IsTrue(
            WaitFor(() =>
                {
                    Pump(client, 1, clientEvents);
                    Pump(server, 1, serverEvents);

                    return peer.State == PeerState.Disconnected;
                }
            ),
            "peer did not reset after DisconnectNow"
        );
    }
}
