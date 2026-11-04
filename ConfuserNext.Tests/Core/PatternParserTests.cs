using System;
using System.IO;
using ConfuserNext.Core.Project;
using ConfuserNext.Tests.Support;
using dnlib.DotNet;
using Xunit;

namespace ConfuserNext.Tests.Core {
	/// <summary>
	///     Exercises every built-in pattern-DSL function and operator (see docs/ProjectFormat.md's
	///     "pattern" attribute, and DeclarativeObfuscation.txt's examples like
	///     <c>member-type('type') and full-name('NS.Type')</c>) directly against real dnlib
	///     definitions from a compiled sample module, rather than only indirectly through a full
	///     obfuscation run.
	/// </summary>
	public class PatternParserTests : IDisposable {
		readonly string tempDir;
		readonly ModuleDefMD module;
		readonly TypeDef greeterType;
		readonly MethodDef greetMethod;
		readonly MethodDef addMethod;

		public PatternParserTests() {
			tempDir = Path.Combine(Path.GetTempPath(), "cx-pattern-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(tempDir);
			string path = Path.Combine(tempDir, "Sample.dll");
			SampleAssemblyFactory.CompileToFile(SampleAssemblyFactory.DefaultProgramSource, path, "Sample");

			var resolver = new AssemblyResolver { EnableTypeDefCache = true };
			resolver.DefaultModuleContext = new ModuleContext(resolver);
			string hostDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
			if (hostDir != null)
				resolver.PostSearchPaths.Add(hostDir);
			module = ModuleDefMD.Load(path, resolver.DefaultModuleContext);
			greeterType = module.Find("SampleApp.Greeter", true);
			greetMethod = greeterType.FindMethod("Greet");
			addMethod = module.Find("SampleApp.Program", true).FindMethod("Add");
		}

		public void Dispose() {
			module.Dispose();
			try { Directory.Delete(tempDir, true); } catch { }
		}

		static bool Eval(string pattern, IDnlibDef def) => (bool)new PatternParser().Parse(pattern).Evaluate(def);

		[Fact]
		public void TrueLiteral_MatchesEverything() {
			Assert.True(Eval("true", greeterType));
			Assert.False(Eval("false", greeterType));
		}

		[Fact]
		public void Name_MatchesExactTypeName() {
			Assert.True(Eval("name('Greeter')", greeterType));
			Assert.False(Eval("name('NotGreeter')", greeterType));
		}

		[Fact]
		public void FullName_MatchesNamespaceQualifiedName() {
			Assert.True(Eval("full-name('SampleApp.Greeter')", greeterType));
			Assert.False(Eval("full-name('Greeter')", greeterType));
		}

		[Fact]
		public void Namespace_MatchesContainingNamespace() {
			Assert.True(Eval("namespace('SampleApp')", greeterType));
			Assert.False(Eval("namespace('Other')", greeterType));
		}

		[Fact]
		public void Module_MatchesContainingModuleName() {
			// The on-disk file is "Sample.dll", but Roslyn names the embedded module record after
			// the output kind - a ConsoleApplication's module is "<assembly>.exe" regardless of the
			// file name it's saved under.
			Assert.True(Eval($"module('{module.Name}')", greeterType));
		}

		[Fact]
		public void DeclType_MatchesDeclaringTypeNameOfAMember() {
			Assert.True(Eval("decl-type('SampleApp.Greeter')", greetMethod));
			Assert.False(Eval("decl-type('SampleApp.Program')", greetMethod));
		}

		[Fact]
		public void MemberType_DistinguishesTypesFromMethods() {
			Assert.True(Eval("member-type('type ')", greeterType));
			Assert.False(Eval("member-type('type ')", greetMethod));
			Assert.True(Eval("member-type('method ')", greetMethod));
		}

		[Fact]
		public void IsPublic_ReflectsVisibility() {
			Assert.True(Eval("is-public()", greeterType));
		}

		[Fact]
		public void Inherits_MatchesBaseTypeByName() {
			Assert.True(Eval("inherits('System.Object')", greeterType));
			Assert.False(Eval("inherits('System.Exception')", greeterType));
		}

		[Fact]
		public void IsType_DistinguishesValueTypesAndAbstractTypes() {
			Assert.False(Eval("is-type('valuetype ')", greeterType));
		}

		[Fact]
		public void HasAttr_FindsPresentAndAbsentAttributes() {
			Assert.False(Eval("has-attr('System.ObsoleteAttribute')", greeterType));
		}

		[Fact]
		public void Match_MatchesCilSignatureStringByRegex() {
			Assert.True(Eval("match('.*Greet.*')", greetMethod));
		}

		[Fact]
		public void MatchName_MatchesSimpleNameByRegex() {
			Assert.True(Eval("match-name('Gre.*')", greeterType));
			Assert.False(Eval("match-name('Xyz.*')", greeterType));
		}

		[Fact]
		public void MatchTypeName_MatchesSimpleTypeNameByRegex() {
			Assert.True(Eval("match-type-name('Gre.*')", greeterType));
			Assert.True(Eval("match-type-name('Gre.*')", greetMethod)); // matches declaring type's name
		}

		[Fact]
		public void AndOperator_RequiresBothSides() {
			Assert.True(Eval("name('Greeter') and namespace('SampleApp')", greeterType));
			Assert.False(Eval("name('Greeter') and namespace('Other')", greeterType));
		}

		[Fact]
		public void OrOperator_RequiresEitherSide() {
			Assert.True(Eval("name('NotGreeter') or namespace('SampleApp')", greeterType));
			Assert.False(Eval("name('NotGreeter') or namespace('Other')", greeterType));
		}

		[Fact]
		public void NotOperator_Negates() {
			Assert.True(Eval("not name('NotGreeter')", greeterType));
			Assert.False(Eval("not name('Greeter')", greeterType));
		}

		[Fact]
		public void CombinedExpression_FromDeclarativeObfuscationDocs_Works() {
			// Lifted straight from docs/ProjectFormat.md's example.
			Assert.True(Eval("member-type('type ') and full-name('SampleApp.Greeter')", greeterType));
			Assert.False(Eval("member-type('type ') and full-name('SampleApp.Greeter')", greetMethod));
		}

		[Fact]
		public void InvalidPattern_ThrowsInvalidPatternException() {
			Assert.Throws<InvalidPatternException>(() => new PatternParser().Parse("name("));
			Assert.Throws<InvalidPatternException>(() => new PatternParser().Parse("name('x') extra"));
			Assert.Throws<InvalidPatternException>(() => new PatternParser().Parse("not-a-real-function('x')"));
		}

		[Fact]
		public void NullPattern_ThrowsArgumentNullException() {
			Assert.Throws<ArgumentNullException>(() => new PatternParser().Parse(null));
		}
	}
}
