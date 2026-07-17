using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http;
using Azure.Identity;

namespace BookShelf.Api.Services;

public class FileStorageService(IConfiguration configuration)
{
    private static readonly string[] PdfExtensions = [".pdf"];
    private static readonly string[] AudioExtensions = [".mp3", ".wav", ".m4a", ".aac", ".ogg"];
    private readonly string containerName = configuration["AzureStorage:ContainerName"] ?? "uploads";
    private readonly BlobContainerClient containerClient = CreateContainerClient(configuration);

    public Task<string> SavePdfAsync(IFormFile file, CancellationToken cancellationToken = default) =>
        SaveAsync(file, "pdfs", PdfExtensions, cancellationToken);

    public Task<string> SaveAudioAsync(IFormFile file, CancellationToken cancellationToken = default) =>
        SaveAsync(file, "audios", AudioExtensions, cancellationToken);

    public async Task<string?> GetReadUrlAsync(string? blobReference, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(blobReference);
        if (blobClient is null)
        {
            return null;
        }

        if (blobClient.CanGenerateSasUri)
        {
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = blobClient.BlobContainerName,
                BlobName = blobClient.Name,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            return blobClient.GenerateSasUri(sasBuilder).ToString();
        }

        var exists = await blobClient.ExistsAsync(cancellationToken);
        return exists.Value ? blobClient.Uri.ToString() : null;
    }

    public async Task DeleteAsync(string? blobUrl, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(blobUrl);
        if (blobClient is null)
        {
            return;
        }

        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
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

        return blobName;
    }

    private BlobClient? GetBlobClient(string? blobReference)
    {
        var blobName = ExtractBlobName(blobReference);
        return string.IsNullOrWhiteSpace(blobName) ? null : containerClient.GetBlobClient(blobName);
    }

    private string? ExtractBlobName(string? blobReference)
    {
        if (string.IsNullOrWhiteSpace(blobReference))
        {
            return null;
        }

        if (!Uri.TryCreate(blobReference, UriKind.Absolute, out var blobUri))
        {
            return blobReference.TrimStart('/');
        }

        var path = Uri.UnescapeDataString(blobUri.AbsolutePath.TrimStart('/'));
        var containerPrefix = $"{containerName}/";
        if (path.StartsWith(containerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return path[containerPrefix.Length..];
        }

        var firstSlash = path.IndexOf('/');
        return firstSlash >= 0 ? path[(firstSlash + 1)..] : path;
    }

    private static BlobContainerClient CreateContainerClient(IConfiguration configuration)
    {
        var containerName = configuration["AzureStorage:ContainerName"] ?? "uploads";
        var connectionString = configuration["AzureStorage:ConnectionString"];

        BlobServiceClient serviceClient;
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            serviceClient = new BlobServiceClient(connectionString);
        }
        else
        {
            var accountName = configuration["AzureStorage:AccountName"];
            if (string.IsNullOrWhiteSpace(accountName))
            {
                throw new InvalidOperationException("AzureStorage:AccountName or AzureStorage:ConnectionString is required.");
            }

            serviceClient = new BlobServiceClient(
                new Uri($"https://{accountName}.blob.core.windows.net"),
                new DefaultAzureCredential());
        }

        var containerClient = serviceClient.GetBlobContainerClient(containerName);
        containerClient.CreateIfNotExists();

        return containerClient;
    }
}
