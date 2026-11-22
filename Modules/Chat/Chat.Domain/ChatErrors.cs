using Shared.Kernel.Results;

namespace Chat.Domain;

public static class ChatErrors
{
    public static readonly Error ConversationNotFound =
        Error.NotFound("CONVERSATION_NOT_FOUND", "Nie znaleziono rozmowy.");

    public static readonly Error MessageNotFound =
        Error.NotFound("MESSAGE_NOT_FOUND", "Nie znaleziono wiadomości w tej rozmowie.");

    public static readonly Error StudioNotAvailable =
        Error.NotFound("CHAT_STUDIO_NOT_AVAILABLE", "Z tym studiem nie można teraz rozpocząć rozmowy.");

    public static readonly Error ProfileRequired = Error.Validation(
        "CHAT_PROFILE_REQUIRED", "Uzupełnij swoje dane kontaktowe, zanim napiszesz do studia.");

    public static readonly Error NoContact = Error.NotFound(
        "CHAT_NO_CONTACT", "Rozmowę można rozpocząć tylko z klientem, który wysłał prośbę o rezerwację.");

    public static readonly Error OwnStudio =
        Error.Validation("CHAT_OWN_STUDIO", "Nie możesz pisać do studia, w którym pracujesz.");

    public static readonly Error MessageEmpty = Error.Validation("MESSAGE_EMPTY", "Wiadomość nie może być pusta.");

    public static readonly Error MessageTooLong = Error.Validation(
        "MESSAGE_TOO_LONG", $"Wiadomość może mieć najwyżej {Conversation.MaxBodyLength} znaków.");
}
