using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Fax;

[Serializable, NetSerializable]
public sealed class AdminFaxEuiState : EuiStateBase
{
    public List<AdminFaxEntry> Entries { get; }
    public List<AdminFaxInboxEntry> InboxEntries { get; }

    public AdminFaxEuiState(List<AdminFaxEntry> entries, List<AdminFaxInboxEntry> inboxEntries)
    {
        Entries = entries;
        InboxEntries = inboxEntries;
    }
}

[Serializable, NetSerializable]
public sealed class AdminFaxEntry
{
    public NetEntity Uid { get; }
    public string Name { get; }
    public string Address { get; }

    public AdminFaxEntry(NetEntity uid, string name, string address)
    {
        Uid = uid;
        Name = name;
        Address = address;
    }
}

[Serializable, NetSerializable]
public sealed class AdminFaxInboxEntry
{
    public string SourceFaxName { get; }
    public string DestinationFaxName { get; }
    public string SenderName { get; }
    public string PaperTitle { get; }
    public string Content { get; }

    public AdminFaxInboxEntry(string sourceFaxName, string destinationFaxName, string senderName, string paperTitle, string content)
    {
        SourceFaxName = sourceFaxName;
        DestinationFaxName = destinationFaxName;
        SenderName = senderName;
        PaperTitle = paperTitle;
        Content = content;
    }
}

public static class AdminFaxEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class Close : EuiMessageBase
    {
    }

    [Serializable, NetSerializable]
    public sealed class Follow : EuiMessageBase
    {
        public NetEntity TargetFax { get; }

        public Follow(NetEntity targetFax)
        {
            TargetFax = targetFax;
        }
    }

    [Serializable, NetSerializable]
    public sealed class Send : EuiMessageBase
    {
        public NetEntity Target { get; }
        public string Title { get; }
        public string From { get; }
        public string Content { get; }
        public string StampState { get; }
        public Color StampColor { get; }

        public Send(NetEntity target, string title, string from, string content, string stamp, Color stampColor)
        {
            Target = target;
            Title = title;
            From = from;
            Content = content;
            StampState = stamp;
            StampColor = stampColor;
        }
    }
}
