using Google.Apis.Bigquery.v2.Data;
using Google.Cloud.BigQuery.V2;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ADS_B_Display.Models;

namespace ADS_B_Display.Models
{
    public class BigQuery : IDbWriterReader
    {
        private const string CredentialFilename = "YourJsonFile.json";
        private const string ProjectId = "scs-lg-arch-5";
        private const string DatasetId = "SBS_Data";

        private string _TableId;

        private Process _pyProcess = null;

        public string CredentialPath { get; set; }
        public string CsvFolderPath { get; set; }
        public string CsvFileName { get; set; }
        public string CsvFullPath { get; set; }
        public string FullTablePath { get; set; }

        public int ReadRowCount { get; set; }
        public int WriteRowCount { get; set; }
        public int FileCount { get; set; }
        public int RowThreshold { get; set; } = 50000;

        public StreamWriter CsvWriter { get; private set; }
        public StreamReader CsvReader { get; private set; }

        public BigQuery(string tableId)
        {
            WriteRowCount = 0;
            ReadRowCount = 0;
            FileCount = 0;
            CsvWriter = null;
            _TableId = tableId;

            // 실행 파일의 상위 폴더 경로 구하기
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string homeDir = Path.GetDirectoryName(exePath);

            CsvFolderPath = Path.Combine(homeDir, "BigQuery");

            string scriptDir = Path.Combine(homeDir, "BigQuery");

            Directory.CreateDirectory(CsvFolderPath); // 폴더가 없으면 생성

            CredentialPath = Path.Combine(homeDir, "BigQuery", CredentialFilename);

            Console.WriteLine($"Set CredentialPath: {CredentialPath}");
        }

        public void SetPathCsvFileName()
        {   
            CsvFileName = $"BigQuery{FileCount}.csv";
            CsvFullPath = Path.Combine(CsvFolderPath, CsvFileName);

            Console.WriteLine($"Set CsvFullPath: {CsvFullPath}");

            FileCount++;
        }

        public static List<BigQueryListItem> GetTableLists()
        {
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string homeDir = Path.GetDirectoryName(exePath);
            string credentialPath = Path.Combine(homeDir, "BigQuery", "YourJsonFile.json");

            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialPath);
            
            // BigQuery 클라이언트 생성
            BigQueryClient client = BigQueryClient.Create(ProjectId);

            // 데이터셋 참조 생성
            DatasetReference datasetRef = client.GetDatasetReference(DatasetId);

            // 테이블 목록 가져오기
            var tables = client.ListTables(datasetRef);

            List<BigQueryListItem> itemList = new List<BigQueryListItem>();
            foreach (var table in tables)
            {
                //Console.WriteLine($"Table ID: {table.Reference.TableId}");

                var item = new BigQueryListItem(table.Reference.TableId);
                itemList.Add(item);
            }

            return itemList;
        }

        public void CreateCsvReader()
        {
            try
            {
                CsvReader = new StreamReader(CsvFullPath);
            }
            catch (Exception)
            {
                Console.WriteLine($"Cannot Open BigQuery CSV File {CsvFullPath}");
                return;
            }
        }

        public void CreateCsvWriter()
        {
            try
            {
                CsvWriter = new StreamWriter(CsvFullPath, false);
            }
            catch (Exception)
            {
                Console.WriteLine($"Cannot Open BigQuery CSV File {CsvFullPath}");
                return;
            }

            string header = "Timestamp,Message Type,Transmission Type,SessionID,AircraftID,HexIdent,FlightID,Date_MSG_Generated,Time_MSG_Generated,Date_MSG_Logged,Time_MSG_Logged,Callsign,Altitude,GroundSpeed,Track,Latitude,Longitude,VerticalRate,Squawk,Alert,Emergency,SPI,IsOnGround";
            CsvWriter.WriteLine(header);
            CsvWriter.Flush();
        }

        public void Close()
        {
            if (CsvWriter != null)
            {
                CsvWriter.Flush();
                CsvWriter.Close();
                CsvWriter = null;
            }

            if (CsvReader != null)
            {
                CsvReader.Close();
                CsvReader = null;
            }

            if ((_pyProcess != null) && (!_pyProcess.HasExited))
            {
                _pyProcess.Kill(); // Python 프로세스 강제 종료
            }            
        }

        public string ReadRow()
        {
            if (CsvReader == null)
                return null;

            string row = CsvReader.ReadLine();
            ReadRowCount++;

            if (ReadRowCount >= RowThreshold || row == null)
            {
                CsvReader.Close();

                SetPathCsvFileName();

                // 다음 파일이 존재하면 열기, 없으면 종료 신호 반환
                if (File.Exists(CsvFullPath))
                {
                    CsvReader = new StreamReader(CsvFullPath);
                    // 첫 줄이 헤더라면 건너뜀
                    CsvReader.ReadLine();
                    row = CsvReader.ReadLine();
                    ReadRowCount++;
                }
                else
                {
                    CsvReader = null;
                    return null; // 더 이상 읽을 파일이 없으므로 종료
                }
            }

            return row ?? string.Empty;
        }

        public void WriteRow(long timestamp, string row)
        {
            if (CsvWriter == null) {
                return;
            }

            row = $"{timestamp},{row}"; // timestamp 추가

            CsvWriter.WriteLine(row);
            WriteRowCount++;
            
            if (WriteRowCount >= RowThreshold)
            {
                CsvWriter.Close();

                UploadToBigQuery();

                SetPathCsvFileName();
                CreateCsvWriter();
                WriteRowCount = 0; // Reset after upload
            }
        }

        public void ReadDataFromDatabase()
        {
            string fullTablePath = "scs-lg-arch-5.SBS_Data." + _TableId;
            string initFileName = "BigQuery0.csv";
            string initFullPath = Path.Combine(CsvFolderPath, initFileName);

            // Task.Run으로 백그라운드 실행 (C# 7.3 async Main 불가)
            Task.Run(async () =>
            {
                bool success = await DownloadBigQueryToCsvAsync(CsvFolderPath, fullTablePath);
                if (!success)
                {
                    Console.WriteLine("BigQuery CSV download failed.");
                    return;
                }
            });

            // 최초 파일이 생성될 때까지 대기
            while (true)
            {
                try
                {
                    using (var stream = File.Open(initFullPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        // 파일이 성공적으로 열리면 곧 닫고 탈출
                        break;
                    }
                }
                catch (IOException)
                {
                    Console.WriteLine("Waiting for BigQuery0.csv to be ready...");
                    Thread.Sleep(1000);
                }
            }

            Console.WriteLine("BigQuery0.csv is ready to read.");
        }

        public void DeleteAllCsvFiles()
        {
            try
            {
                if (Directory.Exists(CsvFolderPath))
                {
                    var csvFiles = Directory.GetFiles(CsvFolderPath, "BigQuery*.csv");
                    foreach (var file in csvFiles)
                    {
                        File.Delete(file);
                    }

                    Console.WriteLine($"Delete All csv files");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting CSV files: {ex.Message}");
            }
        }

        public bool UploadCsvToBigQuery(string credentialFolder, string filename, string tableId)
        {
            string filepath = Path.Combine(credentialFolder, filename);
            string credentialPath = Path.Combine(credentialFolder, "YourJsonFile.json");

            Console.WriteLine("Credential Path: " + credentialPath);
            Console.WriteLine("CSV File: " + filepath);
            Console.WriteLine("BigQuery Table ID: " + tableId);

            try
            {
                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialPath);

                string[] parts = tableId.Split('.');
                if (parts.Length != 3)
                {
                    Console.WriteLine("Invalid Table ID format. Use project.dataset.table");
                    return false;
                }

                string projectId = parts[0];
                string datasetId = parts[1];
                string rawTableId = parts[2];

                TableReference tableRef = new TableReference
                {
                    ProjectId = projectId,
                    DatasetId = datasetId,
                    TableId = rawTableId
                };

                BigQueryClient client = BigQueryClient.Create(projectId);

                UploadCsvOptions uploadOptions = new UploadCsvOptions
                {
                    SkipLeadingRows = 1,
                    Autodetect = true,
                    WriteDisposition = WriteDisposition.WriteAppend
                };

                using (FileStream stream = new FileStream(filepath, FileMode.Open, FileAccess.Read))
                {
                    Console.WriteLine("Uploading CSV...");
                    BigQueryJob job = client.UploadCsv(tableRef, schema: null, input: stream, options: uploadOptions);
                    job.PollUntilCompleted();
                }

                File.Delete(filepath);
                Console.WriteLine("File deleted after successful upload: " + filename);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Upload failed: " + ex.Message);
                return false;
            }
        }

        public void Dispose()
        {
            CsvWriter?.Flush();
            CsvWriter?.Close();
            CsvWriter = null;
        }

        private void UploadToBigQuery()
        {
            if (CsvFileName == "BigQuery0.csv")
            {
                string nowStr = DateTime.Now.ToString("yyyyMMddHHmmss");
                FullTablePath = "scs-lg-arch-5.SBS_Data." + nowStr;
            }

            UploadCsvToBigQuery(CsvFolderPath, CsvFileName, FullTablePath);
        }

        private static async Task<bool> DownloadBigQueryToCsvAsync(string outputFolder, string tableId)
        {
            if (!outputFolder.EndsWith(Path.DirectorySeparatorChar.ToString()))
                outputFolder += Path.DirectorySeparatorChar;

            string credentialPath = Path.Combine(outputFolder, "YourJsonFile.json");
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialPath);

            string[] parts = tableId.Split('.');
            if (parts.Length != 3)
            {
                Console.WriteLine("Invalid Table ID format. Use project.dataset.table");
                return false;
            }

            string projectId = parts[0];
            BigQueryClient client = await BigQueryClient.CreateAsync(projectId);
            Console.WriteLine("BigQuery client initialized.");

            int batchSize = 50000;
            int offset = 0;
            int batchIndex = 0;

            while (true)
            {
                string query = $"SELECT * FROM `{tableId}` ORDER BY timestamp ASC LIMIT {batchSize} OFFSET {offset}";
                Console.WriteLine($"Running query batch {batchIndex} (OFFSET={offset})...");

                BigQueryResults results;
                try
                {
                    results = await client.ExecuteQueryAsync(query, parameters: null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Query failed: " + ex.Message);
                    return false;
                }

                var rows = results.ToList();
                if (rows.Count == 0)
                {
                    Console.WriteLine($"No more rows. Total batches: {batchIndex}");
                    break;
                }

                string tmpPath = Path.Combine(outputFolder, $"BigQuery{batchIndex}.tmp");
                string finalPath = Path.Combine(outputFolder, $"BigQuery{batchIndex}.csv");

                try
                {
                    using (var writer = new StreamWriter(tmpPath, false, Encoding.UTF8))
                    {
                        // Write header
                        string header = string.Join(",", rows[0].Schema.Fields.Select(f => f.Name));
                        await writer.WriteLineAsync(header);

                        foreach (var row in rows)
                        {
                            string line = string.Join(",", row.Schema.Fields.Select(f => CsvEscape(row[f.Name]?.ToString())));
                            await writer.WriteLineAsync(line);
                        }
                    }

                    if (File.Exists(finalPath))
                    {
                        File.Delete(finalPath); // 기존 파일 삭제
                    }
                    File.Move(tmpPath, finalPath);

                    Console.WriteLine($"Saved batch {batchIndex} → {finalPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing CSV: {ex.Message}");
                    return false;
                }

                offset += batchSize;
                batchIndex++;
            }

            return true;
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }

    public class BigQueryListItem
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public string Name { get; set; }
        public BigQueryListItem(string name)
        {
            Name = name;
        }
        public BigQueryListItem(DateTime st, DateTime et)
        {
            StartTime = st;
            EndTime = et;
        }
    }
}