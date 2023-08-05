using SKitLs.Bots.Telegram.ArgedInteractions.Argumentation.Prototype;

namespace WeatherBot.Model
{
    internal class IntWrapper
    {
        [BotActionArgument(0)]
        public int Value { get; set; }

        public IntWrapper() { }
        public IntWrapper(int value) => Value = value;
    }
}