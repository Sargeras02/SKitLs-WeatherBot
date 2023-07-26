namespace WeatherBot.Model
{
    class GeoCodeWrap
    {
        public GResponse Response { get; set; }
    }

    class GResponse
    {
        public GeoObjectCollection GeoObjectCollection { get; set; }
    }

    class GeoObjectCollection
    {
        public GeoObjectWrapper[] FeatureMember { get; set; }
    }

    class GeoObjectWrapper
    {
        public GeoObject GeoObject { get; set; }
    }

    class GeoObject
    {
        public string Name { get; set; }
        public Point Point { get; set; }
    }

    class Point
    {
        public string pos { get; set; }
    }
}