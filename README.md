# Clara Document Comparison System (.NET 9)

Same app, rebuilt on a current, actively-supported version of .NET. This is a
full rewrite of the project structure — **the actual comparison logic,
diffing, and viewer are untouched.** What changed is how the project is put
together, and that change is what fixes the whole class of errors you hit
building the old version.

## Why this fixes the dependency chase

The old project used a format from the .NET Framework era (`packages.config`
+ hand-written `<Reference HintPath="...">` entries). In that format, when a
library needs another library underneath it, Visual Studio does **not** add
that automatically — you have to find it and wire it in by hand. That's why
we spent several rounds patching in `System.Runtime.CompilerServices.Unsafe`,
`Common.Logging`, and `BouncyCastle.Crypto` one at a time as each one failed.

This project uses a **modern SDK-style `.csproj`** with `PackageReference`
instead. In that format, when you reference a package, NuGet reads that
package's own list of things *it* needs and pulls all of them in automatically,
as many layers deep as necessary. You'll see the whole `.csproj` for this
version is about 15 lines — that's not a simplification, that's genuinely all
it needs to say.

## What to install

1. **.NET 9 SDK** — download from **dotnet.microsoft.com/download/dotnet/9.0**
   (get the **SDK**, not just the Runtime — the SDK includes the Runtime and
   also the tools that let you build the project)
2. **Visual Studio 2022, fully updated** — open Visual Studio, go to
   **Help → Check for Updates**, and install anything it offers. .NET 9 needs
   a recent-enough Visual Studio to recognize it; older installs won't see it
   as an option.

## Opening it

1. Extract the zip
2. Double-click **`Clara Document Comparison System.sln`**
3. Visual Studio restores the two NuGet packages automatically on open — no
   manual "Restore NuGet Packages" step needed, and no Package Manager Console
   commands
4. Press **▶ play**

Visual Studio now runs the app directly (Kestrel, the built-in ASP.NET Core
server) instead of through IIS Express — you'll notice the play button says
something like "Clara Document Comparison System" or "https" instead of "IIS
Express." That's expected.

### If Visual Studio doesn't offer `net9.0` as a target

That means it needs updating (see step 2 above), or as a fallback you can run
the app without Visual Studio's build system at all:

1. Open a terminal (**View → Terminal** in Visual Studio, or plain Windows
   Terminal / PowerShell)
2. `cd` into the folder containing `Clara Document Comparison System.csproj`
3. Run:
   ```
   dotnet run
   ```
4. Open the URL it prints (something like `http://localhost:5080`) in your
   browser

This path only needs the .NET 9 SDK installed — it doesn't depend on Visual
Studio's tooling being current, so it's a good way to confirm whether a
problem is "Visual Studio is out of date" versus something else.

## What actually changed, file by file

| Old (.NET Framework 4.7.2) | New (.NET 9) | Why |
|---|---|---|
| `packages.config` + manual `<Reference HintPath>` | `<PackageReference>` in the `.csproj` | Dependencies resolve automatically |
| `Web.config` binding redirects | *(deleted — not needed)* | Modern .NET doesn't require them |
| `Global.asax` / `Global.asax.cs` | `Program.cs` | Modern ASP.NET Core starts the app directly, no separate startup class |
| `App_Start\WebApiConfig.cs` | folded into `Program.cs` | Routing setup is a few lines now, not a separate file |
| `System.Web.Http.ApiController` | `Microsoft.AspNetCore.Mvc.ControllerBase` | The current web framework |
| `MultipartFormDataStreamProvider` | `Request.Form.Files` | ASP.NET Core parses uploaded files natively |
| `System.Web.Hosting.HostingEnvironment.MapPath` | `IWebHostEnvironment.ContentRootPath` | Modern way to find the app's folder on disk |
| `WebClient` (deprecated) | `HttpClient` | Current, supported way to download files |
| Client files at project root | Client files under `wwwroot\` | ASP.NET Core's convention for anything served to the browser |
| `itext7` (itext7.kernel) 7.1.13, needing `Common.Logging` + `BouncyCastle.Crypto` by hand | `itext` 9.7.0 | Current package (iText renamed `itext7` → `itext` at v9); logging and crypto dependencies resolve automatically via NuGet |

**Not changed at all:** `Algorithms\LevenshteinDistance.cs`,
`Algorithms\Reader.cs`, `Services\SimilarityAlgorithm.cs`,
`Services\PdfTextExtractor.cs`, `Services\MyersDiff.cs`,
`Services\DiffBuilder.cs`, `Models\CompareModels.cs`, and everything in
`wwwroot\` (the viewer itself). The difference score, the diffing, and the
side-by-side page are byte-for-byte what you already had working.

## Packages used

| Package | Version | What it's for |
|---|---|---|
| `itext` | 9.7.0 | Reads PDF text for `Reader.cs` (the difference score) — this is iText's current package name, renamed from `itext7` |
| `PdfPig` | 0.1.9 | Reads PDF text *with* word positions, for the visual diff |

Both are current, actively-published packages as of September 2026. Neither
needs anything added by hand — installing them is enough.

`itext` pulls in more than `Reader.cs` strictly uses (it includes layout,
forms, and signing modules alongside the PDF-reading code this app actually
calls) — that's a size trade-off for using a package name that's guaranteed to
exist and resolve correctly, rather than guessing at a narrower submodule name.

## Where things live now

```
Clara Document Comparison System.sln
└── Clara Document Comparison System\
    ├── Program.cs                    starts the app, sets upload size limit
    ├── appsettings.json               settings (replaces Web.config)
    ├── Controllers\CompareController.cs
    ├── Services\
    │   ├── PdfTextExtractor.cs        words + bounding boxes, via PdfPig
    │   ├── MyersDiff.cs               minimal edit script
    │   ├── DiffBuilder.cs             edits → change blocks → rectangles
    │   └── SimilarityAlgorithm.cs     wraps Reader + LevenshteinDistance
    ├── Algorithms\
    │   ├── LevenshteinDistance.cs     your file, unchanged
    │   └── Reader.cs                  your file, unchanged
    ├── Models\CompareModels.cs
    └── wwwroot\                       the viewer (served directly by the app)
        ├── index.html
        ├── css\app.css
        └── js\app.js
```

## Endpoints (unchanged)

| Method | Route | Notes |
|---|---|---|
| POST | `/api/compare/upload` | `multipart/form-data`, two PDFs |
| GET | `/api/compare/urls?lessonCode=SE201&lessonNumber=05&yearOld=2023-2024&yearNew=0` | Your mdita URL convention |

The lesson base URL lives in `appsettings.json` under `"LessonBaseUrl"`.

## Deploying this somewhere other than your own machine

This is no longer an IIS-hosted app in the old sense — ASP.NET Core apps run
via Kestrel and are typically put behind IIS, Nginx, or a reverse proxy only
as a **front door**, not as the actual runtime. If you eventually need to put
this on a server, that's a different (and honestly easier) conversation from
what we've been doing — say the word when you're ready and I'll walk through
it, including whether IIS is even the right choice on that server.
