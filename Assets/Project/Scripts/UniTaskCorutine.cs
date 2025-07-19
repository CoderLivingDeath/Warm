using Cysharp.Threading.Tasks;
using System;
using System.Threading;

public class UniTaskCoroutine : IDisposable
{
    private CancellationTokenSource _cts;
    private Func<CancellationToken, UniTask> _asyncDelegate;
    private UniTask _runningTask = UniTask.CompletedTask;

    public bool IsRunning { get; private set; }

    private bool _disposed;

    public UniTaskCoroutine(Func<CancellationToken, UniTask> asyncDelegate)
    {
        _asyncDelegate = asyncDelegate ?? throw new ArgumentNullException(nameof(asyncDelegate));
    }

    public void Run()
    {
        ThrowIfDisposed();
        if (IsRunning)
            return;

        _cts = new CancellationTokenSource();
        IsRunning = true;
        _runningTask = RunInternalAsync();
    }

    public async UniTask RunAsync()
    {
        ThrowIfDisposed();
        if (IsRunning)
            return;

        _cts = new CancellationTokenSource();
        IsRunning = true;
        _runningTask = RunInternalAsync();
        await _runningTask;
    }

    private async UniTask RunInternalAsync()
    {
        try
        {
            await _asyncDelegate.Invoke(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Ожидаемая отмена
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogException(ex);
            throw;
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public async UniTask StopAsync()
    {
        ThrowIfDisposed();
        if (!IsRunning)
            return;
        _cts?.Cancel();
        try
        {
            await _runningTask;
        }
        catch (OperationCanceledException)
        {
            // Игнорируем отмену
        }
    }

    public void Stop()
    {
        ThrowIfDisposed();
        if (!IsRunning)
            return;
        _cts?.Cancel();
    }

    /// <summary>
    /// Освобождает ресурсы, отменяет корутину, и ожидает завершения задачи при необходимости.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UniTaskCoroutine));
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            if (IsRunning)
            {
                // Безопасно отменяем выполнение
                try
                {
                    _cts?.Cancel();
                }
                catch { }
            }
            _cts?.Dispose();
            _cts = null;
        }

        _disposed = true;
    }

    ~UniTaskCoroutine()
    {
        Dispose(false);
    }
}

public class UniTaskCoroutine<TArg> : IDisposable
{
    private CancellationTokenSource _cts;
    private Func<TArg, CancellationToken, UniTask> _asyncDelegate;
    private UniTask _runningTask = UniTask.CompletedTask;

    private bool _disposed;

    public bool IsRunning { get; private set; }

    public UniTaskCoroutine(Func<TArg, CancellationToken, UniTask> asyncDelegate)
    {
        _asyncDelegate = asyncDelegate ?? throw new ArgumentNullException(nameof(asyncDelegate));
    }

    public void Run(TArg arg)
    {
        ThrowIfDisposed();
        if (IsRunning)
            return;

        _cts = new CancellationTokenSource();
        IsRunning = true;
        _runningTask = RunInternalAsync(arg);
    }

    public async UniTask RunAsync(TArg arg)
    {
        ThrowIfDisposed();
        if (IsRunning)
            return;

        _cts = new CancellationTokenSource();
        IsRunning = true;
        _runningTask = RunInternalAsync(arg);
        await _runningTask;
    }

    private async UniTask RunInternalAsync(TArg arg)
    {
        try
        {
            await _asyncDelegate.Invoke(arg, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Нормальная отмена
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogException(ex);
            throw;
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public async UniTask StopAsync()
    {
        if (!IsRunning)
            return;
        _cts?.Cancel();
        try
        {
            await _runningTask;
        }
        catch (OperationCanceledException)
        {
            // Ожидаемая отмена
        }
    }

    public void Stop()
    {
        if (!IsRunning)
            return;
        _cts?.Cancel();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            if (IsRunning)
            {
                try
                {
                    _cts?.Cancel();
                }
                catch { }
            }
            _cts?.Dispose();
            _cts = null;
        }

        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UniTaskCoroutine<TArg>));
    }

    ~UniTaskCoroutine()
    {
        Dispose(false);
    }
}