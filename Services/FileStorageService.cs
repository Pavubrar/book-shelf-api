using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Azure.Identity;

namespace BookShelf.Api.Services;

public class FileStorageService(IConfiguration configuration)
{
    private static readonly string[] PdfExtensions = [".pdf"];
    private static readonly string[] AudioExtensions = [".mp3", ".wav", ".m4a", ".aac", ".ogg"];
    private readonly BlobContainerClient containerClient = CreateContainerClient(configuration);

    public Task<string> SavePdfAsync(IFormFile file, CancellationToken cancellationToken = default) =>
        SaveAsync(file, "pdfs", PdfExtensions, cancellationToken);

    public Task<string> SaveAudioAsync(IFormFile file, CancellationToken cancellationToken = default) =>
        SaveAsync(file, "audios", AudioExtensions, cancellationToken);

    public async Task DeleteAsync(string? blobUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobUrl))
        {
            return;
        }

        if (!Uri.TryCreate(blobUrl, UriKind.Absolute, out var blobUri))
        {
            return;
        }

        var blobName = Uri.UnescapeDataString(blobUri.AbsolutePath.TrimStart('/'));
        var firstSlash = blobName.IndexOf('/');
        if (firstSlash >= 0)
        {
            blobName = blobName[(firstSlash + 1)..];
        }

        if (string.IsNullOrWhiteSpace(blobName))
        {
            return;
        }

        await containerClient.DeleteBlobIfExistsAsync(blobName, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
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

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var blobName = $"{folderName}/{fileName}";
        var blobClient = containerClient.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = file.ContentType
                }
            },
            cancellationToken);

        return blobClient.Uri.ToString();
    }

   
   private static BlobContainerClient CreateContainerClient(IConfiguration configuration)
{
    var accountName = configuration["AzureStorage:AccountName"];
    if (string.IsNullOrWhiteSpace(accountName))
    {
        throw new InvalidOperationException("AzureStorage:AccountName is missing.");
    }

    var containerName = configuration["AzureStorage:ContainerName"] ?? "uploads";

    var serviceClient = new BlobServiceClient(
        new Uri($"https://{accountName}.blob.core.windows.net"),
        new DefaultAzureCredential());

    var containerClient = serviceClient.GetBlobContainerClient(containerName);

    containerClient.CreateIfNotExists();

    return containerClient;
}
}
