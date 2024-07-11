using System.Net;
using System.Net.Sockets;
using System.Text;

const string localhostAddress = "127.0.0.1";
const int port = 4221;

const string ROOT_PATH = "/";

const string OK = "HTTP/1.1 200 OK\r\n\r\n";
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
    string path = extractUrlPathFromRequest(request);
    byte[] responseBytes = path switch
    {
        ROOT_PATH => Encoding.ASCII.GetBytes(OK),
        _ => Encoding.ASCII.GetBytes(NOT_FOUND),
    };

    socket.Send(responseBytes);
}

static string extractUrlPathFromRequest(string request)
{
    string[] lines = request.Split('\n');
    if (lines.Length == 0) return ROOT_PATH;

    string[] parts = lines[0].Split(' ');
    if (parts.Length < 2) return ROOT_PATH;
    
    return parts[1];
}