using System.Net.Sockets;

class Server
{
    public static Task Main(string[] args)
    {
        Router router = new();

        router.GET();

        router.GET("/echo/{msg}", request =>
        {
            string message = request.PathParameters["msg"];
            string? acceptEncoding = request.Headers.GetValueOrDefault("Accept-Encoding", defaultValue: "");
            if (acceptEncoding == "") return new HttpResponse(200, message, new() { { "Content-Type", "text/plain" } });
            if (acceptEncoding.Contains(',') == false && Constants.availableCompressionSchemes.Contains(acceptEncoding) == false) return new HttpResponse(200, message, new() { { "Content-Type", "text/plain" } });
            List<string> validEncodings = [];
            foreach (var item in acceptEncoding.Split(','))
            {
                string itemName = item.Trim();
                if (Constants.availableCompressionSchemes.Contains(itemName)) validEncodings.Add(itemName);
            }

            if (acceptEncoding == "gzip" || validEncodings.Contains("gzip")) return new HttpResponse(200, message, new() { { "Content-Type", "text/plain" }, { "Content-Encoding", string.Join(", ", validEncodings) } }, true);
            return new HttpResponse(200, message, new() { { "Content-Type", "text/plain" }, { "Content-Encoding", string.Join(", ", validEncodings) } });
        });

        router.GET("/files/{fileName}", request =>
        {
            string filePath = GetFilePath(request.PathParameters["fileName"]);
            if (File.Exists(filePath) == false) return HttpResponse.NotFound();
            return HttpResponse.Ok(File.ReadAllText(filePath), new(){
            {"Content-Type", "application/octet-stream"},
            });
        });

        router.POST("/files/{fileName}", request =>
        {
            string fileContent = request.Body;
            File.WriteAllText(GetFilePath(request.PathParameters["fileName"]), fileContent);
            return HttpResponse.Created(fileContent);
        });
        
        router.GET("/user-agent", request => HttpResponse.Ok(request.Headers["User-Agent"], new() { { "Content-Type", "text/plain" } }));

        TcpListener server = new(Constants.localIpAddress, Constants.PORT);
        server.Start();
        Console.WriteLine($"Server listening on http://{server.Server.LocalEndPoint}");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            Task.Run(() => router.HandleClient(client));
        }
    }

    private static string GetFilePath(string fileName)
    {
        var argv = Environment.GetCommandLineArgs();
        var filesDirectory = argv[2];
        string filePath = $"{filesDirectory}/{fileName}";
        return filePath;
    }
}
