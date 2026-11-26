// "Send feedback" (Settings): there is no feedback server, so the player's note plus a technical
// report (build, device, OS, graphics, the last errors) is copied to the clipboard to paste to the
// developer, and also written to <persistentDataPath>/feedback.txt. Errors and exceptions are kept in a
// small ring buffer from game start.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sigilbound.Client
{
    public static class Feedback
    {
        const int Keep = 20;
        static readonly LinkedList<string> _errors = new LinkedList<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook() { Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog; }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            var line = $"[{DateTime.Now:HH:mm:ss}] {type}: {msg}";
            if (type == LogType.Exception && !string.IsNullOrEmpty(stack)) line += "\n    " + stack.Split('\n')[0].Trim();
            lock (_errors) { _errors.AddLast(line.Length > 600 ? line.Substring(0, 600) : line); while (_errors.Count > Keep) _errors.RemoveFirst(); }
        }

        public static int ErrorCount { get { lock (_errors) return _errors.Count; } }

        public static string Report(string note, bool withErrors)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Sigilbound feedback");
            sb.AppendLine($"When: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Build: {(Updater.MyBuild > 0 ? Updater.MyBuild.ToString() : "dev")} ({Application.version})");
            sb.AppendLine($"Device: {SystemInfo.deviceModel} · {SystemInfo.operatingSystem}");
            sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName} · RAM {SystemInfo.systemMemorySize} MB · screen {Screen.width}x{Screen.height}");
            if (Quality.I != null) sb.AppendLine($"Graphics: {Quality.I.TierName} · fps {Quality.I.FpsMode} · display {Quality.DisplayHz} Hz");
            sb.AppendLine();
            sb.AppendLine(string.IsNullOrWhiteSpace(note) ? "(no message)" : note.Trim());
            if (withErrors)
            {
                sb.AppendLine();
                lock (_errors)
                {
                    sb.AppendLine(_errors.Count == 0 ? "Recent errors: none" : $"Recent errors ({_errors.Count}):");
                    foreach (var e in _errors) sb.AppendLine(e);
                }
            }
            return sb.ToString();
        }

        // Copies the report and saves it to feedback.txt; returns where it was saved (or null).
        public static string Send(string note, bool withErrors)
        {
            var text = Report(note, withErrors);
            GUIUtility.systemCopyBuffer = text;
            try { var p = System.IO.Path.Combine(Application.persistentDataPath, "feedback.txt"); System.IO.File.WriteAllText(p, text); return p; }
            catch (Exception) { return null; }
        }
    }
}
