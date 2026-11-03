using System;
using ConfuserNext.Core.Services;
using ConfuserNext.DynCipher.AST;
using ConfuserNext.DynCipher.Generation;

namespace ConfuserNext.DynCipher {
	public interface IDynCipherService {
		void GenerateCipherPair(RandomGenerator random, out StatementBlock encrypt, out StatementBlock decrypt);
		void GenerateExpressionPair(RandomGenerator random, Expression var, Expression result, int depth, out Expression expression, out Expression inverse);
	}

	internal class DynCipherService : IDynCipherService {
		public void GenerateCipherPair(RandomGenerator random, out StatementBlock encrypt, out StatementBlock decrypt) {
			CipherGenerator.GeneratePair(random, out encrypt, out decrypt);
		}

		public void GenerateExpressionPair(RandomGenerator random, Expression var, Expression result, int depth, out Expression expression, out Expression inverse) {
			ExpressionGenerator.GeneratePair(random, var, result, depth, out expression, out inverse);
		}
	}
}