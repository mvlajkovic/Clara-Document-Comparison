using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// JSON already serializes as camelCase by default in ASP.NET Core, matching
// what the client expects — no extra configuration needed for that.
builder.Services.AddControllers();

// Named HttpClient used by the /api/compare/urls endpoint to download PDFs.
builder.Services.AddHttpClient("lessons");

// Two PDFs up to 60 MB each in one multipart request.
const long maxUploadBytes = 120L * 1024 * 1024;

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxUploadBytes;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxUploadBytes;
});

var app = builder.Build();

// Serves wwwroot/index.html at "/" and everything else under wwwroot/ as-is.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();
