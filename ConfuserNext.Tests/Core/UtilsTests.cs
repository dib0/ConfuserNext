using System;
using System.Collections.Generic;
using ConfuserNext.Core;
using Xunit;

namespace ConfuserNext.Tests.Core {
	public class UtilsTests {
		[Fact]
		public void GetValueOrDefault_ReturnsValueWhenPresent() {
			var dict = new Dictionary<string, int> { { "a", 1 } };
			Assert.Equal(1, dict.GetValueOrDefault("a"));
		}

		[Fact]
		public void GetValueOrDefault_ReturnsDefaultWhenAbsent() {
			var dict = new Dictionary<string, int>();
			Assert.Equal(42, dict.GetValueOrDefault("missing", 42));
			Assert.Equal(0, dict.GetValueOrDefault("missing"));
		}

		[Fact]
		public void GetValueOrDefaultLazy_OnlyInvokesFactoryWhenAbsent() {
			var dict = new Dictionary<string, int> { { "a", 1 } };
			bool factoryCalled = false;
			Assert.Equal(1, dict.GetValueOrDefaultLazy("a", _ => { factoryCalled = true; return -1; }));
			Assert.False(factoryCalled);
			Assert.Equal(-1, dict.GetValueOrDefaultLazy("b", _ => { factoryCalled = true; return -1; }));
			Assert.True(factoryCalled);
		}

		[Fact]
		public void AddListEntry_CreatesListOnFirstAddAndAppendsAfter() {
			var dict = new Dictionary<string, List<int>>();
			dict.AddListEntry("k", 1);
			dict.AddListEntry("k", 2);
			Assert.Equal(new[] { 1, 2 }, dict["k"]);
		}

		[Fact]
		public void AddListEntry_NullKey_Throws() {
			var dict = new Dictionary<string, List<int>>();
			Assert.Throws<ArgumentNullException>(() => dict.AddListEntry(null, 1));
		}

		[Fact]
		public void NullIfEmpty_EmptyBecomesNull_NonEmptyUnchanged() {
			Assert.Null("".NullIfEmpty());
			Assert.Null(((string)null).NullIfEmpty());
			Assert.Equal("x", "x".NullIfEmpty());
		}

		[Fact]
		public void SHA1_IsDeterministicAndMatchesKnownVector() {
			byte[] hash = Utils.SHA1(System.Text.Encoding.ASCII.GetBytes(""));
			Assert.Equal("da39a3ee5e6b4b0d3255bfef95601890afd80709", Utils.ToHexString(hash));
		}

		[Fact]
		public void SHA256_IsDeterministicAndMatchesKnownVector() {
			byte[] hash = Utils.SHA256(System.Text.Encoding.ASCII.GetBytes(""));
			Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", Utils.ToHexString(hash).PadLeft(64, '0'));
		}

		[Fact]
		public void Xor_SameLength_XorsBytewise() {
			byte[] a = { 0b1010, 0b1111 };
			byte[] b = { 0b0101, 0b1111 };
			Assert.Equal(new byte[] { 0b1111, 0 }, Utils.Xor(a, b));
		}

		[Fact]
		public void Xor_MismatchedLength_Throws() {
			Assert.Throws<ArgumentException>(() => Utils.Xor(new byte[1], new byte[2]));
		}

		[Fact]
		public void ToHexString_RoundTripsKnownBytes() {
			Assert.Equal("00ff10", Utils.ToHexString(new byte[] { 0x00, 0xff, 0x10 }));
		}

		[Fact]
		public void EncodeString_ProducesNonEmptyDeterministicOutput() {
			var charset = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
			string a = Utils.EncodeString(new byte[] { 1, 2, 3, 4 }, charset);
			string b = Utils.EncodeString(new byte[] { 1, 2, 3, 4 }, charset);
			Assert.Equal(a, b);
			Assert.NotEmpty(a);
			foreach (char c in a)
				Assert.Contains(c, charset);
		}

		[Fact]
		public void Replace_WithComparison_ReplacesAllOccurrencesCaseInsensitively() {
			string result = "Hello HELLO hello".Replace("hello", "bye", StringComparison.OrdinalIgnoreCase);
			Assert.Equal("bye bye bye", result);
		}

		[Fact]
		public void Replace_NoMatches_ReturnsOriginalString() {
			Assert.Equal("abc", "abc".Replace("xyz", "q", StringComparison.Ordinal));
		}

		[Fact]
		public void GetRelativePath_ComputesRelativePathBetweenFolders() {
			string baseDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "a", "b") + System.IO.Path.DirectorySeparatorChar;
			string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "a", "b", "c", "file.txt");
			string rel = Utils.GetRelativePath(file, baseDir);
			Assert.Equal(System.IO.Path.Combine("c", "file.txt"), rel);
		}
	}

	public class AnnotationsTests {
		[Fact]
		public void SetThenGet_ReturnsStoredValue() {
			var annotations = new Annotations();
			var obj = new object();
			annotations.Set(obj, "key", 123);
			Assert.Equal(123, annotations.Get<int>(obj, "key"));
		}

		[Fact]
		public void Get_MissingKey_ReturnsDefaultValue() {
			var annotations = new Annotations();
			var obj = new object();
			Assert.Equal(-1, annotations.Get(obj, "key", -1));
			Assert.Null(annotations.Get<string>(obj, "key"));
		}

		[Fact]
		public void GetLazy_OnlyInvokesFactoryWhenMissing() {
			var annotations = new Annotations();
			var obj = new object();
			annotations.Set(obj, "key", "stored");
			Assert.Equal("stored", annotations.GetLazy<string>(obj, "key", _ => "computed"));
			Assert.Equal("computed", annotations.GetLazy<string>(obj, "other", _ => "computed"));
		}

		[Fact]
		public void GetOrCreate_CachesCreatedValue() {
			var annotations = new Annotations();
			var obj = new object();
			int calls = 0;
			Func<object, int> factory = _ => { calls++; return 7; };
			Assert.Equal(7, annotations.GetOrCreate(obj, "key", factory));
			Assert.Equal(7, annotations.GetOrCreate(obj, "key", factory));
			Assert.Equal(1, calls);
		}

		[Fact]
		public void DifferentObjects_HaveIndependentAnnotations() {
			var annotations = new Annotations();
			var a = new object();
			var b = new object();
			annotations.Set(a, "key", 1);
			annotations.Set(b, "key", 2);
			Assert.Equal(1, annotations.Get<int>(a, "key"));
			Assert.Equal(2, annotations.Get<int>(b, "key"));
		}

		[Fact]
		public void Set_NullObject_Throws() {
			var annotations = new Annotations();
			Assert.Throws<ArgumentNullException>(() => annotations.Set<int>(null, "key", 1));
		}

		[Fact]
		public void Set_NullKey_Throws() {
			var annotations = new Annotations();
			Assert.Throws<ArgumentNullException>(() => annotations.Set(new object(), null, 1));
		}
	}

	public class NullLoggerTests {
		[Fact]
		public void AllMembers_AreNoOpsAndNeverThrow() {
			ILogger logger = NullLogger.Instance;
			logger.Debug("x");
			logger.DebugFormat("{0}", 1);
			logger.Info("x");
			logger.InfoFormat("{0}", 1);
			logger.Warn("x");
			logger.WarnFormat("{0}", 1);
			logger.WarnException("x", new Exception());
			logger.Error("x");
			logger.ErrorFormat("{0}", 1);
			logger.ErrorException("x", new Exception());
			logger.Progress(1, 2);
			logger.EndProgress();
			logger.Finish(true);
			logger.Finish(false);
		}

		[Fact]
		public void Instance_IsASingleton() {
			Assert.Same(NullLogger.Instance, NullLogger.Instance);
		}
	}

	public class ExceptionTypeTests {
		[Fact]
		public void ConfuserException_CarriesInnerException() {
			var inner = new InvalidOperationException("boom");
			var ex = new ConfuserException(inner);
			Assert.Same(inner, ex.InnerException);
		}

		[Fact]
		public void ConfuserException_AllowsNullInnerException() {
			var ex = new ConfuserException(null);
			Assert.Null(ex.InnerException);
		}

		[Fact]
		public void UnreachableException_DefaultConstructs() {
			Assert.NotNull(new UnreachableException());
		}

		[Fact]
		public void CircularDependencyException_CarriesBothProtections() {
			var a = new ConfuserNext.Protections.AntiDebugProtection();
			var b = new ConfuserNext.Protections.AntiDumpProtection();
			var ex = new CircularDependencyException(a, b);
			Assert.Same(a, ex.ProtectionA);
			Assert.Same(b, ex.ProtectionB);
		}
	}
}
