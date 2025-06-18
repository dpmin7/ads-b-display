using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace ADS_B_Display.Utils
{
    public static class EventBus
    {
        private class SubjectWrapper : IDisposable
        {
            private readonly Subject<object> _subject = new Subject<object>();
            private int _subscriptionCount = 0;
            private readonly string _key;

            public SubjectWrapper(string key)
            {
                _key = key;
            }

            public IObservable<object> GetObservable()
            {
                return new ObservableWithCount(this);
            }

            public void Publish(object evt)
            {
                _subject.OnNext(evt);
            }

            public void Dispose()
            {
                _subject.OnCompleted();
                _subject.Dispose();
            }

            private class ObservableWithCount : IObservable<object>
            {
                private readonly SubjectWrapper _wrapper;

                public ObservableWithCount(SubjectWrapper wrapper)
                {
                    _wrapper = wrapper;
                }

                public IDisposable Subscribe(IObserver<object> observer)
                {
                    var subscription = _wrapper._subject.Subscribe(observer);
                    System.Threading.Interlocked.Increment(ref _wrapper._subscriptionCount);

                    return new SubscriptionWrapper(_wrapper, subscription);
                }
            }

            private class SubscriptionWrapper : IDisposable
            {
                private readonly SubjectWrapper _wrapper;
                private readonly IDisposable _subscription;
                private bool _disposed = false;

                public SubscriptionWrapper(SubjectWrapper wrapper, IDisposable subscription)
                {
                    _wrapper = wrapper;
                    _subscription = subscription;
                }

                public void Dispose()
                {
                    if (!_disposed) {
                        _subscription.Dispose();

                        var count = System.Threading.Interlocked.Decrement(ref _wrapper._subscriptionCount);
                        if (count == 0) {
                            // 모든 구독이 해제됨 → EventBus에서 제거
                            EventBus.RemoveSubject(_wrapper._key);
                            _wrapper.Dispose();
                        }

                        _disposed = true;
                    }
                }
            }
        }

        // string key → SubjectWrapper 관리
        private static readonly ConcurrentDictionary<string, SubjectWrapper> _subjects = new ConcurrentDictionary<string, SubjectWrapper>();

        public static IObservable<object> Observe(string key)
        {
            var wrapper = _subjects.GetOrAdd(key, k => new SubjectWrapper(k));
            return wrapper.GetObservable();
        }

        public static void Publish(string key, object eventParam)
        {
            if (_subjects.TryGetValue(key, out var wrapper)) {
                wrapper.Publish(eventParam);
            }
        }

        internal static void RemoveSubject(string key)
        {
            _subjects.TryRemove(key, out _);
        }
    }

}
