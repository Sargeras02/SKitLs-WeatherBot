using SKitLs.Bots.Telegram.ArgedInteractions.Argumentation.Prototype;

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

        public string GetDisplay() => $"{Name} ({BeautyLatitude(Latitude)} {BeautyLongitude(Longitude)})";

        public static string BeautyLatitude(double coordinate) => $"{(coordinate >= 0 ? "N" : "S")}{BeautyCoordinate(coordinate)}";
        public static string BeautyLongitude(double coordinate) => $"{(coordinate >= 0 ? "E" : "W")}{BeautyCoordinate(coordinate)}";
        public static string BeautyCoordinate(double coordinate)
        {
            int degrees = (int)coordinate;
            double minutesAndSeconds = Math.Abs(coordinate - degrees) * 60;
            int minutes = (int)minutesAndSeconds;
            double seconds = (minutesAndSeconds - minutes) * 60;

            return $"{Math.Abs(degrees)}°{minutes:00}'{seconds:00.00}''";
        }
    }
}