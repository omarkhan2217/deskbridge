// End-to-end tests watch the one cursor and the one foreground window, so they never run in parallel.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]
