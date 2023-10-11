using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TextLogger
{
    // string length limit
    public sealed class TextLogger : IDisposable
    {
        public enum TextFormat
        {
            Default,
            PlainText,
            Hex,
            AutoReplaceHex
        }

        public enum TimeFormat
        {
            None,
            Default,
            ShortTime,
            LongTime
        }

        public enum DateFormat
        {
            None,
            Default,
            ShortDate,
            LongDate
        }

        public bool LogToScreen = false;
        public int LineLimit = 0;
        public int CharLimit = 0;
        public int LineTimeLimit = 100;
        public string LogFileName = string.Empty;
        public bool FilterZeroChars = true;

        //Text, HEX, Auto (change non-readable to <HEX>)
        public TextFormat DefaultTextFormat = TextFormat.AutoReplaceHex;

        public TimeFormat DefaultTimeFormat = TimeFormat.LongTime;
        public DateFormat DefaultDateFormat = DateFormat.ShortDate;
        public Dictionary<int, string> Channels = new Dictionary<int, string>();

        public delegate void TextChangedEventHandler(object sender, TextLoggerEventArgs e);

        public event TextChangedEventHandler? TextChangedEvent;

        public string Text
        {
            get => _text;
            private set
            {
                _text = value;
                TextChangedEvent?.Invoke(this, new TextLoggerEventArgs(_text));
            }
        }

        private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
        private int _prevChannel;
        private DateTime _lastEvent = DateTime.Now;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private string _text = string.Empty;
        private bool _disposed = false;

        public TextLogger()
        {
            Task.Factory.StartNew(async () =>
            {
                var tasks = new List<Task>();

                while (!_cts.IsCancellationRequested)
                {
                    if (_messageQueue.TryDequeue(out var message))
                    {

                        if (LogToScreen)
                        {
                            var t = Task.Run(() => AddTextToBuffer(message), _cts.Token);
                            tasks.Add(t);
                        }

                        if (!string.IsNullOrEmpty(LogFileName))
                        {
                            var t = Task.Run(() => SaveTextToFile(message, LogFileName), _cts.Token);
                            tasks.Add(t);
                        }

                        if (tasks.Any())
                        {
                            await Task.WhenAll(tasks.ToArray());
                            tasks.Clear();
                        }
                    }

                    await Task.Delay(1);
                }
            }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void AddText(string text, int channel = -1)
        {
            AddText(text, channel, DateTime.MinValue, TextFormat.Default, TimeFormat.Default,
                DateFormat.Default);
        }

        public void AddText(string text, int channel, DateTime eventTime)
        {
            AddText(text, channel, eventTime, TextFormat.Default, TimeFormat.Default, DateFormat.Default);
        }

        public void AddText(string text, int channel, DateTime eventTime, TimeFormat timeFormat)
        {
            AddText(text, channel, eventTime, TextFormat.Default, timeFormat, DateFormat.Default);
        }

        public void AddText(string text, int channel, DateTime eventTime, DateFormat dateFormat)
        {
            AddText(text, channel, eventTime, TextFormat.Default, TimeFormat.Default, dateFormat);
        }

        public void AddText(string text, int channel, DateTime eventTime, TextFormat textTextFormat,
            TimeFormat timeFormat)
        {
            AddText(text, channel, eventTime, textTextFormat, timeFormat, DateFormat.Default);
        }

        public void AddText(string text, int channel, DateTime eventTime, TextFormat textTextFormat,
            DateFormat dateFormat)
        {
            AddText(text, channel, eventTime, textTextFormat, TimeFormat.Default, dateFormat);
        }

        public void AddText(string text, int channel, DateTime eventTime, TimeFormat timeFormat, DateFormat dateFormat)
        {
            AddText(text, channel, eventTime, TextFormat.Default, timeFormat, dateFormat);
        }

        public void AddText(string text, int channel, TextFormat textTextFormat)
        {
            AddText(text, channel, DateTime.MinValue, textTextFormat, TimeFormat.Default, DateFormat.Default);
        }

        public void AddText(string text, int channel, TimeFormat timeFormat)
        {
            AddText(text, channel, DateTime.MinValue, TextFormat.Default, timeFormat, DateFormat.Default);
        }

        public void AddText(string text, int channel, DateFormat dateFormat)
        {
            AddText(text, channel, DateTime.MinValue, TextFormat.Default, TimeFormat.Default, dateFormat);
        }

        public void AddText(string text, int channel, TextFormat textTextFormat, TimeFormat timeFormat)
        {
            AddText(text, channel, DateTime.MinValue, textTextFormat, timeFormat, DateFormat.Default);
        }

        public void AddText(string text, int channel, TextFormat textTextFormat, DateFormat dateFormat)
        {
            AddText(text, channel, DateTime.MinValue, textTextFormat, TimeFormat.Default, dateFormat);
        }

        public void AddText(string text, int channel, TimeFormat timeFormat, DateFormat dateFormat)
        {
            AddText(text, channel, DateTime.MinValue, TextFormat.Default, timeFormat, dateFormat);
        }

        private void AddText(string text, int channel, DateTime logTime, TextFormat textFormat,
            TimeFormat timeFormat = TimeFormat.Default, DateFormat dateFormat = DateFormat.Default)
        {
            if (text.Length <= 0)
                return;

            var continueString = false;
            if (channel != _prevChannel)
            {
                _prevChannel = channel;
            }
            else if (LineTimeLimit > 0)
            {
                var t = (int)logTime.Subtract(_lastEvent).TotalMilliseconds;
                if (t <= LineTimeLimit)
                    continueString = true;
            }

            _lastEvent = logTime;
            var tmpStr = new StringBuilder();
            if (!continueString)
            {
                tmpStr.Append(Environment.NewLine);
                if (logTime != DateTime.MinValue)
                {
                    if (dateFormat == DateFormat.Default)
                        dateFormat = DefaultDateFormat;

                    if (dateFormat == DateFormat.LongDate)
                        tmpStr.Append(logTime.ToLongDateString() + " ");
                    else if (dateFormat == DateFormat.ShortDate)
                        tmpStr.Append(logTime.ToShortDateString() + " ");

                    if (timeFormat == TimeFormat.Default)
                        timeFormat = DefaultTimeFormat;

                    if (timeFormat == TimeFormat.LongTime)
                        tmpStr.Append(logTime.ToLongTimeString() + "." + logTime.Millisecond.ToString("D3") + " ");

                    else if (timeFormat == TimeFormat.ShortTime)
                        tmpStr.Append(logTime.ToShortTimeString() + " ");
                }

                if (channel >= 0 && Channels.ContainsKey(channel)
                                 && !string.IsNullOrEmpty(Channels[channel]))
                    tmpStr.Append(Channels[channel] + " ");
            }

            if (textFormat == TextFormat.Default)
                textFormat = DefaultTextFormat;

            if (FilterZeroChars)
                text = FilterZeroChar(text);

            if (textFormat == TextFormat.PlainText)
                tmpStr.Append(text);
            else if (textFormat == TextFormat.Hex)
                tmpStr.Append(ConvertStringToHex(text));
            else if (textFormat == TextFormat.AutoReplaceHex)
                tmpStr.Append(ReplaceUnprintable(text));

            if (tmpStr.Length > 0)
                _messageQueue.Enqueue(tmpStr.ToString());
        }

        public override string ToString()
        {
            return Text;
        }

        public void Clear()
        {
            Text = string.Empty;
        }

        public void Dispose()
        {
            _cts.Cancel();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~TextLogger()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
            }

            Text = string.Empty;
            _disposed = true;
        }

        private void AddTextToBuffer(string text)
        {
            _text += text;
            var textSizeReduced = 0;
            if (CharLimit > 0 && _text.Length > CharLimit) textSizeReduced = _text.Length - CharLimit;

            if (LineLimit > 0 && GetLinesCount(_text, LineLimit, out var pos) && pos > textSizeReduced)
                textSizeReduced = pos;

            if (textSizeReduced > 0)
                Text = _text[textSizeReduced..];
            else
                Text = _text;
        }

        private static void SaveTextToFile(string text, string fileName)
        {
            try
            {
                File.AppendAllText(fileName, text);
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
            }
        }

        private static bool GetLinesCount(string data, int lineLimit, out int pos)
        {
            var divider = new HashSet<char>
            {
                '\r',
                '\n'
            };

            var lineCount = 0;
            pos = 0;
            for (var i = data.Length - 1; i >= 0; i--)
            {
                if (divider.Contains(data[i])) // check 2 divider 
                {
                    lineCount++;
                    if (i - 1 >= 0 && divider.Contains(data[i - 1])) i--;
                }

                if (lineCount < lineLimit) continue;

                pos = i + 1;
                return true;
            }

            return false;
        }

        private static string ReplaceUnprintable(string text, bool leaveCrLf = true)
        {
            var str = new StringBuilder();

            foreach (var c in text)
                if (char.IsControl(c) && !(leaveCrLf && new List<char> { '\r', '\n', '\t' }.Contains(c)))
                {
                    str.Append("<0x" + ConvertStringToHex(c.ToString()).Trim() + ">");
                    if (c == '\n') str.Append('\n');
                }
                else
                {
                    str.Append(c);
                }

            return str.ToString();
        }

        private static string FilterZeroChar(string m, bool replaceWithSpace = true)
        {
            if (string.IsNullOrEmpty(m))
                return string.Empty;

            var n = new StringBuilder();
            foreach (var t in m)
                if (t != 0) n.Append(t);
                else if (replaceWithSpace) n.Append(' ');

            return n.ToString();
        }

        private static string ConvertStringToHex(string utfString)
        {
            if (string.IsNullOrEmpty(utfString))
                return string.Empty;

            var encodedBytes = Encoding.ASCII.GetBytes(utfString);
            var hexStr = new StringBuilder();
            foreach (var b in encodedBytes)
            {
                var c = (char)b;
                hexStr.Append(((int)c).ToString("X2"));
                hexStr.Append(' ');
            }

            return hexStr.ToString();
        }
    }
}
