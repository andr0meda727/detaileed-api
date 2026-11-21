using Chat.Domain;

namespace Unit.Modules.Chat;

[TestFixture]
public class ConversationTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Staff = Guid.NewGuid();

    private static Conversation Open() => Conversation.Open(Guid.NewGuid(), " Detail Studio ", Anna, " Anna Nowak ", Now);

    private static Message Post(Conversation conversation, ChatSide side, string body, DateTime at) =>
        conversation.Post(side, side == ChatSide.Customer ? Anna : Staff, body, Guid.NewGuid(), at).Value!;

    [Test]
    public void Open_CopiesTrimmedNames_AndHasNothingYet()
    {
        var conversation = Open();

        Assert.Multiple(() =>
        {
            Assert.That(conversation.StudioName, Is.EqualTo("Detail Studio"));
            Assert.That(conversation.CustomerName, Is.EqualTo("Anna Nowak"));
            Assert.That(conversation.StudioNameAsOf, Is.EqualTo(Now));
            Assert.That(conversation.LastMessageAt, Is.Null);
            Assert.That(conversation.CustomerReadUpTo, Is.Null);
            Assert.That(conversation.StudioReadUpTo, Is.Null);
        });
    }

    [Test]
    public void Open_NeedsBothParties()
    {
        Assert.That(() => Conversation.Open(Guid.Empty, "S", Anna, "A", Now), Throws.ArgumentException);
        Assert.That(() => Conversation.Open(Guid.NewGuid(), "S", Guid.Empty, "A", Now), Throws.ArgumentException);
        Assert.That(() => Conversation.Open(Guid.NewGuid(), " ", Anna, "A", Now), Throws.ArgumentException);
    }

    [Test]
    public void Post_StoresTheTrimmedMessageInTheConversationsStudio()
    {
        var conversation = Open();
        var clientId = Guid.NewGuid();

        var result = conversation.Post(ChatSide.Customer, Anna, "  Dzień dobry!  ", clientId, Now);

        Assert.That(result.Success, Is.True);
        var message = result.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(message.Body, Is.EqualTo("Dzień dobry!"));
            Assert.That(message.ConversationId, Is.EqualTo(conversation.Id));
            Assert.That(message.StudioId, Is.EqualTo(conversation.StudioId));
            Assert.That(message.Side, Is.EqualTo(ChatSide.Customer));
            Assert.That(message.SenderUserId, Is.EqualTo(Anna));
            Assert.That(message.ClientMessageId, Is.EqualTo(clientId));
            Assert.That(message.SentAt, Is.EqualTo(Now));
            Assert.That(message.Id.Version, Is.EqualTo(7));
            Assert.That(conversation.LastMessageAt, Is.EqualTo(Now));
            Assert.That(conversation.LastMessagePreview, Is.EqualTo("Dzień dobry!"));
        });
    }

    [Test]
    public void Post_CountsAsReadForTheSender_NotForTheOtherSide()
    {
        var conversation = Open();

        Post(conversation, ChatSide.Customer, "Hej", Now);

        Assert.That(conversation.CustomerReadUpTo, Is.EqualTo(Now));
        Assert.That(conversation.StudioReadUpTo, Is.Null);
    }

    [TestCase("")]
    [TestCase("   \n ")]
    public void Post_RejectsAnEmptyMessage(string body)
    {
        var conversation = Open();

        var result = conversation.Post(ChatSide.Studio, Staff, body, Guid.NewGuid(), Now);

        Assert.That(result.ErrorCode, Is.EqualTo("MESSAGE_EMPTY"));
        Assert.That(conversation.LastMessageAt, Is.Null);
    }

    [Test]
    public void Post_AcceptsTheLongestMessage_AndRejectsOneCharacterMore()
    {
        var conversation = Open();
        var longest = new string('a', Conversation.MaxBodyLength);

        Assert.That(conversation.Post(ChatSide.Studio, Staff, longest, Guid.NewGuid(), Now).Success, Is.True);
        Assert.That(conversation.Post(ChatSide.Studio, Staff, longest + "a", Guid.NewGuid(), Now).ErrorCode,
            Is.EqualTo("MESSAGE_TOO_LONG"));
    }

    [Test]
    public void Post_PreviewIsOneLine_CutWithAnEllipsis()
    {
        var conversation = Open();

        Post(conversation, ChatSide.Studio, "Termin:\r\n\r\n  jutro  \n10:00", Now);
        Assert.That(conversation.LastMessagePreview, Is.EqualTo("Termin: jutro 10:00"));

        Post(conversation, ChatSide.Studio, new string('x', 300), Now.AddMinutes(1));
        Assert.That(conversation.LastMessagePreview, Has.Length.EqualTo(Conversation.PreviewLength));
        Assert.That(conversation.LastMessagePreview, Does.EndWith("…"));
    }

    [Test]
    public void Post_DropsSubMicroseconds_SoTheTimeSurvivesPostgres()
    {
        var conversation = Open();

        var message = Post(conversation, ChatSide.Customer, "Hej", Now.AddTicks(1234567));

        Assert.That(message.SentAt, Is.EqualTo(Now.AddTicks(1234560)));
        Assert.That(message.SentAt.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    [Test]
    public void Post_NeverDatesAMessageBeforeThePreviousOne()
    {
        var conversation = Open();
        Post(conversation, ChatSide.Customer, "Pierwsza", Now);

        // The server's clock stepped back
        var second = Post(conversation, ChatSide.Studio, "Druga", Now.AddSeconds(-5));

        Assert.That(second.SentAt, Is.EqualTo(Now));
        Assert.That(conversation.LastMessageAt, Is.EqualTo(Now));
    }

    [Test]
    public void MarkRead_OnlyMovesForward()
    {
        var conversation = Open();

        Assert.That(conversation.MarkRead(ChatSide.Studio, Now), Is.True);
        Assert.That(conversation.MarkRead(ChatSide.Studio, Now), Is.False);
        Assert.That(conversation.MarkRead(ChatSide.Studio, Now.AddMinutes(-1)), Is.False);
        Assert.That(conversation.StudioReadUpTo, Is.EqualTo(Now));

        Assert.That(conversation.MarkRead(ChatSide.Studio, Now.AddMinutes(1)), Is.True);
        Assert.That(conversation.StudioReadUpTo, Is.EqualTo(Now.AddMinutes(1)));
    }

    [Test]
    public void MarkRead_KeepsEachSideApart()
    {
        var conversation = Open();

        conversation.MarkRead(ChatSide.Customer, Now);

        Assert.That(conversation.ReadUpTo(ChatSide.Customer), Is.EqualTo(Now));
        Assert.That(conversation.ReadUpTo(ChatSide.Studio), Is.Null);
    }

    [Test]
    public void RenameStudio_TakesNewerNames_AndIgnoresOlderOrRepeatedOnes()
    {
        var conversation = Open();

        Assert.That(conversation.RenameStudio(" Nowa Nazwa ", Now.AddHours(1)), Is.True);
        Assert.That(conversation.RenameStudio("Nowa Nazwa", Now.AddHours(1)), Is.False);
        Assert.That(conversation.RenameStudio("Stara Nazwa", Now.AddMinutes(30)), Is.False);

        Assert.That(conversation.StudioName, Is.EqualTo("Nowa Nazwa"));
        Assert.That(conversation.StudioNameAsOf, Is.EqualTo(Now.AddHours(1)));
    }
}
