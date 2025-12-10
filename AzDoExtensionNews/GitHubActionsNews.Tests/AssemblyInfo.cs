using Microsoft.VisualStudio.TestTools.UnitTesting;

// Disable test parallelization for tests that use Playwright browser automation
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
