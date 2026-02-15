using System;
using Gearify.SharedKernel.Logging;
using Microsoft.AspNetCore.Builder;
﻿using Serilog;

Log.Logger = SerilogBootstrap.CreateConsole().CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    var startup = new Gearify.OrderService.Startup(builder.Configuration);
    startup.ConfigureServices(builder.Services);

    var app = builder.Build();

    startup.Configure(app, app.Environment);

    Log.Information("Order Service starting...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
