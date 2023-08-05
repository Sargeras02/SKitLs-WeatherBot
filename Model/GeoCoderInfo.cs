using SKitLs.Bots.Telegram.ArgedInteractions.Argumenting.Prototype;

namespace WeatherBot.Model
{
    internal class GeoCoderInfo
    {
        [BotActionArgument(0)]
        public string Name { get; set; } = null!;
        [BotActionArgument(1)]
        public double Longitude { get; set; }
        [BotActionArgument(2)]
        public double Latitude { get; set; }

        public GeoCoderInfo() { }
        public GeoCoderInfo(string name, double longitude, double latitude)
        {
            Name = name;
            Longitude = longitude;
            Latitude = latitude;
        }

        public string GetDisplay() => $"Город: {Name} ({Latitude.ToString().Replace(',', '.')}, {Longitude.ToString().Replace(',', '.')})";
    }
}