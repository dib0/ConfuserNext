using System;
using ConfuserNext.Core.Services;
using ConfuserNext.DynCipher;
using ConfuserNext.DynCipher.AST;
using ConfuserNext.DynCipher.Generation;
using Xunit;

namespace ConfuserNext.Tests.DynCipher {
	/// <summary>
	///     Exercises the polymorphic cipher generator end-to-end: generate a random encrypt/decrypt
	///     statement-pair, JIT-compile both with <see cref="DMCodeGen" /> (the same mechanism
	///     ConfuserNext.Protections uses for its "dynamic" key-derivation modes), and verify the
	///     round trip actually inverts for real data. This is the most meaningful test for this
	///     generator: a passing round trip can only happen if the random element mix (Matrix, NumOp,
	///     Swap, BinOp, RotateBit, AddKey), all four post-processing transforms, and the
	///     IL-generation path are all simultaneously correct.
	/// </summary>
	public class CipherRoundTripTests {
		static RandomGenerator Seeded(string id) => new RandomService(id).GetRandomGenerator(id);

		static (Action<uint[], uint[]> encrypt, Action<uint[], uint[]> decrypt) Compile(RandomGenerator random) {
			var svc = new DynCipherService();
			svc.GenerateCipherPair(random, out StatementBlock encrypt, out StatementBlock decrypt);

			Action<uint[], uint[]> Build(StatementBlock block) {
				var gen = new DMCodeGen(typeof(void), new[] {
					Tuple.Create("{BUFFER}", typeof(uint[])),
					Tuple.Create("{KEY}", typeof(uint[]))
				});
				gen.GenerateCIL(block);
				return gen.Compile<Action<uint[], uint[]>>();
			}

			return (Build(encrypt), Build(decrypt));
		}

		[Theory]
		[InlineData("seed-0")]
		[InlineData("seed-1")]
		[InlineData("seed-2")]
		[InlineData("seed-3")]
		[InlineData("seed-4")]
		[InlineData("seed-5")]
		[InlineData("seed-6")]
		[InlineData("seed-7")]
		[InlineData("seed-8")]
		[InlineData("seed-9")]
		[InlineData("quite-a-different-looking-seed")]
		[InlineData("")]
		public void EncryptThenDecrypt_RecoversOriginalBuffer(string seedId) {
			RandomGenerator genRandom = Seeded("gen:" + seedId);
			RandomGenerator dataRandom = Seeded("data:" + seedId);

			(Action<uint[], uint[]> encrypt, Action<uint[], uint[]> decrypt) = Compile(genRandom);

			var original = new uint[0x10];
			var key = new uint[0x10];
			for (int i = 0; i < 0x10; i++) {
				original[i] = dataRandom.NextUInt32();
				key[i] = dataRandom.NextUInt32();
			}

			var buffer = (uint[])original.Clone();
			encrypt(buffer, key);

			// A real cipher: encrypting must actually change the data (extremely unlikely to collide
			// by chance across all 16 words unless the generated cipher is degenerate).
			Assert.NotEqual(original, buffer);

			decrypt(buffer, key);
			Assert.Equal(original, buffer);
		}

		[Fact]
		public void DifferentSeeds_ProduceDifferentCiphers() {
			RandomGenerator a = Seeded("cipher-a");
			RandomGenerator b = Seeded("cipher-b");

			(Action<uint[], uint[]> encryptA, _) = Compile(a);
			(Action<uint[], uint[]> encryptB, _) = Compile(b);

			var data = new uint[0x10];
			var key = new uint[0x10];
			for (int i = 0; i < 0x10; i++) {
				data[i] = (uint)(i * 0x01010101);
				key[i] = (uint)(~i);
			}

			var resultA = (uint[])data.Clone();
			encryptA(resultA, key);
			var resultB = (uint[])data.Clone();
			encryptB(resultB, key);

			Assert.NotEqual(resultA, resultB);
		}
	}

	/// <summary>
	///     Exercises <see cref="ExpressionGenerator" /> (via <see cref="IDynCipherService.GenerateExpressionPair" />)
	///     the same way ConfuserNext.Protections.ControlFlow.ExpressionPredicate and
	///     ReferenceProxy.ExpressionEncoding do: compile the forward expression and its generated
	///     inverse with DMCodeGen and check invertibility over real integers.
	/// </summary>
	public class ExpressionRoundTripTests {
		static RandomGenerator Seeded(string id) => new RandomService(id).GetRandomGenerator(id);

		[Theory]
		[InlineData(1)]
		[InlineData(2)]
		[InlineData(3)]
		[InlineData(4)]
		[InlineData(5)]
		[InlineData(8)]
		public void ForwardThenInverse_RecoversOriginalValue(int depth) {
			for (int trial = 0; trial < 10; trial++) {
				RandomGenerator random = Seeded($"expr-{depth}-{trial}");
				var svc = new DynCipherService();

				var varVar = new Variable("v");
				var resultVar = new Variable("r");
				svc.GenerateExpressionPair(
					random,
					new VariableExpression { Variable = varVar },
					new VariableExpression { Variable = resultVar },
					depth,
					out Expression expression,
					out Expression inverse);

				Func<int, int> forward = new DMCodeGen(typeof(int), new[] { Tuple.Create("v", typeof(int)) })
					.GenerateCIL(expression)
					.Compile<Func<int, int>>();
				Func<int, int> backward = new DMCodeGen(typeof(int), new[] { Tuple.Create("r", typeof(int)) })
					.GenerateCIL(inverse)
					.Compile<Func<int, int>>();

				foreach (int input in new[] { 0, 1, -1, 12345, int.MinValue, int.MaxValue, -98765 }) {
					int forwardResult = forward(input);
					int recovered = backward(forwardResult);
					Assert.Equal(input, recovered);
				}
			}
		}
	}
}
