using NFMWorld.DriverInterface;
using NFMWorld.Gameplay.RaceHost;
using NFMWorld.Reactor;
using NFMWorld.UI;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorldLibrary.Backend.Gamemodes;

namespace NFMWorld.Gameplay;

public partial class RacePhase
{
    private readonly RaceChatState _chat = new();
    private Key? _chatOpeningKey;

    internal bool HandleChatKey(Key key)
    {
        if (_host is not NetworkRaceHost || IsPaused || _finish.ShowSummary.Value) return false;
        if (_chat.Open.Value)
        {
            if (key == Key.Escape)
            {
                CloseChat();
                return true;
            }

            if (key == Key.Enter)
            {
                SendChat(_chat.Draft.Value);
                return true;
            }

            return false;
        }

        if (key == Key.None || key != SettingsMenu.Bindings.OpenChat || FocusManager.FocusedNode is TextInput or TextArea)
            return false;
        _input.Reset(ClientCar?.Control);
        _chat.Open.Value = true;
        _chatOpeningKey = key;
        return true;
    }

    internal bool ConsumeChatOpeningText()
    {
        if (_chatOpeningKey is null || !_chat.Open.Value) return false;
        _chatOpeningKey = null;
        return true;
    }

    private void SendChat(string message)
    {
        if (_host is NetworkRaceHost network) network.SendChat(message);
        _chat.Draft.Value = "";
        CloseChat();
    }

    private void CloseChat()
    {
        if (!_chat.Open.Value) return;
        _chat.Open.Value = false;
        _chatOpeningKey = null;
        FocusManager.ClearFocus();
        _input.Reset(ClientCar?.Control);
    }

    private void ReceiveChat(S2C_RaceChat message)
    {
        if (!_raceDisposed)
            _chat.Receive(
                message.Sender,
                message.Message,
                new Color((byte)message.Color.R, (byte)message.Color.G, (byte)message.Color.B),
                message.MessageId
            );
    }

    private void ReceivePlayerLeft(S2C_PlayerLeft departure)
    {
        if (_raceDisposed || GamemodeInstance is not BaseClientGamemode gamemode) return;
        foreach (var player in gamemode.Players)
            if (player.Info.Id == departure.PlayerId)
                player.Car?.MarkDeparted(departure.Reason, departure.Announce);
    }
}
