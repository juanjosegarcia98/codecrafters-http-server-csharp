using System.Net.Sockets;

class Router
{
    private readonly List<Route> routes = [];

    private void AddRoute(HttpMethod httpMethod = HttpMethod.GET, string pathTemplate = Constants.ROOT_PATH, Func<HttpRequest, HttpResponse>? handler = null)
    {
        if (routes.FirstOrDefault(r => r.HttpMethod == httpMethod && r.Matches(pathTemplate)) != null) return;
        routes.Add(new Route(httpMethod, pathTemplate, handler??= request => HttpResponse.Ok()));
    }

    public void GET(string pathTemplate = Constants.ROOT_PATH, Func<HttpRequest, HttpResponse>? handler = null) => AddRoute(HttpMethod.GET, pathTemplate, handler);
    public void PATCH(string pathTemplate = Constants.ROOT_PATH, Func<HttpRequest, HttpResponse>? handler = null) => AddRoute(HttpMethod.PATCH, pathTemplate, handler);
    public void POST(string pathTemplate = Constants.ROOT_PATH, Func<HttpRequest, HttpResponse>? handler = null) => AddRoute(HttpMethod.POST, pathTemplate, handler);
    public void PUT(string pathTemplate = Constants.ROOT_PATH, Func<HttpRequest, HttpResponse>? handler = null) => AddRoute(HttpMethod.PUT, pathTemplate, handler);
    public void DELETE(string pathTemplate = Constants.ROOT_PATH, Func<HttpRequest, HttpResponse>? handler = null) => AddRoute(HttpMethod.DELETE, pathTemplate, handler);
    public void CONNECT(string pathTemplate = Constants.ROOT_PATH, Func<HttpRequest, HttpResponse>? handler = null) => AddRoute(HttpMethod.CONNECT, pathTemplate, handler);
    public void HEAD(string pathTemplate = Constants.ROOT_PATH, Func<HttpRequest, HttpResponse>? handler = null) => AddRoute(HttpMethod.HEAD, pathTemplate, handler);
    public void OPTIONS(string pathTemplate = Constants.ROOT_PATH, Func<HttpRequest, HttpResponse>? handler = null) => AddRoute(HttpMethod.OPTIONS, pathTemplate, handler);
    public void TRACE(string pathTemplate = Constants.ROOT_PATH, Func<HttpRequest, HttpResponse>? handler = null) => AddRoute(HttpMethod.TRACE, pathTemplate, handler);
    

    public Task HandleClient(TcpClient client)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        {
            HttpRequest request = HttpRequest.Parse(stream);
            HttpResponse response;

            Route? matchedRoute = FindMatchingRoute(request.Method, request.Path);
            if (matchedRoute != null)
            {
                request.PathParameters = matchedRoute.ExtractPathParameters(request.Path);
                response = matchedRoute.Handler(request);
            }
            else
            {
                response = new HttpResponse { StatusCode = 404 };
            }

            SendResponse(stream, response);
            return Task.CompletedTask;
        }
    }

    private Route? FindMatchingRoute(HttpMethod httpMethod, string path)
    {
        return routes.FirstOrDefault(r => r.HttpMethod == httpMethod && r.Matches(path));
    }

    private static Task SendResponse(NetworkStream stream, HttpResponse response)
    {
        stream.Write(response.ToBytes());
        stream.Close();
        return Task.CompletedTask;
    }
}
