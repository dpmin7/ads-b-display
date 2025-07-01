using ADS_B_Display.Models;
using System;
using System.Threading.Tasks;

namespace ADS_B_Display.Models.Connector
{
    public interface IDBConnector: IDisposable
    {
        void WriteRow(long timestamp, string row);
        string ReadRow();
        
        void ReadDataFromDatabase();
        void StartPlayTiming();
        long GetPlaybackTime(string tableId);
        void Close();
    }
}