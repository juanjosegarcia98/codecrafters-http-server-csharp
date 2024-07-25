using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

class HttpRequest(HttpMethod method, string path,
                    Dictionary<string, string> headers, string body)
{
    public HttpMethod Method { get; } = method;
    public string Path { get; } = path;

    public Dictionary<string, string> PathParameters
    {
        get;
        set;
    } = [];

    public Dictionary<string, string> Headers { get; } = headers;
    public string Body { get; } = body;

    private static HttpMethod ExtractMethod(string request)
    {
        Match requestMethodMatch = Regex.Match(request, Constants.HTTP_METHOD_PATTERN);

        if (requestMethodMatch.Success == false) return HttpMethod.GET;

        return requestMethodMatch.Groups[1].Value.ToUpper() switch
        {
            "GET" => HttpMethod.GET,
            "POST" => HttpMethod.POST,
            "PUT" => HttpMethod.PUT,
            "DELETE" => HttpMethod.DELETE,
            "HEAD" => HttpMethod.HEAD,
            "OPTIONS" => HttpMethod.OPTIONS,
            "PATCH" => HttpMethod.PATCH,
            "TRACE" => HttpMethod.TRACE,
            _ => throw new NotSupportedException($"Unsupported HTTP method: {requestMethodMatch.Groups[1].Value}"),
        };
    }

    private static string ExtractUrlPath(string request)
    {
        string[] lines = request.Split('\n');
        if (lines.Length == 0) return Constants.ROOT_PATH;

        string[] parts = lines[0].Split(' ');
        if (parts.Length < 2) return Constants.ROOT_PATH;

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

    public static HttpRequest Parse(NetworkStream stream)
    {
        int bytesReceived = stream.Read(Constants.byteBuffer);
        string request = Encoding.ASCII.GetString(Constants.byteBuffer, 0, bytesReceived);

        HttpMethod method = ExtractMethod(request);
        string path = ExtractUrlPath(request);
        Dictionary<string, string> headers = ExtractHeaders(request);
        var body = request.Split("\r\n\r\n")[1];

        return new HttpRequest(method, path, headers, body);
    }
}