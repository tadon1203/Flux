using System;
using System.Diagnostics;
using System.Reflection;
using BepInEx.Logging;

namespace Flux.Core;

public static class Logger
{
    private static ManualLogSource _log;

    public static void Initialize(ManualLogSource logSource)
    {
        _log = logSource;
    }

    public static void Info(object message)
    {
        LogInternal(message, _log.LogInfo);
    }

    public static void Debug(object message)
    {
        LogInternal(message, _log.LogDebug);
    }

    public static void Warning(object message)
    {
        LogInternal(message, _log.LogWarning);
    }

    public static void Error(object message)
    {
        LogInternal(message, _log.LogError);
    }

    /// <summary>
    ///     Internal logging method that formats the message with caller info.
    /// </summary>
    private static void LogInternal(object message, Action<object> logAction)
    {
        if (_log == null || logAction == null)
            return;

        // GetFrame(2) to get the caller of Info/Warning/Error, not LogInternal itself.
        StackFrame frame = new StackTrace().GetFrame(2);

        string finalMessage;
        if (frame != null)
        {
            MethodBase method = frame.GetMethod();
            string className = method.DeclaringType?.Name ?? "UnknownClass";
            string methodName = method.Name;

            finalMessage = $"[{className}::{methodName}] {message}";
        }
        else
        {
            finalMessage = message.ToString();
        }

        logAction(finalMessage);
    }
}