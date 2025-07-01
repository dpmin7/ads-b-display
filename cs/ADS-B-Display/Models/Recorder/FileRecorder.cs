using ADS_B_Display.Models.Connector;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.Recorder
{
    public class FileRecorder : IRecorder
    {
        private StreamWriter _writer;

        public void Start(string path = null, IDBConnector dbConnector = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("File path is required for FileRecorder.");
            _writer = new StreamWriter(path, append: true) { AutoFlush = true };
        }

        public void Write(long timestamp, string row)
        {
            _writer?.WriteLine(timestamp);
            _writer?.WriteLine(row);
        }

        public void Stop()
        {
            _writer?.Flush();
            _writer?.Close();
            _writer = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}