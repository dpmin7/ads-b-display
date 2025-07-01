using System;
using System.Threading;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.Connector
{
    public class InputSourceState
    {
        public volatile bool Running;
        public bool First = true;
        public long LastTime = 0;
    }
    public interface IConnector : IDisposable
    {
        void Start(Func<string, long, uint> onMessageReceived, int playBackSpeed, InputSourceState state);
        Task StartAsync(Func<string, long, uint> onMessageReceived, int playBackSpeed, CancellationToken ct, InputSourceState state);
        void Stop();
    }
}