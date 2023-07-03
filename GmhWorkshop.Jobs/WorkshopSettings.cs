﻿namespace GmhWorkshop.Jobs
{
    public class WorkshopSettings
    {
        public string BirdPiBackupDirectory { get; set; }
        public string BirdPiHost { get; set; }
        public string BirdPiRemoteHomeDirectory { get; set; }
        public string BirdPiSftpPassword { get; set; }
        public string BirdPiSftpUser { get; set; }
        public string SensorPushEmail { get; set; }
        public string SensorPushPassword { get; set; }
        public string TempestAccessToken { get; set; }
        public int TempestDeviceId { get; set; }
        public string TempestFileBackupDirectory { get; set; }
        public int TempestMonthsBack { get; set; }
        public int SensorPushMonthsBack { get; set; }
        public string SensorPushBackupDirectory { get; set; }
    }
}