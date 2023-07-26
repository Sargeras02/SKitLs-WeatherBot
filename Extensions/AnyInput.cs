using SKitLs.Bots.Telegram.Core.Model;
using SKitLs.Bots.Telegram.Core.Model.Interactions.Defaults;
using SKitLs.Bots.Telegram.Core.Model.UpdatesCasting.Signed;

namespace WeatherBot.Extensions
{
    internal class AnyInput : DefaultBotAction<SignedMessageTextUpdate>
    {
        public AnyInput(string anyId, BotInteraction<SignedMessageTextUpdate> action) : base("systemAny." + anyId, action)
        { }

        public override bool ShouldBeExecutedOn(SignedMessageTextUpdate update) => true;
    }
}