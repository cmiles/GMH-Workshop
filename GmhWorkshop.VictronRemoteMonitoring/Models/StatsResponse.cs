﻿namespace GmhWorkshop.VictronRemoteMonitoring;

public class StatsResponse
{
    public bool success { get; set; }
    public StatsRecord[] records { get; set; }
    public StatsTotal[] totals { get; set; }
}