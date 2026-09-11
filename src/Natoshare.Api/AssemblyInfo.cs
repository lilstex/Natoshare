using System.Runtime.CompilerServices;

// This lets the integration test project see internal types in this project,
// especially the auto-generated Program class the tests need to boot up the API.
[assembly: InternalsVisibleTo("Natoshare.Api.IntegrationTests")]
