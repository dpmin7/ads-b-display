import sys
import os
import time
import pandas as pd
from google.cloud import bigquery

def setup_bigquery_client(credential_path: str) -> bigquery.Client:
    os.environ["GOOGLE_APPLICATION_CREDENTIALS"] = credential_path
    return bigquery.Client()

def run_query_batch(client: bigquery.Client, table_id: str, batch_size: int, offset: int) -> pd.DataFrame:
    query = f"SELECT * FROM `{table_id}` ORDER BY timestamp ASC LIMIT {batch_size} OFFSET {offset}"
    print(f"▶ Running query: OFFSET={offset}")
    start = time.time()
    df = client.query(query).to_dataframe()
    print(f"▶ Query finished in {time.time() - start:.2f}s")
    return df

def save_dataframe_to_csv(df: pd.DataFrame, output_dir: str, batch_index: int):
    tmp_path = os.path.join(output_dir, f"BigQuery{batch_index}.tmp")
    final_path = os.path.join(output_dir, f"BigQuery{batch_index}.csv")

    start = time.time()
    df.to_csv(tmp_path, index=False, encoding="utf-8")
    print(f"▶ CSV saved to temporary file in {time.time() - start:.2f}s")

    start = time.time()
    os.rename(tmp_path, final_path)
    print(f"▶ File renamed to {final_path} in {time.time() - start:.3f}s")

def main():
    start_time = time.time()

    if len(sys.argv) != 3:
        print("Usage: python ReadDataFromBigQuery.py <BigQueryCredentialFolder> <TableID>")
        sys.exit(1)

    output_folder = sys.argv[1]
    table_id = sys.argv[2]

    if not output_folder.endswith(os.sep):
        output_folder += os.sep

    credential_path = os.path.join(output_folder, "YourJsonFile.json")

    print(f"▶ Initializing BigQuery client...")
    client = setup_bigquery_client(credential_path)
    print(f"▶ BigQuery client ready")

    batch_size = 50000
    offset = 0
    batch_index = 0

    while True:
        loop_start = time.time()
        df = run_query_batch(client, table_id, batch_size, offset)

        if df.empty:
            print(f"▶ No more rows to process. Total batches: {batch_index}")
            break

        save_dataframe_to_csv(df, output_folder, batch_index)

        offset += batch_size
        batch_index += 1
        print(f"▶ Batch {batch_index - 1} complete (Elapsed: {time.time() - loop_start:.2f}s)\n")

    print(f"▶ All batches completed in {time.time() - start_time:.2f}s")

if __name__ == "__main__":
    main()