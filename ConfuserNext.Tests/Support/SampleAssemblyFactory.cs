using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace ConfuserNext.Tests.Support {
	/// <summary>
	///     Compiles small, throwaway sample .NET assemblies on disk for the obfuscation pipeline to
	///     protect, exercising it the same way a real user's build output would. Using the real
	///     Roslyn compiler (rather than hand-built IL) guarantees the inputs are exactly what a
	///     normal `dotnet build` produces - the same shape ConfuserNext is actually designed for.
	/// </summary>
	public static class SampleAssemblyFactory {
		static readonly string[] ReferenceAssemblyNames = {
			"System.Private.CoreLib",
			"System.Runtime",
			"System.Console",
			"netstandard",
			"System.Collections",
			"System.Linq",
			"System.Reflection",
			"mscorlib"
		};

		static IEnumerable<MetadataReference> GetReferences() {
			string tpaList = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
			if (!string.IsNullOrEmpty(tpaList)) {
				foreach (string path in tpaList.Split(Path.PathSeparator)) {
					string name = Path.GetFileNameWithoutExtension(path);
					if (ReferenceAssemblyNames.Contains(name) && File.Exists(path))
						yield return MetadataReference.CreateFromFile(path);
				}
			}
			else {
				// .NET Framework fallback: resolve by loaded AppDomain assemblies.
				foreach (string name in ReferenceAssemblyNames) {
					Assembly asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
					if (asm != null && !string.IsNullOrEmpty(asm.Location))
						yield return MetadataReference.CreateFromFile(asm.Location);
				}
			}
		}

		/// <summary>
		///     Compiles <paramref name="source" /> into a console assembly named
		///     <paramref name="assemblyName" />, writes it to <paramref name="outputPath" />, and
		///     returns the compiled bytes. An optional embedded text resource can be attached,
		///     matching a project with an &lt;EmbeddedResource&gt; item.
		/// </summary>
		public static byte[] CompileToFile(
			string source,
			string outputPath,
			string assemblyName = null,
			string embeddedResourceName = null,
			string embeddedResourceText = null,
			OutputKind outputKind = OutputKind.ConsoleApplication) {
			assemblyName ??= Path.GetFileNameWithoutExtension(outputPath);

			var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
			SyntaxTree tree = CSharpSyntaxTree.ParseText(source, parseOptions);
			// Real `dotnet build` output always carries this attribute, and ConfuserNext.Core's
			// CoreCLR-vs-.NET-Framework detection (DnlibUtils.TargetsCoreCLR, used by e.g. the
			// Compressor packer to pick a working technique) reads it - without this, these
			// in-memory-compiled fixtures would silently look like a framework-less/unidentifiable
			// target to any code that inspects it, unlike a real build's output.
			SyntaxTree tfmTree = CSharpSyntaxTree.ParseText(
				$"[assembly: System.Runtime.Versioning.TargetFramework(\".NETCoreApp,Version=v{Environment.Version.Major}.0\")]",
				parseOptions);
			CSharpCompilation compilation = CSharpCompilation.Create(
				assemblyName,
				new[] { tree, tfmTree },
				GetReferences(),
				new CSharpCompilationOptions(outputKind, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));

			var resources = new List<ResourceDescription>();
			if (embeddedResourceName != null) {
				byte[] data = Encoding.UTF8.GetBytes(embeddedResourceText ?? "");
				resources.Add(new ResourceDescription(embeddedResourceName, () => new MemoryStream(data), isPublic: true));
			}

			using var ms = new MemoryStream();
			EmitResult result = compilation.Emit(ms, manifestResources: resources);
			if (!result.Success) {
				IEnumerable<Diagnostic> errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
				throw new InvalidOperationException(
					"Failed to compile sample assembly:\n" + string.Join("\n", errors.Select(e => e.ToString())));
			}

			byte[] bytes = ms.ToArray();
			Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
			File.WriteAllBytes(outputPath, bytes);
			return bytes;
		}

		/// <summary>
		///     A general-purpose sample program: string/number/array constants to encode, a few
		///     ordinary and virtual method calls to proxy, a loop and branch for control flow, and
		///     (optionally) an embedded resource read-back - enough surface for every stock
		///     protection to find something to do.
		/// </summary>
		public const string DefaultProgramSource = @"
using System;
using System.IO;
using System.Reflection;

namespace SampleApp {
	public class Greeter {
		public virtual string Greet(string name) {
			return ""Hello, "" + name + ""!"";
		}
	}

	public class Program {
		public static int Counter;

		public static int Add(int a, int b) {
			return a + b;
		}

		static int Main(string[] args) {
			string message = ""hello constants protection"";
			string other = ""another distinct string literal"";
			int number = 123456789;
			long bigNumber = 9876543210L;
			double pi = 3.14159;
			byte[] data = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

			Console.WriteLine(message);
			Console.WriteLine(other);
			Console.WriteLine(number);
			Console.WriteLine(bigNumber);
			Console.WriteLine(pi);
			Console.WriteLine(data.Length);

			var greeter = new Greeter();
			Console.WriteLine(greeter.Greet(""World""));

			int sum = 0;
			for (int i = 0; i < 10; i++) {
				if (i % 2 == 0)
					sum += Add(sum, i);
				else
					sum -= i;
			}
			Console.WriteLine(sum);

			Counter++;
			Console.WriteLine(Counter);

			try {
				var asm = Assembly.GetExecutingAssembly();
				using var s = asm.GetManifestResourceStream(""SampleApp.data.txt"");
				if (s != null) {
					using var r = new StreamReader(s);
					Console.WriteLine(r.ReadToEnd().Trim());
				}
			}
			catch (Exception ex) {
				Console.WriteLine(""resource error: "" + ex.Message);
			}

			return 0;
		}
	}
}
";
	}
}
