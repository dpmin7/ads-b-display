using ADS_B_Display.Models;
using System;
using System.Threading.Tasks;

namespace ADS_B_Display.Models
{
    public interface IDbWriterReader : IDisposable
    {
        void SetPathCsvFileName();
        void CreateCsvWriter();
        void CreateCsvReader();
        void WriteRow(long timestamp, string row);
        string ReadRow();
        void Close();
        void DeleteAllCsvFiles();
        void ReadDataFromDatabase();
    }
}