using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

const string localhostAddress = "127.0.0.1";
const int port = 4221;

const string ROOT_PATH = @"^/$";
const string ECHO_PATH = @"/echo/([^/]+)";

string[] routes = [ROOT_PATH, ECHO_PATH];

const string OK = "HTTP/1.1 200 OK\r\n\r\n";
string OK_BODY_TEXT(string body) => $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\n\r\n{body}";
const string NOT_FOUND = "HTTP/1.1 404 Not Found\r\n\r\n";

IPAddress localIpAddress = IPAddress.Parse(localhostAddress);
byte[] buffer = new byte[1024];

TcpListener server = new(localIpAddress, port);
server.Start();
Console.WriteLine($"Server listening on http://{localIpAddress}:{port}");

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
    
    string requestPath = extractUrlPathFromRequest(request);

    string? routeMatched = routes.Where(route => Regex.Match(requestPath, route).Success).FirstOrDefault(defaultValue: null);

    switch (routeMatched)
    {
        case ROOT_PATH:
            handleRootRoute(socket);
            break;
        case ECHO_PATH:
            handleEchoRoute(socket, requestPath);
            break;
        default:
            handleNotFoundRoute(socket);
            break;
    }
}

static string extractUrlPathFromRequest(string request)
{
    string[] lines = request.Split('\n');
    if (lines.Length == 0) return ROOT_PATH;

    string[] parts = lines[0].Split(' ');
    if (parts.Length < 2) return ROOT_PATH;
    
    return parts[1];
}

void handleNotFoundRoute(Socket socket) {
    socket.Send(Encoding.ASCII.GetBytes(NOT_FOUND));
}

void handleRootRoute(Socket socket) {
    socket.Send(Encoding.ASCII.GetBytes(OK));
}

void handleEchoRoute(Socket socket, string requestPath) {
    string message = Regex.Match(requestPath, ECHO_PATH).Groups[1].Value;
    socket.Send(Encoding.ASCII.GetBytes(OK_BODY_TEXT(message)));
}
