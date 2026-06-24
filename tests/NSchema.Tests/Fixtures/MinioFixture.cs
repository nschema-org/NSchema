using Amazon.Runtime;
using Amazon.S3;
using Testcontainers.Minio;

namespace NSchema.Tests.Fixtures;

/// <summary>
/// Starts a throwaway MinIO (S3-compatible) container, shared across the whole test assembly, and creates a bucket for the s3 state-store tests.
/// </summary>
public sealed class MinioFixture : IAsyncLifetime
{
    private readonly MinioContainer _container = new MinioBuilder("minio/minio:latest").Build();

    public string Endpoint { get; private set; } = null!;
    public string AccessKey { get; private set; } = null!;
    public string SecretKey { get; private set; } = null!;
    public string BucketName { get; } = $"nschema-cli-{Guid.NewGuid():N}";

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        Endpoint = _container.GetConnectionString();
        AccessKey = _container.GetAccessKey();
        SecretKey = _container.GetSecretKey();

        using var s3 = new AmazonS3Client(
            new BasicAWSCredentials(AccessKey, SecretKey),
            new AmazonS3Config { ServiceURL = Endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1" });
        await s3.PutBucketAsync(BucketName);
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
