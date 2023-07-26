using SKitLs.Bots.Telegram.Stateful.Model;
using SKitLs.Bots.Telegram.Stateful.Prototype;
using WeatherBot.Model;

namespace WeatherBot.Users
{
    internal class BotUser : IStatefulUser
    {
        public List<GeoCoderInfo> Favs { get; } = new();

        public IUserState State { get; set; }
        public long TelegramId { get; set; }

        public BotUser(long telegramId)
        {
            State = new DefaultUserState();
            TelegramId = telegramId;
        }

        public void ResetState() => State = new DefaultUserState();
    }
}