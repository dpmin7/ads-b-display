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

namespace ADS_B_Display.Models
{
    public class BigQuery : IDisposable
    {
        private const string UploadFilename = "SimpleCSVtoBigQuery.py";
        private const string DeleteFilename = "DeleteDataInBigQuery.py";
        private const string ReadFilename = "ReadDataFromBigQuery.py";
        private const string CredentialFilename = "YourJsonFile.json";

        private const string ProjectId = "scs-lg-arch-5";
        private const string DatasetId = "SBS_Data";

        private bool _useBigQuery = false;
        private string _TableId;

        private Process _pyProcess = null;

        public string UploadScriptPath { get; set; }
        public string DeleteScriptPath { get; set; }
        public string ReadScriptPath { get; set; }
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

        public BigQuery(string tableId, bool useBigQuery = false)
        {
            WriteRowCount = 0;
            ReadRowCount = 0;
            FileCount = 0;
            CsvWriter = null;
            _useBigQuery = useBigQuery;
            _TableId = tableId;

            // 실행 파일의 상위 폴더 경로 구하기
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string homeDir = Path.GetDirectoryName(exePath);

            CsvFolderPath = Path.Combine(homeDir, "BigQuery");

            string scriptDir = Path.Combine(homeDir, "BigQuery");

            Directory.CreateDirectory(CsvFolderPath); // 폴더가 없으면 생성

            UploadScriptPath = Path.Combine(homeDir, "BigQuery", UploadFilename);
            DeleteScriptPath = Path.Combine(homeDir, "BigQuery", DeleteFilename);
            ReadScriptPath = Path.Combine(homeDir, "BigQuery", ReadFilename);
            CredentialPath = Path.Combine(homeDir, "BigQuery", CredentialFilename);

            Console.WriteLine($"Set UploadScriptPath: {UploadScriptPath}");
            Console.WriteLine($"Set DeleteScriptPath: {DeleteScriptPath}");
            Console.WriteLine($"Set CredentialPath: {CredentialPath}");
        }

        public void SetPathBigQueryCsvFileName()
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

            if (!_pyProcess.HasExited)
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

                // 다음 파일 경로 설정
                //CsvFileName = $"BigQuery{FileCount}.csv";
                //CsvFullPath = Path.Combine(CsvFolderPath, CsvFileName);
                //FileCount++;
                //ReadRowCount = 0;
                SetPathBigQueryCsvFileName();

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

                if (_useBigQuery)
                    UploadToBigQuery();

                SetPathBigQueryCsvFileName();
                CreateCsvWriter();
                WriteRowCount = 0; // Reset after upload
            }
        }

        public void UploadToBigQuery()
        {
            if (CsvFileName == "BigQuery0.csv")
            {
                string nowStr = DateTime.Now.ToString("yyyyMMddHHmmss");
                FullTablePath = "scs-lg-arch-5.SBS_Data." + nowStr;
            }

            PerformPythonScript($"\"{UploadScriptPath}\" \"{CsvFolderPath}\" \"{CsvFileName}\" \"{FullTablePath}\"");
        }

        public void DeleteBigQueryData()
        {
            PerformPythonScript($"\"{DeleteScriptPath}\" \"{CsvFolderPath}\"");
        }

        public void ReadBigQueryData()
        {
            //Task.Run(() => DownloadBigQueryToGzippedCsvFiles(
            //    "scs-lg-arch-5",
            //    "SBS_Data",
            //    "FirstRun",
            //    CredentialPath
            //));

            string fullTablePath = "scs-lg-arch-5.SBS_Data." + _TableId;

            PerformPythonScript($"\"{ReadScriptPath}\" \"{CsvFolderPath}\" \"{fullTablePath}\"");

            string initFileName = $"BigQuery0.csv"; 
            string initFullPath = Path.Combine(CsvFolderPath, initFileName);

            while (true)
            {
                try
                {
                    using (var stream = File.Open(initFullPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        // 파일이 열렸으므로 바로 닫고 루프 탈출
                        break;
                    }
                }
                catch (IOException)
                {
                    Console.WriteLine($"Downloading BigQuery0.csv...: ");

                    // 파일이 아직 다른 프로세스(파이썬)에서 열려 있음
                    Thread.Sleep(1000); // 1s 대기 후 재시도
                }
            }
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

        private void PerformPythonScript(string args)
        {
            try
            {
                //Console.WriteLine($"args: {args}");

                var psi = new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = "python",
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    //CreateNoWindow = false, // For Debug
                    //UseShellExecute = true, // For Debug
                    //RedirectStandardOutput = true,
                    //RedirectStandardError = true,
                };

                Console.WriteLine($"Running Python Script: {psi.FileName} {psi.Arguments}");

                _pyProcess = new Process { StartInfo = psi };

                _pyProcess.Start();

                // Sync
                //using (var process = new Process { StartInfo = psi })
                //{
                //    process.Start();          // 프로세스 시작
                //    process.WaitForExit();    // 종료까지 대기 (동기 처리)

                //    // 필요 시 결과 확인
                //    string output = process.StandardOutput.ReadToEnd();
                //    string error = process.StandardError.ReadToEnd();
                //    Console.WriteLine("Output: " + output);
                //    Console.WriteLine("Error: " + error);
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Python Script Error: {ex.Message}");
            }
        }

        public void DownloadBigQueryToGzippedCsvFiles(string projectId, string datasetId, string tableId, string credentialJsonPath)
        {
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialJsonPath);

            var totalSw = Stopwatch.StartNew();

            var client = BigQueryClient.Create(projectId);
            string tableRef = $"{projectId}.{datasetId}.{tableId}";

            // 전체 row 수 조회
            var countSw = Stopwatch.StartNew();
            var totalQuery = $"SELECT COUNT(*) as total FROM `{tableRef}`";
            var totalRows = (long)client.ExecuteQuery(totalQuery, null).First()["total"];
            countSw.Stop();
            Console.WriteLine($"[BigQuery] Row count 쿼리 소요 시간: {countSw.ElapsedMilliseconds} ms, 총 {totalRows} rows");

            int batchSize = RowThreshold;
            int offset = 0;
            int fileCount = 0;

            while (offset < totalRows)
            {
                var querySw = Stopwatch.StartNew();
                var query = $"SELECT * FROM `{tableRef}` ORDER BY timestamp LIMIT {batchSize} OFFSET {offset}";
                var result = client.ExecuteQuery(query, null);
                querySw.Stop();
                Console.WriteLine($"[BigQuery] 쿼리({fileCount}) 소요 시간: {querySw.ElapsedMilliseconds} ms (OFFSET={offset})");

                var fileSw = Stopwatch.StartNew();
                string gzFile = Path.Combine(CsvFolderPath, $"BigQuery{fileCount}.csv.gz");

                using (var fileStream = new FileStream(gzFile, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal))
                using (var writer = new StreamWriter(gzipStream, Encoding.UTF8))
                {
                    var schema = result.Schema.Fields;
                    writer.WriteLine(string.Join(",", schema.Select(f => f.Name)));

                    var sb = new StringBuilder(1024);
                    foreach (var row in result)
                    {
                        sb.Clear();
                        for (int i = 0; i < schema.Count; i++)
                        {
                            if (i > 0) sb.Append(',');
                            var value = row[schema[i].Name];
                            var str = value?.ToString() ?? "";
                            if (str.Contains(',') || str.Contains('"') || str.Contains('\n') || str.Contains('\r'))
                            {
                                str = $"\"{str.Replace("\"", "\"\"")}\"";
                            }
                            sb.Append(str);
                        }
                        writer.WriteLine(sb.ToString());
                    }
                }

                fileSw.Stop();
                Console.WriteLine($"[BigQuery] 압축 파일 저장({fileCount}) 소요 시간: {fileSw.ElapsedMilliseconds} ms ({gzFile})");

                offset += batchSize;
                fileCount++;
            }

            totalSw.Stop();
            Console.WriteLine($"[BigQuery] 전체 다운로드 소요 시간: {totalSw.ElapsedMilliseconds} ms");
        }

        public void Dispose()
        {
            CsvWriter?.Flush();
            CsvWriter?.Close();
            CsvWriter = null;
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