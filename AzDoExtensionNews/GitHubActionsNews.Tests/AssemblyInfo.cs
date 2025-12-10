using Microsoft.VisualStudio.TestTools.UnitTesting;

// Disable test parallelization at the assembly level since most tests (2 out of 3 test classes)
// use Playwright browser automation which should not be parallelized due to browser resource constraints
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
