using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TextLogger
{
    // string length limit
    public class TextLogger : IDisposable, INotifyPropertyChanged
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
        public Dictionary<byte, string> Channels = new Dictionary<byte, string>();
        public event PropertyChangedEventHandler? PropertyChanged;
        public string Text { get; private set; } = string.Empty;

        private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
        private byte _prevChannel;
        private DateTime _lastEvent = DateTime.Now;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _disposed = false;

        public TextLogger()
        {
            Task.Run(() =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (!_messageQueue.TryDequeue(out var message))
                        continue;

                    if (LogToScreen)
                        AddTextToBuffer(message);

                    if (!string.IsNullOrEmpty(LogFileName))
                        SaveTextToFile(message, LogFileName);
                }
            }, _cts.Token);
        }
        public bool AddText(string text, byte channel)
        {
            return AddText(text, channel, DateTime.MinValue, TextFormat.Default, TimeFormat.Default,
                DateFormat.Default);
        }

        public bool AddText(string text, byte channel, DateTime eventTime)
        {
            return AddText(text, channel, eventTime, TextFormat.Default, TimeFormat.Default, DateFormat.Default);
        }

        public bool AddText(string text, byte channel, DateTime eventTime, TimeFormat timeFormat)
        {
            return AddText(text, channel, eventTime, TextFormat.Default, timeFormat, DateFormat.Default);
        }

        public bool AddText(string text, byte channel, DateTime eventTime, DateFormat dateFormat)
        {
            return AddText(text, channel, eventTime, TextFormat.Default, TimeFormat.Default, dateFormat);
        }

        public bool AddText(string text, byte channel, DateTime eventTime, TextFormat textTextFormat,
            TimeFormat timeFormat)
        {
            return AddText(text, channel, eventTime, textTextFormat, timeFormat, DateFormat.Default);
        }

        public bool AddText(string text, byte channel, DateTime eventTime, TextFormat textTextFormat,
            DateFormat dateFormat)
        {
            return AddText(text, channel, eventTime, textTextFormat, TimeFormat.Default, dateFormat);
        }

        public bool AddText(string text, byte channel, DateTime eventTime, TimeFormat timeFormat, DateFormat dateFormat)
        {
            return AddText(text, channel, eventTime, TextFormat.Default, timeFormat, dateFormat);
        }

        public bool AddText(string text, byte channel, TextFormat textTextFormat)
        {
            return AddText(text, channel, DateTime.MinValue, textTextFormat, TimeFormat.Default, DateFormat.Default);
        }

        public bool AddText(string text, byte channel, TimeFormat timeFormat)
        {
            return AddText(text, channel, DateTime.MinValue, TextFormat.Default, timeFormat, DateFormat.Default);
        }

        public bool AddText(string text, byte channel, DateFormat dateFormat)
        {
            return AddText(text, channel, DateTime.MinValue, TextFormat.Default, TimeFormat.Default, dateFormat);
        }

        public bool AddText(string text, byte channel, TextFormat textTextFormat, TimeFormat timeFormat)
        {
            return AddText(text, channel, DateTime.MinValue, textTextFormat, timeFormat, DateFormat.Default);
        }

        public bool AddText(string text, byte channel, TextFormat textTextFormat, DateFormat dateFormat)
        {
            return AddText(text, channel, DateTime.MinValue, textTextFormat, TimeFormat.Default, dateFormat);
        }

        public bool AddText(string text, byte channel, TimeFormat timeFormat, DateFormat dateFormat)
        {
            return AddText(text, channel, DateTime.MinValue, TextFormat.Default, timeFormat, dateFormat);
        }

        private bool AddText(string text, byte channel, DateTime logTime, TextFormat textFormat,
            TimeFormat timeFormat = TimeFormat.Default, DateFormat dateFormat = DateFormat.Default)
        {
            if (text.Length <= 0) return true;

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

                if (Channels.ContainsKey(channel) && !string.IsNullOrEmpty(Channels[channel]))
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
            else if (textFormat == TextFormat.AutoReplaceHex) tmpStr.Append(ReplaceUnprintable(text));

            if (tmpStr.Length > 0)
                _messageQueue.Enqueue(tmpStr.ToString());

            return true;
        }

        public override string ToString()
        {
            return Text;
        }

        public void Clear()
        {
            Text = string.Empty;
            OnPropertyChanged();
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

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing) ;

            Text = string.Empty;
            _disposed = true;
        }

        private void AddTextToBuffer(string text)
        {
            Text += text;
            var textSizeReduced = 0;
            if (CharLimit > 0 && Text.Length > CharLimit) textSizeReduced = Text.Length - CharLimit;

            if (LineLimit > 0 && GetLinesCount(Text, LineLimit, out var pos) && pos > textSizeReduced)
                textSizeReduced = pos;

            if (textSizeReduced > 0)
                Text = Text[textSizeReduced..];

            OnPropertyChanged();
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
            if (string.IsNullOrEmpty(m)) return string.Empty;

            var n = new StringBuilder();
            foreach (var t in m)
                if (t != 0) n.Append(t);
                else if (replaceWithSpace) n.Append(' ');

            return n.ToString();
        }

        private static string ConvertStringToHex(string utfString)
        {
            if (string.IsNullOrEmpty(utfString)) return string.Empty;

            var encodedBytes = Encoding.ASCII.GetBytes(utfString);
            var hexStr = new StringBuilder();
            foreach (var b in encodedBytes)
            {
                var c = (char)b;
                hexStr.Append(((int)c).ToString("X2 "));
                //hexStr.Append(' ');
            }

            return hexStr.ToString();
        }
    }
}
