Build Fix List — All Changes Needed
Fix 1: WebScraper/WebScraper.csproj — Package version mismatches and missing packages
1a. Remove standalone Polly and upgrade Microsoft.Extensions.Http.Resilience:
Replace:
    <!-- Resilience -->
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="8.0.0" />
    <PackageReference Include="Polly" Version="8.6.5" />
    <!-- Resilience -->    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="8.0.0" />    <PackageReference Include="Polly" Version="8.6.5" />
With:
    <!-- Resilience -->
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="8.10.0" />
    <!-- Resilience -->    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="8.10.0" />
1b. Add missing Serilog.Settings.Configuration package (needed for ReadFrom.Configuration() in Program.cs):
Replace:
    <!-- Logging -->
    <PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
    <!-- Logging -->    <PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />    <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
With:
    <!-- Logging -->
    <PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Serilog.Settings.Configuration" Version="8.0.4" />
    <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
    <!-- Logging -->    <PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />    <PackageReference Include="Serilog.Settings.Configuration" Version="8.0.4" />    <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
1c. Replace unavailable Microsoft.Extensions.Configuration.Memory with Microsoft.Extensions.Configuration (the base package includes in-memory collection support in v8+):
Replace:
    <PackageReference Include="Microsoft.Extensions.Configuration.Memory" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Memory" Version="8.0.0" />
With:
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
Fix 2: WebScraper/Services/DataProviderFactory.cs — Missing using + wrong namespace
2a. Replace Polly.Retry using with Microsoft.Extensions.Http.Resilience:
Replace:
using Polly.Retry;
using Polly.Retry;
With:
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Http.Resilience;
2b. Fix fully-qualified circuit breaker type (line 140):
Replace:
        builder.AddCircuitBreaker(new Polly.CircuitBreaker.HttpCircuitBreakerStrategyOptions
        builder.AddCircuitBreaker(new Polly.CircuitBreaker.HttpCircuitBreakerStrategyOptions
With:
        builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
Fix 3: WebScraper/Services/Scrapers/GameScraperService.cs — Wrong class reference (line 198)
GameScraperService calls TeamScraperService.PfrToNflAbbreviation() but that method doesn't exist on TeamScraperService. GameScraperService already has its own copy at line 202.
Replace:
        return TeamScraperService.PfrToNflAbbreviation(pfrAbbr);
        return TeamScraperService.PfrToNflAbbreviation(pfrAbbr);
With:
        return PfrToNflAbbreviation(pfrAbbr);
        return PfrToNflAbbreviation(pfrAbbr);
Fix 4: tests/WebScraper.Tests/WebScraper.Tests.csproj — Unavailable package
Same as Fix 1c — replace the unavailable package:
Replace:
    <PackageReference Include="Microsoft.Extensions.Configuration.Memory" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Memory" Version="8.0.0" />
With:
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
Fix 5: Create tests/WebScraper.Tests/GlobalUsings.cs — Missing using directives
All test files are missing using Xunit; and the repository tests are missing the EF Core using (needed for CloseConnection()). Create this new file:
File: tests/WebScraper.Tests/GlobalUsings.cs
global using Microsoft.EntityFrameworkCore;
global using Xunit;
global using Microsoft.EntityFrameworkCore;global using Xunit;
Summary Table
#	File	Error Code	Root Cause	Fix
1a	WebScraper.csproj	CS0246	Polly 8.6.5 incompatible with Http.Resilience 8.0.0	Upgrade to Http.Resilience 8.10.0, remove standalone Polly
1b	WebScraper.csproj	CS1061	Missing Serilog.Settings.Configuration	Add package v8.0.4
1c	WebScraper.csproj	NU1101	Configuration.Memory package unavailable	Use Configuration v8.0.0
2a	DataProviderFactory.cs	CS0246	Missing using for HttpRetryStrategyOptions	Add using Microsoft.Extensions.Http.Resilience
2b	DataProviderFactory.cs	CS0234	Wrong namespace for circuit breaker type	Remove Polly.CircuitBreaker. prefix
3	GameScraperService.cs	CS0117	Calls non-existent method on wrong class	Call own PfrToNflAbbreviation
4	WebScraper.Tests.csproj	NU1101	Same as 1c	Same fix
5	GlobalUsings.cs (new)	CS0246 (402 errors)	Missing using Xunit; and using Microsoft.EntityFrameworkCore;	Create GlobalUsings.cs
After all fixes: dotnet clean && dotnet build succeeds with 0 errors, and dotnet test passes all 194 tests.