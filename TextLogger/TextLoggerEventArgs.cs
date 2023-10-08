namespace TextLogger
{
    public class TextLoggerEventArgs
    {
        public readonly string Text;

        public TextLoggerEventArgs(string text)
        {
            Text = text;
        }
    }
}