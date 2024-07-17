using Microsoft.Extensions.Options;
using TollProxer;

const string ver = "1.1";
Console.WriteLine($"Start configuring TollProxer {ver}");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<ProxyOptions>()
    .BindConfiguration("Proxy");

builder.Services.AddHttpClient();

var app = builder.Build();

app.Map("{**slug}", HandleProxy);

Console.WriteLine($"Run TollProxer {ver}");

app.Run(@"http://*:5000");

static async Task HandleProxy(HttpContext context, IOptionsSnapshot<ProxyOptions> options, ILoggerFactory loggerFactory, HttpClient httpClient)
{
    Console.WriteLine($"Handle request");
    
    var logger = loggerFactory.CreateLogger(nameof(HandleProxy));

    if (!context.Request.Headers.TryGetValue("Toll-Proxed-Destination-Host", out var proxyHostHeader))
    {
        logger.LogInformation("No destination domain proxy is passed");
        context.Response.StatusCode = 400;
        return;
    }

    var allowedHeaders = context.Request.Headers.TryGetValue("Toll-Allowed-Headers", out var allowed)
        ? new HashSet<string>(new[] { allowed.First() ?? string.Empty })
        : options.Value.PassHeaderSet;
    
    var proxyHost = proxyHostHeader.ToString();
    if (proxyHostHeader.Count != 1)
    {
        logger.LogInformation("Disallowed domain name");
        context.Response.StatusCode = 400;
        return;
    }

    var method = context.Request.Method;

    var uriBuilder = new UriBuilder();

    uriBuilder.Scheme = "https";
    uriBuilder.Host = proxyHost;
    uriBuilder.Path = context.Request.Path;
    uriBuilder.Query = context.Request.QueryString.ToString();

    using var request = new HttpRequestMessage(HttpMethod.Parse(method), uriBuilder.Uri);

    if (context.Request.Headers.ContentLength is { } contentLength)
    {
        request.Content = new StreamLengthContent(context.Request.Body, contentLength);
    }

    foreach (var header in context.Request.Headers)
    {
        if (allowedHeaders.Contains(header.Key))
        {
            request.Headers.Add(header.Key, header.Value.ToString());
        }
    }

    var fullLog = context.Request.Headers.ContainsKey("Toll-Full-Log");
    
    using var response = await httpClient.SendAsync(request);

    logger.LogInformation("Sent request to {Host}", proxyHost);

    var responseStr = await response.Content.ReadAsStringAsync();
    if (fullLog)
    {
        logger.LogInformation($"Response {response.StatusCode}");
        logger.LogInformation($"Response Body {responseStr}");
    }

    context.Response.StatusCode = (int)response.StatusCode;

    context.Response.ContentLength = response.Content.Headers.ContentLength;
    context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
    await context.Response.StartAsync();
    await context.Response.WriteAsync(responseStr);
    await context.Response.CompleteAsync();
}

public class StreamLengthContent : StreamContent
{
    private readonly long contentLength;

    public StreamLengthContent(Stream content, long contentLength) : base(content)
    {
        this.contentLength = contentLength;
    }

    protected override bool TryComputeLength(out long length)
    {
        length = contentLength;
        return true;
    }
}
