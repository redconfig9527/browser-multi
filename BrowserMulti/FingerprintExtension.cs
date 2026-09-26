namespace BrowserMulti;

/// <summary>
/// 为实例生成防指纹浏览器扩展（MV3，MAIN world 注入）。
/// 覆盖：Canvas 噪声、WebGL 厂商/渲染器伪装、readPixels 噪声、AudioContext 噪声、
/// navigator 设备信息（platform/内存/核心数/触点）、移动端屏幕尺寸。
/// 噪声由实例级种子驱动，跨会话稳定（指纹一致性）。
/// </summary>
public static class FingerprintExtension
{
    public static string GetDir(InstanceConfig inst) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Data", "_fp", inst.Id));

    /// <summary>根据 UA 推断指纹画像，保证 UA 与深层指纹一致。</summary>
    public static string DetectProfile(string userAgent)
    {
        var ua = userAgent ?? "";
        if (ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase)) return "iphone";
        if (ua.Contains("Android", StringComparison.OrdinalIgnoreCase)) return "android";
        if (ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)) return "mac";
        return "win";
    }

    public static void Write(InstanceConfig inst)
    {
        string dir = GetDir(inst);
        Directory.CreateDirectory(dir);
        WriteAtomic(Path.Combine(dir, "manifest.json"), ManifestJson);
        WriteAtomic(Path.Combine(dir, "fp.js"), BuildScript(inst));
    }

    /// <summary>原子写入：先写临时文件再替换，避免中断产生半截文件导致扩展加载失败。</summary>
    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new System.Text.UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }

    private static int ParseSeed(InstanceConfig inst)
    {
        if (string.IsNullOrWhiteSpace(inst.FpSeed)) return 12345;
        // 对整个种子串做 FNV-1a 散列，比只取前 8 位十六进制更均匀，且不会因长度不足而失败
        unchecked
        {
            uint h = 2166136261;
            foreach (char c in inst.FpSeed)
            {
                h ^= c;
                h *= 16777619;
            }
            int v = (int)(h & 0x7FFFFFFF);
            return v == 0 ? 12345 : v;
        }
    }

    private const string ManifestJson =
"""
{
  "manifest_version": 3,
  "name": "FP Shield",
  "version": "1.0",
  "description": "Anti-fingerprint protection for this instance",
  "content_scripts": [
    {
      "matches": ["<all_urls>"],
      "js": ["fp.js"],
      "run_at": "document_start",
      "world": "MAIN",
      "all_frames": true,
      "match_about_blank": true
    }
  ]
}
""";

    private static string BuildScript(InstanceConfig inst)
    {
        return ScriptTemplate
            .Replace("{SEED}", ParseSeed(inst).ToString())
            .Replace("{PROFILE}", DetectProfile(inst.UserAgent));
    }

    private const string ScriptTemplate =
"""
"use strict";
(function () {
  if (window.__fpShield) { return; }
  window.__fpShield = true;

  var SEED = {SEED};
  var PROFILE = "{PROFILE}";
  var isMobile = (PROFILE === "iphone") || (PROFILE === "android");

  function hash(x, y) {
    var h = (x * 374761393 + y * 668265263 + SEED) | 0;
    h = Math.imul(h ^ (h >>> 13), 1274126177);
    return (h ^ (h >>> 16)) >>> 0;
  }
  function byteNoise(i) { return (hash(i, i * 31 + 7) & 7) - 3; }
  function clamp255(v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }

  var navMap = {
    win:     { platform: "Win32",        hc: 12, dm: 8, touch: 0, lang: "zh-CN", langs: ["zh-CN", "zh", "en-US", "en"] },
    mac:     { platform: "MacIntel",     hc: 8,  dm: 8, touch: 0, lang: "zh-CN", langs: ["zh-CN", "zh", "en-US", "en"] },
    iphone:  { platform: "iPhone",       hc: 6,  dm: 4, touch: 5, lang: "zh-CN", langs: ["zh-CN", "zh-Hans-CN", "en-US", "en"] },
    android: { platform: "Linux armv8l", hc: 8,  dm: 8, touch: 5, lang: "zh-CN", langs: ["zh-CN", "zh-Hans-CN", "en-US", "en"] }
  };
  var nav = navMap[PROFILE] || navMap.win;
  var fontPlatform = (PROFILE === "win") ? "Windows" : (PROFILE === "mac" ? "macOS" : "Linux");

  function hashStr(s) {
    var h = 2166136261;
    for (var i = 0; i < s.length; i++) {
      h ^= s.charCodeAt(i);
      h = Math.imul(h, 16777619);
    }
    return (h ^ (h >>> 16)) >>> 0;
  }
  function allowed(kind, name) {
    return (hashStr(kind + "|" + name + "|" + SEED) % 100) < 78;
  }

  function def(obj, prop, value) {
    try {
      Object.defineProperty(obj, prop, {
        get: function () { return value; },
        configurable: true
      });
    } catch (e) { }
  }

  try {
    def(Navigator.prototype, "platform", nav.platform);
    def(Navigator.prototype, "hardwareConcurrency", nav.hc);
    def(Navigator.prototype, "deviceMemory", nav.dm);
    def(Navigator.prototype, "maxTouchPoints", nav.touch);
    def(Navigator.prototype, "language", nav.lang);
    def(Navigator.prototype, "languages", nav.langs);
    if (isMobile) {
      def(Navigator.prototype, "plugins", {
        length: 0, item: function () { return null; },
        namedItem: function () { return null; }, refresh: function () { }
      });
      def(Navigator.prototype, "mimeTypes", {
        length: 0, item: function () { return null; }, namedItem: function () { return null; }
      });
    }
  } catch (e) { }

  if (typeof Intl !== "undefined") {
    try {
      var origResolved = Intl.DateTimeFormat.prototype.resolvedOptions;
      Intl.DateTimeFormat.prototype.resolvedOptions = function () {
        var o = origResolved.apply(this, arguments);
        try { o.timeZone = "Asia/Shanghai"; } catch (e) { }
        return o;
      };
      var origDTF = Intl.DateTimeFormat;
      Intl.DateTimeFormat = function (loc, opt) {
        return new origDTF(loc || nav.lang, opt);
      };
    } catch (e) { }
  }

  var FONT_LISTS = {
    win: [
      "Arial", "Arial Black", "Calibri", "Cambria", "Candara", "Comic Sans MS",
      "Consolas", "Constantia", "Corbel", "Courier New", "Ebrima", "Franklin Gothic Medium",
      "Gabriola", "Gadugi", "Georgia", "Impact", "Javanese Text", "Leelawadee UI",
      "Lucida Console", "Lucida Sans Unicode", "Malgun Gothic", "Marlett", "Microsoft Himalaya",
      "Microsoft JhengHei", "Microsoft New Tai Lue", "Microsoft PhagsPa", "Microsoft Sans Serif",
      "Microsoft Tai Le", "Microsoft YaHei", "MingLiU-ExtB", "Mongolian Baiti", "MS Gothic",
      "MV Boli", "Myanmar Text", "Nirmala UI", "Palatino Linotype", "Segoe MDL2 Assets",
      "Segoe Print", "Segoe Script", "Segoe UI", "SimSun", "Sitka", "Sylfaen", "Symbol",
      "Tahoma", "Times New Roman", "Trebuchet MS", "Verdana", "Webdings", "Wingdings",
      "Yu Gothic", "SimHei", "KaiTi", "FangSong"
    ],
    mac: [
      "American Typewriter", "Andale Mono", "Apple Chancery", "Arial", "Arial Black",
      "Arial Narrow", "Avenir", "Avenir Next", "Baskerville", "Big Caslon", "Brush Script MT",
      "Chalkboard", "Cochin", "Comic Sans MS", "Copperplate", "Courier New", "Didot",
      "Futura", "Geneva", "Georgia", "Gill Sans", "Helvetica", "Helvetica Neue", "Herculanum",
      "Hoefler Text", "Impact", "Lucida Grande", "Luminari", "Marker Felt", "Menlo",
      "Monaco", "Optima", "Papyrus", "Phosphate", "Rockwell", "Savoye LET", "SignPainter",
      "Skia", "Snell Roundhand", "Tahoma", "Times New Roman", "Trattatello", "Trebuchet MS",
      "Verdana", "Zapfino"
    ],
    linux: [
      "Arial", "Bitstream Charter", "Century Schoolbook L", "Courier New", "DejaVu Sans",
      "DejaVu Sans Mono", "DejaVu Serif", "Dingbats", "FreeMono", "FreeSans", "FreeSerif",
      "Garuda", "Georgia", "Impact", "Liberation Mono", "Liberation Sans", "Liberation Serif",
      "Loma", "Nimbus Mono L", "Nimbus Roman No9 L", "Nimbus Sans L", "Norasi", "OpenSymbol",
      "Phetsarath OT", "Purisa", "Standard Symbols L", "Times New Roman", "Trebuchet MS",
      "Utopia", "Verdana", "Waree"
    ]
  };
  var fontList = FONT_LISTS[PROFILE] || FONT_LISTS.win;
  var allowedFonts = fontList.filter(function (f) { return allowed("font", f); });

  try {
    var fsProto = FontFaceSet.prototype;

    function filterFonts(arr) {
      return arr.filter(function (f) { return allowedFonts.indexOf(f) >= 0; });
    }

    var origCheck = fsProto.check;
    if (origCheck) {
      fsProto.check = function (font, text) {
        if (typeof font === "string" && font.indexOf(" ") >= 0) {
          var first = font.split(" ")[1];
          if (first && allowedFonts.indexOf(first.replace(/["']/g, "")) < 0) { return false; }
        }
        return origCheck.apply(this, arguments);
      };
    }
  } catch (e) { }

  try {
    def(Document.prototype, "fonts", new FontFaceSet());
  } catch (e) { }

  if (isMobile) {
    var dims = (PROFILE === "iphone")
      ? { w: 390, h: 844, dpr: 3 }
      : { w: 412, h: 915, dpr: 2.625 };
    def(Screen.prototype, "width", dims.w);
    def(Screen.prototype, "height", dims.h);
    def(Screen.prototype, "availWidth", dims.w);
    def(Screen.prototype, "availHeight", dims.h);
    def(Screen.prototype, "colorDepth", 24);
    def(Screen.prototype, "pixelDepth", 24);
    def(window, "devicePixelRatio", dims.dpr);
  }

  var origGetImageData = CanvasRenderingContext2D.prototype.getImageData;
  function noiseData(data) {
    for (var i = 0; i < data.length; i += 4) {
      var dv = byteNoise((i / 4) | 0);
      if (dv !== 0) { data[i] = clamp255(data[i] + dv); }
    }
  }

  try {
    CanvasRenderingContext2D.prototype.getImageData = function () {
      var d = origGetImageData.apply(this, arguments);
      try { noiseData(d.data); } catch (e) { }
      return d;
    };

    function perturb2d(c) {
      try {
        var ctx = c.getContext("2d");
        if (!ctx || !c.width || !c.height) { return; }
        var d = origGetImageData.call(ctx, 0, 0, c.width, c.height);
        noiseData(d.data);
        ctx.putImageData(d, 0, 0);
      } catch (e) { }
    }

    var origToDataURL = HTMLCanvasElement.prototype.toDataURL;
    HTMLCanvasElement.prototype.toDataURL = function () {
      perturb2d(this);
      return origToDataURL.apply(this, arguments);
    };
    var origToBlob = HTMLCanvasElement.prototype.toBlob;
    HTMLCanvasElement.prototype.toBlob = function () {
      perturb2d(this);
      return origToBlob.apply(this, arguments);
    };
  } catch (e) { }

  try {
    var glInfo = {
      win:     { v: "Google Inc. (NVIDIA)", r: "ANGLE (NVIDIA, NVIDIA GeForce RTX 3060 Direct3D11 vs_5_0 ps_5_0, D3D11)" },
      mac:     { v: "Google Inc. (Apple)",  r: "ANGLE (Apple, Apple M2, OpenGL 4.1)" },
      iphone:  { v: "Apple Inc.",           r: "Apple GPU" },
      android: { v: "Qualcomm",             r: "Adreno (TM) 740" }
    }[PROFILE];

    [WebGLRenderingContext,
     (typeof WebGL2RenderingContext !== "undefined") ? WebGL2RenderingContext : null
    ].forEach(function (C) {
      if (!C) { return; }
      var origGetParam = C.prototype.getParameter;
      C.prototype.getParameter = function (p) {
        if (p === 37445) { return glInfo.v; }
        if (p === 37446) { return glInfo.r; }
        return origGetParam.apply(this, arguments);
      };
      var origRead = C.prototype.readPixels;
      C.prototype.readPixels = function () {
        var r = origRead.apply(this, arguments);
        try {
          var px = arguments[6];
          if (px && px.length) {
            for (var i = 0; i < px.length; i += 4) {
              var dv = byteNoise(0x100000 + ((i / 4) | 0));
              if (dv !== 0) { px[i] = clamp255(px[i] + dv); }
            }
          }
        } catch (e) { }
        return r;
      };
    });
  } catch (e) { }

  try {
    var origGCD = OfflineAudioContext.prototype.getChannelData;
    OfflineAudioContext.prototype.getChannelData = function () {
      var d = origGCD.apply(this, arguments);
      try {
        for (var i = 0; i < d.length; i += 997) {
          d[i] = d[i] + byteNoise(0x200000 + i) * 1e-7;
        }
      } catch (e) { }
      return d;
    };
  } catch (e) { }

  // WebRTC 内网 IP 防泄露（SDP 中的候选地址改写为中性地址，避免暴露本机网段）
  try {
    var RTC = window.RTCPeerConnection || window.webkitRTCPeerConnection;
    if (RTC && RTC.prototype) {
      var spoofIp = isMobile ? "192.168.1.88" : "192.168.1." + (50 + (SEED % 150));
      var origSetLocal = RTC.prototype.setLocalDescription;
      if (origSetLocal) {
        RTC.prototype.setLocalDescription = function () {
          var self = this;
          var args = arguments;
          var r = origSetLocal.apply(self, args);
          var patch = function () {
            try {
              var d = self.localDescription;
              if (d && d.sdp) {
                var clean = d.sdp.replace(
                  /(c=IN IP4 )(\d{1,3}\.){3}\d{1,3}/g, "$1" + spoofIp
                ).replace(
                  /(a=candidate:\S+ \d+ \S+ \d+ )(\d{1,3}\.){3}\d{1,3}/g, "$1" + spoofIp
                );
                if (clean !== d.sdp) {
                  var desc = { type: d.type, sdp: clean };
                  try { Object.defineProperty(self, "localDescription", { get: function () { return desc; }, configurable: true }); } catch (e) { }
                }
              }
            } catch (e) { }
          };
          if (r && typeof r.then === "function") { r.then(patch, patch); } else { patch(); }
          return r;
        };
      }
      var origSetRemote = RTC.prototype.setRemoteDescription;
      if (origSetRemote) {
        RTC.prototype.setRemoteDescription = function (desc) {
          try {
            if (desc && desc.sdp) {
              var clean = desc.sdp.replace(
                /(c=IN IP4 )(\d{1,3}\.){3}\d{1,3}/g, "$1" + spoofIp
              ).replace(
                /(a=candidate:\S+ \d+ \S+ \d+ )(\d{1,3}\.){3}\d{1,3}/g, "$1" + spoofIp
              );
              desc = { type: desc.type, sdp: clean };
            }
          } catch (e) { }
          return origSetRemote.call(this, desc);
        };
      }
      var origIce = RTC.prototype.addIceCandidate;
      if (origIce) {
        RTC.prototype.addIceCandidate = function (cand) {
          try {
            if (cand && cand.candidate) {
              cand = { candidate: cand.candidate.replace(
                /(\S+ \d+ \S+ \d+ )(\d{1,3}\.){3}\d{1,3}/g, "$1" + spoofIp
              ), sdpMid: cand.sdpMid, sdpMLineIndex: cand.sdpMLineIndex };
            }
          } catch (e) { }
          return origIce.call(this, cand);
        };
      }
    }
  } catch (e) { }
})();
""";
}
