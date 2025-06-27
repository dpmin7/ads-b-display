import sys
import os
import time
import pandas as pd
from google.cloud import bigquery

def main():
    global_start = time.time()

    if len(sys.argv) == 3:
        output_folder = sys.argv[1]
        table_id = sys.argv[2]

        if not output_folder.endswith("\\") and not output_folder.endswith("/"):
            output_folder += "\\"

        print(f"[{time.time() - global_start:.2f}s] ▶ Arguments parsed: {output_folder}")
    else:
        print("Usage: python ReadDataFromBigQuery.py <BigQueryCredentialFolder> <TableID>")
        os._exit(0)

    # Set credentials and create BigQuery client
    start = time.time()
    os.environ["GOOGLE_APPLICATION_CREDENTIALS"] = output_folder + "YourJsonFile.json"
    client = bigquery.Client()
    print(f"[{time.time() - global_start:.2f}s] ▶ BigQuery client initialized (Elapsed: {time.time() - start:.2f}s)")

    batch_size = 50000
    offset = 0
    file_count = 0

    while True:
        loop_start = time.time()

        query = f"SELECT * FROM `{table_id}` ORDER BY timestamp ASC LIMIT {batch_size} OFFSET {offset}"
        print(f"[{time.time() - global_start:.2f}s] ▶ Query execution started: {query}")

        start = time.time()
        query_job = client.query(query)
        df = query_job.to_dataframe()
        query_duration = time.time() - start
        print(f"[{time.time() - global_start:.2f}s] ▶ Query completed and DataFrame created (Elapsed: {query_duration:.2f}s)")

        if df.empty:
            print(f"[{time.time() - global_start:.2f}s] ▶ No more data. Exiting.")
            break

        tmp_file = os.path.join(output_folder, f"BigQuery{file_count}.tmp")
        final_file = os.path.join(output_folder, f"BigQuery{file_count}.csv")

        start = time.time()
        df.to_csv(tmp_file, index=False, encoding="utf-8")
        csv_write_duration = time.time() - start
        print(f"[{time.time() - global_start:.2f}s] ▶ Temporary CSV file saved (Elapsed: {csv_write_duration:.2f}s)")

        start = time.time()
        os.rename(tmp_file, final_file)
        rename_duration = time.time() - start
        print(f"[{time.time() - global_start:.2f}s] ▶ File renamed to final CSV (Elapsed: {rename_duration:.3f}s)")

        print(f"[{time.time() - global_start:.2f}s] ▶ Batch {file_count} completed (Total: {time.time() - loop_start:.2f}s)\n")

        offset += batch_size
        file_count += 1

    print(f"[{time.time() - global_start:.2f}s] ▶ All tasks completed.")

if __name__ == "__main__":
    main()