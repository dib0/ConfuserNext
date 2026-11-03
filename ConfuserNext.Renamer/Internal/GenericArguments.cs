// Vendored from dnlib (src/DotNet/GenericArguments.cs), which no longer exposes these
// types publicly. ConfuserNext.Renamer's GenericArgumentResolver relies on this exact
// generic var/mvar substitution behavior.
using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace ConfuserNext.Renamer {
	readonly struct GenericArgumentsStack {
		readonly List<IList<TypeSig>> argsStack;
		readonly bool isTypeVar;

		public GenericArgumentsStack(bool isTypeVar) {
			argsStack = new List<IList<TypeSig>>();
			this.isTypeVar = isTypeVar;
		}

		public void Push(IList<TypeSig> args) => argsStack.Add(args);

		public IList<TypeSig> Pop() {
			int index = argsStack.Count - 1;
			var result = argsStack[index];
			argsStack.RemoveAt(index);
			return result;
		}

		public TypeSig Resolve(uint number) {
			TypeSig result = null;
			for (int i = argsStack.Count - 1; i >= 0; i--) {
				var args = argsStack[i];
				if (number >= args.Count)
					return null;
				var typeSig = args[(int)number];
				var gvar = typeSig as GenericSig;
				if (gvar is null || gvar.IsTypeVar != isTypeVar)
					return typeSig;
				result = gvar;
				number = gvar.Number;
			}
			return result;
		}
	}

	sealed class GenericArguments {
		GenericArgumentsStack typeArgsStack = new GenericArgumentsStack(true);
		GenericArgumentsStack methodArgsStack = new GenericArgumentsStack(false);

		public void PushTypeArgs(IList<TypeSig> typeArgs) => typeArgsStack.Push(typeArgs);
		public IList<TypeSig> PopTypeArgs() => typeArgsStack.Pop();
		public void PushMethodArgs(IList<TypeSig> methodArgs) => methodArgsStack.Push(methodArgs);
		public IList<TypeSig> PopMethodArgs() => methodArgsStack.Pop();

		public TypeSig Resolve(TypeSig typeSig) {
			if (typeSig is null)
				return null;

			var sig = typeSig;

			if (sig is GenericMVar genericMVar) {
				var newSig = methodArgsStack.Resolve(genericMVar.Number);
				if (newSig is null || newSig == sig)
					return sig;
				return newSig;
			}

			if (sig is GenericVar genericVar) {
				var newSig = typeArgsStack.Resolve(genericVar.Number);
				if (newSig is null || newSig == sig)
					return sig;
				return newSig;
			}

			return sig;
		}
	}

	struct RecursionCounter {
		public const int MAX_RECURSION_COUNT = 100;
		int counter;

		public int Counter => counter;

		public bool Increment() {
			if (counter >= MAX_RECURSION_COUNT)
				return false;
			counter++;
			return true;
		}

		public void Decrement() {
#if DEBUG
			if (counter <= 0)
				throw new InvalidOperationException("recursionCounter <= 0");
#endif
			counter--;
		}

		public override string ToString() => counter.ToString();
	}
}
