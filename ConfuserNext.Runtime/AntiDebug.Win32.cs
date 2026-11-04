using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace ConfuserNext.Runtime {
	internal static class AntiDebugWin32 {
		// See the equivalent comment in AntiDebug.Safe.cs: built one char at a time (individual
		// stelem assignments) rather than a C# array initializer, which would compile to a
		// reference into this assembly's own <PrivateImplementationDetails> type that does not
		// resolve once this method is cloned into a protected module.
		static string BuildCorProfilerName() {
			var c = new char[12];
			c[0] = 'C'; c[1] = 'O'; c[2] = 'R'; c[3] = '_'; c[4] = 'P';
			c[5] = 'R'; c[6] = 'O'; c[7] = 'F'; c[8] = 'I'; c[9] = 'L';
			c[10] = 'E'; c[11] = 'R';
			return new string(c);
		}

		static string BuildCorEnableProfilingName() {
			var c = new char[20];
			c[0] = 'C'; c[1] = 'O'; c[2] = 'R'; c[3] = '_'; c[4] = 'E';
			c[5] = 'N'; c[6] = 'A'; c[7] = 'B'; c[8] = 'L'; c[9] = 'E';
			c[10] = '_'; c[11] = 'P'; c[12] = 'R'; c[13] = 'O'; c[14] = 'F';
			c[15] = 'I'; c[16] = 'L'; c[17] = 'I'; c[18] = 'N'; c[19] = 'G';
			return new string(c);
		}

		static void Initialize() {
			if (Environment.GetEnvironmentVariable(BuildCorProfilerName()) != null ||
			    Environment.GetEnvironmentVariable(BuildCorEnableProfilingName()) != null)
				Environment.FailFast(null);

			var thread = new Thread(Worker);
			thread.IsBackground = true;
			thread.Start(null);
		}

		[DllImport("kernel32.dll")]
		static extern bool CloseHandle(IntPtr hObject);

		[DllImport("kernel32.dll")]
		static extern bool IsDebuggerPresent();

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		static extern int OutputDebugString(string str);

		static void Worker(object thread) {
			var th = thread as Thread;
			if (th == null) {
				th = new Thread(Worker);
				th.IsBackground = true;
				th.Start(Thread.CurrentThread);
				Thread.Sleep(500);
			}
			while (true) {
				// Managed
				if (Debugger.IsAttached || Debugger.IsLogging())
					Environment.FailFast("");

				// IsDebuggerPresent
				if (IsDebuggerPresent())
					Environment.FailFast("");

				// OpenProcess
				Process ps = Process.GetCurrentProcess();
				if (ps.Handle == IntPtr.Zero)
					Environment.FailFast("");
				ps.Close();

				// OutputDebugString
				if (OutputDebugString("") > IntPtr.Size)
					Environment.FailFast("");

				// CloseHandle
				try {
					CloseHandle(IntPtr.Zero);
				}
				catch {
					Environment.FailFast("");
				}

				if (!th.IsAlive)
					Environment.FailFast("");

				Thread.Sleep(1000);
			}
		}
	}
}
