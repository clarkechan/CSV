# CSV 合并工具 v3.2

> 将两台检测机台输出的 CSV 按条码自动合并为一份，并持续监控目录、实时合并新数据。

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows&logoColor=white)

## 功能

- **按条码合并**：以 `2D Barcode` 为键，把机台 1 和机台 2 的记录合并为一行
- **自动判定**：任一字段为 `NG` 即判整条记录为 NG，写入 `OK(1)/NG(0)` 和 `Data_Bin` 两列
- **班次标记**：根据 `Time` 自动生成 `Shift_D/N`（08:00–20:00 为白班 D，其余为夜班 N）
- **字段覆盖**：按配置文件中的 `modifications` 统一改写指定字段的值
- **实时监控**：启动时先合并一次存量文件，之后监听目录，有新 CSV 就自动合并
- **后台运行**：启动后隐藏控制台窗口，并自动关闭已在运行的旧进程
- **日志**（v3.2 新增）：所有操作和异常都会写入程序目录下的 `merge_program.log`

## 目录约定

```
sourceDir1/<文件夹>/<前缀>_xxx.CSV      ← 机台 1
sourceDir2/<文件夹>/<前缀>_yyy.CSV      ← 机台 2
destinationDir/<前缀>_xxx.CSV          ← 合并结果
```

两个机台下名称相同的文件夹，以及文件名前缀（第一个 `_` 之前的部分）相同的 CSV，会被识别为同一组进行合并。

## 配置

在程序目录下放置 `config.json`：

```json
{
  "paths": {
    "sourceDir1": "D:\\Machine1",
    "sourceDir2": "D:\\Machine2",
    "destinationDir": "D:\\Merged"
  },
  "modifications": {
    "字段名": "统一改写后的值"
  }
}
```

## 构建与运行

```bash
dotnet build -c Release
```

运行生成的 `CSV.exe` 即可（会在后台常驻）。依赖：[CsvHelper](https://joshclose.github.io/CsvHelper/)、Newtonsoft.Json。

## License

见 [LICENSE.txt](LICENSE.txt)。
