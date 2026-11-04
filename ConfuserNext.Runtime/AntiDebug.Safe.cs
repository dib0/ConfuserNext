using System;
using System.Diagnostics;
using System.Threading;

namespace ConfuserNext.Runtime {
	internal static class AntiDebugSafe {
		// Builds a string from char codes at runtime instead of a single IL "ldstr" literal.
		// Avoids leaving an exact-match string (e.g. "ENABLE_PROFILING", "GetEnvironmentVariable")
		// in metadata that a static pattern-matching unpacker can search for directly.
		static string Decode(params int[] codes) {
			var chars = new char[codes.Length];
			for (int i = 0; i < codes.Length; i++)
				chars[i] = (char)codes[i];
			return new string(chars);
		}

		static void Initialize() {
			string envVar = Decode('C', 'O', 'R', '_', 'E', 'N', 'A', 'B', 'L', 'E', '_', 'P', 'R', 'O', 'F', 'I', 'L', 'I', 'N', 'G');
			string getEnvVarMethod = Decode('G', 'e', 't', 'E', 'n', 'v', 'i', 'r', 'o', 'n', 'm', 'e', 'n', 't', 'V', 'a', 'r', 'i', 'a', 'b', 'l', 'e');
			var env = typeof(Environment);
			var method = env.GetMethod(getEnvVarMethod, new[] { typeof(string) });
			if (method != null &&
			    "1".Equals(method.Invoke(null, new object[] { envVar })))
				Environment.FailFast(null);

			var thread = new Thread(Worker);
			thread.IsBackground = true;
			thread.Start(null);
		}

		static void Worker(object thread) {
			var th = thread as Thread;
			if (th == null) {
				th = new Thread(Worker);
				th.IsBackground = true;
				th.Start(Thread.CurrentThread);
				Thread.Sleep(500);
			}
			while (true) {
				if (Debugger.IsAttached || Debugger.IsLogging())
					Environment.FailFast(null);

				if (!th.IsAlive)
					Environment.FailFast(null);

				Thread.Sleep(1000);
			}
		}
	}
}