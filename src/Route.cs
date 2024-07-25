using System.Text.RegularExpressions;

class Route
{
    public HttpMethod HttpMethod { get; } 
    public string PathTemplate { get; }
    public Func<HttpRequest, HttpResponse> Handler { get; }
    private Regex PathRegex { get; }
    private List<string> ParameterNames { get; }

    public Route(HttpMethod httpMethod, string pathTemplate, Func<HttpRequest, HttpResponse> handler)
    {
        HttpMethod = httpMethod;
        PathTemplate = pathTemplate;
        Handler = handler;
        (PathRegex, ParameterNames) = CreateRegexFromTemplate(pathTemplate);
    }

    public bool Matches(string path)
    {
        return PathRegex.IsMatch(path);
    }

    public Dictionary<string, string> ExtractPathParameters(string path)
    {
        var match = PathRegex.Match(path);
        var parameters = new Dictionary<string, string>();

        for (int i = 0; i < ParameterNames.Count; i++)
        {
            parameters[ParameterNames[i]] = match.Groups[i + 1].Value;
        }

        return parameters;
    }

    private static (Regex, List<string>) CreateRegexFromTemplate(string template)
    {
        var parameterNames = new List<string>();
        var regexPattern = "^";

        var parts = template.Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0) regexPattern += "/";

            if (parts[i].StartsWith("{") && parts[i].EndsWith("}"))
            {
                var paramName = parts[i].Trim('{', '}');
                parameterNames.Add(paramName);
                regexPattern += "([^/]+)";
            }
            else
            {
                regexPattern += Regex.Escape(parts[i]);
            }
        }

        regexPattern += "$";

        return (new Regex(regexPattern), parameterNames);
    }
}
