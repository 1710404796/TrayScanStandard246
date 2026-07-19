using CommunityToolkit.Mvvm.Messaging.Messages;
using TrayScanStandard.Data.Models;

namespace TrayScanStandard.Messages;

public class PalletLogCreatedMessage(PalletLog log) : ValueChangedMessage<PalletLog>(log);
