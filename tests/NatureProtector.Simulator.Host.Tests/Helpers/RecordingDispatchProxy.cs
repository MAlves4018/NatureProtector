using System.Reflection;

namespace NatureProtector.Simulator.Host.Tests.Helpers;

internal class RecordingDispatchProxy<T> : DispatchProxy
    where T : class
{
    public Dictionary<string, object?> ReturnValues { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, object?> Properties { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Action<IReadOnlyList<object?>>> Callbacks { get; } = new(StringComparer.Ordinal);
    public List<InvocationRecord> Invocations { get; } = [];
    private readonly Dictionary<string, List<Delegate>> _eventHandlers = new(StringComparer.Ordinal);

    public static (T Proxy, RecordingDispatchProxy<T> Recorder) CreateProxy()
    {
        var proxy = DispatchProxy.Create<T, RecordingDispatchProxy<T>>();
        return (proxy, (RecordingDispatchProxy<T>)(object)proxy);
    }

    public void RaiseEvent<TEventArgs>(string eventName, object? sender, TEventArgs args)
        where TEventArgs : EventArgs
    {
        if (!_eventHandlers.TryGetValue(eventName, out var handlers))
        {
            return;
        }

        foreach (var handler in handlers.ToArray())
        {
            handler.DynamicInvoke(sender, args);
        }
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        var invocationArgs = args?.ToArray() ?? [];
        Invocations.Add(new InvocationRecord(targetMethod.Name, invocationArgs));
        if (Callbacks.TryGetValue(targetMethod.Name, out var callback))
        {
            callback(invocationArgs);
        }

        if (targetMethod.Name.StartsWith("set_", StringComparison.Ordinal))
        {
            Properties[targetMethod.Name[4..]] = invocationArgs[0];
            return null;
        }

        if (targetMethod.Name.StartsWith("add_", StringComparison.Ordinal) &&
            invocationArgs.FirstOrDefault() is Delegate addedHandler)
        {
            var eventName = targetMethod.Name[4..];
            if (!_eventHandlers.TryGetValue(eventName, out var handlers))
            {
                handlers = [];
                _eventHandlers[eventName] = handlers;
            }

            handlers.Add(addedHandler);
            return null;
        }

        if (targetMethod.Name.StartsWith("remove_", StringComparison.Ordinal) &&
            invocationArgs.FirstOrDefault() is Delegate removedHandler)
        {
            var eventName = targetMethod.Name[7..];
            if (_eventHandlers.TryGetValue(eventName, out var handlers))
            {
                handlers.Remove(removedHandler);
            }

            return null;
        }

        if (targetMethod.Name.StartsWith("get_", StringComparison.Ordinal))
        {
            var propertyName = targetMethod.Name[4..];

            if (Properties.TryGetValue(propertyName, out var propertyValue))
            {
                return propertyValue;
            }

            if (ReturnValues.TryGetValue(targetMethod.Name, out var getterValue))
            {
                return getterValue;
            }

            if (ReturnValues.TryGetValue(propertyName, out var configuredPropertyValue))
            {
                return configuredPropertyValue;
            }
        }

        if (ReturnValues.TryGetValue(targetMethod.Name, out var configuredValue))
        {
            return configuredValue;
        }

        return GetDefaultValue(targetMethod.ReturnType);
    }

    private static object? GetDefaultValue(Type returnType)
    {
        if (returnType == typeof(void))
        {
            return null;
        }

        if (returnType == typeof(Task))
        {
            return Task.CompletedTask;
        }

        return returnType.IsValueType
            ? Activator.CreateInstance(returnType)
            : null;
    }

    internal sealed record InvocationRecord(string MethodName, IReadOnlyList<object?> Arguments);
}
