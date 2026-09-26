using System.Net;

namespace BrowserMulti;

/// <summary>
/// 探测代理出口的真实地区（国家代码），用于让时区与 IP 保持一致。
/// 原理：<b>经由该代理</b>去请求 IP 归属地接口，返回的即为代理出口地区。
/// 所有请求都走代理本身，且失败一律静默回退，绝不影响启动流程。
/// </summary>
public static class ProxyGeoLookup
{
    /// <summary>探测结果。</summary>
    public readonly record struct GeoResult(bool Success, string CountryCode, string TimeZone, string Detail)
    {
        public static GeoResult Fail(string detail) => new(false, "", TimeZoneMap.DefaultTimeZone, detail);
    }

    private static readonly HttpClient Shared = new(new HttpClientHandler
    {
        // 由调用方为每次查询独立指定代理，故共享 Handler 不使用默认代理
        UseProxy = false,
        AllowAutoRedirect = true
    })
    { Timeout = TimeSpan.FromSeconds(6) };

    /// <summary>
    /// 经由指定代理探测出口地区。
    /// </summary>
    /// <param name="proxyServer">形如 http://127.0.0.1:7890 或 socks5://127.0.0.1:1080</param>
    /// <param name="timeoutSeconds">超时秒数</param>
    public static GeoResult Lookup(string? proxyServer, int timeoutSeconds = 6)
    {
        if (string.IsNullOrWhiteSpace(proxyServer))
            return GeoResult.Fail("未配置代理，使用默认时区");

        if (!TryBuildWebProxy(proxyServer, out var proxy, out var why))
            return GeoResult.Fail(why);

        // 依次尝试多个接口，任一成功即返回。均为纯文本/JSON 的轻量接口。
        var probes = new Func<WebProxy, (bool ok, string cc, string raw)>[]
        {
            p => ProbeText(p, "http://ip-api.com/line/?fields=countryCode", 2),
            p => ProbeText(p, "https://api.country.is/", -1),          // 返回 JSON {"country":"US"}
            p => ProbeText(p, "https://ipinfo.io/country", -1),        // 返回纯文本 "US"
        };

        string lastErr = "未获取到结果";
        foreach (var probe in probes)
        {
            try
            {
                var (ok, cc, raw) = probe(proxy);
                if (ok && cc.Length == 2)
                {
                    var tz = TimeZoneMap.FromCountry(cc);
                    return new GeoResult(true, cc.ToUpperInvariant(), tz, $"代理出口地区 {cc.ToUpperInvariant()}");
                }
                lastErr = string.IsNullOrWhiteSpace(raw) ? "返回内容无法解析" : $"返回内容无法解析：{Truncate(raw, 40)}";
            }
            catch (Exception ex)
            {
                lastErr = $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        return GeoResult.Fail(lastErr);
    }

    private static (bool ok, string cc, string raw) ProbeText(WebProxy proxy, string url, int jsonFieldIndex)
    {
        using var handler = new HttpClientHandler { Proxy = proxy, UseProxy = true };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "browser-multi/1.0");

        var text = client.GetStringAsync(url).GetAwaiter().GetResult().Trim();

        if (jsonFieldIndex < 0)
        {
            // 可能是 JSON（{"country":"US"}）或纯文本（US）
            var cc = ExtractCountry(text);
            return (cc.Length == 2, cc, text);
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var v = lines.Length > jsonFieldIndex ? lines[jsonFieldIndex] : "";
        return (v.Length == 2, v, text);
    }

    /// <summary>从返回文本中提取 2 位国家代码（兼容纯文本与简单 JSON）。</summary>
    private static string ExtractCountry(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var t = text.Trim();

        // JSON：找第一个 "key":"XX" 形式的 2 位值
        if (t.StartsWith('{'))
        {
            foreach (var m in System.Text.RegularExpressions.Regex.Matches(
                         t, "\"([A-Za-z]{2})\""))
            {
                var v = ((System.Text.RegularExpressions.Match)m).Groups[1].Value;
                // 跳过常见的非国家字段值
                if (!v.Equals("ok", StringComparison.OrdinalIgnoreCase))
                    return v.ToUpperInvariant();
            }
            return "";
        }

        // 纯文本：整个串就是 2 位代码
        return t.Length == 2 && char.IsLetter(t[0]) && char.IsLetter(t[1])
            ? t.ToUpperInvariant()
            : "";
    }

    /// <summary>把 http(s)/socks 代理串解析为 WebProxy。</summary>
    public static bool TryBuildWebProxy(string proxyServer, out WebProxy proxy, out string why)
    {
        proxy = null!;
        why = "";
        if (!Uri.TryCreate(proxyServer.Trim(), UriKind.Absolute, out var uri))
        {
            why = "代理地址无法解析";
            return false;
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        if (scheme is not ("http" or "https" or "socks5" or "socks4"))
        {
            why = $"不支持的代理协议：{uri.Scheme}";
            return false;
        }

        // Uri 对 http/https 会自动补默认端口（80/443），所以必须用 OriginalString 判断用户是否真的写了端口
        if (uri.IsDefaultPort)
        {
            var authEnd = uri.OriginalString.IndexOf("://", StringComparison.Ordinal);
            var afterAuth = authEnd >= 0 ? uri.OriginalString[(authEnd + 3)..] : uri.OriginalString;
            var at = afterAuth.LastIndexOf('@');
            var hostPart = at >= 0 ? afterAuth[(at + 1)..] : afterAuth;
            var slash = hostPart.IndexOfAny(new[] { '/', '?', '#' });
            if (slash >= 0) hostPart = hostPart[..slash];
            if (!hostPart.Contains(':'))
            {
                why = "代理地址缺少端口";
                return false;
            }
        }

        // .NET 的 WebProxy 对 socks4/socks5 原生支持（通过 socks5:// 前缀）
        var addr = scheme is "socks4" or "socks5"
            ? new Uri($"{scheme}://{uri.Host}:{uri.Port}")
            : new Uri($"http://{uri.Host}:{uri.Port}");

        proxy = new WebProxy(addr)
        {
            BypassProxyOnLocal = false,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            proxy.Credentials = new NetworkCredential(
                Uri.UnescapeDataString(parts[0]),
                parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "");
        }

        return true;
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}
