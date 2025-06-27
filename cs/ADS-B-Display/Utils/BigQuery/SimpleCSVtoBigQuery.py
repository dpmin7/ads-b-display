import os
import sys
import winsound
from google.cloud import bigquery


def read_csv_file(filepath: str, table_id: str, client: bigquery.Client, job_config: bigquery.LoadJobConfig) -> bool:
    try:
        print(f"Reading file: {filepath}")
        with open(filepath, "rb") as source_file:
            job = client.load_table_from_file(source_file, table_id, job_config=job_config)
        job.result()  # Waits for the job to complete

        os.remove(filepath)
        print(f"File '{filepath}' deleted successfully.")
        return True

    except Exception as e:
        print(f"Error processing file '{filepath}': {e}")
        return False


def main():
    if len(sys.argv) != 4:
        print("Usage: python script.py <CredentialFolder> <Filename> <TableID>")
        sys.exit(1)

    credential_folder = sys.argv[1]
    filename = sys.argv[2]
    table_id = sys.argv[3]

    filepath = os.path.join(credential_folder, filename)
    credential_path = os.path.join(credential_folder, "YourJsonFile.json")

    print(f"Credential Path: {credential_path}")
    print(f"CSV File: {filepath}")
    print(f"BigQuery Table ID: {table_id}")

    os.environ["GOOGLE_APPLICATION_CREDENTIALS"] = credential_path

    client = bigquery.Client()

    job_config = bigquery.LoadJobConfig(
        source_format=bigquery.SourceFormat.CSV,
        autodetect=True,
        skip_leading_rows=1,
        write_disposition=bigquery.WriteDisposition.WRITE_APPEND
    )

    success = read_csv_file(filepath, table_id, client, job_config)

    if success:
        print("CSV upload successful.")
        winsound.Beep(2500, 1000)
    else:
        print("CSV upload failed.")
        sys.exit(2)


if __name__ == "__main__":
    main()