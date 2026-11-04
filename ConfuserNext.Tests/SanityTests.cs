using ConfuserNext.Core.Services;
using Xunit;

namespace ConfuserNext.Tests {
	public class SanityTests {
		[Fact]
		public void InternalsAreVisible() {
			// RandomService is internal to ConfuserNext.Core - if this compiles, InternalsVisibleTo works.
			IRandomService svc = new RandomService("seed");
			Assert.NotNull(svc.GetRandomGenerator("id"));
		}
	}
}
