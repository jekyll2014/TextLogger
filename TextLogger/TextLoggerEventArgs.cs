namespace TextLogger
{
    public class TextLoggerEventArgs
    {
        public readonly string Text;

        public TextLoggerEventArgs(string text)
        {
            this.Text = text;
        }
    }
}