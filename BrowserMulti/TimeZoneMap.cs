namespace BrowserMulti;

/// <summary>
/// 国家/地区代码 → 时区映射，用于让实例的时区跟随代理出口地区，
/// 避免出现「境外 IP + 中国时区」这类一眼可见的关联特征。
/// 纯离线表，不依赖网络。
/// </summary>
public static class TimeZoneMap
{
    /// <summary>无代理或无法判定地区时使用的默认时区（与原行为一致）。</summary>
    public const string DefaultTimeZone = "Asia/Shanghai";

    /// <summary>默认时区的 UTC 偏移（分钟），用于伪造 Date.getTimezoneOffset。</summary>
    public const int DefaultOffsetMinutes = -480; // UTC+8

    /// <summary>
    /// 常见国家/地区代码（ISO 3166-1 alpha-2，大写）→ IANA 时区。
    /// 覆盖面以主要地区为主；未收录的国家会回退到按 UTC 偏移就近匹配。
    /// </summary>
    private static readonly Dictionary<string, string> CountryToZone = new(StringComparer.OrdinalIgnoreCase)
    {
        // 东亚
        ["CN"] = "Asia/Shanghai",
        ["HK"] = "Asia/Hong_Kong",
        ["MO"] = "Asia/Macau",
        ["TW"] = "Asia/Taipei",
        ["JP"] = "Asia/Tokyo",
        ["KR"] = "Asia/Seoul",
        ["KP"] = "Asia/Pyongyang",
        ["MN"] = "Asia/Ulaanbaatar",
        // 东南亚
        ["SG"] = "Asia/Singapore",
        ["MY"] = "Asia/Kuala_Lumpur",
        ["TH"] = "Asia/Bangkok",
        ["VN"] = "Asia/Ho_Chi_Minh",
        ["PH"] = "Asia/Manila",
        ["ID"] = "Asia/Jakarta",
        ["KH"] = "Asia/Phnom_Penh",
        ["LA"] = "Asia/Vientiane",
        ["MM"] = "Asia/Yangon",
        ["BN"] = "Asia/Brunei",
        // 南亚 / 中亚
        ["IN"] = "Asia/Kolkata",
        ["PK"] = "Asia/Karachi",
        ["BD"] = "Asia/Dhaka",
        ["LK"] = "Asia/Colombo",
        ["NP"] = "Asia/Kathmandu",
        ["KZ"] = "Asia/Almaty",
        ["UZ"] = "Asia/Tashkent",
        // 西亚 / 中东
        ["AE"] = "Asia/Dubai",
        ["SA"] = "Asia/Riyadh",
        ["IL"] = "Asia/Jerusalem",
        ["TR"] = "Europe/Istanbul",
        ["IR"] = "Asia/Tehran",
        ["QA"] = "Asia/Qatar",
        ["KW"] = "Asia/Kuwait",
        ["IQ"] = "Asia/Baghdad",
        // 欧洲
        ["GB"] = "Europe/London",
        ["IE"] = "Europe/Dublin",
        ["DE"] = "Europe/Berlin",
        ["FR"] = "Europe/Paris",
        ["NL"] = "Europe/Amsterdam",
        ["BE"] = "Europe/Brussels",
        ["ES"] = "Europe/Madrid",
        ["PT"] = "Europe/Lisbon",
        ["IT"] = "Europe/Rome",
        ["CH"] = "Europe/Zurich",
        ["AT"] = "Europe/Vienna",
        ["SE"] = "Europe/Stockholm",
        ["NO"] = "Europe/Oslo",
        ["DK"] = "Europe/Copenhagen",
        ["FI"] = "Europe/Helsinki",
        ["PL"] = "Europe/Warsaw",
        ["CZ"] = "Europe/Prague",
        ["HU"] = "Europe/Budapest",
        ["RO"] = "Europe/Bucharest",
        ["GR"] = "Europe/Athens",
        ["UA"] = "Europe/Kyiv",
        ["RU"] = "Europe/Moscow",
        // 北美
        ["US"] = "America/New_York",
        ["CA"] = "America/Toronto",
        ["MX"] = "America/Mexico_City",
        // 南美
        ["BR"] = "America/Sao_Paulo",
        ["AR"] = "America/Argentina/Buenos_Aires",
        ["CL"] = "America/Santiago",
        ["CO"] = "America/Bogota",
        ["PE"] = "America/Lima",
        ["VE"] = "America/Caracas",
        // 大洋洲
        ["AU"] = "Australia/Sydney",
        ["NZ"] = "Pacific/Auckland",
        // 非洲
        ["ZA"] = "Africa/Johannesburg",
        ["EG"] = "Africa/Cairo",
        ["NG"] = "Africa/Lagos",
        ["KE"] = "Africa/Nairobi",
        ["MA"] = "Africa/Casablanca",
    };

    /// <summary>常见时区 → 标准 UTC 偏移（分钟，注意符号与 JS getTimezoneOffset 相反）。</summary>
    private static readonly Dictionary<string, int> ZoneToOffset = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Asia/Shanghai"] = -480,
        ["Asia/Hong_Kong"] = -480,
        ["Asia/Macau"] = -480,
        ["Asia/Taipei"] = -480,
        ["Asia/Singapore"] = -480,
        ["Asia/Kuala_Lumpur"] = -480,
        ["Asia/Manila"] = -480,
        ["Asia/Brunei"] = -480,
        ["Asia/Perth"] = -480,
        ["Asia/Tokyo"] = -540,
        ["Asia/Seoul"] = -540,
        ["Asia/Pyongyang"] = -540,
        ["Asia/Bangkok"] = -420,
        ["Asia/Jakarta"] = -420,
        ["Asia/Ho_Chi_Minh"] = -420,
        ["Asia/Phnom_Penh"] = -420,
        ["Asia/Vientiane"] = -420,
        ["Asia/Ulaanbaatar"] = -480,
        ["Asia/Yangon"] = -390,
        ["Asia/Kolkata"] = -330,
        ["Asia/Colombo"] = -330,
        ["Asia/Kathmandu"] = -345,
        ["Asia/Dhaka"] = -360,
        ["Asia/Almaty"] = -360,
        ["Asia/Karachi"] = -300,
        ["Asia/Tashkent"] = -300,
        ["Asia/Dubai"] = -240,
        ["Asia/Qatar"] = -180,
        ["Asia/Kuwait"] = -180,
        ["Asia/Riyadh"] = -180,
        ["Asia/Baghdad"] = -180,
        ["Asia/Tehran"] = -210,
        ["Asia/Jerusalem"] = -180,
        ["Europe/Istanbul"] = -180,
        ["Europe/Moscow"] = -180,
        ["Europe/Athens"] = -120,
        ["Europe/Kyiv"] = -120,
        ["Europe/Bucharest"] = -120,
        ["Europe/Helsinki"] = -120,
        ["Europe/Warsaw"] = -120,
        ["Europe/Prague"] = -120,
        ["Europe/Budapest"] = -120,
        ["Europe/Berlin"] = -60,
        ["Europe/Paris"] = -60,
        ["Europe/Amsterdam"] = -60,
        ["Europe/Brussels"] = -60,
        ["Europe/Madrid"] = -60,
        ["Europe/Rome"] = -60,
        ["Europe/Zurich"] = -60,
        ["Europe/Vienna"] = -60,
        ["Europe/Stockholm"] = -60,
        ["Europe/Oslo"] = -60,
        ["Europe/Copenhagen"] = -60,
        ["Europe/London"] = 0,
        ["Europe/Dublin"] = 0,
        ["Europe/Lisbon"] = 0,
        ["Africa/Lagos"] = -60,
        ["Africa/Casablanca"] = -60,
        ["Africa/Cairo"] = -120,
        ["Africa/Johannesburg"] = -120,
        ["Africa/Nairobi"] = -180,
        ["America/Sao_Paulo"] = 180,
        ["America/Argentina/Buenos_Aires"] = 180,
        ["America/Santiago"] = 240,
        ["America/Caracas"] = 240,
        ["America/Bogota"] = 300,
        ["America/Lima"] = 300,
        ["America/New_York"] = 300,
        ["America/Toronto"] = 300,
        ["America/Chicago"] = 360,
        ["America/Mexico_City"] = 360,
        ["America/Denver"] = 420,
        ["America/Los_Angeles"] = 480,
        ["Pacific/Auckland"] = -720,
        ["Australia/Sydney"] = -600,
    };

    /// <summary>根据国家代码取时区；未知则返回默认时区。</summary>
    public static string FromCountry(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return DefaultTimeZone;
        // 兼容上游可能回传的 "US" / "us" / "United States" 等；此处只认 2 位代码
        var cc = countryCode.Trim().ToUpperInvariant();
        return CountryToZone.TryGetValue(cc, out var tz) ? tz : DefaultTimeZone;
    }

    /// <summary>取时区对应的 UTC 偏移（分钟，符号同 JS getTimezoneOffset）；未知返回默认偏移。</summary>
    public static int OffsetOf(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone)) return DefaultOffsetMinutes;
        return ZoneToOffset.TryGetValue(timeZone.Trim(), out var off) ? off : DefaultOffsetMinutes;
    }

    /// <summary>时区是否已知（可判定偏移）。</summary>
    public static bool IsKnown(string? timeZone)
        => !string.IsNullOrWhiteSpace(timeZone) && ZoneToOffset.ContainsKey(timeZone.Trim());
}
