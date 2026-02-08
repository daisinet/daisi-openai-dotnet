using Daisi.OpenAI.Extensions;
using Daisi.OpenAI.Middleware;
using Daisi.SDK.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure DAISI network settings from appsettings.json
var daisiConfig = builder.Configuration.GetSection("Daisi");
DaisiStaticSettings.OrcIpAddressOrDomain = daisiConfig["OrcDomain"] ?? "orc.daisinet.com";
DaisiStaticSettings.OrcPort = daisiConfig.GetValue<int>("OrcPort", 443);
DaisiStaticSettings.OrcUseSSL = daisiConfig.GetValue<bool>("OrcUseSSL", true);

// Register all DAISI OpenAI services
builder.Services.AddDaisiOpenAI();

var app = builder.Build();

// Error handling middleware (outermost)
app.UseMiddleware<OpenAIErrorMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Map all OpenAI-compatible endpoints
app.MapOpenAIEndpoints();

app.Run();
