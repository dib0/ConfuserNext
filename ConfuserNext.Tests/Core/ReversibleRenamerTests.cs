using ConfuserNext.Renamer;
using Xunit;

namespace ConfuserNext.Tests.Core {
	/// <summary>
	///     Covers the PBKDF2-based reversible name encryption hardened earlier this session (see
	///     session notes: this used to be a single unsalted SHA-256 hash plus RijndaelManaged).
	/// </summary>
	public class ReversibleRenamerTests {
		[Theory]
		[InlineData("hunter2", "SomeClassName")]
		[InlineData("hunter2", "AnotherName_With_Underscores")]
		[InlineData("a different password entirely", "X")]
		[InlineData("", "EmptyPasswordStillWorks")]
		public void EncryptThenDecrypt_RecoversOriginalName(string password, string name) {
			var renamer = new ReversibleRenamer(password);
			string encrypted = renamer.Encrypt(name);
			Assert.NotEqual(name, encrypted);
			Assert.Equal(name, renamer.Decrypt(encrypted));
		}

		[Fact]
		public void SameNameEncryptedTwice_WithSamePassword_ProducesSameCiphertext() {
			// The IV is deterministically derived from the name itself (see ReversibleRenamer.GetIVId),
			// not randomized, so re-encrypting the same name must be reproducible - this is required
			// for consistent renaming across multiple runs with the same project seed/password.
			var renamer = new ReversibleRenamer("pw");
			Assert.Equal(renamer.Encrypt("Foo"), renamer.Encrypt("Foo"));
		}

		[Fact]
		public void DifferentPasswords_ProduceDifferentCiphertextForSameName() {
			var a = new ReversibleRenamer("password-one");
			var b = new ReversibleRenamer("password-two");
			Assert.NotEqual(a.Encrypt("SameName"), b.Encrypt("SameName"));
		}

		[Fact]
		public void DecryptWithWrongPassword_DoesNotRecoverOriginalName() {
			var a = new ReversibleRenamer("correct-password");
			var b = new ReversibleRenamer("wrong-password");
			string encrypted = a.Encrypt("SecretTypeName");
			// Decrypting with the wrong key either throws (bad padding) or returns garbage; either
			// way, it must never silently "succeed" with the original plaintext.
			try {
				string decrypted = b.Decrypt(encrypted);
				Assert.NotEqual("SecretTypeName", decrypted);
			}
			catch (System.Security.Cryptography.CryptographicException) {
				// Expected: PKCS7 padding validation failing under the wrong key is a valid outcome.
			}
		}

		[Fact]
		public void EncryptedName_IsUrlSafeBase64WithoutPadding() {
			var renamer = new ReversibleRenamer("pw");
			string encrypted = renamer.Encrypt("AnyName");
			Assert.DoesNotContain("+", encrypted);
			Assert.DoesNotContain("/", encrypted);
			Assert.DoesNotContain("=", encrypted);
		}
	}
}
