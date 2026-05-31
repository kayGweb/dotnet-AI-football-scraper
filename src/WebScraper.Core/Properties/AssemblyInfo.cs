using System.Runtime.CompilerServices;

// Expose internal members (e.g. EspnGameService event-ID cache helpers) to the test assembly.
[assembly: InternalsVisibleTo("WebScraper.Core.Tests")]
