// SPDX-License-Identifier: MPL-2.0
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Nini.Config;
using OpenGenesisLINK.Addons.AssetService;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static IniConfigSource NewConfig(bool enabled)
{
    var source = new IniConfigSource();
    source.AddConfig("OGLS3Storage").Set("Enabled", enabled);
    return source;
}

static void ExpectInvalid(IniConfigSource source, string description)
{
    bool invalid = false;
    try { _ = OGLS3StorageOptions.Read(source); }
    catch (InvalidOperationException) { invalid = true; }
    Check(invalid, description);
}

// Defaults are OFF even with missing section or empty credentials.
// Critically, these tests do not create a network client.
Check(!OGLS3StorageOptions.Read(new IniConfigSource()).Enabled,
    "Missing S3 section must not activate");
Check(!OGLS3StorageOptions.Read(NewConfig(false)).Enabled,
    "Enabled=false must not activate or require credentials");
bool constructionDenied = false;
try { using var forbidden = new OGLS3ObjectStore(OGLS3StorageOptions.Read(NewConfig(false))); }
catch (InvalidOperationException) { constructionDenied = true; }
Check(constructionDenied, "Disabled provider was constructible");

var absentCredentials = NewConfig(true);
absentCredentials.Configs["OGLS3Storage"]!.Set("Endpoint", "http://127.0.0.1:19119");
// No environment variables are needed to test the disabled path.
// Explicitly empty config secrets must fail if provider is enabled.
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OGL_S3_ACCESS_KEY")) &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OGL_S3_SECRET_KEY")))
    ExpectInvalid(absentCredentials, "Enabled S3 must reject missing credentials");

var badHttp = NewConfig(true);
var sec = badHttp.Configs["OGLS3Storage"]!;
sec.Set("Endpoint", "http://storage.example.com:9000");
sec.Set("AccessKey", "ci-test-only");
sec.Set("SecretKey", "ci-test-only");
ExpectInvalid(badHttp, "Public HTTP endpoint was accepted");

sec.Set("Endpoint", "https://storage.example.com");
ExpectInvalid(badHttp, "UseSSL mismatch must fail closed");
sec.Set("UseSSL", true);
Check(OGLS3StorageOptions.Read(badHttp).Enabled, "Explicit HTTPS config must validate");
sec.Set("MaxRetryAttempts", 0);
ExpectInvalid(badHttp, "Unbounded/invalid retry value was accepted");
Console.WriteLine("S3 configuration, fail-closed opt-in and secret-safe validation: PASS");

if (!args.Contains("--s3-integration", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("Isolated S3 integration tests not requested; no S3 network requests made.");
    return;
}

// Intentionally refuse any remote or non-unique target, even when a CI
// variable is misconfigured. The test alone may create a test-only bucket.
string endpoint = Environment.GetEnvironmentVariable("OGL_S3_CI_ENDPOINT") ?? "";
string access = Environment.GetEnvironmentVariable("OGL_S3_CI_ACCESS_KEY") ?? "";
string secret = Environment.GetEnvironmentVariable("OGL_S3_CI_SECRET_KEY") ?? "";
string bucket = Environment.GetEnvironmentVariable("OGL_S3_CI_BUCKET") ?? "";
Check(Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) &&
      endpointUri.IsLoopback && endpointUri.Scheme == "http" &&
      endpointUri.Port == 19119, "Integration test permits only local port 19119");
Check(bucket.StartsWith("ogl-s3-ci-", StringComparison.Ordinal) &&
      bucket.Length >= 17 && bucket.Length <= 63,
      "Integration test requires a unique test-only bucket");
Check(!string.IsNullOrWhiteSpace(access) && !string.IsNullOrWhiteSpace(secret),
    "Integration test credentials missing");

Environment.SetEnvironmentVariable("OGL_S3_ACCESS_KEY", access);
Environment.SetEnvironmentVariable("OGL_S3_SECRET_KEY", secret);
var config = NewConfig(true);
var settings = config.Configs["OGLS3Storage"]!;
settings.Set("Endpoint", endpoint);
settings.Set("Bucket", bucket);
settings.Set("Region", "us-east-1");
settings.Set("ForcePathStyle", true);
settings.Set("UseSSL", false);
settings.Set("ConnectionTimeoutSeconds", 2);
settings.Set("RequestTimeoutSeconds", 10);
settings.Set("MaxRetryAttempts", 2);
using var store = new OGLS3ObjectStore(OGLS3StorageOptions.Read(config));
using var owner = new AmazonS3Client(new BasicAWSCredentials(access, secret),
    new AmazonS3Config { ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1" });
bool missingBucketRejected = false;
try { store.CheckBucket(); }
catch (InvalidOperationException) { missingBucketRejected = true; }
Check(missingBucketRejected, "S3 provider created or accepted missing bucket");

// Test harness creates the unique isolated bucket; the actual provider never does.
await owner.PutBucketAsync(new PutBucketRequest { BucketName = bucket });
var objects = new List<string>();
try
{
    store.CheckBucket();
    Check(!store.Exists(new string('a', 64)), "HEAD erroneously found missing object");
    Check(store.Get(new string('a', 64)) == null, "GET erroneously found missing object");

    foreach (string type in new[] { "texture", "mesh", "animation", "sound", "inventory" })
    {
        byte[] sample = Encoding.UTF8.GetBytes(type + "-asset-" + Guid.NewGuid());
        string hash = Convert.ToHexString(SHA256.HashData(sample)).ToLowerInvariant();
        Check(store.Put(sample) == hash, "PUT returned wrong content hash");
        string key = "assets/sha256/" + hash[..2] + "/" + hash.Substring(2, 2) + "/" + hash;
        objects.Add(key);
        Check(store.Exists(hash), "HEAD failed after completed PUT");
        Check(store.Get(hash)!.SequenceEqual(sample), "GET modified binary data");
        Check(store.Put(sample) == hash, "Idempotent conditional write failed");
    }
    byte[] tampered = { 42, 0, 1, 253 };
    string falseHash = new string('b', 64);
    string badKey = "assets/sha256/bb/bb/" + falseHash;
    objects.Add(badKey);
    await owner.PutObjectAsync(new PutObjectRequest
    {
        BucketName = bucket, Key = badKey,
        InputStream = new MemoryStream(tampered), UseChunkEncoding = false
    });
    bool corruptionDetected = false;
    try { store.Get(falseHash); }
    catch (InvalidDataException) { corruptionDetected = true; }
    Check(corruptionDetected, "S3 accepted corrupt object bytes");

    // Simulate a disconnected WireGuard/S3 endpoint using an unused localhost
    // port. This must not be reported as a missing object or success.
    var unavailableCfg = NewConfig(true);
    var uc = unavailableCfg.Configs["OGLS3Storage"]!;
    uc.Set("Endpoint", "http://127.0.0.1:19120");
    uc.Set("Bucket", bucket);
    uc.Set("ConnectionTimeoutSeconds", 1);
    uc.Set("RequestTimeoutSeconds", 2);
    uc.Set("MaxRetryAttempts", 1);
    using var unavailable = new OGLS3ObjectStore(OGLS3StorageOptions.Read(unavailableCfg));
    bool connectionFailed = false;
    try { unavailable.Exists(new string('a', 64)); }
    catch (Exception) { connectionFailed = true; }
    Check(connectionFailed, "S3 connectivity failure incorrectly returned a cache miss");
    Console.WriteLine("Isolated MinIO-compatible S3 Put/Get/Head/corruption/disconnect: PASS");
}
finally
{
    if (!args.Contains("--keep-test-bucket", StringComparer.OrdinalIgnoreCase))
    {
        foreach (string key in objects)
            await owner.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = key });
        await owner.DeleteBucketAsync(new DeleteBucketRequest { BucketName = bucket });
    }
    else
        Console.WriteLine("Test-only bucket remains in ephemeral CI container until container teardown.");
}
