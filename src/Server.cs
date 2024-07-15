using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

const string LOCALHOST_ADDRESS = "127.0.0.1";
const int PORT = 4221;

const string ROOT_ROUTE = @"^/$";
const string ECHO_ROUTE = @"/echo/([^/]+)";
const string USER_AGENT_ROUTE = @"^/user-agent$";

const string OK = "HTTP/1.1 200 OK\r\n\r\n";
const string NOT_FOUND = "HTTP/1.1 404 Not Found\r\n\r\n";

const string REQUEST_HEADER_PATTERN = @"^([\w-]+):\s*(.+)$";

string okBodyResponse(string body) => $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\n\r\n{body}";

string[] serverRoutes = [ROOT_ROUTE, ECHO_ROUTE, USER_AGENT_ROUTE];

IPAddress localIpAddress = IPAddress.Parse(LOCALHOST_ADDRESS);
byte[] buffer = new byte[1024];

TcpListener server = new(localIpAddress, PORT);
server.Start();
Console.WriteLine($"Server listening on http://{localIpAddress}:{PORT}");

while (true)
{
    Socket socket = server.AcceptSocket();

    handleRequest(socket);

    socket.Close();
}

void handleRequest(Socket socket)
{
    int bytesReceived = socket.Receive(buffer);
    string request = Encoding.ASCII.GetString(buffer, 0, bytesReceived);

    Dictionary<string, string> requestHeaders = extractHeadersFromRequest(request);
    
    string requestPath = extractUrlPathFromRequest(request);

    string? routeMatched = serverRoutes.Where(route => Regex.Match(requestPath, route).Success).FirstOrDefault(defaultValue: null);

    switch (routeMatched)
    {
        case ROOT_ROUTE:
            handleRootRoute(socket);
            break;
        case ECHO_ROUTE:
            var pathParameters = extractPathParametersFromUrl(requestPath, ECHO_ROUTE);
            handleEchoRoute(socket, pathParameters[1]);
            break;
        case USER_AGENT_ROUTE:
            handleUserAgentRoute(socket, requestHeaders["User-Agent"]);
            break;
        default:
            handleNotFoundRoute(socket);
            break;
    }
}

void handleNotFoundRoute(Socket socket)
{
    socket.Send(Encoding.ASCII.GetBytes(NOT_FOUND));
}

void handleRootRoute(Socket socket)
{
    socket.Send(Encoding.ASCII.GetBytes(OK));
}

void handleEchoRoute(Socket socket, string body)
{
    socket.Send(Encoding.ASCII.GetBytes(okBodyResponse(body)));
}

void handleUserAgentRoute(Socket socket, string userAgent)
{
    socket.Send(Encoding.ASCII.GetBytes(okBodyResponse(userAgent)));
}

static string extractUrlPathFromRequest(string request)
{
    string[] lines = request.Split('\n');
    if (lines.Length == 0) return ROOT_ROUTE;

    string[] parts = lines[0].Split(' ');
    if (parts.Length < 2) return ROOT_ROUTE;

    return parts[1];
}

static Dictionary<int, string> extractPathParametersFromUrl(string urlPath, string pattern)
{
    Dictionary<int, string> pathParameters = new Dictionary<int, string>();

    Match match = Regex.Match(urlPath, pattern);

    for (int i = 1; i <= match.Groups.Count; i++)
    {
        pathParameters[i] = match.Groups[i].Value;
    }

    return pathParameters;
}

static Dictionary<string, string> extractHeadersFromRequest(string request)
{
    Dictionary<string, string> headers = new Dictionary<string, string>();

    MatchCollection matches = Regex.Matches(request, REQUEST_HEADER_PATTERN, RegexOptions.Multiline);

    foreach (Match match in matches)
    {
        string key = match.Groups[1].Value;
        string value = match.Groups[2].Value;
        headers[key] = value.Remove(value.Length - 1);
    }

    return headers;
}