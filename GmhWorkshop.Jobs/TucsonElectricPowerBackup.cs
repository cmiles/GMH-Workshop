﻿using System.Globalization;
using System.IO.Compression;
using System.Xml.Serialization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Playwright;
using Pw02.GreenButtonXml;
using Pw02.TepCsv;
using Serilog;

namespace GmhWorkshop.Jobs;

/// <summary>
/// A Playwright based Backup for TEP Data - handles multiple accounts but doesn't currently
/// process multiple meters per account.
/// </summary>
public static class TucsonElectricPowerBackup
{
    public static async Task Run(WorkshopSettings settings, IProgress<string> progress)
    {
        Log.Information("Starting {jobName}", "TucsonElectricPowerBackup");

        if (string.IsNullOrWhiteSpace(settings.TepEmail))
        {
            Log.Error("TepEmail is Blank");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.TepPassword))
        {
            Log.Error("TepPassword is Blank");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.TepBackupDirectory))
        {
            Log.Error("TepBackupDirectory is Blank");
            return;
        }

        var backupDirectory = new DirectoryInfo(settings.TepBackupDirectory);
        if (!backupDirectory.Exists)
        {
            backupDirectory.Create();
        }

        progress.Report("Starting Playwright and Logging into TEP");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        var context = await browser.NewContextAsync();

        var page = await context.NewPageAsync();

        await page.GotoAsync("https://account.tep.com/MyAccount/Login");

        await page.WaitForLoadStateAsync();

        await page.GetByLabel("E-mail*").ClickAsync();

        await page.GetByLabel("E-mail*").FillAsync(settings.TepEmail);

        await page.GetByLabel("Password*").ClickAsync();

        await page.GetByLabel("Password*").FillAsync(settings.TepPassword);

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Login" }).ClickAsync();

        //2023-7-7: There are maybe un-needed delays throughout this code - the target for this routine
        //is an unattended ?weekly run of the code so it doesn't matter - I'd like for the
        //code to be cleaner but mainly I want it to run without issue...
        await Task.Delay(1000);

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Task.Delay(5000);

        //2023-7-7: This is not what is recommended in the Playwright documentation but esp. as a
        //newer Playwright user I'm a bit puzzled about a better way. I suspect that 
        //Playwright's emphasis on testing over scraping influences the suggestions in
        //the docs about what is advisable? Also this apparent mis-spelling is 'correct'...
        var accountLinksSelector = "css=#AcctSelTabel > tbody > tr > td:first-of-type > a";

        var countOfAccounts = (await page.Locator(accountLinksSelector).AllAsync()).Count;

        var frozenNow = DateTime.Now;


        for (var i = 0; i < countOfAccounts; i++)
        {
            //Getting the accounts each time could produce odd errors if the re-loaded page featured
            //different accounts or different orders but that seems unlikely.
            var accounts = await page.Locator(accountLinksSelector).AllAsync();

            var accountNumber = await accounts[i].TextContentAsync();

            Log.Verbose("Starting Processing for TEP Account {accountNumber}", accountNumber);

            await accounts[i].ClickAsync();

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Task.Delay(3000);

            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Download Usage" }).ClickAsync();

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Task.Delay(1000);

            await page.GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = "CSV" }).CheckAsync();

            var csvDownload = await page.RunAndWaitForDownloadAsync(async () =>
            {
                await page.GetByRole(AriaRole.Button,
                        new PageGetByRoleOptions { Name = "Downloads usage data Download" })
                    .ClickAsync();
            });
            
            var csvTempFile =
                new FileInfo(Path.Combine(backupDirectory.FullName,
                    @$"TempDownload-TEP-Account-{accountNumber}-CSV-{frozenNow:yyyy-MM-dd-HH-mm-ss}.csv"));

            await csvDownload.SaveAsAsync(csvTempFile.FullName);
            await csvDownload.DeleteAsync();

            progress.Report($"Saved {csvTempFile}");

            var configuration = new CsvConfiguration(CultureInfo.InvariantCulture);
            var csvRows = new List<TepCsvRow>();

            using (var reader = new StreamReader(csvTempFile.FullName))
            using (var csv = new CsvReader(reader, configuration))
            {
                await csv.ReadAsync();
                await csv.ReadAsync();

                csv.ReadHeader();
                csvRows.AddRange(csv.GetRecords<TepCsvRow>());
            }

            progress.Report($"Found {csvRows.Count} CSV Entries in {csvTempFile.FullName}");

            var startDate = csvRows.MinBy(x => x.Date)!.Date;

            //The last entry for a day will list 12am as the end time - meaning 0 on the following day.
            var lastDateEntry = csvRows.OrderBy(x => x.Date).ThenBy(x => x.StartTime).Last()!;
            var lastDate = lastDateEntry.EndTime == TimeOnly.MinValue ? lastDateEntry.Date.AddDays(1) : lastDateEntry.Date;

            var csvDatedFile =
                new FileInfo(Path.Combine(backupDirectory.FullName,
                    @$"TEP-Account-{accountNumber}-CSV-{startDate:yyyy-MM-dd}-to-{lastDate:yyyy-MM-dd}.csv"));

            if (!csvDatedFile.Exists)
            {
                csvTempFile.MoveTo(csvDatedFile.FullName);
                Log.Verbose("Saved TEP CSV Data to {fileName}", csvDatedFile.FullName);
            }
            else
            {
                csvTempFile.Delete();
                progress.Report($"No new data found for TEP {accountNumber}'s CSV Data Download.");
            }

            await Task.Delay(3000);

            await page.GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = "XML" }).CheckAsync();

            var xmlZipTempDownload = await page.RunAndWaitForDownloadAsync(async () =>
            {
                await page.GetByRole(AriaRole.Button,
                        new PageGetByRoleOptions { Name = "Downloads usage data Download" })
                    .ClickAsync();
            });

            var xmlTempZipFile =
                new FileInfo(Path.Combine(backupDirectory.FullName,
                    @$"TempDownload-TEP-Account-{accountNumber}-XML-ZIP-{frozenNow:yyyy-MM-dd-HH-mm-ss}.zip"));

            await xmlZipTempDownload.SaveAsAsync(xmlTempZipFile.FullName);
            await xmlZipTempDownload.DeleteAsync();

            progress.Report("Processing TEP XML Zip File");

            using (var xmlZipArchive = new ZipArchive(new FileStream(xmlTempZipFile.FullName, FileMode.Open)))
            {
                var xmlFiles = xmlZipArchive.Entries.Where(x => x.FullName.EndsWith(".xml")).ToList();

                for (var n = 0; n < xmlFiles.Count; n++)
                {
                    var temporaryXmlFile =
                        new FileInfo(Path.Combine(backupDirectory.FullName,
                            @$"TempDownload-TEP-Account-{accountNumber}-XML-{n}-{frozenNow:yyyy-MM-dd-HH-mm-ss}.xml"));
                    xmlFiles[n].ExtractToFile(temporaryXmlFile.FullName);

                    var serializer = new XmlSerializer(typeof(feed));

                    var xmlGreenButtonReader = new StreamReader(temporaryXmlFile.FullName);
                    var xmlReadings = (feed)serializer.Deserialize(xmlGreenButtonReader);
                    xmlGreenButtonReader.Close();

                    var observationInterval = xmlReadings.entry.First(x => x.content.IntervalBlock is { Length: > 0 })
                        .content
                        .IntervalBlock.SelectMany(y => y.IntervalReading).ToList();

                    progress.Report($"Found {observationInterval.Count} XML Intervals");

                    var startIntervalEntry = observationInterval.MinBy(x => x.timePeriod.start);
                    var endIntervalEntry = observationInterval.MaxBy(x => x.timePeriod.start);

                    var startDateTime = DateTimeOffset.FromUnixTimeSeconds(startIntervalEntry.timePeriod.start)
                        .Add(xmlReadings.updated.Offset);
                    var endDateTime = DateTimeOffset.FromUnixTimeSeconds(endIntervalEntry.timePeriod.start)
                        .AddSeconds(endIntervalEntry.timePeriod.duration).Add(xmlReadings.updated.Offset);

                    var finalFile = new FileInfo(Path.Combine(backupDirectory.FullName,
                        @$"TEP-Account-{accountNumber}-XML-{startDateTime:yyyy-MM-dd}-to-{endDateTime:yyyy-MM-dd}.xml"));

                    if (!finalFile.Exists)
                    {
                        temporaryXmlFile.MoveTo(finalFile.FullName);
                        Log.Verbose("Saved TEP XML Data to {fileName}", finalFile.FullName);
                    }
                    else
                    {
                        temporaryXmlFile.Delete();
                        progress.Report($"No new data found for TEP {accountNumber}'s XML Data Download.");
                    }
                }
            }

            xmlTempZipFile.Delete();

            await page.GotoAsync("https://account.tep.com/MyAccount/AccountSelection");

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Task.Delay(5000);
        }

        await page.GetByRole(AriaRole.Button, new() { Name = "Log Out" }).ClickAsync();

        Log.Information("Finished {jobName}", "TucsonElectricPowerBackup");
    }
}