using Microsoft.Extensions.Options;
using TollProxer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<ProxyOptions>()
    .BindConfiguration("Proxy");

builder.Services.AddHttpClient();

var app = builder.Build();

app.Map("{**slug}", HandleProxy);

app.Run();

static async Task HandleProxy(HttpContext context, IOptionsSnapshot<ProxyOptions> options, ILoggerFactory loggerFactory, HttpClient httpClient)
{
    var logger = loggerFactory.CreateLogger(nameof(HandleProxy));

    if (!context.Request.Headers.TryGetValue("Toll-Proxed-Destination-Host", out var proxyHostHeader))
    {
        logger.LogInformation("No destination domain proxy is passed");
        context.Response.StatusCode = 400;
        return;
    }

    var proxyHost = proxyHostHeader.ToString();
    if (proxyHostHeader.Count != 1 || !options.Value.AllowedDestinationHostSet.Contains(proxyHost))
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
        if (options.Value.PassHeaders.Contains(header.Key))
        {
            request.Headers.Add(header.Key, header.Value.ToString());
        }
    }

    using var response = await httpClient.SendAsync(request);

    logger.LogInformation("Sent request to {Host}", proxyHost);

    context.Response.StatusCode = (int)response.StatusCode;

    context.Response.ContentLength = response.Content.Headers.ContentLength;
    context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
    await context.Response.StartAsync();
    await response.Content.CopyToAsync(context.Response.Body);
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
