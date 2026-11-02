using NetworkProfiles.Core.Execution;
using NetworkProfiles.Service;

// Background service that applies adapter settings (IP addresses, gateway,
// DNS) on behalf of the Network Profiles app, so the app itself can run
// without administrator rights. It runs as LocalSystem and accepts requests
// on a local named pipe.
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = PipeServer.ServiceName);
builder.Services.AddSingleton(new NetworkConfigurator(new NetshRunner()));
builder.Services.AddSingleton<RequestHandler>();
builder.Services.AddHostedService<PipeServer>();

builder.Build().Run();
