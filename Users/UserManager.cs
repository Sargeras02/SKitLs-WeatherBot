using SKitLs.Bots.Telegram.Core.Model;
using SKitLs.Bots.Telegram.Core.Model.UpdatesCasting;
using SKitLs.Bots.Telegram.Core.Prototypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeatherBot.Users
{
    internal class UserManager : IUsersManager
    {
        public UserDataChanged? SignedEventHandled => null;

        public List<BotUser> Users { get; } = new();

        public async Task<IBotUser?> GetUserById(long telegramId) => await Task.FromResult(Users.Find(x => x.TelegramId ==  telegramId));

        public async Task<bool> IsUserRegistered(long telegramId) => await GetUserById(telegramId) is not null;

        public async Task<IBotUser?> RegisterNewUser(ICastedUpdate update)
        {
            var user = ChatScanner.GetSender(update.OriginalSource, this)!;
            var @new = new BotUser(user.Id);
            Users.Add(@new);
            return await Task.FromResult(@new);
        }
    }
}