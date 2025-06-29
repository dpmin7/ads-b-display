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

                    // ✨ 수정된 로직 ✨
                    // 타일이 오래되었는지 여부를 Process() 호출 직전에만 확인합니다.
                    if (tile.IsOld)
                    {
                        // 오래된 타일은 로딩할 필요가 없으므로 그냥 건너뜁니다.
                        // 이 시점에서 쿼드트리 참조는 이미 끊어졌고, 큐에서도 제거되었으므로
                        // 다른 곳에 참조가 없다면 GC가 수거해 갈 것입니다.
                        continue;
                    }

                    // 이제 이 타일은 '오래되지 않은 것'이 확실하므로 로딩을 진행합니다.
                    try
                    {
                        await Process(tile);
                    }
                    catch (Exception e) { Console.Error.WriteLine($"Error: {e}"); }

                    // 로딩 후의 후처리 로직은 그대로 둡니다.
                    if (tile.IsLoaded && tile.IsSaveable)
                        SaveStorage?.Enqueue(tile);
                    else
                        NextLoadStorage?.Enqueue(tile);
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
