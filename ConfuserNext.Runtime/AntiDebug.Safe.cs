using System;
using System.Diagnostics;
using System.Threading;

namespace ConfuserNext.Runtime {
	internal static class AntiDebugSafe {
		// Builds a string one char at a time (individual stelem assignments) instead of a single
		// IL "ldstr" literal or a C#-array-initializer-with-InitializeArray (the latter would
		// compile to a reference into this assembly's own <PrivateImplementationDetails> type,
		// which does not resolve once this method is cloned into a protected module). Avoids
		// leaving an exact-match string (e.g. "ENABLE_PROFILING", "GetEnvironmentVariable") in
		// metadata that a static pattern-matching unpacker can search for directly.
		static string BuildEnvVarName() {
			var c = new char[20];
			c[0] = 'C'; c[1] = 'O'; c[2] = 'R'; c[3] = '_'; c[4] = 'E';
			c[5] = 'N'; c[6] = 'A'; c[7] = 'B'; c[8] = 'L'; c[9] = 'E';
			c[10] = '_'; c[11] = 'P'; c[12] = 'R'; c[13] = 'O'; c[14] = 'F';
			c[15] = 'I'; c[16] = 'L'; c[17] = 'I'; c[18] = 'N'; c[19] = 'G';
			return new string(c);
		}

		static string BuildGetEnvVarMethodName() {
			var c = new char[22];
			c[0] = 'G'; c[1] = 'e'; c[2] = 't'; c[3] = 'E'; c[4] = 'n';
			c[5] = 'v'; c[6] = 'i'; c[7] = 'r'; c[8] = 'o'; c[9] = 'n';
			c[10] = 'm'; c[11] = 'e'; c[12] = 'n'; c[13] = 't'; c[14] = 'V';
			c[15] = 'a'; c[16] = 'r'; c[17] = 'i'; c[18] = 'a'; c[19] = 'b';
			c[20] = 'l'; c[21] = 'e';
			return new string(c);
		}

		static void Initialize() {
			string envVar = BuildEnvVarName();
			string getEnvVarMethod = BuildGetEnvVarMethodName();
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