using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

class Constants
{
    public static readonly string LOCALHOST_ADDRESS = "127.0.0.1";
    public static readonly int PORT = 4221;

    public const string ROOT_ROUTE_PATTERN = @"^/$";
    public const string ECHO_ROUTE_PATTERN = @"/echo/([^/]+)";
    public const string FILES_ROUTE_PATTERN = @"/files/([^/]+)";
    public const string USER_AGENT_ROUTE_PATTERN = @"^/user-agent$";

    public static readonly string HTTP_METHOD_PATTERN = @"^(GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS|TRACE|CONNECT)\s";
    public static readonly string REQUEST_HEADER_PATTERN = @"^([\w-]+):\s*(.+)$";

    public static readonly string[] serverRoutes = [ROOT_ROUTE_PATTERN, ECHO_ROUTE_PATTERN, FILES_ROUTE_PATTERN, USER_AGENT_ROUTE_PATTERN];

    public static readonly string[] availableCompressionSchemes = ["gzip", "deflate", "br", "zstd", "compress"];

    public static readonly IPAddress localIpAddress = IPAddress.Parse(LOCALHOST_ADDRESS);
    public static readonly byte[] buffer = new byte[1024];

    public static readonly Dictionary<int, string> StatusDescriptions = new()
    {
        // 1xx Informational
        { 100, "Continue" },
        { 101, "Switching Protocols" },
        { 102, "Processing" },
        { 103, "Early Hints" },

        // 2xx Success
        { 200, "OK" },
        { 201, "Created" },
        { 202, "Accepted" },
        { 203, "Non-Authoritative Information" },
        { 204, "No Content" },
        { 205, "Reset Content" },
        { 206, "Partial Content" },
        { 207, "Multi-Status" },
        { 208, "Already Reported" },
        { 226, "IM Used" },

        // 3xx Redirection
        { 300, "Multiple Choices" },
        { 301, "Moved Permanently" },
        { 302, "Found" },
        { 303, "See Other" },
        { 304, "Not Modified" },
        { 305, "Use Proxy" },
        { 307, "Temporary Redirect" },
        { 308, "Permanent Redirect" },

        // 4xx Client Error
        { 400, "Bad Request" },
        { 401, "Unauthorized" },
        { 402, "Payment Required" },
        { 403, "Forbidden" },
        { 404, "Not Found" },
        { 405, "Method Not Allowed" },
        { 406, "Not Acceptable" },
        { 407, "Proxy Authentication Required" },
        { 408, "Request Timeout" },
        { 409, "Conflict" },
        { 410, "Gone" },
        { 411, "Length Required" },
        { 412, "Precondition Failed" },
        { 413, "Payload Too Large" },
        { 414, "URI Too Long" },
        { 415, "Unsupported Media Type" },
        { 416, "Range Not Satisfiable" },
        { 417, "Expectation Failed" },
        { 418, "I'm a teapot" },
        { 421, "Misdirected Request" },
        { 422, "Unprocessable Entity" },
        { 423, "Locked" },
        { 424, "Failed Dependency" },
        { 425, "Too Early" },
        { 426, "Upgrade Required" },
        { 428, "Precondition Required" },
        { 429, "Too Many Requests" },
        { 431, "Request Header Fields Too Large" },
        { 451, "Unavailable For Legal Reasons" },

        // 5xx Server Error
        { 500, "Internal Server Error" },
        { 501, "Not Implemented" },
        { 502, "Bad Gateway" },
        { 503, "Service Unavailable" },
        { 504, "Gateway Timeout" },
        { 505, "HTTP Version Not Supported" },
        { 506, "Variant Also Negotiates" },
        { 507, "Insufficient Storage" },
        { 508, "Loop Detected" },
        { 510, "Not Extended" },
        { 511, "Network Authentication Required" }
    };
}

class Server
{
    public static Task Main(string[] args)
    {
        TcpListener server = new(Constants.localIpAddress, Constants.PORT);
        server.Start();
        Console.WriteLine($"Server listening on http://{server.Server.LocalEndPoint}");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            Task.Run(() => HandleClient(client));
        }
    }

    private static Task HandleClient(TcpClient client)
    {
        try
        {
            NetworkStream stream = client.GetStream();
            return HandleRequest(stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
            return Task.CompletedTask;
        }
    }

    private static Task HandleRequest(NetworkStream stream)
    {
        int bytesReceived = stream.Read(Constants.buffer);

        string requestString = Encoding.ASCII.GetString(Constants.buffer, 0, bytesReceived);

        HttpRequest request = HttpRequest.Parse(requestString);

        string? routeMatched = Constants.serverRoutes.Where(route => Regex.Match(request.Path, route).Success).FirstOrDefault(defaultValue: null);

        switch (routeMatched)
        {
            case Constants.ROOT_ROUTE_PATTERN:
                return HandleRootRoute(stream);
            case Constants.ECHO_ROUTE_PATTERN:
                var pathParameters = ExtractPathParametersFromUrl(request.Path, Constants.ECHO_ROUTE_PATTERN);
                if (request.Headers.GetValueOrDefault("Accept-Encoding", defaultValue: null) == null)
                {
                    return HandleEchoRoute(stream, pathParameters[1]);
                }
                string contentEncoding = request.Headers["Accept-Encoding"];
                string content = pathParameters[1];
                if (contentEncoding.Contains(',') == false && Constants.availableCompressionSchemes.Contains(request.Headers["Accept-Encoding"]) == false)
                {
                    return HandleEchoRoute(stream, content);
                }
                if (contentEncoding.Contains(','))
                {
                    List<string> validEncodings = [];
                    foreach (var item in contentEncoding.Split(','))
                    {
                        string itemName = item.Trim();
                        if (Constants.availableCompressionSchemes.Contains(itemName)) validEncodings.Add(itemName);
                    }
                    contentEncoding = string.Join(", ", validEncodings);
                }
                if (contentEncoding.Split(',', StringSplitOptions.TrimEntries)
                  .ToHashSet()
                  .Contains("gzip"))
                {
                    var bytes = Encoding.UTF8.GetBytes(content);
                    var memoryStream = new MemoryStream();
                    var gzipStream =
        new GZipStream(memoryStream, CompressionMode.Compress, true);
                    gzipStream.Write(bytes, 0, bytes.Length);
                    gzipStream.Flush();
                    gzipStream.Close();
                    var compressedBody = memoryStream.ToArray();
                    return HandleGzipCompressedEchoRoute(stream, contentEncoding, compressedBody);
                }

                return HandleCompressedEchoRoute(stream, content, contentEncoding);

            case Constants.FILES_ROUTE_PATTERN:
                var argv = Environment.GetCommandLineArgs();
                var filesDirectory = argv[2];
                var pathParams = ExtractPathParametersFromUrl(request.Path, Constants.FILES_ROUTE_PATTERN);
                string filePath = $"{filesDirectory}/{pathParams[1]}";
                if (request.Method == "POST") return HandleFilesPostRoute(stream, filePath, request.Body);
                return HandleFilesRoute(stream, filePath);
            case Constants.USER_AGENT_ROUTE_PATTERN:
                return HandleUserAgentRoute(stream, request.Headers["User-Agent"]);
            default:
                return HandleNotFoundRoute(stream);
        }
    }

    private static Task HandleNotFoundRoute(NetworkStream stream)
    {
        stream.Write(Encoding.ASCII.GetBytes(HttpResponse.NotFound()));
        stream.Close();
        return Task.CompletedTask;
    }

    private static Task HandleRootRoute(NetworkStream stream)
    {
        stream.Write(Encoding.ASCII.GetBytes(HttpResponse.Ok()));
        stream.Close();
        return Task.CompletedTask;
    }

    private static Task HandleEchoRoute(NetworkStream stream, string body)
    {
        stream.Write(Encoding.ASCII.GetBytes(HttpResponse.Ok(body, new() {
            {"Content-Type", "text/plain"},
            { "Content-Length", body.Length.ToString() }
            })));
        stream.Close();
        return Task.CompletedTask;
    }

    private static Task HandleCompressedEchoRoute(NetworkStream stream, string body, string contentEncoding)
    {
        stream.Write(Encoding.ASCII.GetBytes(HttpResponse.Ok(body, new(){
            {"Content-Type", "text/plain"},
            {"Content-Length", body.Length.ToString()},
            {"Content-Encoding", contentEncoding}
            })));
        stream.Close();
        return Task.CompletedTask;
    }

    private static Task HandleGzipCompressedEchoRoute(NetworkStream stream, string contentEncoding, byte[] compressedBody)
    {
        var compressedResponse =
                $"HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Encoding: {contentEncoding}\r\nContent-Length: {compressedBody.Length}\r\n\r\n";
        byte[] response = [.. Encoding.UTF8.GetBytes(compressedResponse), .. compressedBody];
        stream.Write(response);
        stream.Close();
        return Task.CompletedTask;
    }

    private static Task HandleFilesRoute(NetworkStream stream, string filePath)
    {
        if (File.Exists(filePath) == false)
        {
            HandleNotFoundRoute(stream);
        }
        string fileContent = File.ReadAllText(filePath);
        stream.Write(Encoding.ASCII.GetBytes(HttpResponse.Ok(fileContent, new(){
            {"Content-Type", "application/octet-stream"},
            {"Content-Length", fileContent.Length.ToString()}
            })));
        stream.Close();
        return Task.CompletedTask;
    }

    private static Task HandleFilesPostRoute(NetworkStream stream, string filePath, string content)
    {
        File.WriteAllText(filePath, content);
        stream.Write(Encoding.ASCII.GetBytes(HttpResponse.Created()));
        stream.Close();
        return Task.CompletedTask;
    }

    private static Task HandleUserAgentRoute(NetworkStream stream, string userAgent)
    {
        stream.Write(Encoding.ASCII.GetBytes(HttpResponse.Ok(userAgent, new(){
            {"Content-Type", "text/plain"},
            {"Content-Length", userAgent.Length.ToString()}
            })));
        stream.Close();
        return Task.CompletedTask;
    }

    private static Dictionary<int, string> ExtractPathParametersFromUrl(string urlPath, string pattern)
    {
        Dictionary<int, string> pathParameters = new();

        Match match = Regex.Match(urlPath, pattern);

        for (int i = 1; i <= match.Groups.Count; i++)
        {
            pathParameters[i] = match.Groups[i].Value;
        }

        return pathParameters;
    }
}

class HttpRequest
{
    private HttpRequest(string method, string path,
                        Dictionary<string, string> headers, string body)
    {
        Method = method;
        Path = path;
        Headers = headers;
        Body = body;
    }
    public string Method { get; }
    public string Path { get; }
    public Dictionary<string, string> Headers { get; }
    public string Body { get; }

    private static string ExtractMethod(string request)
    {
        Match requestMethodMatch = Regex.Match(request, Constants.HTTP_METHOD_PATTERN);

        if (requestMethodMatch.Success == false) return "GET";

        return requestMethodMatch.Groups[1].Value;
    }

    private static string ExtractUrlPath(string request)
    {
        string[] lines = request.Split('\n');
        if (lines.Length == 0) return Constants.ROOT_ROUTE_PATTERN;

        string[] parts = lines[0].Split(' ');
        if (parts.Length < 2) return Constants.ROOT_ROUTE_PATTERN;

        return parts[1];
    }

    private static Dictionary<string, string> ExtractHeaders(string request)
    {
        Dictionary<string, string> headers = [];

        MatchCollection matches = Regex.Matches(request, Constants.REQUEST_HEADER_PATTERN, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            string key = match.Groups[1].Value;
            string value = match.Groups[2].Value;
            headers[key] = value.Remove(value.Length - 1);
        }

        return headers;
    }

    public static HttpRequest Parse(string request)
    {
        string method = ExtractMethod(request);
        string path = ExtractUrlPath(request);
        Dictionary<string, string> headers = ExtractHeaders(request);
        var body = request.Split("\r\n\r\n")[1];

        return new HttpRequest(method, path, headers, body);
    }
}

class HttpResponse
{
    public HttpResponse(int? statusCode = null, string? body = null, Dictionary<string, string>? headers = null)
    {
        StatusCode = statusCode ?? 200;
        Body = body ?? "";
        Headers = headers ?? [];
    }

    public static string Ok(string? body = null, Dictionary<string, string>? headers = null)
    {
        HttpResponse response = new(body: body, headers: headers);
        return response.ToString();
    }

    public static string Created(string? body = null, Dictionary<string, string>? headers = null)
    {
        HttpResponse response = new(201, body: body, headers: headers);
        return response.ToString();
    }

    public static string NotFound(string? body = null, Dictionary<string, string>? headers = null)
    {
        HttpResponse response = new(404, body: body, headers: headers);
        return response.ToString();
    }

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

    public Dictionary<string, string> Headers { get; set; }

    public string Body
    {
        get;
        set;
    }

    public string StatusDescription => Constants.StatusDescriptions[StatusCode];

    private string HeadersToString()
    {
        if (Headers.Count == 0) return "";
        List<string> headers = [];
        foreach (KeyValuePair<string, string> header in Headers)
        {
            headers.Add($"{header.Key}: {header.Value}");
        }
        return $"{string.Join("\r\n", headers)}\r\n\r\n";
    }

    override public string ToString() => $"HTTP/{HttpVersion} {StatusCode} {StatusDescription}\r\n{(Headers.Count > 0 ? HeadersToString() : "\r\n")}{Body}";
}
