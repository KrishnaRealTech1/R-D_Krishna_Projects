using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using GCam.Windows.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GCam.Windows.Services;

public sealed class DashboardService : IAsyncDisposable, IDisposable
{
    private const string AuthCookie = "gcam_auth";
    private readonly DashboardBridge _bridge;
    private WebApplication? _app;
    private CloudflareSettings? _settings;

    public string Status { get; private set; } = "Stopped";
    public bool IsRunning => _app is not null;
    public string LocalUrl => _settings is null ? "" : $"http://127.0.0.1:{_settings.DashboardPort}";

    public DashboardService(DashboardBridge bridge) => _bridge = bridge;

    public void Start(CloudflareSettings settings)
    {
        Stop();
        if (!settings.DashboardEnabled)
        {
            Status = "Disabled";
            return;
        }

        string bind = NormalizeBindAddress(settings.BindAddress);
        int port = Math.Clamp(settings.DashboardPort, 1024, 65535);
        settings.BindAddress = bind;
        settings.DashboardPort = port;
        _settings = settings;

        var options = new WebApplicationOptions
        {
            ApplicationName = typeof(DashboardService).Assembly.FullName,
            ContentRootPath = AppContext.BaseDirectory
        };
        var builder = WebApplication.CreateBuilder(options);
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://{bind}:{port}");
        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        var app = builder.Build();
        MapRoutes(app);
        app.StartAsync().GetAwaiter().GetResult();
        _app = app;
        Status = $"Running on {bind}:{port}";
    }

    private void MapRoutes(WebApplication app)
    {
        app.MapGet("/", () => Results.Content(DashboardHtml, "text/html; charset=utf-8"));
        app.MapGet("/live", () => Results.Content(LiveHtml, "text/html; charset=utf-8"));
        app.MapGet("/health", () => Results.Json(new { ok = true, service = "gcam-windows-dashboard", time = DateTimeOffset.Now }));

        app.MapPost("/api/auth", async (HttpContext ctx) =>
        {
            var req = await ctx.Request.ReadFromJsonAsync<DashboardAuthRequest>();
            if (req is null || !ApiKeyMatches(req.Key)) return Results.Unauthorized();
            ctx.Response.Cookies.Append(AuthCookie, ApiKeyHash(), new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromHours(12),
                IsEssential = true,
                Secure = false
            });
            return Results.Json(new { ok = true });
        });

        app.MapPost("/api/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete(AuthCookie);
            return Results.Json(new { ok = true });
        });

        app.MapGet("/api/auth/status", (HttpContext ctx) =>
            Results.Json(new { authenticated = IsAuthorized(ctx), keyRequired = !string.IsNullOrWhiteSpace(_settings?.ControlApiKey) }));

        app.MapGet("/api/state", (HttpContext ctx) =>
            IsAuthorized(ctx) ? Results.Json(_bridge.GetState()) : Results.Unauthorized());

        app.MapGet("/api/controls/state", (HttpContext ctx) =>
            IsAuthorized(ctx) ? Results.Json(_bridge.GetControls()) : Results.Unauthorized());

        app.MapGet("/api/events", (HttpContext ctx) =>
        {
            if (!IsAuthorized(ctx)) return Results.Unauthorized();
            int limit = 50;
            if (int.TryParse(ctx.Request.Query["limit"], out int requested)) limit = Math.Clamp(requested, 1, 500);
            return Results.Json(_bridge.GetEvents().Take(limit));
        });

        app.MapGet("/api/live.jpg", (HttpContext ctx) =>
        {
            if (!IsAuthorized(ctx)) return Results.Unauthorized();
            byte[]? jpeg = _bridge.GetSnapshotJpeg();
            if (jpeg is null || jpeg.Length == 0) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            ctx.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            return Results.File(jpeg, "image/jpeg");
        });

        app.MapGet("/api/events/{id:guid}/image", (Guid id, HttpContext ctx) =>
        {
            if (!IsAuthorized(ctx)) return Results.Unauthorized();
            var row = _bridge.GetEvents().FirstOrDefault(e => e.EventId == id);
            return row?.ImagePath is { Length: > 0 } image && File.Exists(image)
                ? Results.File(image, "image/jpeg", enableRangeProcessing: true)
                : Results.NotFound();
        });

        app.MapGet("/api/events/{id:guid}/video", (Guid id, HttpContext ctx) =>
        {
            if (!IsAuthorized(ctx)) return Results.Unauthorized();
            var row = _bridge.GetEvents().FirstOrDefault(e => e.EventId == id);
            return row?.VideoPath is { Length: > 0 } video && File.Exists(video)
                ? Results.File(video, "video/mp4", enableRangeProcessing: true)
                : Results.NotFound();
        });

        app.MapPost("/api/runtime/start", (HttpContext ctx) =>
        {
            if (!IsAuthorized(ctx)) return Results.Unauthorized();
            _bridge.StartRuntime();
            return Results.Json(new { ok = true, message = "Runtime start requested" });
        });

        app.MapPost("/api/runtime/stop", (HttpContext ctx) =>
        {
            if (!IsAuthorized(ctx)) return Results.Unauthorized();
            _bridge.StopRuntime();
            return Results.Json(new { ok = true, message = "Runtime stop requested" });
        });

        app.MapPost("/api/controls/warning_audio", async (HttpContext ctx) =>
        {
            if (!IsAuthorized(ctx)) return Results.Unauthorized();
            var req = await ctx.Request.ReadFromJsonAsync<BoolControlUpdate>();
            if (req is null) return Results.BadRequest(new { ok = false, error = "Invalid JSON" });
            _bridge.SetWarningMaster(req.Enabled);
            return Results.Json(new { ok = true, enabled = req.Enabled, controls = _bridge.GetControls() });
        });

        app.MapPost("/api/controls/event", async (HttpContext ctx) =>
        {
            if (!IsAuthorized(ctx)) return Results.Unauthorized();
            var req = await ctx.Request.ReadFromJsonAsync<EventControlUpdate>();
            if (req is null || !TryParseEventKind(req.Event, out _))
                return Results.BadRequest(new { ok = false, error = "event must be person, vehicle, lpd/licenseplate, or garbage" });
            _bridge.UpdateEventControl(req);
            return Results.Json(new { ok = true, controls = _bridge.GetControls() });
        });

        // Compatibility with the old Python dashboard endpoint/feature-flag naming.
        app.MapPost("/api/controls/feature", async (HttpContext ctx) =>
        {
            if (!IsAuthorized(ctx)) return Results.Unauthorized();
            var req = await ctx.Request.ReadFromJsonAsync<FeatureControlUpdate>();
            if (req is null || string.IsNullOrWhiteSpace(req.Flag)) return Results.BadRequest(new { ok = false, error = "flag is required" });
            try
            {
                _bridge.SetFeatureFlag(req.Flag, req.Enabled);
                return Results.Json(new { ok = true, flag = req.Flag, enabled = req.Enabled, controls = _bridge.GetControls() });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { ok = false, error = ex.Message });
            }
        });

        app.MapPost("/api/test-event", async (HttpContext ctx) =>
        {
            if (!IsAuthorized(ctx)) return Results.Unauthorized();
            var req = await ctx.Request.ReadFromJsonAsync<TestEventRequest>();
            if (req is null || !TryParseEventKind(req.Event, out EventKind kind))
                return Results.BadRequest(new { ok = false, error = "Unknown event" });
            bool ok = _bridge.TriggerTestEvent(kind);
            return ok ? Results.Json(new { ok = true, eventType = kind.ToString() }) : Results.Conflict(new { ok = false, error = "Runtime/live frame not ready" });
        });
    }

    private bool IsAuthorized(HttpContext ctx)
    {
        if (string.IsNullOrWhiteSpace(_settings?.ControlApiKey)) return true;
        if (ctx.Request.Headers.TryGetValue("X-GCam-Key", out var key) && ApiKeyMatches(key.ToString())) return true;
        return ctx.Request.Cookies.TryGetValue(AuthCookie, out string? cookie) && FixedEquals(cookie, ApiKeyHash());
    }

    private bool ApiKeyMatches(string? candidate)
    {
        string expected = _settings?.ControlApiKey ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expected)) return true;
        return FixedEquals(candidate ?? string.Empty, expected);
    }

    private string ApiKeyHash()
    {
        string key = _settings?.ControlApiKey ?? string.Empty;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
    }

    private static bool FixedEquals(string a, string b)
    {
        byte[] x = Encoding.UTF8.GetBytes(a);
        byte[] y = Encoding.UTF8.GetBytes(b);
        return x.Length == y.Length && CryptographicOperations.FixedTimeEquals(x, y);
    }

    public static bool TryParseEventKind(string? value, out EventKind kind)
    {
        switch ((value ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "person": kind = EventKind.Person; return true;
            case "vehicle": case "car": kind = EventKind.Vehicle; return true;
            case "lpd": case "plate": case "licenseplate": case "license_plate": case "license plate": kind = EventKind.LicensePlate; return true;
            case "garbage": case "trash": kind = EventKind.Garbage; return true;
            default: kind = default; return false;
        }
    }

    private static string NormalizeBindAddress(string? value)
    {
        value = (value ?? "0.0.0.0").Trim();
        return value is "127.0.0.1" or "localhost" or "0.0.0.0" ? value : "0.0.0.0";
    }

    public void Stop()
    {
        if (_app is null) return;
        try { _app.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult(); } catch { }
        try { _app.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
        _app = null;
        Status = "Stopped";
    }

    public void Dispose() => Stop();

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            try { await _app.StopAsync(TimeSpan.FromSeconds(2)); } catch { }
            try { await _app.DisposeAsync(); } catch { }
            _app = null;
        }
        Status = "Stopped";
    }

    private const string LiveHtml = """
<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>G-Cam Live</title>
<style>html,body{margin:0;height:100%;background:#05070b;color:#fff;font-family:Segoe UI,Arial}.wrap{height:100%;display:flex;align-items:center;justify-content:center}img{max-width:100%;max-height:100%;object-fit:contain}.msg{position:fixed;top:10px;left:10px;background:#111b;padding:8px;border-radius:8px}</style></head>
<body><div class="msg" id="m">Open the main dashboard and sign in first.</div><div class="wrap"><img id="live"></div>
<script>function tick(){const i=document.getElementById('live');i.src='/api/live.jpg?t='+Date.now();i.onload=()=>document.getElementById('m').style.display='none';i.onerror=()=>document.getElementById('m').style.display='block'}setInterval(tick,700);tick();</script></body></html>
""";

    private const string DashboardHtml = """
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>G-Cam Windows Dashboard</title>
<style>
:root{--bg:#07101b;--card:#101a2e;--card2:#16233f;--line:#263657;--text:#eef4ff;--muted:#9fb1d0;--accent:#39c6ff;--ok:#22c55e;--warn:#f59e0b;--bad:#ef4444}
*{box-sizing:border-box}body{margin:0;background:linear-gradient(180deg,#07101b,#0f172a);color:var(--text);font-family:Segoe UI,Arial,sans-serif}.top{position:sticky;top:0;z-index:5;background:#07101bee;backdrop-filter:blur(8px);border-bottom:1px solid var(--line);padding:14px 18px;display:flex;gap:12px;align-items:center;justify-content:space-between}.brand{font-weight:800;font-size:20px}.muted{color:var(--muted);font-size:13px}.wrap{max-width:1450px;margin:auto;padding:16px}.grid{display:grid;grid-template-columns:minmax(0,2fr) minmax(320px,1fr);gap:14px}.card{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:14px;box-shadow:0 10px 30px #0003}.live{background:#000;min-height:340px;display:flex;align-items:center;justify-content:center;overflow:hidden}.live img{width:100%;max-height:70vh;object-fit:contain}.stats{display:grid;grid-template-columns:repeat(2,1fr);gap:8px}.stat{background:var(--card2);padding:10px;border-radius:10px}.stat b{display:block;font-size:17px;margin-top:4px}.toolbar{display:flex;gap:8px;flex-wrap:wrap}button,input{font:inherit}button{border:0;border-radius:9px;padding:9px 12px;font-weight:700;cursor:pointer;background:var(--accent);color:#001421}button.secondary{background:#263657;color:var(--text)}button.danger{background:var(--bad);color:#fff}input[type=text],input[type=password],input[type=number]{background:#091223;color:var(--text);border:1px solid var(--line);border-radius:8px;padding:8px;max-width:180px}.eventsCtl{display:grid;grid-template-columns:120px repeat(4,80px) 95px 95px 100px;gap:8px;align-items:center;overflow:auto}.eventsCtl .head{color:var(--muted);font-size:12px}.eventsCtl .row{display:contents}.eventsCtl label{white-space:nowrap}.sectionTitle{font-size:17px;font-weight:800;margin-bottom:10px}.full{grid-column:1/-1}table{width:100%;border-collapse:collapse;font-size:13px}th,td{text-align:left;padding:8px;border-bottom:1px solid var(--line)}a{color:var(--accent)}#login{position:fixed;inset:0;background:#020817e8;z-index:20;display:flex;align-items:center;justify-content:center}.loginCard{width:min(430px,90vw);background:var(--card);border:1px solid var(--line);border-radius:18px;padding:22px}.loginCard input{max-width:none;width:100%;margin:10px 0}.pill{display:inline-block;padding:3px 8px;border-radius:999px;background:#24324f;color:var(--muted);font-size:12px}.ok{color:var(--ok)}.bad{color:var(--bad)}@media(max-width:900px){.grid{grid-template-columns:1fr}.eventsCtl{grid-template-columns:105px repeat(4,70px) 85px 85px 90px}}
</style></head><body>
<div id="login"><div class="loginCard"><div class="brand">G-Cam Remote Dashboard</div><div class="muted">Enter the Control API Key shown in the Windows application's Cloudflare / Dashboard tab.</div><input id="key" type="password" placeholder="Control API Key"><div class="toolbar"><button onclick="login()">Sign in</button></div><div id="loginMsg" class="muted" style="margin-top:10px"></div></div></div>
<div class="top"><div><div class="brand">G-Cam Windows AI Dashboard</div><div class="muted">Person • Vehicle • LPD • Garbage • Event Evidence</div></div><div class="toolbar"><button onclick="post('/api/runtime/start',{})">Start Runtime</button><button class="danger" onclick="post('/api/runtime/stop',{})">Stop Runtime</button><button class="secondary" onclick="logout()">Logout</button></div></div>
<div class="wrap"><div class="grid">
<div class="card live"><img id="live" alt="Live camera"></div>
<div class="card"><div class="sectionTitle">Runtime Status</div><div class="stats"><div class="stat">Camera<b id="camera">-</b></div><div class="stat">Buffer<b id="buffer">-</b></div><div class="stat">Detections<b id="detections">0</b></div><div class="stat">Events<b id="eventsCount">0</b></div></div><div style="margin-top:12px" class="muted" id="models">Models: -</div><div style="margin-top:8px" class="muted" id="tunnel">Tunnel: -</div><div style="margin-top:8px" class="pill" id="latest">No event</div></div>
<div class="card full"><div class="sectionTitle">Detection / Evidence Controls</div><div class="toolbar" style="margin-bottom:12px"><label><input type="checkbox" id="warningMaster" onchange="warningMaster()"> Master warning audio</label></div><div class="eventsCtl" id="controls"></div></div>
<div class="card full"><div class="sectionTitle">Recent Events</div><div style="overflow:auto"><table><thead><tr><th>Time</th><th>Event</th><th>Confidence</th><th>Status</th><th>Image</th><th>Video</th></tr></thead><tbody id="eventRows"></tbody></table></div></div>
</div></div>
<script>
const kinds=['person','vehicle','lpd','garbage'];let signedIn=false;
async function login(){const key=document.getElementById('key').value;const r=await fetch('/api/auth',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({key})});if(r.ok){signedIn=true;document.getElementById('login').style.display='none';refreshAll()}else document.getElementById('loginMsg').textContent='Invalid key';}
async function logout(){await fetch('/api/logout',{method:'POST'});signedIn=false;document.getElementById('login').style.display='flex';}
async function post(url,obj){const r=await fetch(url,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(obj)});if(r.status===401){document.getElementById('login').style.display='flex';signedIn=false;return null}const j=await r.json().catch(()=>({}));if(!r.ok)alert(j.error||'Request failed');setTimeout(refreshAll,250);return j}
function ctlHtml(c){const k=c.event.toLowerCase()==='licenseplate'?'lpd':c.event.toLowerCase();return `<div class="row"><b>${k.toUpperCase()}</b><label><input id="${k}_det" type="checkbox" ${c.detectionEnabled?'checked':''}> Detect</label><label><input id="${k}_warn" type="checkbox" ${c.warningAudioEnabled?'checked':''}> Audio</label><label><input id="${k}_img" type="checkbox" ${c.imageCaptureEnabled?'checked':''}> Image</label><label><input id="${k}_vid" type="checkbox" ${c.videoCaptureEnabled?'checked':''}> Video</label><input id="${k}_delay" type="number" min="0" step="0.1" value="${c.detectionDelaySeconds}"><input id="${k}_cool" type="number" min="0" step="0.5" value="${c.cooldownSeconds}"><span><button onclick="saveCtl('${k}')">Save</button> <button class="secondary" onclick="testEvt('${k}')">Test</button></span></div>`}
async function loadControls(){const r=await fetch('/api/controls/state');if(r.status===401){document.getElementById('login').style.display='flex';signedIn=false;return}const j=await r.json();document.getElementById('warningMaster').checked=j.warningMasterEnabled;document.getElementById('controls').innerHTML='<div class="head">Event</div><div class="head">Detection</div><div class="head">Warning</div><div class="head">Image</div><div class="head">Video</div><div class="head">Delay s</div><div class="head">Cooldown s</div><div class="head">Actions</div>'+j.events.map(ctlHtml).join('')}
async function saveCtl(k){await post('/api/controls/event',{event:k,detectionEnabled:document.getElementById(k+'_det').checked,warningAudioEnabled:document.getElementById(k+'_warn').checked,imageCaptureEnabled:document.getElementById(k+'_img').checked,videoCaptureEnabled:document.getElementById(k+'_vid').checked,detectionDelaySeconds:Number(document.getElementById(k+'_delay').value),cooldownSeconds:Number(document.getElementById(k+'_cool').value)})}
async function warningMaster(){await post('/api/controls/warning_audio',{enabled:document.getElementById('warningMaster').checked})}async function testEvt(k){await post('/api/test-event',{event:k})}
async function loadState(){const r=await fetch('/api/state');if(!r.ok)return;const s=await r.json();document.getElementById('camera').textContent=s.cameraStatus;document.getElementById('buffer').textContent=s.bufferStatus;document.getElementById('detections').textContent=s.detectionsSeen;document.getElementById('eventsCount').textContent=s.eventsTriggered;document.getElementById('models').textContent='Models: '+s.modelStatus;document.getElementById('tunnel').textContent='Tunnel: '+s.tunnelStatus;document.getElementById('latest').textContent=s.latestEvent?`${new Date(s.latestEvent.timestamp).toLocaleString()} | ${s.latestEvent.kind} | ${s.latestEvent.status}`:'No event'}
async function loadEvents(){const r=await fetch('/api/events?limit=30');if(!r.ok)return;const rows=await r.json();document.getElementById('eventRows').innerHTML=rows.map(e=>`<tr><td>${new Date(e.timestamp).toLocaleString()}</td><td>${e.kind}</td><td>${Math.round(e.confidence*100)}%</td><td>${e.status}</td><td>${e.imagePath?`<a target="_blank" href="/api/events/${e.eventId}/image">View</a>`:'-'}</td><td>${e.videoPath?`<a target="_blank" href="/api/events/${e.eventId}/video">Play</a>`:'-'}</td></tr>`).join('')}
function liveTick(){if(!signedIn)return;const i=document.getElementById('live');i.src='/api/live.jpg?t='+Date.now()}
async function refreshAll(){if(!signedIn)return;await Promise.all([loadState(),loadControls(),loadEvents()])}
(async()=>{const r=await fetch('/api/auth/status');const j=await r.json();if(j.authenticated){signedIn=true;document.getElementById('login').style.display='none';refreshAll()}})();setInterval(()=>{if(signedIn){loadState();loadEvents()}},2000);setInterval(liveTick,750);
</script></body></html>
""";
}
