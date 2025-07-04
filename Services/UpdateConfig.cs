namespace chronos_screentime.Services
{
    public static class UpdateConfig
    {
        // Current version of the application
        public const string CurrentVersion = "1.1.7";
        
        // GitHub repository information
        public const string RepositoryOwner = "ghassanelgendy";
        public const string RepositoryName = "chronos-screentime";
        
        // Update URLs - change these when you release new versions
        public const string LatestVersion = "v1.1.7";
        public static string ManifestUrl => $"https://github.com/{RepositoryOwner}/{RepositoryName}/releases/download/{LatestVersion}/manifest.json";
        
        // Update check interval (24 hours)
        public static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(24);
    }
} 