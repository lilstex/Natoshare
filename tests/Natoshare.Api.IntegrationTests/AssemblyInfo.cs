using Xunit;

// Each test class here boots the real Program.cs in-process, which sets up Serilog's
// shared static logger and tears it down again at the end. If two of these run at the
// same time, they fight over that same static logger and the app crashes on startup.
// Running the test classes one after another avoids that.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
