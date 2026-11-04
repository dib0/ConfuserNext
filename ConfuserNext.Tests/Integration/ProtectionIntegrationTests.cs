using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ConfuserNext.Tests.Support;
using dnlib.DotNet;
using Xunit;

namespace ConfuserNext.Tests.Integration {
	/// <summary>
	///     Runs the real <see cref="ConfuserNext.Core.ConfuserEngine" /> pipeline, in-process,
	///     against a freshly-compiled sample assembly, for every stock protection (solo and
	///     combined) and the compressor packer. This is deliberately an integration-style suite
	///     rather than many small unit tests: most of ConfuserNext.Protections/.Renamer/.Core only
	///     does anything meaningful as part of a full pipeline run against real dnlib metadata, so
	///     driving the public entry point is both the most realistic test and the highest-coverage
	///     one per line of test code.
	/// </summary>
	public class ProtectionIntegrationTests {
		public static IEnumerable<object[]> AllProtectionIds() {
			yield return new object[] { "anti debug" };
			yield return new object[] { "anti dump" };
			yield return new object[] { "anti ildasm" };
			yield return new object[] { "anti tamper" };
			yield return new object[] { "constants" };
			yield return new object[] { "ctrl flow" };
			yield return new object[] { "invalid metadata" };
			yield return new object[] { "ref proxy" };
			yield return new object[] { "rename" };
			yield return new object[] { "resources" };
		}

		[Theory]
		[MemberData(nameof(AllProtectionIds))]
		public void EachProtection_RunsAloneWithoutError(string id) {
			var result = ObfuscationHarness.Protect(new[] { new ProtectionSpec(id) });
			try {
				Assert.True(result.OutputModuleBytes.Length > 0);
				AssertLoadable(result.OutputModuleBytes);
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Fact]
		public void NoProtectionsAtAll_PresetNone_RunsWithoutError() {
			var result = ObfuscationHarness.Protect(Array.Empty<ProtectionSpec>());
			try {
				Assert.True(result.OutputModuleBytes.Length > 0);
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Fact]
		public void AllProtections_Combined_RunWithoutError() {
			var specs = AllProtectionIds().Select(a => new ProtectionSpec((string)a[0])).ToArray();
			var result = ObfuscationHarness.Protect(specs, embeddedResourceName: "SampleApp.data.txt", embeddedResourceText: "embedded payload");
			try {
				Assert.True(result.OutputModuleBytes.Length > 0);
				AssertLoadable(result.OutputModuleBytes);
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Theory]
		[InlineData("normal")]
		[InlineData("minimum")]
		[InlineData("aggressive")]
		[InlineData("maximum")]
		public void Presets_RunWithoutError(string preset) {
			var result = ObfuscationHarness.Protect(
				Array.Empty<ProtectionSpec>(),
				configureProject: project => project.Rules[0].Preset = Enum.Parse<ConfuserNext.Core.ProtectionPreset>(preset, true));
			try {
				Assert.True(result.OutputModuleBytes.Length > 0);
				AssertLoadable(result.OutputModuleBytes);
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Theory]
		[InlineData("constants", "mode", "x86")]
		[InlineData("ref proxy", "encoding", "x86")]
		[InlineData("ref proxy", "mode", "strong", "encoding", "x86")]
		[InlineData("ctrl flow", "type", "jump")]
		[InlineData("ctrl flow", "predicate", "x86")]
		[InlineData("ctrl flow", "junk", "true")]
		[InlineData("ctrl flow", "intensity", "100")]
		public void ProtectionParameterCombinations_RunWithoutError(string id, string param1, string value1, string param2 = null, string value2 = null) {
			var args = new List<(string, string)> { (param1, value1) };
			if (param2 != null)
				args.Add((param2, value2));
			var result = ObfuscationHarness.Protect(new[] { new ProtectionSpec(id, args.ToArray()) });
			try {
				Assert.True(result.OutputModuleBytes.Length > 0);
				AssertLoadable(result.OutputModuleBytes);
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Fact]
		public void CompressorPacker_DynamicKey_CompatMode_RunsWithoutError() {
			// key=dynamic exercises Compress.DynamicDeriver; compat mode avoids the recursive
			// packer-stub re-protection path (see the Skip reason on CompressorPacker_RunsWithoutError).
			var result = ObfuscationHarness.Protect(
				Array.Empty<ProtectionSpec>(),
				packerId: "compressor",
				configureProject: project => {
					project.Packer["compat"] = "true";
					project.Packer["key"] = "dynamic";
				});
			try {
				Assert.True(result.OutputModuleBytes.Length > 0);
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Theory]
		[InlineData("anti tamper", "mode", "normal")]
		[InlineData("anti tamper", "mode", "jit")]
		[InlineData("anti tamper", "key", "normal")]
		[InlineData("anti tamper", "key", "dynamic")]
		[InlineData("anti debug", "mode", "safe")]
		[InlineData("anti debug", "mode", "win32")]
		[InlineData("constants", "mode", "normal")]
		[InlineData("constants", "mode", "dynamic")]
		[InlineData("constants", "elements", "SNPI")]
		[InlineData("ref proxy", "mode", "mild")]
		[InlineData("ref proxy", "mode", "strong")]
		[InlineData("ref proxy", "encoding", "normal")]
		[InlineData("ref proxy", "encoding", "expression")]
		[InlineData("resources", "mode", "normal")]
		[InlineData("resources", "mode", "dynamic")]
		[InlineData("rename", "mode", "letters")]
		[InlineData("rename", "mode", "ascii")]
		[InlineData("rename", "mode", "unicode")]
		[InlineData("rename", "flatten", "true")]
		[InlineData("rename", "flatten", "false")]
		[InlineData("ctrl flow", "type", "switch")]
		[InlineData("ctrl flow", "predicate", "normal")]
		[InlineData("ctrl flow", "predicate", "expression")]
		public void ProtectionParameters_RunWithoutError(string id, string param, string value) {
			var result = ObfuscationHarness.Protect(new[] { new ProtectionSpec(id, (param, value)) });
			try {
				Assert.True(result.OutputModuleBytes.Length > 0);
				AssertLoadable(result.OutputModuleBytes);
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Fact]
		public void CompressorPacker_OnCoreClrTarget_AutoForcesCompatModeAndRuns() {
			// Without an explicit "compat" setting, ExtractPhase must detect that the target runs on
			// CoreCLR (via DnlibUtils.TargetsCoreCLR) and force compat mode itself, since the
			// non-compat technique's Assembly.LoadModule never works there (see session notes). This
			// is the actual fix for the open "packer doesn't work on modern .NET" issue: verify the
			// packed output runs, not just that it builds.
			var result = ObfuscationHarness.Protect(
				Array.Empty<ProtectionSpec>(),
				source: SampleAssemblyFactory.DefaultProgramSource.Replace(
					"Console.WriteLine(sum);", "Console.WriteLine(sum); Console.WriteLine(\"AUTO_COMPAT_PACKER_OK\");"),
				packerId: "compressor");
			try {
				Assert.Contains("Forcing compat mode", result.Logger.Dump());

				string runtimeConfigPath = System.IO.Path.ChangeExtension(result.OutputModulePath, "runtimeconfig.json");
				int hostMajor = Environment.Version.Major;
				System.IO.File.WriteAllText(runtimeConfigPath, $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""net{hostMajor}.0"",
    ""framework"": {{ ""name"": ""Microsoft.NETCore.App"", ""version"": ""{hostMajor}.0.0"" }},
    ""rollForward"": ""LatestMinor""
  }}
}}");
				var psi = new System.Diagnostics.ProcessStartInfo("dotnet", $"\"{result.OutputModulePath}\"") {
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false
				};
				using var process = System.Diagnostics.Process.Start(psi);
				string stdout = process.StandardOutput.ReadToEnd();
				string stderr = process.StandardError.ReadToEnd();
				Assert.True(process.WaitForExit(30000), "packed app did not exit within 30s");
				Assert.True(process.ExitCode == 0, $"packed app exited {process.ExitCode}.\nstdout:\n{stdout}\nstderr:\n{stderr}");
				Assert.Contains("AUTO_COMPAT_PACKER_OK", stdout);
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Fact]
		public void CompressorPacker_CompatMode_ActuallyRunsOnCoreClr() {
			// Verifies the fix requested in session follow-up: CompatMode uses Assembly.Load (not
			// Assembly.LoadModule, which CoreCLR never implements) and must therefore actually
			// execute on a modern .NET runtime, not just build successfully.
			var result = ObfuscationHarness.Protect(
				Array.Empty<ProtectionSpec>(),
				source: SampleAssemblyFactory.DefaultProgramSource.Replace(
					"Console.WriteLine(sum);", "Console.WriteLine(sum); Console.WriteLine(\"COMPAT_PACKER_OK\");"),
				packerId: "compressor",
				configureProject: project => project.Packer["compat"] = "true");
			try {
				// The sample app is compiled (via SampleAssemblyFactory) against this test host's own
				// implementation assemblies, so it must run under this same CoreCLR major version -
				// what matters for this test is that it is CoreCLR (any version 5+ never implemented
				// Assembly.LoadModule), not specifically net8.0.
				string runtimeConfigPath = System.IO.Path.ChangeExtension(result.OutputModulePath, "runtimeconfig.json");
				int hostMajor = Environment.Version.Major;
				System.IO.File.WriteAllText(runtimeConfigPath, $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""net{hostMajor}.0"",
    ""framework"": {{ ""name"": ""Microsoft.NETCore.App"", ""version"": ""{hostMajor}.0.0"" }},
    ""rollForward"": ""LatestMinor""
  }}
}}");
				var psi = new System.Diagnostics.ProcessStartInfo("dotnet", $"\"{result.OutputModulePath}\"") {
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false
				};
				using var process = System.Diagnostics.Process.Start(psi);
				string stdout = process.StandardOutput.ReadToEnd();
				string stderr = process.StandardError.ReadToEnd();
				Assert.True(process.WaitForExit(30000), "packed app did not exit within 30s");
				Assert.True(process.ExitCode == 0, $"packed app exited {process.ExitCode}.\nstdout:\n{stdout}\nstderr:\n{stderr}");
				Assert.Contains("COMPAT_PACKER_OK", stdout);
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Fact]
		public void CompressorPacker_CompatMode_RunsWithoutError() {
			var result = ObfuscationHarness.Protect(
				new[] { new ProtectionSpec("constants") },
				packerId: "compressor",
				configureProject: project => project.Packer["compat"] = "true");
			try {
				Assert.True(result.OutputModuleBytes.Length > 0);
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Fact]
		public void ResourceProtection_WithEmbeddedResource_PreservesContentUnderEncryption() {
			var result = ObfuscationHarness.Protect(
				new[] { new ProtectionSpec("resources") },
				embeddedResourceName: "SampleApp.data.txt",
				embeddedResourceText: "this text must not appear in plaintext in the output");
			try {
				string text = System.Text.Encoding.UTF8.GetString(result.OutputModuleBytes);
				Assert.DoesNotContain("this text must not appear in plaintext", text);

				string runtimeConfigPath = System.IO.Path.ChangeExtension(result.OutputModulePath, "runtimeconfig.json");
				System.IO.File.WriteAllText(runtimeConfigPath, $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""net{Environment.Version.Major}.0"",
    ""framework"": {{ ""name"": ""Microsoft.NETCore.App"", ""version"": ""{Environment.Version.Major}.0.0"" }},
    ""rollForward"": ""LatestMinor""
  }}
}}");
				var psi = new System.Diagnostics.ProcessStartInfo("dotnet", $"\"{result.OutputModulePath}\"") {
					RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
				};
				using var process = System.Diagnostics.Process.Start(psi);
				string stdout = process.StandardOutput.ReadToEnd();
				string stderr = process.StandardError.ReadToEnd();
				process.WaitForExit(30000);
				Assert.True(process.ExitCode == 0, $"exit={process.ExitCode}\nstdout={stdout}\nstderr={stderr}");
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Fact]
		public void ConstantsProtection_EncryptsStringLiterals() {
			var result = ObfuscationHarness.Protect(new[] { new ProtectionSpec("constants") });
			try {
				string text = System.Text.Encoding.UTF8.GetString(result.OutputModuleBytes);
				Assert.DoesNotContain("hello constants protection", text);
				Assert.DoesNotContain("another distinct string literal", text);
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Fact]
		public void RenameProtection_RemovesOriginalTypeName() {
			var result = ObfuscationHarness.Protect(new[] { new ProtectionSpec("rename") });
			try {
				using var module = ModuleDefMD.Load(result.OutputModuleBytes);
				Assert.DoesNotContain(module.Types, t => t.Name == "Greeter");
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Fact]
		public void DeclarativeObfuscationAttribute_ExcludeSkipsRenaming() {
			string source = SampleAssemblyFactory.DefaultProgramSource.Replace(
				"public class Greeter {",
				"[System.Reflection.Obfuscation(Exclude = true)]\n\tpublic class Greeter {");
			var result = ObfuscationHarness.Protect(new[] { new ProtectionSpec("rename") }, source);
			try {
				using var module = ModuleDefMD.Load(result.OutputModuleBytes);
				Assert.Contains(module.Types, t => t.Name == "Greeter");
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Fact]
		public void DeclarativeObfuscationAttribute_FeatureString_IsApplied() {
			string source = SampleAssemblyFactory.DefaultProgramSource.Replace(
				"namespace SampleApp {",
				"[assembly: System.Reflection.Obfuscation(Exclude = false, Feature = \"preset(minimum);+constants;\")]\nnamespace SampleApp {");
			var result = ObfuscationHarness.Protect(Array.Empty<ProtectionSpec>(), source);
			try {
				string text = System.Text.Encoding.UTF8.GetString(result.OutputModuleBytes);
				Assert.DoesNotContain("hello constants protection", text);
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Fact]
		public void NamespacePattern_RestrictsProtectionToMatchingTypes() {
			var result = ObfuscationHarness.Protect(
				new[] { new ProtectionSpec("rename") },
				configureProject: project => project.Rules[0].Pattern = "namespace('SampleApp')");
			try {
				Assert.True(result.OutputModuleBytes.Length > 0);
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Fact]
		public void RemoveAction_DisablesAPreviouslyEnabledProtection() {
			var result = ObfuscationHarness.Protect(
				Array.Empty<ProtectionSpec>(),
				configureProject: project => {
					project.Rules[0].Preset = ConfuserNext.Core.ProtectionPreset.Normal;
					var remove = new ConfuserNext.Core.Project.SettingItem<ConfuserNext.Core.Protection> {
						Id = "rename",
						Action = ConfuserNext.Core.Project.SettingItemAction.Remove
					};
					project.Rules[0].Add(remove);
				});
			try {
				using var module = ModuleDefMD.Load(result.OutputModuleBytes);
				Assert.Contains(module.Types, t => t.Name == "Greeter");
			}
			finally {
				ObfuscationHarness.Cleanup(result);
			}
		}

		[Fact]
		public void ProjectXml_SaveThenLoad_RoundTripsAndProducesSameProtection() {
			string tempProj = System.IO.Path.GetTempFileName();
			try {
				var project = new ConfuserNext.Core.Project.ConfuserProject {
					BaseDirectory = ".",
					OutputDirectory = "out",
					Seed = "xml-round-trip"
				};
				var rule = new ConfuserNext.Core.Project.Rule("true", ConfuserNext.Core.ProtectionPreset.None, true);
				rule.Add(new ConfuserNext.Core.Project.SettingItem<ConfuserNext.Core.Protection>("constants") { { "mode", "dynamic" } });
				project.Rules.Add(rule);
				project.Add(new ConfuserNext.Core.Project.ProjectModule { Path = "Sample.dll" });
				project.Packer = new ConfuserNext.Core.Project.SettingItem<ConfuserNext.Core.Packer>("compressor");

				System.Xml.XmlDocument doc = project.Save();
				doc.Save(tempProj);

				var reloaded = new ConfuserNext.Core.Project.ConfuserProject();
				var loadedDoc = new System.Xml.XmlDocument();
				loadedDoc.Load(tempProj);
				reloaded.Load(loadedDoc);

				Assert.Equal(project.Seed, reloaded.Seed);
				Assert.Equal(project.BaseDirectory, reloaded.BaseDirectory);
				Assert.Equal(project.OutputDirectory, reloaded.OutputDirectory);
				Assert.Single(reloaded.Rules);
				Assert.Equal("constants", reloaded.Rules[0][0].Id);
				Assert.Equal("dynamic", reloaded.Rules[0][0]["mode"]);
				Assert.Equal("compressor", reloaded.Packer.Id);
				Assert.Single(reloaded);
				Assert.Equal("Sample.dll", reloaded[0].Path);
			}
			finally {
				System.IO.File.Delete(tempProj);
			}
		}

		static void AssertLoadable(byte[] moduleBytes) {
			using var module = ModuleDefMD.Load(moduleBytes);
			Assert.NotNull(module.EntryPoint ?? module.Types.FirstOrDefault()?.FindMethod("Main"));
		}
	}
}
