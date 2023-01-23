using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Universal_Updater
{
    class DownloadPackages
    {
        static Tuple<DateTime, long, long> DownloadingProgress = new Tuple<DateTime, long, long>(DateTime.MinValue, 0, 0);
        static string[] installedPackages = File.ReadAllLines(@"C:\ProgramData\Universal Updater\InstalledPackages.csv");
        static readonly string[] filteredPackages = new string[installedPackages.Length];
        static bool isFeatureInstalled = false;
        static readonly string[] knownPackages = { "ms_bootsequence_retail.efiesp.spkg", "ms_bootsequence_retail.mainos.spkg", "ms_commsenhancementglobal.mainos.spkg", "ms_commsmessagingglobal.mainos.spkg", "microsoftphonefm.platformmanifest.efiesp", "microsoftphonefm.platformmanifest.mainos", "microsoftphonefm.platformmanifest.updateos", "UserInstallableFM.PlatformManifest" };
        static readonly string[] featurePackages = { "MS_RCS_FEATURE_PACK.MainOS.cbsr", "ms_projecta.mainos" };
        static readonly string[] packageExtension = { ".spkg", ".cbs_", ".cab" };
        static int packagePosition = 0;
        static Process downloadProcess;
        static Uri downloadFile;
        static string localPath, localFile;
        static long fileSize;

        public static bool OnlineUpdate(string updateBuild)
        {
            for (int i = 1; i < installedPackages.Length; i++)
            {
                for (int j = 0; j < packageExtension.Length; j++)
                {
                    var requiredPackages = Program.GetResourceFile($"{updateBuild}.txt", 0).Split("\n").Where(k => k.Contains(installedPackages[i].Split(',')[1] + packageExtension[j], StringComparison.OrdinalIgnoreCase)).ToArray();
                    foreach (string packages in requiredPackages)
                    {
                        if (packages.Contains("ms_projecta.mainos", StringComparison.OrdinalIgnoreCase) || packages.Contains("MS_RCS_FEATURE_PACK.MainOS.cbsr", StringComparison.OrdinalIgnoreCase))
                        {
                            isFeatureInstalled = true;
                        }
                        filteredPackages[packagePosition++] = packages;
                        break;
                    }
                }
            }
            for (int i = 0; i < knownPackages.Length; i++)
            {
                if (!string.Join("\n", filteredPackages).Contains(knownPackages[i], StringComparison.OrdinalIgnoreCase))
                {
                    var knownPackage = Program.GetResourceFile($"{updateBuild}.txt", 0).Split("\n").Where(j => j.Contains(knownPackages[i], StringComparison.OrdinalIgnoreCase)).ToArray();
                    foreach (string package in knownPackage)
                    {
                        filteredPackages[packagePosition++] = package;
                        break;
                    }
                }
            }
            return isFeatureInstalled;
        }

        public static void OfflineUpdate(string packagePath, string pushFeature)
        {
            for (int i = 1; i < installedPackages.Length; i++)
            {
                for (int j = 0; j < packageExtension.Length; j++)
                {
                    var requiredPackages = Directory.GetFiles(packagePath).Where(k => k.Contains(installedPackages[i].Split(',')[1] + packageExtension[j], StringComparison.OrdinalIgnoreCase)).ToArray();
                    foreach (string packages in requiredPackages)
                    {
                        filteredPackages[packagePosition++] = packages;
                        break;
                    }
                }
            }
            for (int i = 0; i < knownPackages.Length; i++)
            {
                if (!string.Join("\n", filteredPackages).Contains(knownPackages[i], StringComparison.OrdinalIgnoreCase))
                {
                    var knownPackage = Directory.GetFiles(packagePath).Where(j => j.Contains(knownPackages[i], StringComparison.OrdinalIgnoreCase)).ToArray();
                    foreach (string package in knownPackage)
                    {
                        filteredPackages[packagePosition++] = package;
                        break;
                    }
                }
            }
            if (pushFeature == "Y")
            {
                for (int i = 0; i < featurePackages.Length; i++)
                {
                    var requiredPackages = Directory.GetFiles(packagePath).Where(j => j.Contains(featurePackages[i], StringComparison.OrdinalIgnoreCase)).ToArray();
                    foreach (string packages in requiredPackages)
                    {
                        filteredPackages[packagePosition++] = packages;
                        break;
                    }
                }
            }
            for (int i = 0; i < filteredPackages.Where(j => !string.IsNullOrWhiteSpace(j)).Count(); i++)
            {
                Console.WriteLine($@"[{i + 1}/{filteredPackages.Where(j => !string.IsNullOrWhiteSpace(j)).Count()}] {filteredPackages[i].Split("\\").Last()}");
                File.Copy(filteredPackages[i], $@"{Environment.CurrentDirectory}\{GetDeviceInfo.SerialNumber[0]}\Packages\{filteredPackages[i].Split("\\").Last()}", true);
            }
        }

        public static async Task DownloadUpdate(string update)
        {
            WebClient client = new WebClient();
            int downloadMode = 0;

            downloadProcess = new Process();
            downloadProcess.StartInfo.FileName = @"C:\ProgramData\Universal Updater\wget.exe";
            localPath = $@"{Environment.CurrentDirectory}\{GetDeviceInfo.SerialNumber[0]}\Packages\{update}";

            for (int i = 0; i < filteredPackages.Where(j => !string.IsNullOrWhiteSpace(j)).Count(); i++)
            {
                downloadFile = new Uri(filteredPackages[i]);
                localFile = downloadFile.LocalPath.Split("/").Last();
                Console.WriteLine($@"[{i + 1}/{filteredPackages.Where(j => !string.IsNullOrWhiteSpace(j)).Count()}] {downloadFile.LocalPath.Split("/").Last()}");
                if (update == "15254.603")
                {
                    // Get File Size
                    int downloadCount = 5;
                    do
                    {
                        await DownloadUpdateGetLength();
                        if (fileSize > 0)
                            break;

                        if (--downloadCount > 0)
                        {
                            Console.WriteLine(">>> [Error] Download Fail. Wait 10s and do again.");
                            await Task.Delay(10000);
                            //System.Threading.Thread.Sleep(10000);
                            continue;
                        }
                        else
                        {
                            Console.WriteLine(">>> [Error] Download Fail. PROGRAMM STOPPED.");
                            Console.ReadKey();
                            Environment.Exit(-1);
                        }
                    } while (true);

                    // Clear download flag
                    if (downloadMode == -1 || downloadMode == 1)
                        downloadMode = 0;

                    // Use Local Save
                    FileInfo fileInfo = new FileInfo($@"{localPath}\{localFile}");
                    if(fileInfo.Exists && fileSize == fileInfo.Length)
                    {
                        if (downloadMode == 0)
                        {                            
                            do
                            {
                                Console.WriteLine($@">>> [Found] File allready exists. Download again? y - Yes. n - No. a - Download all. s - Skip all exists.");
                                ConsoleKeyInfo key = Console.ReadKey(true);
                                switch (key.KeyChar) {
                                    case 'y': downloadMode = 1; break;
                                    case 'n': downloadMode = -1; break;
                                    case 'a': downloadMode = 2; break;
                                    case 's': downloadMode = -2; break;
                                }

                            } while (downloadMode == 0);
                        }
                    }

                    if(downloadMode < 0 && fileInfo.Exists && fileSize == fileInfo.Length)
                        continue;

                    // Download
                    do
                    {
                        downloadProcess.StartInfo.Arguments = $@"-q -c -P ""{localPath}"" {downloadFile} --no-check-certificate --show-progress";
                        //downloadProcess.StartInfo.Arguments = $@"-q -c -P ""{Environment.CurrentDirectory}\{GetDeviceInfo.SerialNumber[0]}\Packages"" {downloadFile} --no-check-certificate --show-progress";
                        downloadProcess.StartInfo.RedirectStandardOutput = false;
                        downloadProcess.StartInfo.RedirectStandardError = false;
                        downloadProcess.StartInfo.RedirectStandardInput = false;
                        downloadProcess.Start();
                        Console.Title = "Universal Updater.exe";
                        downloadProcess.WaitForExit();
                    }
                    while (fileSize != new FileInfo($@"{localPath}\{localFile}").Length);
                }
                else
                {
                    var fileStream = client.OpenRead(downloadFile);
                    var fileSize = Convert.ToInt64(client.ResponseHeaders["Content-Length"]);
                    fileStream.Close();
                    do
                    {
                        client.DownloadProgressChanged += DownloadProgressChanged;
                        await client.DownloadFileTaskAsync(downloadFile, $@"{localPath}\{localFile}");
                        Console.Title = "Universal Updater.exe";
                    }
                    while (fileSize != new FileInfo($@"{localPath}\{localFile}").Length);
                }
            }
        }

        private static async Task DownloadUpdateGetLength()
        {
            downloadProcess.StartInfo.Arguments = $@"{downloadFile} --spider";
            downloadProcess.StartInfo.RedirectStandardOutput = true;
            downloadProcess.StartInfo.RedirectStandardError = true;
            downloadProcess.StartInfo.RedirectStandardInput = true;
            downloadProcess.Start();
            downloadProcess.WaitForExit();
            var logOutput = (await downloadProcess.StandardError.ReadToEndAsync()).Split("\n");
            var logLength = logOutput.Where(j => j.Contains("Length:", StringComparison.OrdinalIgnoreCase));

            if (logLength.Count() != 0)
                fileSize = Convert.ToInt64(logLength.ToArray()[0].Split(' ')[1]);
            else
                fileSize = 0;

            return ;
        }

        private static void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs downloadProgressChangedEventArgs)
        {
            DownloadingProgress = new Tuple<DateTime, long, long>(DateTime.Now, downloadProgressChangedEventArgs.TotalBytesToReceive, downloadProgressChangedEventArgs.BytesReceived);
            Console.Title = $"Universal Updater.exe  [Downloading: {downloadFile.LocalPath.Split("/").Last().Remove(28)}... {((DownloadingProgress.Item3 * 100) / DownloadingProgress.Item2)}% - {DownloadingProgress.Item3}/{DownloadingProgress.Item2}]";
        }
    }
}
