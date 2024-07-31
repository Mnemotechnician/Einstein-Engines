using System.Linq;
using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared.CrewManifest;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.CartridgeLoader.Cartridges;

[GenerateTypedNameReferences]
public sealed partial class NanoMessageUiFragment : BoxContainer
{
    private Dictionary<ulong, string> _userNames = new();

    public NanoMessageUiFragment()
    {
        RobustXamlLoader.Load(this);

        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;

        AddRecipientButton.OnPressed += _ => AddRecipient();
        ChatSendButton.OnPressed += _ => SendMessage();

    }

    public void UpdateState(NanoMessageUiState state)
    {
        RecipientsContainer.RemoveAllChildren();
        ChatContainer.RemoveAllChildren();
        _userNames.Clear();

        foreach (var user in state.KnownRecipients)
        {
            if (user.Name is {} name)
                _userNames[user.Id] = name;
        }

        ChatHeaderLabel.SetMessage(state.ConnectedServerLabel is {} label
            ? Loc.GetString("nano-message-server-header", ("server", label))
            : Loc.GetString("nano-message-no-server-header"));

        // Recipients
        var index = 0;
        foreach (var recipient in state.KnownRecipients)
        {
            var recipientName = _userNames.TryGetValue(recipient.Id, out var name)
                ? name
                : Loc.GetString("nano-message-unknown-user-short");
            var entry = new NanoMessageEntryRecipient(++index, recipient, recipientName);
            RecipientsContainer.AddChild(entry);

            if (state.OpenedConversation is { } convo && (convo.User1 == recipient.Id || convo.User2 == recipient.Id))
                entry.Root.Disabled = true;
            else
                entry.Root.OnPressed += _ => PickRecipient(recipient.Id);
        }

        // Chat messages
        if (state.OpenedConversation is { } conv)
        {
            var user1Name = ResolveUserName(conv.User1);
            var user2Name = ResolveUserName(conv.User2);

            foreach (var message in conv.Messages)
            {
                var name = message.Sender == conv.User1 ? user1Name : user2Name;
                var entry = new NanoMessageEntryMessage(message, name);
                ChatContainer.AddChild(entry);
            }
        }
    }

    private string ResolveUserName(ulong id)
    {
        return _userNames.TryGetValue(id, out var name)
            ? Loc.GetString("nano-message-user", ("name", name), ("id", id))
            : Loc.GetString("nano-message-unknown-user", ("id", id));
    }

    private void PickRecipient(ulong id)
    {
        // TODO
    }

    private void AddRecipient()
    {
        // TODO
    }

    private void SendMessage()
    {
        // TODO
    }

    private void RefreshServer()
    {
        // TODO
    }
}

