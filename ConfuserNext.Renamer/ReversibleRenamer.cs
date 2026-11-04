using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ConfuserNext.Renamer {
	public class ReversibleRenamer {
		// Fixed, application-specific salt: there is no per-project salt storage, so the
		// derivation must stay reproducible from the password alone. The salt's role here
		// is only to stop precomputed (rainbow-table) attacks against the raw password hash;
		// the iteration count is what raises the cost of brute-forcing the password itself.
		static readonly byte[] KdfSalt = Encoding.UTF8.GetBytes("ConfuserNext.ReversibleRenamer.v1");
		const int KdfIterations = 100_000;

		Aes cipher;
		byte[] key;

		public ReversibleRenamer(string password) {
			cipher = Aes.Create();
			// The instance constructor used below is obsolete (SYSLIB0060) in favor of the static
			// Pbkdf2(...) method - but that method does not exist on net48, only on .NET 6+, hence
			// the split to stay warning-free on every TFM this project targets.
#if NET6_0_OR_GREATER
			key = Rfc2898DeriveBytes.Pbkdf2(password, KdfSalt, KdfIterations, HashAlgorithmName.SHA256, 32);
#else
			using (var kdf = new Rfc2898DeriveBytes(password, KdfSalt, KdfIterations, HashAlgorithmName.SHA256))
				key = kdf.GetBytes(32);
#endif
			cipher.Key = key;
		}

		static string Base64Encode(byte[] buf) {
			return Convert.ToBase64String(buf).Trim('=').Replace('+', '$').Replace('/', '_');
		}

		static byte[] Base64Decode(string str) {
			str = str.Replace('$', '+').Replace('_', '/').PadRight((str.Length + 3) & ~3, '=');
			return Convert.FromBase64String(str);
		}

		byte[] GetIV(byte ivId) {
			byte[] iv = new byte[cipher.BlockSize / 8];
			for (int i = 0; i < iv.Length; i++)
				iv[i] = (byte)(ivId ^ key[i]);
			return iv;
		}

        static byte GetIVId(string str) {
			byte x = (byte)str[0];
			for (int i = 1; i < str.Length; i++)
				x = (byte)(x * 3 + (byte)str[i]);
			return x;
		}

		public string Encrypt(string name) {
			byte ivId = ReversibleRenamer.GetIVId(name);
			cipher.IV = GetIV(ivId);
			var buf = Encoding.UTF8.GetBytes(name);

			using (var ms = new MemoryStream()) {
				ms.WriteByte(ivId);
				using (var stream = new CryptoStream(ms, cipher.CreateEncryptor(), CryptoStreamMode.Write))
					stream.Write(buf, 0, buf.Length);

				buf = ms.ToArray();
				return Base64Encode(buf);
			}
		}

		public string Decrypt(string name) {
			using (var ms = new MemoryStream(Base64Decode(name))) {
				byte ivId = (byte)ms.ReadByte();
				cipher.IV = GetIV(ivId);

				var result = new MemoryStream();
				using (var stream = new CryptoStream(ms, cipher.CreateDecryptor(), CryptoStreamMode.Read))
					stream.CopyTo(result);

				return Encoding.UTF8.GetString(result.ToArray());
			}
		}
	}
}