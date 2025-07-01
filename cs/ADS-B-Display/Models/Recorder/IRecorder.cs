using ADS_B_Display.Models.Connector;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.Recorder
{    public interface IRecorder : IDisposable
    {
        void Start(string path = null, IDBConnector dbConnector = null);
        void Stop();
        void Write(long timestamp, string row);
    }
}