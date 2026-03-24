using System.Net.Http.Headers;
using System.Text.Json;

namespace BizManager.Services;

/// <summary>
/// Lightweight Supabase Storage client — uploads a file stream to a bucket
/// and returns the public URL. Works without the heavy Supabase SDK.
/// </summary>
public class SupabaseStorageService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;    // e.g. https://xxxx.supabase.co/storage/v1
    private readonly string _anonKey;    // service_role or anon key

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_baseUrl) && !string.IsNullOrWhiteSpace(_anonKey);

    public SupabaseStorageService(string supabaseProjectUrl, string supabaseKey)
    {
        _anonKey = supabaseKey;
        // strip trailing slash and append /storage/v1
        _baseUrl = supabaseProjectUrl.TrimEnd('/') + "/storage/v1";
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("apikey", supabaseKey);
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");
    }

    /// <summary>
    /// Uploads a stream to Supabase Storage.
    /// </summary>
    /// <param name="bucket">Bucket name (e.g. "product-images")</param>
    /// <param name="objectPath">Path inside the bucket, e.g. "products/123_abc.jpg"</param>
    /// <param name="stream">File data</param>
    /// <param name="contentType">MIME type</param>
    /// <returns>Public URL of the uploaded file</returns>
    public async Task<string> UploadAsync(string bucket, string objectPath, Stream stream, string contentType)
    {
        var url = $"{_baseUrl}/object/{bucket}/{objectPath}";

        var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        // Use PUT for upsert (creates or replaces)
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = content
        };
        request.Headers.Add("x-upsert", "true");

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Supabase upload failed [{response.StatusCode}]: {body}");
        }

        // Return the public URL. Assumes bucket has public access enabled.
        return GetPublicUrl(bucket, objectPath);
    }

    /// <summary>
    /// Builds the public URL for an object (bucket must have public access).
    /// </summary>
    public string GetPublicUrl(string bucket, string objectPath)
    {
        // Remove /storage/v1 suffix to get project root, then re-add public URL pattern
        var projectUrl = _baseUrl.Replace("/storage/v1", "");
        return $"{projectUrl}/storage/v1/object/public/{bucket}/{objectPath}";
    }

    /// <summary>
    /// Deletes an object from storage given its full public URL or object path.
    /// </summary>
    public async Task DeleteByUrlAsync(string bucket, string objectPath)
    {
        if (string.IsNullOrWhiteSpace(objectPath)) return;
        var url = $"{_baseUrl}/object/{bucket}/{objectPath}";
        try { await _http.DeleteAsync(url); } catch { /* swallow delete errors */ }
    }

    /// <summary>
    /// Given a full Supabase public URL, extract the object path within the bucket.
    /// </summary>
    public static string? ExtractObjectPath(string? publicUrl, string bucket)
    {
        if (string.IsNullOrWhiteSpace(publicUrl)) return null;
        // URL pattern: .../storage/v1/object/public/{bucket}/{path}
        var marker = $"/object/public/{bucket}/";
        var idx = publicUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? publicUrl[(idx + marker.Length)..] : null;
    }

    /// <summary>
    /// Determines content type from file extension.
    /// </summary>
    public static string GetContentType(string ext) => ext.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"            => "image/png",
        ".webp"           => "image/webp",
        ".gif"            => "image/gif",
        ".pdf"            => "application/pdf",
        _                 => "application/octet-stream"
    };
}
