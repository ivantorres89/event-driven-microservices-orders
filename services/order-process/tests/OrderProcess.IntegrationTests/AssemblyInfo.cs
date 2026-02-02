using Xunit;

// Integration tests share a single SQL database (contoso-integrationtests).
// Disable parallelization to avoid cross-test interference and migration races.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
