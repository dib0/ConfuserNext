using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ConfuserNext.Core;
using ConfuserNext.Tests.Support;
using dnlib.DotNet;
using Xunit;

namespace ConfuserNext.Tests.Core {
	/// <summary>
	///     Covers Marker.LoadSNKey's PFX-loading path, rewritten to avoid the obsolete
	///     X509Certificate2() + Import(...) pair and the obsolete PrivateKey property
	///     (SYSLIB0026/SYSLIB0028) while staying warning-free on net48/net8.0/net10.0 alike.
	/// </summary>
	public class MarkerLoadSNKeyTests : IDisposable {
		readonly string pfxPath;
		const string Password = "test-password";

		public MarkerLoadSNKeyTests() {
			using RSA rsa = RSA.Create(2048);
			var req = new CertificateRequest("CN=ConfuserNext Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			using X509Certificate2 cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

			pfxPath = Path.GetTempFileName();
			File.WriteAllBytes(pfxPath, cert.Export(X509ContentType.Pfx, Password));
		}

		public void Dispose() {
			try { File.Delete(pfxPath); } catch { }
		}

		[Fact]
		public void LoadSNKey_FromPfx_ProducesAUsableStrongNameKey() {
			var logger = new RecordingLogger();
			var context = new ConfuserContext { Logger = logger };

			StrongNameKey key = Marker.LoadSNKey(context, pfxPath, Password);

			Assert.NotNull(key);
			Assert.True(logger.Errors.IsEmpty, "LoadSNKey logged an error:\n" + logger.Dump());
			// A real RSA key was round-tripped if a public key could be derived from it.
			Assert.NotEmpty(key.PublicKey);
		}

		[Fact]
		public void LoadSNKey_NullPath_ReturnsNull() {
			var context = new ConfuserContext { Logger = new RecordingLogger() };
			Assert.Null(Marker.LoadSNKey(context, null, Password));
		}

		[Fact]
		public void LoadSNKey_WrongPassword_ThrowsConfuserException() {
			var context = new ConfuserContext { Logger = new RecordingLogger() };
			Assert.Throws<ConfuserException>(() => Marker.LoadSNKey(context, pfxPath, "definitely-wrong"));
		}
	}
}
