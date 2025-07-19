using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

public class UniTaskCoroutineContainer : IDisposable
{
    private readonly List<UniTaskCoroutine> _coroutines = new List<UniTaskCoroutine>();
    private readonly DisposerContainer _disposer = new DisposerContainer();
    private bool _disposed;

    /// <summary>
    /// ��������� �������� � ��������� � ������������ ��� ��������������� Dispose.
    /// </summary>
    public void Add(UniTaskCoroutine coroutine)
    {
        ThrowIfDisposed();
        if (coroutine == null) throw new ArgumentNullException(nameof(coroutine));
        _coroutines.Add(coroutine);
        _disposer.Add(coroutine);
    }

    /// <summary>
    /// ��������� ��� ������������������ ��������.
    /// </summary>
    public void RunAll()
    {
        ThrowIfDisposed();
        foreach (var coroutine in _coroutines)
            coroutine.Run();
    }

    /// <summary>
    /// ���������� ��������� ��� ������������������ �������� � ��� �� ����������.
    /// </summary>
    public async UniTask RunAllAsync()
    {
        ThrowIfDisposed();
        var tasks = new List<UniTask>();
        foreach (var coroutine in _coroutines)
            tasks.Add(coroutine.RunAsync());
        await UniTask.WhenAll(tasks);
    }

    /// <summary>
    /// ������������� ��� ������������������ ��������.
    /// </summary>
    public void StopAll()
    {
        ThrowIfDisposed();
        foreach (var coroutine in _coroutines)
            coroutine.Stop();
    }

    /// <summary>
    /// ���������� ������������� ��� ������������������ �������� � ��� �� ����������.
    /// </summary>
    public async UniTask StopAllAsync()
    {
        ThrowIfDisposed();
        var tasks = new List<UniTask>();
        foreach (var coroutine in _coroutines)
            tasks.Add(coroutine.StopAsync());
        await UniTask.WhenAll(tasks);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // ����������� ��������� ���� ������� ����� ������������� ��������
            try
            {
                StopAll();
            }
            catch { /* ���������� ������ ��� ��������� */ }

            _disposer.Dispose();
            _coroutines.Clear();
        }
        _disposed = true;
    }
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(UniTaskCoroutineContainer));
    }

    ~UniTaskCoroutineContainer()
    {
        Dispose(false);
    }
}
public class UniTaskCoroutineContainer<TArg> : IDisposable
{
    private readonly IList<UniTaskCoroutine<TArg>> _coroutines = new List<UniTaskCoroutine<TArg>>();
    private readonly DisposerContainer _disposer = new DisposerContainer();
    private bool _disposed;

    /// <summary>
    /// ��������� �������� � ��������� � ������������ ��� ��������������� Dispose.
    /// </summary>
    public void Add(UniTaskCoroutine<TArg> coroutine)
    {
        ThrowIfDisposed();
        if (coroutine == null) throw new ArgumentNullException(nameof(coroutine));
        _coroutines.Add(coroutine);
        _disposer.Add(coroutine);
    }

    /// <summary>
    /// ��������� ��� �������� � ���������� ����������.
    /// </summary>
    public void RunAll(TArg arg)
    {
        ThrowIfDisposed();
        foreach (var coroutine in _coroutines)
            coroutine.Run(arg);
    }

    /// <summary>
    /// ���������� ��������� ��� �������� � ���������� ���������� � ��� ���������� ������� ������.
    /// </summary>
    public async UniTask RunAllAsync(TArg arg)
    {
        ThrowIfDisposed();
        var tasks = new List<UniTask>();
        foreach (var coroutine in _coroutines)
            tasks.Add(coroutine.RunAsync(arg));
        await UniTask.WhenAll(tasks);
    }

    /// <summary>
    /// ������������� ��� ��������.
    /// </summary>
    public void StopAll()
    {
        ThrowIfDisposed();
        foreach (var coroutine in _coroutines)
            coroutine.Stop();
    }

    /// <summary>
    /// ���������� ������������� ��� �������� � ��� ����������.
    /// </summary>
    public async UniTask StopAllAsync()
    {
        ThrowIfDisposed();
        var tasks = new List<UniTask>();
        foreach (var coroutine in _coroutines)
            tasks.Add(coroutine.StopAsync());
        await UniTask.WhenAll(tasks);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            try { StopAll(); } catch { }
            _disposer.Dispose();
            _coroutines.Clear();
        }
        _disposed = true;
    }
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(UniTaskCoroutineContainer<TArg>));
    }

    ~UniTaskCoroutineContainer()
    {
        Dispose(false);
    }

}
