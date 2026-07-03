using ISO11820.Global;
using ISO11820.Models;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using OfficeOpenXml.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using Serilog;

namespace ISO11820.Services;

/// <summary>
/// 导出服务 — CSV / Excel / PDF
/// </summary>
public class ExportService
{
    /// <summary>
    /// 导出CSV文件（试验数据）
    /// </summary>
    public string ExportCsv(TestMaster test, List<string> csvLines, string baseDir)
    {
        var dir = Path.Combine(baseDir, "TestData", test.ProductId, test.TestId);
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, "sensor_data.csv");

        var header = "Time,Temp1,Temp2,TempSurface,TempCenter,TempCalibration";
        File.WriteAllText(filePath, header + "\n" + string.Join("\n", csvLines));
        Log.Information("CSV导出完成: {Path}", filePath);
        return filePath;
    }

    /// <summary>
    /// 导出Excel报告
    /// </summary>
    public string ExportExcel(TestMaster test, List<string> csvLines, string baseDir, string chartImagePath = "")
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var dir = Path.Combine(baseDir, "Reports");
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"{test.TestId}_报告.xlsx");

        using var package = new ExcelPackage();
        var ctx = AppRuntime.Instance;
        var config = ctx.Configuration;

        // ===== Sheet1: 试验信息 =====
        var sheet1 = package.Workbook.Worksheets.Add("试验信息");
        sheet1.Cells["A1"].Value = "ISO 11820 建筑材料不燃性试验报告";
        sheet1.Cells["A1"].Style.Font.Size = 16;
        sheet1.Cells["A1"].Style.Font.Bold = true;

        int row = 3;
        void AddInfo(string label, string value)
        {
            sheet1.Cells[$"A{row}"].Value = label;
            sheet1.Cells[$"B{row}"].Value = value;
            sheet1.Cells[$"A{row}"].Style.Font.Bold = true;
            row++;
        }

        AddInfo("试验编号", test.TestId);
        AddInfo("样品编号", test.ProductId);
        AddInfo("样品名称", ctx.Db.GetProduct(test.ProductId)?.ProductName ?? "");
        AddInfo("试验日期", test.TestDate.ToString("yyyy-MM-dd"));
        AddInfo("操作员", test.Operator);
        AddInfo("环境温度", $"{test.AmbTemp:F1} ℃");
        AddInfo("环境湿度", $"{test.AmbHumi:F1} %");
        AddInfo("试验前质量", $"{test.PreWeight:F2} g");
        AddInfo("试验后质量", $"{test.PostWeight:F2} g");
        AddInfo("失重量", $"{test.LostWeight:F2} g");
        AddInfo("失重率", $"{test.LostWeightPer:F2} %");
        AddInfo("炉温1温升", $"{test.DeltaTf1:F1} ℃");
        AddInfo("炉温2温升", $"{test.DeltaTf2:F1} ℃");
        AddInfo("表面温升", $"{test.DeltaTs:F1} ℃");
        AddInfo("中心温升", $"{test.DeltaTc:F1} ℃");
        AddInfo("样品温升", $"{test.DeltaTf:F1} ℃");
        AddInfo("总试验时长", $"{test.TotalTestTime} 秒");
        AddInfo("火焰发生时刻", $"{test.FlameTime} 秒");
        AddInfo("火焰持续时间", $"{test.FlameDuration} 秒");
        AddInfo("恒功率值", $"{test.ConstPower}");

        row++;
        // 判定结论
        bool passed = test.DeltaTf <= 50 && test.LostWeightPer <= 50 && test.FlameDuration < 5;
        sheet1.Cells[$"A{row}"].Value = "判定结论";
        sheet1.Cells[$"A{row}"].Style.Font.Bold = true;
        sheet1.Cells[$"B{row}"].Value = passed ? "通过" : "不通过";
        sheet1.Cells[$"B{row}"].Style.Font.Color.SetColor(passed ? System.Drawing.Color.Green : System.Drawing.Color.Red);
        sheet1.Cells[$"B{row}"].Style.Font.Bold = true;
        sheet1.Cells[$"B{row}"].Style.Font.Size = 14;

        sheet1.Column(1).Width = 20;
        sheet1.Column(2).Width = 30;

        // ===== Sheet2: 温度数据 =====
        var sheet2 = package.Workbook.Worksheets.Add("温度数据");
        sheet2.Cells["A1"].Value = "Time(s)";
        sheet2.Cells["B1"].Value = "炉温1(℃)";
        sheet2.Cells["C1"].Value = "炉温2(℃)";
        sheet2.Cells["D1"].Value = "表面温度(℃)";
        sheet2.Cells["E1"].Value = "中心温度(℃)";
        sheet2.Cells["F1"].Value = "校准温度(℃)";

        for (int i = 0; i < csvLines.Count; i++)
        {
            var parts = csvLines[i].Split(',');
            for (int j = 0; j < parts.Length && j < 6; j++)
            {
                sheet2.Cells[i + 2, j + 1].Value = double.TryParse(parts[j], out var v) ? v : parts[j];
            }
        }

        // ===== Sheet3: 温度曲线图 =====
        if (csvLines.Count > 0)
        {
            var sheet3 = package.Workbook.Worksheets.Add("温度曲线");
            // 复制数据用于图表
            for (int i = 0; i < csvLines.Count; i++)
            {
                var parts = csvLines[i].Split(',');
                for (int j = 0; j < parts.Length && j < 6; j++)
                {
                    sheet3.Cells[i + 1, j + 1].Value = double.TryParse(parts[j], out var v) ? v : parts[j];
                }
            }

            try
            {
                var chart = sheet3.Drawings.AddChart("TempChart", OfficeOpenXml.Drawing.Chart.eChartType.XYScatterLinesNoMarkers);
                chart.Title.Text = "温度变化曲线";
                chart.SetPosition(0, 0, 6, 0);
                chart.SetSize(800, 500);

                for (int col = 1; col <= 4; col++)
                {
                    var series = chart.Series.Add(
                        sheet3.Cells[1, col + 1, csvLines.Count, col + 1],
                        sheet3.Cells[1, 1, csvLines.Count, 1]);
                    var names = new[] { "", "炉温1", "炉温2", "表面温度", "中心温度" };
                    series.Header = names[col];
                }
            }
            catch
            {
                // EPPlus 图表创建失败时静默跳过，仅有数据
                Log.Warning("Excel图表创建失败，已跳过");
            }
        }

        package.SaveAs(new FileInfo(filePath));
        Log.Information("Excel导出完成: {Path}", filePath);
        return filePath;
    }

    /// <summary>
    /// 导出PDF报告
    /// </summary>
    public string ExportPdf(TestMaster test, List<string> csvLines, string baseDir, string chartImagePath = "")
    {
        var dir = Path.Combine(baseDir, "Reports");
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"{test.TestId}_报告.pdf");

        using var document = new PdfDocument();
        document.Info.Title = $"ISO 11820 试验报告 - {test.TestId}";
        document.Info.Author = test.Operator;

        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        XGraphics gfx = XGraphics.FromPdfPage(page);
        var fontTitle = new XFont("Microsoft YaHei", 18, XFontStyleEx.Bold);
        var fontH2 = new XFont("Microsoft YaHei", 13, XFontStyleEx.Bold);
        var fontNormal = new XFont("Microsoft YaHei", 10, XFontStyleEx.Regular);
        var fontSmall = new XFont("Microsoft YaHei", 9, XFontStyleEx.Regular);

        double y = 40;

        // 标题
        gfx.DrawString("ISO 11820 建筑材料不燃性试验报告", fontTitle, XBrushes.Black,
            new XRect(0, y, page.Width, 30), XStringFormats.TopCenter);
        y += 40;

        // 分隔线
        gfx.DrawLine(XPens.Gray, 40, y, page.Width - 40, y);
        y += 20;

        // 试验概要
        var ctx = AppRuntime.Instance;
        var product = ctx.Db.GetProduct(test.ProductId);

        void DrawRow(string label, string value)
        {
            gfx.DrawString(label, fontH2, XBrushes.Black, 50, y);
            gfx.DrawString(value, fontNormal, XBrushes.Black, 200, y);
            y += 20;
        }

        DrawRow("试验编号：", test.TestId);
        DrawRow("样品编号：", test.ProductId);
        DrawRow("样品名称：", product?.ProductName ?? "");
        DrawRow("规格型号：", product?.Specific ?? "");
        DrawRow("试验日期：", test.TestDate.ToString("yyyy-MM-dd"));
        DrawRow("操作员：", test.Operator);
        DrawRow("环境温度：", $"{test.AmbTemp:F1} ℃");
        DrawRow("环境湿度：", $"{test.AmbHumi:F1} %");
        DrawRow("试验前质量：", $"{test.PreWeight:F2} g");
        DrawRow("试验后质量：", $"{test.PostWeight:F2} g");
        DrawRow("失重率：", $"{test.LostWeightPer:F2} %");
        DrawRow("样品温升：", $"{test.DeltaTf:F1} ℃");
        DrawRow("试验时长：", $"{test.TotalTestTime} 秒");
        DrawRow("火焰持续时间：", $"{test.FlameDuration} 秒");

        y += 10;
        gfx.DrawLine(XPens.Gray, 40, y, page.Width - 40, y);
        y += 20;

        // 判定结论
        bool passed = test.DeltaTf <= 50 && test.LostWeightPer <= 50 && test.FlameDuration < 5;
        var resultFont = new XFont("Microsoft YaHei", 14, XFontStyleEx.Bold);
        var resultBrush = passed ? XBrushes.Green : XBrushes.Red;
        gfx.DrawString($"判定结论：{(passed ? "通过" : "不通过")}", resultFont, resultBrush, 50, y);
        y += 30;

        // 判定依据
        gfx.DrawString($"判定标准：ΔT ≤ 50℃, 失重率 ≤ 50%, 火焰持续时间 < 5s", fontSmall, XBrushes.DimGray, 50, y);
        y += 30;

        // 温度曲线图（如果有图片）
        if (!string.IsNullOrEmpty(chartImagePath) && File.Exists(chartImagePath))
        {
            try
            {
                using var img = XImage.FromFile(chartImagePath);
                double imgW = page.Width - 100;
                double imgH = img.PixelHeight * imgW / img.PixelWidth;
                if (imgH > 300) { imgH = 300; imgW = img.PixelWidth * 300 / img.PixelHeight; }
                gfx.DrawImage(img, 50, y, imgW, imgH);
                y += imgH + 20;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "PDF中插入曲线图失败");
            }
        }

        // 温度数据摘要表
        gfx.DrawLine(XPens.Gray, 40, y, page.Width - 40, y);
        y += 20;

        gfx.DrawString("温度数据摘要", fontH2, XBrushes.Black, 50, y);
        y += 20;

        // 表头
        string[] headers = { "时间(s)", "炉温1", "炉温2", "表面温", "中心温" };
        double[] colX = { 50, 120, 200, 280, 360 };
        double colW = (page.Width - 100) / 5;

        for (int i = 0; i < headers.Length; i++)
        {
            gfx.DrawString(headers[i], fontSmall, XBrushes.Black, colX[i], y);
        }
        y += 15;

        // 每隔一定间隔显示数据行（最多30行）
        int step = Math.Max(1, csvLines.Count / 30);
        for (int i = 0; i < csvLines.Count; i += step)
        {
            var parts = csvLines[i].Split(',');
            if (parts.Length >= 5)
            {
                for (int j = 0; j < 5 && j < parts.Length; j++)
                    gfx.DrawString(parts[j], fontSmall, XBrushes.Black, colX[j], y);
                y += 14;

                // 换页检查
                if (y > page.Height - 50)
                {
                    y = 40;
                    page = document.AddPage();
                    gfx.Dispose();
                    gfx = XGraphics.FromPdfPage(page);
                }
            }
        }

        gfx.Dispose();
        document.Save(filePath);
        Log.Information("PDF导出完成: {Path}", filePath);
        return filePath;
    }
}
