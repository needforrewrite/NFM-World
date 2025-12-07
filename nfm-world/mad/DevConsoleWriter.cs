using System;
using System.IO;
using System.Text;

namespace NFMWorld.Mad
{
    public class DevConsoleWriter : TextWriter
    {
        private readonly DevConsole _devConsole;
        private readonly TextWriter _originalOut;
        private string _logLevel = "default";

        public DevConsoleWriter(DevConsole devConsole, TextWriter originalOut)
        {
            _devConsole = devConsole;
            _originalOut = originalOut;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char[] buffer, int index, int count)
        {
            _devConsole.Log(new string(buffer, index, count), _logLevel);
            _originalOut.Write(buffer, index, count);
        }

        public override void Write(string? value)
        {
            if (value != null)
            {
                _devConsole.Log(value, _logLevel);
                _originalOut.Write(value);
            }
        }

        public override void Write(ReadOnlySpan<char> buffer)
        {
            if (!buffer.IsEmpty)
            {
                var str = new string(buffer);
                _devConsole.Log(str, _logLevel);
                _originalOut.Write(str);
            }
        }

        public void WriteLine(string? value, string logLevel)
        {
            if (value != null)
            {
                _devConsole.Log(value, logLevel);
                _originalOut.WriteLine(value);
            }
        }

        public void Write(string? value, string logLevel)
        {
            if (value != null)
            {
                _devConsole.Log(value, _logLevel);
                _originalOut.Write(value);
            }
        }

        public override void WriteLine(char[] buffer, int index, int count)
        {
            _devConsole.Log(new string(buffer, index, count), _logLevel);
            _originalOut.WriteLine(buffer, index, count);
        }

        public override void WriteLine(ReadOnlySpan<char> buffer)
        {
            if (!buffer.IsEmpty)
            {
                var str = new string(buffer);
                _devConsole.Log(str, _logLevel);
                _originalOut.WriteLine(str);
            }
        }
    }
}