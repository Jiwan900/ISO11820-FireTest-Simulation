using PdfSharp.Fonts;

namespace ISO11820.Services;

/// <summary>
/// PDFsharp 系统字体解析器 — 解决 "No appropriate font found" 错误
/// </summary>
public class SystemFontResolver : IFontResolver
{
    public string DefaultFontName => "Arial";

    public byte[]? GetFont(string faceName)
    {
        // Windows 系统字体路径
        string fontPath = faceName switch
        {
            "Microsoft YaHei" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "msyh.ttc"),
            "Consolas"        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "consola.ttf"),
            _                 => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), faceName + ".ttf")
        };

        if (File.Exists(fontPath))
            return File.ReadAllBytes(fontPath);

        // 回退：搜索字体目录
        if (!fontPath.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
        {
            fontPath = Path.ChangeExtension(fontPath, ".ttf");
            if (File.Exists(fontPath))
                return File.ReadAllBytes(fontPath);
        }

        // 用 Arial 兜底
        var arialPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
        if (File.Exists(arialPath))
            return File.ReadAllBytes(arialPath);

        return null;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (familyName == "Microsoft YaHei" || familyName == "Consolas" || familyName.Contains("YaHei"))
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                familyName == "Consolas" ? "consola.ttf" : "msyh.ttc");

            if (File.Exists(path))
                return new FontResolverInfo(familyName);

            path = Path.ChangeExtension(path, ".ttf");
            if (File.Exists(path))
                return new FontResolverInfo(familyName);
        }

        // 通用回退
        string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), familyName + ".ttf");
        if (File.Exists(fallback))
            return new FontResolverInfo(familyName);

        fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
        if (File.Exists(fallback))
            return new FontResolverInfo("Arial");

        return null;
    }
}
