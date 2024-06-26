﻿// See https://aka.ms/new-console-template for more information

using GmhWorkshop.Jobs;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Enrichers.CallerInfo;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .Enrich.WithCallerInfo(includeFileInfo: true,
        assemblyPrefix: "GmhWorkshop.",
        prefix: "gmhworkshop")
    .WriteTo.Console(restrictedToMinimumLevel:LogEventLevel.Verbose)
    .CreateLogger();

var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var workshopConfig =
    config.GetSection("WorkshopSettings").Get<WorkshopSettings>();

Console.WriteLine(workshopConfig.BirdPiHost);

//await EcobeeBackup.Run(workshopConfig, new Progress<string>(Console.WriteLine));
await TucsonElectricPowerBackup.Run(workshopConfig, new Progress<string>(Console.WriteLine));
await TempestWeatherBackup.Run(workshopConfig, new Progress<string>(Console.WriteLine));
await SensorPushBackup.Run(workshopConfig, new Progress<string>(Console.WriteLine));
await BirdPiBackup.Run(workshopConfig, new Progress<string>(Console.WriteLine));


Log.CloseAndFlush();