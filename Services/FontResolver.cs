using PdfSharp.Fonts;

namespace ISO11820.Services;

/// <summary>
/// PDFsharp 系统字体解析器 — 解决 "No appropriate font found" 错误
/// </summary>
public class SystemFontResolver : IFontResolver
{
    private readonly string _fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

    // Windows 字体文件名映射
    private static readonly Dictionary<string, string[]> FontFileNames = new()
    {
        ["Microsoft YaHei"] = new[] { "msyh.ttc", "msyh.ttf", "msyhbd.ttf" },
        ["Consolas"]        = new[] { "consola.ttf", "consolab.ttf", "consolaz.ttf" },
        ["Arial"]           = new[] { "arial.ttf", "arialbd.ttf", "ariali.ttf" },
    };

    public string DefaultFontName => "Arial";

    public byte[]? GetFont(string faceName)
    {
        // 1. 查映射表
        if (FontFileNames.TryGetValue(faceName, out var fileNames))
        {
            foreach (var fn in fileNames)
            {
                string fp = Path.Combine(_fontsDir, fn);
                if (File.Exists(fp))
                    return File.ReadAllBytes(fp);
            }
        }

        // 2. 直接按名称查找
        string direct = Path.Combine(_fontsDir, faceName + ".ttf");
        if (File.Exists(direct)) return File.ReadAllBytes(direct);
        direct = Path.Combine(_fontsDir, faceName + ".ttc");
        if (File.Exists(direct)) return File.ReadAllBytes(direct);

        // 3. 兜底：Arial
        string arial = Path.Combine(_fontsDir, "arial.ttf");
        if (File.Exists(arial)) return File.ReadAllBytes(arial);

        // 4. 最后手段：随便找个 ttf
        try
        {
            var any = Directory.GetFiles(_fontsDir, "*.ttf").FirstOrDefault();
            if (any != null) return File.ReadAllBytes(any);
        }
        catch { }

        return null;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // 尝试解析
        if (TryResolve(familyName, out var info)) return info;
        if (TryResolve("Arial", out info)) return info;

        // 绝对兜底：随便找一个存在的 ttf
        try
        {
            var any = Directory.GetFiles(_fontsDir, "*.ttf").FirstOrDefault();
            if (any != null)
            {
                string name = Path.GetFileNameWithoutExtension(any);
                return new FontResolverInfo(name);
            }
        }
        catch { }

        // 最后最后：返回自身（避免 return null 导致崩溃）
        return new FontResolverInfo(familyName);
    }

    private bool TryResolve(string familyName, out FontResolverInfo info)
    {
        info = null!;

        if (FontFileNames.TryGetValue(familyName, out var fileNames))
        {
            foreach (var fn in fileNames)
            {
                if (File.Exists(Path.Combine(_fontsDir, fn)))
                {
                    info = new FontResolverInfo(familyName);
                    return true;
                }
            }
        }

        // 直接查找
        if (File.Exists(Path.Combine(_fontsDir, familyName + ".ttf")) ||
            File.Exists(Path.Combine(_fontsDir, familyName + ".ttc")))
        {
            info = new FontResolverInfo(familyName);
            return true;
        }

        return false;
    }
}
