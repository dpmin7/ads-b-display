import sys
import os
from google.cloud import bigquery

def main():
    if len(sys.argv) == 2:  
       global_filepath = sys.argv[1]+"\\"
       print(f"The first argument is: {global_filepath}")
    else:
       print(f"Failure 1\n")	
       os._exit(0)
    current_directory = os.getcwd()
    print(current_directory)
    # Set credentials
    api_key = global_filepath+"YourJsonFile.json"
    os.environ["GOOGLE_APPLICATION_CREDENTIALS"] = api_key

    # Construct a BigQuery client object.
    client = bigquery.Client()
    
    # 전체 데이터 삭제 쿼리 (TRUNCATE TABLE)
    table_id = "scs-lg-arch-5.SBS_Data.FirstRun"
    query = f"TRUNCATE TABLE `{table_id}`"

    print(f"Executing: {query}")
    try:
        query_job = client.query(query)
        query_job.result()  # 쿼리 완료까지 대기
        print(f"All data in table '{table_id}' has been deleted (TRUNCATE).")
    except Exception as e:
        print(f"Error while truncating table: {e}")
        sys.exit(2)

if __name__ == "__main__":
    main()