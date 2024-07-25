using System.IO.Compression;
using System.Text;

class HttpResponse
{
    private readonly bool _compressBody;
    public HttpResponse(int? statusCode = null, string? body = null, Dictionary<string, string>? headers = null, bool compressBody = false)
    {
        StatusCode = statusCode ?? 200;
        Body = body ?? "";
        if (body != null) Headers["Content-Length"] = body.Length.ToString();
        if (headers != null) foreach (var header in headers) Headers[header.Key] = header.Value;
        _compressBody = compressBody;
    }

    public static HttpResponse Ok(string? body = null, Dictionary<string, string>? headers = null) => new(body: body, headers: headers);

    public static HttpResponse Created(string? body = null, Dictionary<string, string>? headers = null) => new(201, body: body, headers: headers);

    public static HttpResponse NotFound(string? body = null, Dictionary<string, string>? headers = null) => new(404, body: body, headers: headers);

    public string HttpVersion
    {
        get;
        set;
    } = "1.1";

    public int StatusCode
    {
        get;
        set;
    }

    public string StatusDescription => Constants.StatusDescriptions.GetValueOrDefault(StatusCode, defaultValue: "Unknown");

    public Dictionary<string, string> Headers { get; set; } = [];

    public string Body
    {
        get;
        set;
    }

    private string HeadersToString()
    {
        if (Headers.Count == 0) return "\r\n";
        List<string> headers = [];
        foreach (KeyValuePair<string, string> header in Headers)
        {
            headers.Add($"{header.Key}: {header.Value}");
        }
        return $"{string.Join("\r\n", headers)}\r\n\r\n";
    }

    override public string ToString() => $"HTTP/{HttpVersion} {StatusCode} {StatusDescription}\r\n{HeadersToString()}{Body}";

    public byte[] ToBytes()
{
    byte[] bodyBytes = Encoding.UTF8.GetBytes(Body);
    byte[] compressedBody = bodyBytes;
    
    if (_compressBody)
    {
        using (var memoryStream = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
            {
                gzipStream.Write(bodyBytes, 0, bodyBytes.Length);
            } // GZipStream is automatically flushed and disposed here
            compressedBody = memoryStream.ToArray();
        }
        
        // Update Content-Length header with compressed length
        Headers["Content-Length"] = compressedBody.Length.ToString();
    }
    else
    {
        // Update Content-Length header with uncompressed length
        Headers["Content-Length"] = bodyBytes.Length.ToString();
    }
    
    string headerString = $"HTTP/{HttpVersion} {StatusCode} {StatusDescription}\r\n{HeadersToString()}";
    byte[] headerBytes = Encoding.UTF8.GetBytes(headerString);
    
    return [.. headerBytes, .. compressedBody];
}
}