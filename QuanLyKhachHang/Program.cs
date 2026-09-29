using System.Diagnostics;
using Velopack;
using Velopack.Sources;

namespace QuanLyKhachHang
{
    internal static class Program
    {
        private const string UpdateRepositoryUrl = "https://github.com/caydankidieu1/QuanLyKhachHang";

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            VelopackApp.Build()
                .SetAutoApplyOnStartup(true)
                .Run();

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            _ = DownloadUpdatesInBackgroundAsync();
            Application.Run(new MainMenuForm());
        }

        private static async Task DownloadUpdatesInBackgroundAsync()
        {
            var updateManager = new UpdateManager(
                new GithubSource(UpdateRepositoryUrl, null, false));

            if (!updateManager.IsInstalled)
            {
                return;
            }

            try
            {
                var update = await updateManager.CheckForUpdatesAsync();
                if (update is not null)
                {
                    await updateManager.DownloadUpdatesAsync(update);
                }
            }
            catch (Exception exception)
            {
                Trace.WriteLine($"Unable to check for updates: {exception}");
            }
        }
    }
}