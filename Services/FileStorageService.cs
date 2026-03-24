using Microsoft.AspNetCore.Http;

namespace BookShelf.Api.Services;

public class FileStorageService(IWebHostEnvironment environment)
{
    private static readonly string[] PdfExtensions = [".pdf"];
    private static readonly string[] AudioExtensions = [".mp3", ".wav", ".m4a", ".aac", ".ogg"];

    public Task<string> SavePdfAsync(IFormFile file, CancellationToken cancellationToken = default) =>
        SaveAsync(file, "pdfs", PdfExtensions, cancellationToken);

    public Task<string> SaveAudioAsync(IFormFile file, CancellationToken cancellationToken = default) =>
        SaveAsync(file, "audios", AudioExtensions, cancellationToken);

    public void Delete(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var trimmedPath = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.Combine(GetWebRootPath(), trimmedPath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
    }

    private async Task<string> SaveAsync(
        IFormFile file,
        string folderName,
        IReadOnlyCollection<string> allowedExtensions,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("The uploaded file is empty.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"Unsupported file type: {extension}");
        }

        var folderPath = Path.Combine(GetWebRootPath(), "uploads", folderName);
        Directory.CreateDirectory(folderPath);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(folderPath, fileName);

        await using var stream = File.Create(absolutePath);
        await file.CopyToAsync(stream, cancellationToken);

        return $"/uploads/{folderName}/{fileName}";
    }

    private string GetWebRootPath()
    {
        var webRootPath = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(environment.ContentRootPath, "wwwroot");
            Directory.CreateDirectory(webRootPath);
        }

        return webRootPath;
    }
}
