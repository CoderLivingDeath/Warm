using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Warm.Project.Infrastructure.EventBus;
public class InputService : IDisposable
{
    private readonly InputSystem_Actions _inputActions;
    private readonly EventBus _eventBus;
    private readonly InputSubscribersContainer _subscribers;

    private bool _disposed;

    public InputService(EventBus eventBus, InputSystem_Actions inputActions, InputSubscribersContainer subscribers)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _subscribers = subscribers ?? throw new ArgumentNullException(nameof(subscribers));
        _inputActions = inputActions ?? throw new ArgumentNullException(nameof(inputActions));

        Enable();
    }

    public void Enable()
    {
        _inputActions.Enable();
        _subscribers.Enable();
    }

    public void Disable()
    {
        _inputActions.Disable();
        _subscribers.Disable();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _subscribers.Dispose();
        _inputActions.Dispose();

        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(InputService));
    }
}

public class InputSubscriber : IDisposable
{
    public enum CallType
    {
        OnStarted,
        OnPerformed,
        OnCanceled
    }

    public InputSubscriber(InputAction action, Action<InputAction.CallbackContext> callback, CallType type)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
        Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        Type = type;
    }

    public InputAction Action { get; }
    public Action<InputAction.CallbackContext> Callback { get; }
    public CallType Type { get; }

    public bool IsActive => _isActive;
    public bool IsDisposed { get; private set; }
    private bool _isActive;

    public void Enable()
    {
        ThrowIfDisposed();
        if (_isActive) return;

        switch (Type)
        {
            case CallType.OnStarted:
                Action.started += Callback;
                break;
            case CallType.OnPerformed:
                Action.performed += Callback;
                break;
            case CallType.OnCanceled:
                Action.canceled += Callback;
                break;
        }

        _isActive = true;
    }

    public void Disable()
    {
        ThrowIfDisposed();
        if (!_isActive) return;

        switch (Type)
        {
            case CallType.OnStarted:
                Action.started -= Callback;
                break;
            case CallType.OnPerformed:
                Action.performed -= Callback;
                break;
            case CallType.OnCanceled:
                Action.canceled -= Callback;
                break;
        }

        _isActive = false;
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        Disable();
        IsDisposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(InputSubscriber));
    }
}

/// <summary>
/// Контейнер для управления подписчиками на события InputAction.
/// Позволяет подписывать, отписывать и перечислять подписчиков.
/// </summary>
public class InputSubscribersContainer : IEnumerable<KeyValuePair<string, InputSubscriber>>, IDisposable
{

    private const string CALLTYPE_STARTED = "started";
    private const string CALLTYPE_PERFORMED = "performed";
    private const string CALLTYPE_CANCELED = "canceled";

    public bool IsActive => _isActive;
    private bool _isActive = false;

    public Dictionary<string, InputSubscriber> Subscribers => _subscribers;
    private readonly Dictionary<string, InputSubscriber> _subscribers = new();

    private bool _disposed;

    public InputSubscribersContainer() { }

    /// <summary>
    /// Получение или установка подписчика по ключу.
    /// При установке старый подписчик отписывается и заменяется на новый.
    /// </summary>
    public InputSubscriber this[string key]
    {
        get
        {
            if (TryFormatKey(key, out var formatedKey))
            {
                if (_subscribers.TryGetValue(formatedKey, out var value))
                    return value;

                throw new KeyNotFoundException($"No InputSubscriber with key '{formatedKey}'.");
            }
            else
            {
                throw new KeyNotFoundException($"Invalid key format '{key}'.");
            }
        }
        set
        {
            ThrowIfDisposed();
            if (TryFormatKey(key, out var formatedKey))
            {

                if (_subscribers.ContainsKey(formatedKey))
                {
                    _subscribers[formatedKey].Dispose();
                }
                _subscribers[formatedKey] = value ?? throw new ArgumentNullException(nameof(value));
            }
            else
            {
                throw new KeyNotFoundException($"Invalid key format '{key}'.");
            }
        }
    }

    public void Enable()
    {
        ThrowIfDisposed();
        if (IsActive) return;

        foreach (var item in _subscribers)
        {
            if (item.Value.IsActive) return;
            item.Value.Enable();
        }

        _isActive = true;
    }

    public void Disable()
    {
        ThrowIfDisposed();
        if (!IsActive) return;

        foreach (var item in _subscribers)
        {
            if (!item.Value.IsActive) return;
            item.Value.Disable();
        }

        _isActive = false;
    }
    public void Subscribe(string key, InputSubscriber subscriber)
    {
        ThrowIfDisposed();
        var formatedKey = FormatKey(key, subscriber.Type);

        if (_subscribers.ContainsKey(formatedKey)) throw new InvalidOperationException($"such a key already exists: {formatedKey}");
        if (subscriber == null)
            throw new ArgumentNullException(nameof(subscriber));

        // Если есть старый подписчик — освободить его ресурсы
        if (_subscribers.TryGetValue(formatedKey, out var oldSubscriber))
        {
            oldSubscriber.Dispose();
        }
        _subscribers[formatedKey] = subscriber;
    }

    /// <summary>
    /// Добавить одного подписчика.
    /// </summary>
    public void Subscribe(string key, InputAction action, Action<InputAction.CallbackContext> callback, InputSubscriber.CallType callType)
    {
        ThrowIfDisposed();
        var formatedKey = FormatKey(key, callType);

        if (_subscribers.ContainsKey(formatedKey)) throw new InvalidOperationException($"such a key already exists: {formatedKey}");
        if (callback == null) throw new ArgumentNullException(nameof(callback));

        var subscriber = new InputSubscriber(action, callback, callType);
        _subscribers.Add(formatedKey, subscriber);
    }

    public void SubscribeStarted(string key, InputAction action, Action<InputAction.CallbackContext> callback)
        => Subscribe(key, action, callback, InputSubscriber.CallType.OnStarted);

    public void SubscribePerformed(string key, InputAction action, Action<InputAction.CallbackContext> callback)
        => Subscribe(key, action, callback, InputSubscriber.CallType.OnPerformed);

    public void SubscribeCanceled(string key, InputAction action, Action<InputAction.CallbackContext> callback)
        => Subscribe(key, action, callback, InputSubscriber.CallType.OnCanceled);

    public void Unsubscribe(string key)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));

        if (_subscribers.TryGetValue(key, out var subscriber))
        {
            subscriber.Dispose();
            _subscribers.Remove(key);
        }
    }

    /// <summary>
    /// Отключить и удалить подписчика по ключу и (опционально) типу вызова.
    /// </summary>
    public void Unsubscribe(string key, InputSubscriber.CallType callType)
    {
        ThrowIfDisposed();

        var formatedKey = FormatKey(key, callType);
        if (_subscribers.TryGetValue(formatedKey, out var subscriber))
        {
            subscriber.Dispose();
            _subscribers.Remove(formatedKey);
        }

    }

    public void UnsubscribeStarted(string key) => Unsubscribe(key, InputSubscriber.CallType.OnStarted);
    public void UnsubscribePerformed(string key) => Unsubscribe(key, InputSubscriber.CallType.OnPerformed);
    public void UnsubscribeCanceled(string key) => Unsubscribe(key, InputSubscriber.CallType.OnCanceled);

    private string FormatKey(string name, InputSubscriber.CallType callType)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));

        string typeStr = callType switch
        {
            InputSubscriber.CallType.OnStarted => CALLTYPE_STARTED,
            InputSubscriber.CallType.OnPerformed => CALLTYPE_PERFORMED,
            InputSubscriber.CallType.OnCanceled => CALLTYPE_CANCELED,
            _ => throw new ArgumentOutOfRangeException(nameof(callType))
        };

        // Удаляем пробелы, приводим все к нижнему регистру и соединяем
        string namePart = name.Trim().ToLowerInvariant().Replace(" ", "");
        return $"{namePart}/{typeStr}";
    }

    /// <summary>
    /// Приводит строку к формату "название/тип_вызова" (без пробелов, нижний регистр, разделитель '/')
    /// и проверяет, соответствует ли она этому формату.
    /// </summary>
    /// <param name="input">Входная строка.</param>
    /// <param name="result">Преобразованная строка в требуемом формате, если успешно.</param>
    /// <returns>True, если строка приведена к виду "название/тип_вызова", иначе false.</returns>
    public static bool TryFormatKey(string input, out string result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(input) || !input.Contains('/'))
            return false;

        // Приводим к нижнему регистру и убираем пробелы по краям
        string formatted = input.Trim().ToLowerInvariant();

        // Разбиваем по символу '/'
        var parts = formatted.Split('/');
        if (parts.Length != 2)
            return false;

        string name = parts[0].Trim();
        string type = parts[1].Trim();

        // Имя и тип не должны быть пустыми и не содержать пробелов
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(type)
            || name.Contains(' ') || type.Contains(' '))
            return false;

        // Собираем обратно для единообразия
        result = $"{name}/{type}";
        return true;
    }

    /// <summary>
    /// Освобождает все ресурсы всех подписчиков и очищает контейнер.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        foreach (var subscriber in _subscribers.Values)
        {
            subscriber.Dispose();
        }
        _subscribers.Clear();
        _disposed = true;
    }

    /// <summary>
    /// Реализация IEnumerable для перебора подписчиков.
    /// </summary>
    public IEnumerator<KeyValuePair<string, InputSubscriber>> GetEnumerator()
    {
        return _subscribers.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(InputSubscribersContainer));
    }
}