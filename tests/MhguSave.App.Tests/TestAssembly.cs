// Each fixture clones multi-megabyte saves and creates thousands of editor rows.
// Bound test concurrency so the suite also runs alongside the user's game/emulator.
[assembly: Xunit.CollectionBehavior(MaxParallelThreads = 1)]
