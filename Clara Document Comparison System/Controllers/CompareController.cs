using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Clara.DocumentComparison.Models;
using Clara.DocumentComparison.Services;
using Microsoft.AspNetCore.Mvc;

namespace Clara.DocumentComparison.Controllers;

[ApiController]
[Route("api/compare")]
public class CompareController : ControllerBase
{
    private const long MaxBytes = 60L * 1024 * 1024;

    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public CompareController(IWebHostEnvironment env, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _env = env;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    /// <summary>
    /// POST api/compare/upload
    /// multipart/form-data with two file fields (any names; first = left, second = right).
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(CancellationToken cancellationToken)
    {
        var files = Request.Form.Files;
        if (files.Count < 2)
            return BadRequest(new { error = "Two PDF files are required." });

        string workFolder = WorkFolder();
        string leftPath = Path.Combine(workFolder, Guid.NewGuid() + ".pdf");
        string rightPath = Path.Combine(workFolder, Guid.NewGuid() + ".pdf");

        try
        {
            await SaveAsync(files[0], leftPath, cancellationToken);
            await SaveAsync(files[1], rightPath, cancellationToken);

            foreach (var path in new[] { leftPath, rightPath })
            {
                var info = new FileInfo(path);
                if (info.Length == 0) return BadRequest(new { error = "One of the files is empty." });
                if (info.Length > MaxBytes) return BadRequest(new { error = "Files must be under 60 MB." });
                if (!LooksLikePdf(path)) return BadRequest(new { error = "Both files must be PDFs." });
            }

            string leftName = CleanName(files[0].FileName) ?? "Document 1";
            string rightName = CleanName(files[1].FileName) ?? "Document 2";

            var result = Compare(leftPath, rightPath, leftName, rightName);
            return Ok(result);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Compare failed: " + ex);
            return StatusCode(500, new { error = ex.Message });
        }
        finally
        {
            TryDelete(leftPath);
            TryDelete(rightPath);
        }
    }

    /// <summary>
    /// GET api/compare/urls?lessonCode=SE201&amp;lessonNumber=05&amp;yearOld=2023-2024&amp;yearNew=0
    /// Same URL convention as your existing endpoint. 0 means the current school year.
    /// </summary>
    [HttpGet("urls")]
    public async Task<IActionResult> FromUrls(
        string lessonCode, string lessonNumber, string yearOld, string yearNew,
        string lessonCodeNew, CancellationToken cancellationToken)
    {
        string leftPath = null;
        string rightPath = null;

        try
        {
            string baseUrl = _configuration["LessonBaseUrl"] ?? "http://mdita.metropolitan.ac.rs/qdita-temp/";
            string yearNewPart = yearNew == "0" ? "" : yearNew + "/";
            string codeNew = string.IsNullOrEmpty(lessonCodeNew) ? lessonCode : lessonCodeNew;
            string number = lessonNumber.PadLeft(2, '0');

            string url1 = $"{baseUrl}{yearOld}/{lessonCode}/L{number}-PDF/{lessonCode}-L{number}.pdf";
            string url2 = $"{baseUrl}{yearNewPart}{codeNew}/L{number}-PDF/{codeNew}-L{number}.pdf";

            string folder = WorkFolder();
            leftPath = Path.Combine(folder, Guid.NewGuid() + ".pdf");
            rightPath = Path.Combine(folder, Guid.NewGuid() + ".pdf");

            var client = _httpClientFactory.CreateClient("lessons");

            try { await DownloadAsync(client, url1, leftPath, cancellationToken); }
            catch (Exception ex) { throw new Exception($"Couldn't download the old file.\nURL: {url1}\n{ex.Message}", ex); }

            try { await DownloadAsync(client, url2, rightPath, cancellationToken); }
            catch (Exception ex) { throw new Exception($"Couldn't download the new file.\nURL: {url2}\n{ex.Message}", ex); }

            var result = Compare(leftPath, rightPath,
                $"{lessonCode}-L{number} ({yearOld})",
                $"{codeNew}-L{number} ({(yearNew == "0" ? "current" : yearNew)})");

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        finally
        {
            TryDelete(leftPath);
            TryDelete(rightPath);
        }
    }

    private static CompareResult Compare(string leftPath, string rightPath, string leftName, string rightName)
    {
        var sw = Stopwatch.StartNew();

        var left = PdfTextExtractor.Extract(leftPath);
        var right = PdfTextExtractor.Extract(rightPath);

        List<Highlight> leftHighlights, rightHighlights;
        List<ChangeBlock> blocks;
        DiffBuilder.Build(left.Tokens, right.Tokens, out leftHighlights, out rightHighlights, out blocks);

        double difference = SimilarityAlgorithm.ComputeDifference(leftPath, rightPath);

        sw.Stop();

        return new CompareResult
        {
            Difference = difference.ToString("0.00", CultureInfo.InvariantCulture),
            DifferenceValue = difference,
            Algorithm = SimilarityAlgorithm.AlgorithmId == 2 ? "Manhattan" : "Cosine",
            Left = new SideResult { Pages = left.Pages, Highlights = leftHighlights, WordCount = left.Tokens.Count },
            Right = new SideResult { Pages = right.Pages, Highlights = rightHighlights, WordCount = right.Tokens.Count },
            Blocks = blocks,
            RemovedCount = blocks.Count(x => x.Kind == "removed"),
            AddedCount = blocks.Count(x => x.Kind == "added"),
            ChangedCount = blocks.Count(x => x.Kind == "changed"),
            LeftName = leftName,
            RightName = rightName,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }

    private string WorkFolder()
    {
        string folder = Path.Combine(_env.ContentRootPath, "App_Data", "uploads");
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static async Task SaveAsync(IFormFile file, string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream, cancellationToken);
    }

    private static async Task DownloadAsync(HttpClient client, string url, string path, CancellationToken cancellationToken)
    {
        await using var responseStream = await client.GetStreamAsync(url, cancellationToken);
        await using var fileStream = new FileStream(path, FileMode.Create);
        await responseStream.CopyToAsync(fileStream, cancellationToken);
    }

    private static bool LooksLikePdf(string path)
    {
        using var fs = System.IO.File.OpenRead(path);
        var header = new byte[5];
        if (fs.Read(header, 0, 5) < 5) return false;
        return header[0] == '%' && header[1] == 'P' && header[2] == 'D' && header[3] == 'F';
    }

    private static string CleanName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return Path.GetFileName(raw.Trim('"'));
    }

    private static void TryDelete(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
        catch (Exception ex) { Debug.WriteLine("Cleanup failed for " + path + ": " + ex.Message); }
    }
}
