using Google.Apis.Bigquery.v2.Data;
using Google.Cloud.BigQuery.V2;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ADS_B_Display.Models.Connector
{
    public class BigQueryConnector : IDBConnector
    {
        private const string CredentialFilename = "YourJsonFile.json";
        private const string ProjectId = "scs-lg-arch-5";
        private const string DatasetId = "SBS_Data";

        private string _TableId;

        public string CredentialPath { get; set; }
        public string CsvFolderPath { get; set; }
        public string CsvFileName { get; set; }
        public string CsvFullPath { get; set; }
        public string FullTablePath { get; set; }

        public int WriteRowCount { get; set; }
        public int FileCount { get; set; }
        public int RowThreshold { get; set; } = 50000;

        public StreamWriter CsvWriter { get; private set; }

        private BlockingCollection<string> _rowQueue = new BlockingCollection<string>(boundedCapacity: 100000); // 메모리 사용량 제한

        // ... 기존 필드 ...
        private Stopwatch _playToFirstRowStopwatch = new Stopwatch();
        public long PlayToFirstRowElapsedMilliseconds { get; private set; } = -1;

        public BigQueryConnector(string tableId)
        {
            WriteRowCount = 0;
            FileCount = 0;
            CsvWriter = null;
            _TableId = tableId;

            // 실행 파일의 상위 폴더 경로 구하기
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string homeDir = Path.GetDirectoryName(exePath);

            CsvFolderPath = Path.Combine(homeDir, "BigQuery");
            Directory.CreateDirectory(CsvFolderPath); // 폴더가 없으면 생성

            CredentialPath = Path.Combine(homeDir, "BigQuery", CredentialFilename);

            

            Console.WriteLine($"Set CredentialPath: {CredentialPath}");

            SetNextCsvFile();
            CreateCsvWriter();
        }
        private void SetNextCsvFile()
        {
            CsvFileName = $"BigQuery{FileCount}.csv";
            CsvFullPath = Path.Combine(CsvFolderPath, CsvFileName);
            FileCount++;
        }

        public static List<BigQueryListItem> GetTableLists()
        {
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string homeDir = Path.GetDirectoryName(exePath);


            string credentialPath = Path.Combine(homeDir, "BigQuery", "YourJsonFile.json");
            if (!File.Exists(credentialPath))
            {
                MessageBox.Show(Application.Current.MainWindow, $"{credentialPath} is not exist.");
                return null;
            }

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

        private void CreateCsvWriter()
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
        }
        public string ReadRow()
        {
            string row;
            if (_rowQueue.TryTake(out row, Timeout.Infinite))
                return row;
            return null;
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
                CsvWriter.Flush();
                CsvWriter.Close();

                if (CsvFileName == "BigQuery0.csv")
                {
                    string nowStr = DateTime.Now.ToString("yyyyMMddHHmmss");
                    FullTablePath = "scs-lg-arch-5.SBS_Data." + nowStr;
                }

                string uploadCsvFolder = CsvFolderPath;
                string uploadCsvFile = CsvFileName;
                string uploadTableId = FullTablePath;

                // 비동기로 업로드 실행
                Task.Run(() => UploadCsvToBigQuery(uploadCsvFolder, uploadCsvFile, uploadTableId));

                SetNextCsvFile();
                CreateCsvWriter();
                WriteRowCount = 0; // Reset after upload
            }
        }

        // Play 버튼 클릭 시 호출
        public void StartPlayTiming()
        {
            PlayToFirstRowElapsedMilliseconds = -1;
            _playToFirstRowStopwatch.Restart();
        }

        public long GetPlaybackTime(string tableId)
        {
            long playbackTime = 0;
            string fullTablePath = $"scs-lg-arch-5.SBS_Data.{tableId}";
            var client = BigQueryClient.Create("scs-lg-arch-5");
            string query = $"SELECT MIN(timestamp) AS min_ts, MAX(timestamp) AS max_ts FROM `{fullTablePath}`";
            var result = client.ExecuteQuery(query, parameters: null).FirstOrDefault();

            if (result != null)
            {
                long minTs = result["min_ts"] == null ? 0 : Convert.ToInt64(result["min_ts"]);
                long maxTs = result["max_ts"] == null ? 0 : Convert.ToInt64(result["max_ts"]);
                playbackTime = maxTs - minTs;
            }

            //Console.WriteLine($"Playback time for table {tableId}: {playbackTime} ms (Min: {result["min_ts"]}, Max: {result["max_ts"]})");

            return playbackTime;
        }

        public void ReadDataFromDatabase()
        {
            string fullTablePath = "scs-lg-arch-5.SBS_Data." + _TableId;
            int batchSize = 50000;
            int offset = 0;
            bool isFirstBatch = true;
            bool isFirstRow = true;
            List<string> fieldNames = null;

            // 기존 큐 비우기
            _rowQueue = new BlockingCollection<string>(boundedCapacity: 100000);

            Task.Run(() =>
            {
                var client = BigQueryClient.Create(ProjectId);

                while (true)
                {
                    string query = $"SELECT * FROM `{fullTablePath}` ORDER BY timestamp ASC LIMIT {batchSize} OFFSET {offset}";
                    var results = client.ExecuteQuery(query, parameters: null);

                    bool hasRow = false;
                    foreach (var row in results)
                    {
                        if (isFirstBatch)
                        {
                            fieldNames = results.Schema.Fields.Select(f => f.Name).ToList();
                            _rowQueue.Add(string.Join(",", fieldNames)); // 헤더
                            isFirstBatch = false;
                        }

                        string line = string.Join(",", fieldNames.Select(f => CsvEscape(row[f]?.ToString())));
                        if (isFirstRow)
                        {
                            if (_playToFirstRowStopwatch.IsRunning)
                            {
                                _playToFirstRowStopwatch.Stop();
                                PlayToFirstRowElapsedMilliseconds = _playToFirstRowStopwatch.ElapsedMilliseconds;
                                Console.WriteLine($"Play~BigQuery 첫 row까지 소요 시간: {PlayToFirstRowElapsedMilliseconds} ms");
                            }
                            isFirstRow = false;
                        }
                        _rowQueue.Add(line);
                        hasRow = true;
                    }

                    if (!hasRow)
                        break;

                    offset += batchSize;
                }
                _rowQueue.CompleteAdding(); // 데이터 끝 신호
            });
        }

        private bool UploadCsvToBigQuery(string credentialFolder, string filename, string tableId)
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