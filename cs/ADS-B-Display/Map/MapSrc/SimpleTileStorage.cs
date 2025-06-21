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
            try {
                foreach (var tile in _queue.GetConsumingEnumerable(token)) {
                    if (token.IsCancellationRequested) break;
                    if (!tile.IsOld) {
                        try { 
                            await Process(tile);
                        } catch (Exception e) { Console.Error.WriteLine($"Error: {e}"); }
                         if (tile.IsLoaded && tile.IsSaveable)
                            SaveStorage?.Enqueue(tile);
                        else
                            NextLoadStorage?.Enqueue(tile);
                    }
                }
            } catch (OperationCanceledException) { }
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
