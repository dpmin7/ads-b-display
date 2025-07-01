using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ADS_B_Display.Map.MapSrc
{
    public abstract class SimpleTileStorage : ITileStorage, IDisposable
    {
        private readonly BlockingCollection<Tile> _queue = new BlockingCollection<Tile>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Task _worker;

        protected SimpleTileStorage()
        {
            _worker = Task.Run(() => WorkerLoop(_cts.Token));
        }

        private async Task WorkerLoop(CancellationToken token)
        {
            try
            {
                foreach (var tile in _queue.GetConsumingEnumerable(token))
                {
                    if (token.IsCancellationRequested) break;

                    // ✨ --- 최종 수정된 로직 --- ✨

                    // 1. 작업을 시작하기 전, 타일의 로드 상태를 미리 저장합니다.
                    bool wasAlreadyLoaded = tile.IsLoaded;

                    // 2. 실제 로딩 또는 저장 작업을 처리합니다.
                    try
                    {
                        await Process(tile);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine($"Error processing tile {tile.X},{tile.Y},{tile.Level}: {e}");
                    }

                    // 3. 작업 후 상태에 따라 후속 작업을 딱 한 번만 수행하여 무한 루프를 방지합니다.
                    if (!wasAlreadyLoaded && tile.IsLoaded)
                    {
                        // Case A: 로딩에 성공한 경우 (예: 인터넷에서 다운로드 완료)
                        // -> 이제 이 타일을 저장 큐로 보냅니다.
                        SaveStorage?.Enqueue(tile);
                    }
                    else if (!wasAlreadyLoaded && !tile.IsLoaded)
                    {
                        // Case B: 로딩에 실패한 경우 (예: 디스크에도 없고, 다음 소스가 있다면)
                        // -> 다음 로더(인터넷)의 큐로 보냅니다.
                        NextLoadStorage?.Enqueue(tile);
                    }
                    // Case C: 이미 로드된 상태로 큐에 들어온 경우 (예: 저장 큐에서 온 타일)
                    // -> 모든 작업이 끝났으므로 아무것도 하지 않고 루프를 종료하여 타일을 해제합니다.
                }
            }
            catch (OperationCanceledException) { }
        }

        protected abstract Task Process(Tile tile);

        public void Enqueue(Tile tile) => _queue.Add(tile);
        protected ITileStorage NextLoadStorage { get; private set; }
        protected ITileStorage SaveStorage { get; private set; }
        public void SetNextLoadStorage(ITileStorage next) => NextLoadStorage = next;
        public void SetSaveStorage(ITileStorage save) => SaveStorage = save;
        public void Detach() { NextLoadStorage = null; SaveStorage = null; }

        public void Dispose()
        {
            _cts.Cancel();
            _queue.CompleteAdding();
            _worker.Wait();
            _cts.Dispose();
            _queue.Dispose();
        }
    }
}
