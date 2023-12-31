﻿using System.Text;

namespace TwitchBot
{
    internal class MultiWriter : TextWriter
    {
        readonly StreamWriter fileOutput;
        readonly TextWriter consoleOutput;
        
        public MultiWriter(TextWriter stdOut, string file)
        {
            consoleOutput = stdOut;
            fileOutput = new StreamWriter(new FileStream(file, FileMode.Append))
            {
                AutoFlush = true
            };
        }

        public override Encoding Encoding => consoleOutput.Encoding;

        public override void WriteLine(string? value)
        {
            consoleOutput.WriteLine(value);
            fileOutput.WriteLine(value);
        }
    }
}
