using SKitLs.Bots.Telegram.AdvancedMessages.Model.Menus;
using SKitLs.Bots.Telegram.AdvancedMessages.Prototype;
using SKitLs.Bots.Telegram.ArgedInteractions.Interactions.Model;
using SKitLs.Bots.Telegram.Core.Model.UpdatesCasting;
using SKitLs.Bots.Telegram.PageNavs;
using SKitLs.Bots.Telegram.PageNavs.Prototype;
using WeatherBot.Users;

namespace WeatherBot.Model
{
    internal class SavedFavoriteMenu : IPageMenu
    {
        public BotArgedCallback<GeoCoderInfo> OpenCallback { get; private set; }

        public SavedFavoriteMenu(BotArgedCallback<GeoCoderInfo> openCallback)
        {
            OpenCallback = openCallback;
        }

        public IMesMenu Build(IBotPage? previous, IBotPage owner, ISignedUpdate update)
        {
            IMenuManager mm = update.Owner.ResolveService<IMenuManager>();

            var res = new PairedInlineMenu(update.Owner);

            if (update.Sender is BotUser user)
            {
                foreach (var favorite in user.Favs)
                {
                    res.Add(favorite.Name, OpenCallback, favorite);
                }
            }

            if (previous is not null)
            {
                res.Add("Назад", mm.BackCallback, singleLine: true);
            }

            return res;
        }
    }
}