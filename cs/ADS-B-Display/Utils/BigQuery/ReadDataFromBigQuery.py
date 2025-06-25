import sys
import os
import time
import pandas as pd
from google.cloud import bigquery

def main():
    global_start = time.time()

    if len(sys.argv) == 2:
        output_folder = sys.argv[1]

        if not output_folder.endswith("\\") and not output_folder.endswith("/"):
            output_folder += "\\"

        print(f"[{time.time() - global_start:.2f}s] ▶ 인자 처리 완료: {output_folder}")
    else:
        print("Usage: python ReadDataFromBigQuery.py <BigQueryCredentialFolder>")
        os._exit(0)

    # 인증 및 클라이언트 생성
    start = time.time()
    os.environ["GOOGLE_APPLICATION_CREDENTIALS"] = output_folder + "YourJsonFile.json"
    client = bigquery.Client()
    print(f"[{time.time() - global_start:.2f}s] ▶ BigQuery Client 생성 완료 (소요: {time.time() - start:.2f}s)")

    table_id = "scs-lg-arch-5.SBS_Data.FirstRun"
    batch_size = 50000
    offset = 0
    file_count = 0

    while True:
        loop_start = time.time()

        query = f"SELECT * FROM `{table_id}` ORDER BY timestamp ASC LIMIT {batch_size} OFFSET {offset}"
        print(f"[{time.time() - global_start:.2f}s] ▶ 쿼리 실행 시작: {query}")

        start = time.time()
        query_job = client.query(query)
        df = query_job.to_dataframe()
        query_duration = time.time() - start
        print(f"[{time.time() - global_start:.2f}s] ▶ 쿼리 및 DataFrame 변환 완료 (소요: {query_duration:.2f}s)")

        if df.empty:
            print(f"[{time.time() - global_start:.2f}s] ▶ 더 이상 데이터 없음. 종료.")
            break

        tmp_file = os.path.join(output_folder, f"BigQuery{file_count}.tmp")
        final_file = os.path.join(output_folder, f"BigQuery{file_count}.csv")

        start = time.time()
        df.to_csv(tmp_file, index=False, encoding="utf-8")
        csv_write_duration = time.time() - start
        print(f"[{time.time() - global_start:.2f}s] ▶ CSV 임시 저장 완료 (소요: {csv_write_duration:.2f}s)")

        start = time.time()
        os.rename(tmp_file, final_file)
        rename_duration = time.time() - start
        print(f"[{time.time() - global_start:.2f}s] ▶ 파일 리네임 완료 (소요: {rename_duration:.3f}s)")

        print(f"[{time.time() - global_start:.2f}s] ▶ Batch {file_count} 완료. 총 소요: {time.time() - loop_start:.2f}s\n")

        offset += batch_size
        file_count += 1

    print(f"[{time.time() - global_start:.2f}s] ▶ 전체 작업 완료.")

if __name__ == "__main__":
    main()
