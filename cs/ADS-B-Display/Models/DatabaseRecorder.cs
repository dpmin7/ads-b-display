using System;
using System.Threading;
using System.Threading.Tasks;

namespace ADS_B_Display.Models
{
    public class DbRecorder : IRecorder
    {
        private IDBConnector _dbConnector;

        public void Start(string path = null, IDBConnector dbConnector = null)
        {
            if (dbConnector == null)
                throw new ArgumentNullException(nameof(dbConnector));
            _dbConnector = dbConnector;
        }

        public void Write(long timestamp, string row)
        {
            _dbConnector?.WriteRow(timestamp, row);
        }

        public void Stop()
        {
            _dbConnector?.Close();
            _dbConnector = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}