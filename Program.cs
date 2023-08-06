using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SKitLs.Bots.Telegram.AdvancedMessages.AdvancedDelivery;
using SKitLs.Bots.Telegram.AdvancedMessages.Model;
using SKitLs.Bots.Telegram.AdvancedMessages.Model.Menus;
using SKitLs.Bots.Telegram.AdvancedMessages.Model.Messages;
using SKitLs.Bots.Telegram.AdvancedMessages.Model.Messages.Text;
using SKitLs.Bots.Telegram.AdvancedMessages.Prototype;
using SKitLs.Bots.Telegram.ArgedInteractions.Argumentation;
using SKitLs.Bots.Telegram.ArgedInteractions.Interactions.Model;
using SKitLs.Bots.Telegram.BotProcesses.Model;
using SKitLs.Bots.Telegram.BotProcesses.Model.Defaults;
using SKitLs.Bots.Telegram.BotProcesses.Model.Defaults.Processes.Util;
using SKitLs.Bots.Telegram.BotProcesses.Prototype;
using SKitLs.Bots.Telegram.Core.Model.Building;
using SKitLs.Bots.Telegram.Core.Model.Interactions;
using SKitLs.Bots.Telegram.Core.Model.Interactions.Defaults;
using SKitLs.Bots.Telegram.Core.Model.Management.Defaults;
using SKitLs.Bots.Telegram.Core.Model.UpdateHandlers.Defaults;
using SKitLs.Bots.Telegram.Core.Model.UpdatesCasting;
using SKitLs.Bots.Telegram.Core.Model.UpdatesCasting.Signed;
using SKitLs.Bots.Telegram.DataBases;
using SKitLs.Bots.Telegram.DataBases.Model;
using SKitLs.Bots.Telegram.DataBases.Model.Args;
using SKitLs.Bots.Telegram.DataBases.Model.Datasets;
using SKitLs.Bots.Telegram.PageNavs;
using SKitLs.Bots.Telegram.PageNavs.Model;
using SKitLs.Bots.Telegram.Stateful.Model;
using SKitLs.Bots.Telegram.Stateful.Prototype;
using SKitLs.Utils.Localizations.Prototype;
using System.Globalization;
using System.Net.Sockets;
using Telegram.Bot;
using WeatherBot.Extensions;
using WeatherBot.Model;
using WeatherBot.Users;

namespace WeatherBot
{
    internal class Program
    {
        private static readonly bool RequestApi = true;
        private static readonly string GeocoderApiKey = "geoKey";
        private static readonly string WeatherApiKey = "weatherKey";

        private static readonly string BotApiKey = "botKey";

        public static DefaultUserState DefaultState = new(0, "default");
        public static DefaultUserState InputCityState = new(10, "typing");

        static async Task Main(string[] args)
        {
            BotBuilder.DebugSettings.DebugLanguage = LangKey.RU;
            BotBuilder.DebugSettings.UpdateLocalsPath("resources/locals");

            var dataManager = GetDataManager();
            var mm = GetMenuManager(dataManager);

            var privateMessages = new DefaultSignedMessageUpdateHandler();
            var statefulInputs = new DefaultStatefulManager<SignedMessageTextUpdate>();
            var privateTexts = new DefaultSignedMessageTextUpdateHandler
            {
                CommandsManager = new DefaultActionManager<SignedMessageTextUpdate>(),
                TextInputManager = statefulInputs,
            };
            privateTexts.CommandsManager.AddSafely(StartCommand);

            var inputStateSection = new DefaultStateSection<SignedMessageTextUpdate>();
            inputStateSection.EnableState(InputCityState);
            inputStateSection.AddSafely(ExitInput);
            inputStateSection.AddSafely(InputCity);
            statefulInputs.AddSectionSafely(inputStateSection);
            
            privateMessages.TextMessageUpdateHandler = privateTexts;

            var statefulCallbacks = new DefaultStatefulManager<SignedCallbackUpdate>();
            var privateCallbacks = new DefaultCallbackHandler()
            {
                CallbackManager = statefulCallbacks,
            };
            privateCallbacks.CallbackManager.AddSafely(StartSearching);
            privateCallbacks.CallbackManager.AddSafely(FollowGeocode);
            //privateCallbacks.CallbackManager.AddSafely(UnfollowGeocode);
            privateCallbacks.CallbackManager.AddSafely(OpenFollow);
            privateCallbacks.CallbackManager.AddSafely(LoadWeather);
            mm.ApplyTo(privateCallbacks.CallbackManager);

            ChatDesigner privates = ChatDesigner.NewDesigner()
                .UseUsersManager(new UserManager())
                .UseMessageHandler(privateMessages)
                .UseCallbackHandler(privateCallbacks);

            var bot = BotBuilder.NewBuilder(BotApiKey)
                .EnablePrivates(privates)
                .AddService<IArgsSerializeService>(new DefaultArgsSerializeService())
                .AddService<IMenuManager>(mm)
                .AddService<IProcessManager>(new DefaultProcessManager())
                .AddService<IDataManager>(dataManager)
                .CustomDelivery(new AdvancedDeliverySystem())
                .Build();

            dataManager.ApplyTo(statefulInputs);
            dataManager.ApplyTo(statefulCallbacks);
            bot.Settings.BotLanguage = LangKey.RU;

            await bot.Listen();
        }

        #region Setup
        private static IDataManager GetDataManager()
        {
            var dm = new DefaultDataManager("Избранное [DM]");

            var favsId = "favs";
            var favorites = new UserContextDataSet<GeoCoderInfo>(favsId, LoadFromJson<GeoCoderInfo>(favsId).Result, dsLabel: "Города");
            favorites.Properties.AllowAdd = false;
            favorites.Properties.AllowEdit = false;
            favorites.UpdateProcess(RemoveWithUnfollow, DbActionType.Remove);
            favorites.AddAction(LoadWeather);
            favorites.ObjectAdded += (i, u) => SaveDataToJson(favorites.GetAll(), favsId);
            favorites.ObjectUpdated += (i, u) => SaveDataToJson(favorites.GetAll(), favsId);
            favorites.ObjectRemoved += (i, u) => SaveDataToJson(favorites.GetAll(), favsId);
            dm.AddAsync(favorites);

            return dm;
        }
        private static IMenuManager GetMenuManager(IDataManager dm)
        {
            var mm = new DefaultMenuManager();

            var mainBody = new OutputMessageText("Добро пожаловать!\n\nЧего желаете?");
            var mainMenu = new PageNavMenu();
            var mainPage = new StaticPage("main", "Главная", mainBody, mainMenu);

            var savedBody = new DynamicMessage(GetSavedList);
            var savedPage = new WidgetPage("saved", "Избранное", savedBody, new SavedFavoriteMenu(OpenFollow));

            mainMenu.PathTo(savedPage);
            mainMenu.PathTo(dm.GetRootPage());
            mainMenu.AddAction(StartSearching);

            mm.Define(mainPage);
            mm.Define(savedPage);

            dm.ApplyTo(mm);
            return mm;
        }
        private static IOutputMessage GetSavedList(ISignedUpdate? update)
        {
            var message = "Избранное:\n\n";
            if (update is not null && update.Sender is BotUser user)
            {
                var favs = user.GetFavorites(update);
                if (favs.Count == 0) message += "Ничего нет";
                foreach (var favorite in favs)
                {
                    message += $"- {favorite.Name}\n";
                }
            }
            return new OutputMessageText(message);
        }
        #endregion

        #region Methods
        private static async Task<string> GetWeatherInfo(GeoCoderInfo geo) => await GetWeatherInfo(geo.Latitude, geo.Longitude);
        private static async Task<string> GetWeatherInfo(double latitude, double longitude)
        {
            var resultMessage = string.Empty;

            var weatherApiUrl = $"https://api.weather.yandex.ru/v2/informers?" +
                    $"lat={latitude.ToString().Replace(',', '.')}&lon={longitude.ToString().Replace(',', '.')}";
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Yandex-API-Key", WeatherApiKey);

                HttpResponseMessage response = await client.GetAsync(weatherApiUrl);
                string content = await response.Content.ReadAsStringAsync();

                JObject jsonObject = JObject.Parse(content);
                JObject factObject = jsonObject["fact"] as JObject;

                // Получение значений полей
                double temp = (double)factObject["temp"];
                resultMessage += $"Температура: {temp} °C\n";
                double feelsLike = (double)factObject["feels_like"];
                resultMessage += $"Ощущается как: {feelsLike} °C\n";
                //string icon = (string)factObject["icon"];
                double windSpeed = (double)factObject["wind_speed"];
                resultMessage += $"Скорость ветра: {windSpeed} м/с\n";
                int pressureMm = (int)factObject["pressure_mm"];
                resultMessage += $"Давление: {pressureMm} мм рт.ст.\n";
                int humidity = (int)factObject["humidity"];
                resultMessage += $"Влажность: {humidity}%\n";

                string daytime = (string)factObject["daytime"];
                bool polar = (bool)factObject["polar"];
                string season = (string)factObject["season"];
                long obsTime = (long)factObject["obs_time"];
            }

            return resultMessage;
        }
        #endregion

        #region Commands
        private static DefaultCommand StartCommand => new("start", Do_StartAsync);
        private static async Task Do_StartAsync(SignedMessageTextUpdate update)
        {
            var mm = update.Owner.ResolveService<IMenuManager>();

            // Получаем определённую по id страницу
            // new StaticPage("main", "Главная", mainBody, mainMenu);
            var page = mm.GetDefined("main");

            await mm.PushPageAsync(page, update, true);
        }
        #endregion

        #region Callbacks
        private static DefaultCallback StartSearching => new("startSearch", "Найти", Do_SearchAsync);
        private static async Task Do_SearchAsync(SignedCallbackUpdate update)
        {
            if (update.Sender is IStatefulUser stateful)
            {
                stateful.State = InputCityState;

                var message = new OutputMessageText($"Введите город или \"{ExitInput.ActionNameBase}\"")
                {
                    Menu = new ReplyMenu(ExitInput.ActionNameBase),
                };
                await update.Owner.DeliveryService.ReplyToSender(new EditWrapper(message, update.TriggerMessageId), update);
            }
        }
        private static BotArgedCallback<GeoCoderInfo> FollowGeocode => new(new LabeledData("В избранное", "FollowGeocode"), Do_FollowGeocodeAsync);
        private static async Task Do_FollowGeocodeAsync(GeoCoderInfo args, SignedCallbackUpdate update)
        {
            if (update.Sender is BotUser user)
            {
                //user.Favs.Add(args);
                var geoCodes = update.Owner.ResolveService<IDataManager>().GetSet<GeoCoderInfo>();
                var code = geoCodes.Find(x => x.Longitude ==  args.Longitude && x.Latitude == args.Latitude);
                if (code is null)
                {
                    code = args;
                    await geoCodes.AddAsync(code, update);
                }
                code.Owners.Add(update.Sender.TelegramId);

                await update.Owner.Bot.EditMessageReplyMarkupAsync(update.ChatId, update.TriggerMessageId, null);
                await update.Owner.Bot.AnswerCallbackQueryAsync(update.Callback.Id, "Город сохранён в избранное!", showAlert: false);
            }
        }
        
        //private static BotArgedCallback<GeoCoderInfo> UnfollowGeocode => new(new LabeledData("Удалить", "UnfollowGeocode"), Do_UnfollowGeocodeAsync);
        private static TextInputsProcessBase<GeoCoderInfo> RemoveWithUnfollow => new TerminatorProcess<GeoCoderInfo>(IST.Dynamic(), Do_UnfollowGeocodeAsync);
        private static async Task Do_UnfollowGeocodeAsync(TextInputsArguments<GeoCoderInfo> args, SignedCallbackUpdate update)
        {
            //if (update.Sender is BotUser user)
            //{
            var geoCodes = update.Owner.ResolveService<IDataManager>().GetSet<GeoCoderInfo>();

            if (args.CompleteStatus == ProcessCompleteStatus.Success)
            {
                //user.Favs.RemoveAt(args.Value);
                var code = geoCodes.Find(x => x.Longitude == args.BuildingInstance.Longitude && x.Latitude == args.BuildingInstance.Latitude);
                if (code is not null)
                {
                    code.Owners.Remove(update.Sender.TelegramId);
                    await geoCodes.UpdateAsync(code, update);
                }
            }

            var resultText = geoCodes.ResolveStatus(args.CompleteStatus, DbActionType.Remove);
            var menu = new PairedInlineMenu();
            menu.Add("Выйти", update.Owner.ResolveService<IMenuManager>().BackCallback);
            var message = new OutputMessageText(update.Message.Text + $"\n\n{resultText}")
            {
                Menu = menu,
            };
            await update.Owner.DeliveryService.ReplyToSender(new EditWrapper(message, update.TriggerMessageId), update);
            //}
        }
        private static BotArgedCallback<GeoCoderInfo> OpenFollow => new(new LabeledData("{ Открыть }", "OpenFollow"), Do_OpenFollowAsync);
        private static async Task Do_OpenFollowAsync(GeoCoderInfo args, SignedCallbackUpdate update)
        {
            if (update.Sender is BotUser user)
            {
                var menu = new PairedInlineMenu(update.Owner);
                //menu.Add(UnfollowGeocode, args);
                //menu.Add(LoadWeather, args);
                menu.Add("Назад", update.Owner.ResolveService<IMenuManager>().BackCallback);
                var message = new OutputMessageText(args.GetDisplay())
                {
                    Menu = menu,
                };
                await update.Owner.DeliveryService.ReplyToSender(new EditWrapper(message, update.TriggerMessageId), update);
            }
        }
        private static BotArgedCallback<DtoArg<GeoCoderInfo>> LoadWeather => new(new LabeledData("😮‍💨 Узнать погоду", "LoadWeather"), Do_LoadWeatherAsync);
        private static async Task Do_LoadWeatherAsync(DtoArg<GeoCoderInfo> args, SignedCallbackUpdate update)
        {
            //if (update.Sender is BotUser user)
            //{
            var dm = update.Owner.ResolveService<IDataManager>();
            var menu = new PairedInlineMenu(update.Owner);
            menu.Add(dm.RemoveExistingCallback, new ObjInfoArg(dm.GetSet<GeoCoderInfo>(), args.DataId));
            menu.Add("Назад", update.Owner.ResolveService<IMenuManager>().BackCallback);
            var message = new OutputMessageText(update.Message.Text + "\n\n" + await GetWeatherInfo(args.GetValue()))
            {
                Menu = menu,
            };
            await update.Owner.DeliveryService.ReplyToSender(new EditWrapper(message, update.TriggerMessageId), update);
            //}
        }
        #endregion

        private static DefaultTextInput ExitInput => new("Выйти", Do_ExitInputCityAsync);
        private static async Task Do_ExitInputCityAsync(SignedMessageTextUpdate update)
        {
            if (update.Sender is IStatefulUser stateful)
            {
                stateful.State = DefaultState;

                var message = new OutputMessageText($"Вы больше ничего не вводите.")
                {
                    Menu = new ReplyCleaner(),
                };
                await update.Owner.DeliveryService.ReplyToSender(message, update);
            }
        }

        private static AnyInput InputCity => new("city", Do_InputCityAsync);
        private static async Task Do_InputCityAsync(SignedMessageTextUpdate update)
        {
            string cityName = update.Text;
            var geocoderApiUrl = $"https://geocode-maps.yandex.ru/1.x/?apikey={GeocoderApiKey}&geocode={cityName}&format=json";
            double longitude = double.MaxValue;
            double latitude = double.MaxValue;
            if (RequestApi)
            {
                using (var httpClient = new HttpClient())
                {
                    string responseString = await httpClient.GetStringAsync(geocoderApiUrl);
                    Console.WriteLine(responseString[..100]);
                    var response = JsonConvert.DeserializeObject<GeoCodeWrap>(responseString);
                    cityName = response?.Response?.GeoObjectCollection?.FeatureMember?[0]?.GeoObject?.Name ?? update.Text;
                    string? pos = response?.Response?.GeoObjectCollection?.FeatureMember?[0]?.GeoObject?.Point?.pos;
                    string[] coordinates = pos?.Split(' ') ?? Array.Empty<string>();

                    if (coordinates.Length != 2
                        || !double.TryParse(coordinates[0], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude)
                        || !double.TryParse(coordinates[1], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude))
                    {
                        await update.Owner.DeliveryService.ReplyToSender("Не удалось найти искомый город...", update);
                        return;
                    }
                }
            }

            var place = new GeoCoderInfo(cityName, longitude, latitude);
            var resultMessage = $"Погода в запрошенном месте:\n{place.GetDisplay()}\n\n";

            if (RequestApi)
            {
                resultMessage += await GetWeatherInfo(place);
            }

            var menu = new PairedInlineMenu(update.Owner);
            menu.Add(FollowGeocode, new(cityName, longitude, latitude));

            var resp = await update.Owner.Bot.SendLocationAsync(update.ChatId, latitude, longitude);
            var message = new OutputMessageText(resultMessage)
            {
                Menu = menu,
                ReplyToMessageId = resp.MessageId
            };
            await update.Owner.DeliveryService.ReplyToSender(message, update);
        }

        #region Database
        private static readonly object locker = new();
        private static Task<List<T>?> LoadFromJson<T>(string dataName)
        {
            var filePath = $"resources/database.{dataName}.json";
            if (!Directory.Exists(new FileInfo(filePath).DirectoryName))
                Directory.CreateDirectory(new FileInfo(filePath).DirectoryName!);

            lock (locker)
            {
                List<T>? res = null;
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    res = JsonConvert.DeserializeObject<List<T>>(json);
                }
                return Task.FromResult(res);
            }
        }
        private static Task SaveDataToJson<T>(List<T> data, string dataName)
        {
            var filePath = $"resources/database.{dataName}.json";
            if (!Directory.Exists(new FileInfo(filePath).DirectoryName))
                Directory.CreateDirectory(new FileInfo(filePath).DirectoryName!);
            lock (locker)
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            return Task.CompletedTask;
        }
        #endregion
    }
}