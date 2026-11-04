using System;
using System.Collections.Generic;
using System.IO;
using ConfuserNext.Core;
using ConfuserNext.Core.Project;

namespace ConfuserNext.Tests.Support {
	/// <summary>
	///     A protection setting to apply within a test's single global rule: the protection id plus
	///     optional "name=value" arguments (matching a &lt;protection id="..."&gt;&lt;argument .../&gt;
	///     element in a .crproj).
	/// </summary>
	public sealed class ProtectionSpec {
		public ProtectionSpec(string id, params (string name, string value)[] arguments) {
			Id = id;
			Arguments = arguments;
		}

		public string Id { get; }
		public (string name, string value)[] Arguments { get; }
	}

	/// <summary>
	///     Drives <see cref="ConfuserEngine" /> in-process (no CLI/subprocess involved) against a
	///     throwaway module on disk, the same way ConfuserNext.CLI does when given a project file.
	///     Running in-process means code coverage tooling sees everything the engine actually
	///     executes, not just the harness's own glue code.
	/// </summary>
	public sealed class ObfuscationResult {
		public RecordingLogger Logger { get; init; }
		public string OutputModulePath { get; init; }
		public byte[] OutputModuleBytes { get; init; }
		public string TempDirectory { get; init; }
	}

	public static class ObfuscationHarness {
		/// <summary>
		///     Compiles <paramref name="source" /> (see <see cref="SampleAssemblyFactory" />),
		///     protects it with a single global rule (preset "none", pattern "true") containing
		///     <paramref name="protections" />, and returns the result. Throws if the engine logs
		///     any error-level message, so a silently-swallowed failure never passes a test.
		/// </summary>
		public static ObfuscationResult Protect(
			IEnumerable<ProtectionSpec> protections,
			string source = null,
			string packerId = null,
			string embeddedResourceName = null,
			string embeddedResourceText = null,
			string seed = "unit-test-seed",
			Action<ConfuserProject> configureProject = null) {
			string tempDir = Path.Combine(Path.GetTempPath(), "cx-tests-" + Guid.NewGuid().ToString("N"));
			string srcDir = Path.Combine(tempDir, "src");
			string outDir = Path.Combine(tempDir, "out");
			Directory.CreateDirectory(srcDir);
			Directory.CreateDirectory(outDir);

			string modulePath = Path.Combine(srcDir, "Sample.dll");
			SampleAssemblyFactory.CompileToFile(
				source ?? SampleAssemblyFactory.DefaultProgramSource,
				modulePath,
				"Sample",
				embeddedResourceName,
				embeddedResourceText);

			var rule = new Rule(pattern: "true", preset: ProtectionPreset.None, inherit: true);
			foreach (ProtectionSpec spec in protections) {
				var item = new SettingItem<Protection>(spec.Id);
				foreach ((string name, string value) in spec.Arguments)
					item[name] = value;
				rule.Add(item);
			}

			var project = new ConfuserProject {
				BaseDirectory = srcDir,
				OutputDirectory = outDir,
				Seed = seed
			};
			project.Rules.Add(rule);
			project.Add(new ProjectModule { Path = "Sample.dll" });
			if (packerId != null)
				project.Packer = new SettingItem<Packer>(packerId);

			configureProject?.Invoke(project);

			var logger = new RecordingLogger();
			ConfuserEngine.Run(new ConfuserParameters {
				Project = project,
				Logger = logger
			}).Wait();

			if (logger.Successful != true) {
				throw new InvalidOperationException(
					"ConfuserEngine did not finish successfully:\n" + logger.Dump());
			}
			if (!logger.Errors.IsEmpty) {
				throw new InvalidOperationException(
					"ConfuserEngine logged errors:\n" + logger.Dump());
			}

			string outputPath = Path.Combine(outDir, "Sample.dll");
			byte[] outputBytes = File.ReadAllBytes(outputPath);

			return new ObfuscationResult {
				Logger = logger,
				OutputModulePath = outputPath,
				OutputModuleBytes = outputBytes,
				TempDirectory = tempDir
			};
		}

		public static void Cleanup(ObfuscationResult result) {
			try {
				if (result?.TempDirectory != null && Directory.Exists(result.TempDirectory))
					Directory.Delete(result.TempDirectory, true);
			}
			catch {
				// best-effort; temp dirs are harmless to leak in CI
			}
		}
	}
}
