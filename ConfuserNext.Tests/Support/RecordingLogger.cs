using System;
using System.Collections.Concurrent;
using ConfuserNext.Core;

namespace ConfuserNext.Tests.Support {
	/// <summary>
	///     Captures every log line instead of writing to the console, so tests can assert "no
	///     errors were logged" and print context on failure without flooding test output on success.
	/// </summary>
	public class RecordingLogger : ILogger {
		public readonly ConcurrentQueue<string> Errors = new ConcurrentQueue<string>();
		public readonly ConcurrentQueue<string> Warnings = new ConcurrentQueue<string>();
		public readonly ConcurrentQueue<string> All = new ConcurrentQueue<string>();
		public bool? Successful;

		public void Debug(string msg) => All.Enqueue("[DEBUG] " + msg);
		public void DebugFormat(string format, params object[] args) => Debug(string.Format(format, args));
		public void Info(string msg) => All.Enqueue("[INFO] " + msg);
		public void InfoFormat(string format, params object[] args) => Info(string.Format(format, args));
		public void Warn(string msg) { Warnings.Enqueue(msg); All.Enqueue("[WARN] " + msg); }
		public void WarnFormat(string format, params object[] args) => Warn(string.Format(format, args));
		public void WarnException(string msg, Exception ex) => Warn(msg + ": " + ex);
		public void Error(string msg) { Errors.Enqueue(msg); All.Enqueue("[ERROR] " + msg); }
		public void ErrorFormat(string format, params object[] args) => Error(string.Format(format, args));
		public void ErrorException(string msg, Exception ex) => Error(msg + ": " + ex);
		public void Progress(int progress, int overall) { }
		public void EndProgress() { }
		public void Finish(bool successful) => Successful = successful;

		public string Dump() => string.Join("\n", All);
	}
}
