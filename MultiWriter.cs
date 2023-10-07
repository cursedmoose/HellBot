using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchBot
{
    internal class MultiWriter : TextWriter
    {
        StreamWriter fileOutput;
        TextWriter consoleOutput;
        
        public MultiWriter(TextWriter stdOut, string file)
        {
            consoleOutput = stdOut;
            fileOutput = new StreamWriter(new FileStream(file, FileMode.Append));
            fileOutput.AutoFlush = true;
        }

        public override Encoding Encoding => consoleOutput.Encoding;

        public override void WriteLine(string? value)
        {
            consoleOutput.WriteLine(value);
            fileOutput.WriteLine(value);
        }
    }
}
