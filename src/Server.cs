using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

class Server
{
    private static readonly string LOCALHOST_ADDRESS = "127.0.0.1";
    private static readonly int PORT = 4221;

    const string ROOT_ROUTE = @"^/$";
    const string ECHO_ROUTE = @"/echo/([^/]+)";
    const string USER_AGENT_ROUTE = @"^/user-agent$";

    private static readonly string OK = "HTTP/1.1 200 OK\r\n\r\n";
    private static readonly string NOT_FOUND = "HTTP/1.1 404 Not Found\r\n\r\n";

    private static readonly string REQUEST_HEADER_PATTERN = @"^([\w-]+):\s*(.+)$";


    private static readonly string[] serverRoutes = [ROOT_ROUTE, ECHO_ROUTE, USER_AGENT_ROUTE];

    private static readonly IPAddress localIpAddress = IPAddress.Parse(LOCALHOST_ADDRESS);
    private static readonly byte[] buffer = new byte[1024];
    
    private static string OkBodyResponse(string body) => $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\n\r\n{body}";

    public static Task Main()
    {
        TcpListener server = new(localIpAddress, PORT);
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
        int bytesReceived = stream.Read(buffer);
        string request = Encoding.ASCII.GetString(buffer, 0, bytesReceived);

        Dictionary<string, string> requestHeaders = ExtractHeadersFromRequest(request);

        string requestPath = ExtractUrlPathFromRequest(request);

        string? routeMatched = serverRoutes.Where(route => Regex.Match(requestPath, route).Success).FirstOrDefault(defaultValue: null);

        switch (routeMatched)
        {
            case ROOT_ROUTE:
                return HandleRootRoute(stream);
            case ECHO_ROUTE:
                var pathParameters = ExtractPathParametersFromUrl(requestPath, ECHO_ROUTE);
                return HandleEchoRoute(stream, pathParameters[1]);
            case USER_AGENT_ROUTE:
                return HandleUserAgentRoute(stream, requestHeaders["User-Agent"]);
            default:
                return HandleNotFoundRoute(stream);
        }
    }

    private static Task HandleNotFoundRoute(NetworkStream stream)
    {
        stream.Write(Encoding.ASCII.GetBytes(NOT_FOUND));
        stream.Close();
        return Task.CompletedTask;
    }

    private static Task HandleRootRoute(NetworkStream stream)
    {
        stream.Write(Encoding.ASCII.GetBytes(OK));
        stream.Close();
        return Task.CompletedTask;
    }

    private static Task HandleEchoRoute(NetworkStream stream, string body)
    {
        stream.Write(Encoding.ASCII.GetBytes(OkBodyResponse(body)));
        stream.Close();
        return Task.CompletedTask;
    }

    private static Task HandleUserAgentRoute(NetworkStream stream, string userAgent)
    {
        stream.Write(Encoding.ASCII.GetBytes(OkBodyResponse(userAgent)));
        stream.Close();
        return Task.CompletedTask;
    }

    private static string ExtractUrlPathFromRequest(string request)
    {
        string[] lines = request.Split('\n');
        if (lines.Length == 0) return ROOT_ROUTE;

        string[] parts = lines[0].Split(' ');
        if (parts.Length < 2) return ROOT_ROUTE;

        return parts[1];
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

    private static Dictionary<string, string> ExtractHeadersFromRequest(string request)
    {
        Dictionary<string, string> headers = new();

        MatchCollection matches = Regex.Matches(request, REQUEST_HEADER_PATTERN, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            string key = match.Groups[1].Value;
            string value = match.Groups[2].Value;
            headers[key] = value.Remove(value.Length - 1);
        }

        return headers;
    }
}
