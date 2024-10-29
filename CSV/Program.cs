using CsvHelper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

class Program
{
    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    const int SW_HIDE = 0;
    const int SW_SHOW = 5;

    static Dictionary<string, string> modifications;
    static string sourceDir1;
    static string sourceDir2;
    static string destinationDir;

    static void Main(string[] args)
    {
        try
        {
            ClosePreviousProcesses();
            LoadConfig("config.json");

            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);

            Log("程序启动，开始执行合并操作...");
            MergeFiles(sourceDir1, sourceDir2, destinationDir);
            Task.Run(() => WatchDirectory(sourceDir1, sourceDir2, destinationDir));

            while (true)
            {
                Task.Delay(1000).Wait();
            }
        }
        catch (Exception ex)
        {
            Log($"程序崩溃，错误信息: {ex.Message}，堆栈信息: {ex.StackTrace}");
            throw;
        }
    }

    static void ClosePreviousProcesses()
    {
        try
        {
            string currentProcessName = Process.GetCurrentProcess().ProcessName;
            Process[] runningProcesses = Process.GetProcessesByName(currentProcessName);

            foreach (var process in runningProcesses)
            {
                if (process.Id != Process.GetCurrentProcess().Id)
                {
                    Log($"关闭进程: {process.Id}");
                    process.Kill();
                }
            }
        }
        catch (Exception ex)
        {
            Log($"关闭进程时出错: {ex.Message}");
        }
    }

    static void LoadConfig(string configPath)
    {
        try
        {
            string json = File.ReadAllText(configPath);
            JObject fullConfig = JObject.Parse(json);

            var paths = fullConfig["paths"];
            sourceDir1 = paths["sourceDir1"].ToString();
            sourceDir2 = paths["sourceDir2"].ToString();
            destinationDir = paths["destinationDir"].ToString();

            modifications = fullConfig["modifications"].ToObject<Dictionary<string, string>>();

            Log("配置文件加载成功");
        }
        catch (Exception ex)
        {
            Log($"加载配置文件时出错: {ex.Message}");
        }
    }

    static void WatchDirectory(string sourceDir1, string sourceDir2, string destinationDir)
    {
        var watcher1 = new FileSystemWatcher(sourceDir1);
        var watcher2 = new FileSystemWatcher(sourceDir2);

        watcher1.IncludeSubdirectories = true;
        watcher2.IncludeSubdirectories = true;

        watcher1.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
        watcher2.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;

        watcher1.Filter = "*.CSV";
        watcher2.Filter = "*.CSV";
        watcher1.Created += (s, e) => OnChanged(sourceDir1, sourceDir2, destinationDir);
        watcher2.Created += (s, e) => OnChanged(sourceDir1, sourceDir2, destinationDir);
        watcher1.Changed += (s, e) => OnChanged(sourceDir1, sourceDir2, destinationDir);
        watcher2.Changed += (s, e) => OnChanged(sourceDir1, sourceDir2, destinationDir);

        watcher1.EnableRaisingEvents = true;
        watcher2.EnableRaisingEvents = true;

        Log("文件监控已启动");

        while (true)
        {
            Task.Delay(1000).Wait();
        }
    }

    static void OnChanged(string sourceDir1, string sourceDir2, string destinationDir)
    {
        Log("检测到文件变化，开始检查是否可以合并...");
        MergeFiles(sourceDir1, sourceDir2, destinationDir);
    }

    static void MergeFiles(string sourceDir1, string sourceDir2, string destinationDir)
    {
        try
        {
            var folders1 = Directory.GetDirectories(sourceDir1);
            var folders2 = Directory.GetDirectories(sourceDir2);

            foreach (var folder1 in folders1)
            {
                var folderName = Path.GetFileName(folder1);
                var folder2 = folders2.FirstOrDefault(f => Path.GetFileName(f) == folderName);

                if (folder2 != null)
                {
                    var csvFiles1 = Directory.GetFiles(folder1, "*.CSV");
                    var csvFiles2 = Directory.GetFiles(folder2, "*.CSV");

                    foreach (var csvFile1 in csvFiles1)
                    {
                        var csvFile1Prefix = Path.GetFileName(csvFile1).Split('_')[0];
                        var csvFile2 = csvFiles2.FirstOrDefault(f => Path.GetFileName(f).StartsWith(csvFile1Prefix));

                        if (csvFile2 != null)
                        {
                            Log($"找到匹配的文件: {csvFile1} 和 {csvFile2}，开始合并...");

                            var mergedFilePath = Path.Combine(destinationDir, Path.GetFileName(csvFile1));
                            var csv1 = ReadCsvFile(csvFile1);
                            var csv2 = ReadCsvFile(csvFile2);

                            var mergedCsv = MergeCsvFiles(csv1, csv2);
                            WriteCsvFile(mergedFilePath, mergedCsv);

                            Log($"合并完成: {mergedFilePath}");
                        }
                        else
                        {
                            Log($"只找到一个文件: {csvFile1}，等待另一个机台的文件生成...");
                        }
                    }
                }
                else
                {
                    Log($"在机台 1 中找到文件夹: {folderName}，但在机台 2 中未找到相应文件夹，暂时跳过合并。");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"MergeFiles 方法出错，错误信息: {ex.Message}，堆栈信息: {ex.StackTrace}");
        }
    }
    
    static List<Dictionary<string, string>> ReadCsvFile(string filePath)
    {
        try
        {
            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<dynamic>().ToList();
                var recordList = new List<Dictionary<string, string>>();
                foreach (var record in records)
                {
                    var recordDict = ((IDictionary<string, object>)record)
                        .ToDictionary(k => k.Key, v => v.Value?.ToString() ?? "");
                    recordList.Add(recordDict);
                }
                Log($"成功读取 CSV 文件: {filePath}");
                return recordList;
            }
        }
        catch (Exception ex)
        {
            Log($"读取 CSV 文件时出错，文件路径: {filePath}，错误信息: {ex.Message}");
            return new List<Dictionary<string, string>>();
        }
    }

    static List<Dictionary<string, string>> MergeCsvFiles(List<Dictionary<string, string>> csv1, List<Dictionary<string, string>> csv2)
    {
        var mergedRecords = new List<Dictionary<string, string>>();

        var dict1 = csv1.ToDictionary(x => x["2D Barcode"]);
        var dict2 = csv2.ToDictionary(x => x["2D Barcode"]);

        foreach (var record1 in dict1)
        {
            var barcode = record1.Key;
            var mergedRecord = new Dictionary<string, string>(record1.Value);

            if (dict2.ContainsKey(barcode))
            {
                var record2 = dict2[barcode];
                //mergedRecord[""] = "\u0020";

                if (record2.ContainsKey("Time"))
                {
                    mergedRecord["time"] = record2["Time"];
                }

                if (record2.ContainsKey("Project"))
                {
                    mergedRecord["project"] = record2["Project"];
                }

                foreach (var key in record2.Keys)
                {
                    if (!mergedRecord.ContainsKey(key))
                    {
                        mergedRecord[key] = record2[key];
                    }
                }

                // D/N
                if (mergedRecord.ContainsKey("Time") && DateTime.TryParse(mergedRecord["Time"], out DateTime time))
                {
                    mergedRecord["Shift_D/N"] = (time.Hour >= 8 && time.Hour < 20) ? "D" : "N";
                }

                bool hasNG = mergedRecord.Values.Any(v => v.Equals("NG", StringComparison.OrdinalIgnoreCase))
                             || record2.Values.Any(v => v.Equals("NG", StringComparison.OrdinalIgnoreCase));

                mergedRecord["OK(1)/NG(0)"] = hasNG ? "NG" : "OK";
                mergedRecord["Data_Bin"] = hasNG ? "NG" : "OK";

                ApplyModifications(mergedRecord);
            }

            mergedRecords.Add(mergedRecord);
        }

        foreach (var record2 in dict2)
        {
            if (!dict1.ContainsKey(record2.Key))
            {
                mergedRecords.Add(new Dictionary<string, string>(record2.Value));
            }
        }

        return mergedRecords;
    }

    static void ApplyModifications(Dictionary<string, string> record)
    {
        foreach (var modification in modifications)
        {
            if (record.ContainsKey(modification.Key))
            {
                record[modification.Key] = modification.Value;
            }
        }
    }

    static void WriteCsvFile(string filePath, List<Dictionary<string, string>> records)
    {
        try
        {
            using (var writer = new StreamWriter(filePath))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                if (records.Count > 0)
                {
                    var headers = records.SelectMany(r => r.Keys).Distinct().ToList();
                    foreach (var header in headers)
                    {
                        csv.WriteField(header);
                    }
                    csv.NextRecord();

                    foreach (var record in records)
                    {
                        foreach (var header in headers)
                        {
                            csv.WriteField(record.ContainsKey(header) ? record[header] : "");
                        }
                        csv.NextRecord();
                    }
                    Log($"成功写入合并后的 CSV 文件: {filePath}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"写入 CSV 文件时出错，文件路径: {filePath}，错误信息: {ex.Message}");
        }
    }

    static void Log(string message)
    {
        string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "merge_program.log");
        try
        {
            using (var logWriter = new StreamWriter(logFilePath, true))
            {
                logWriter.WriteLine($"{DateTime.Now}: {message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"无法写入日志: {ex.Message}");
        }
    }
}
