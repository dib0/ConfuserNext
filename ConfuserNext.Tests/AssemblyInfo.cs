using Xunit;

// The integration tests drive the real ConfuserEngine against real files on disk (temp source and
// output directories, and - for the compressor packer - a recursive ConfuserEngine.Run that writes
// and immediately re-reads its own stub module). Running many of these concurrently multiplies
// contention for whatever is momentarily locking freshly-written executable files in this
// environment (see the Skip reason on CompressorPacker_RunsWithoutError), which was observed to
// turn an otherwise-reliable suite flaky under coverage instrumentation's added overhead.
// Serializing isn't a performance concern here: the whole suite still runs in a few seconds.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
