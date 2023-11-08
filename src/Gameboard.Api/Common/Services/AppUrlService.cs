using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Gameboard.Api.Common.Services;

public interface IAppUrlService
{
    string GetBaseUrl();
    string ToAppAbsoluteUrl(string relativeUrl);
}

internal class AppUrlService : IAppUrlService
{
    private readonly IWebHostEnvironment _env;
    private readonly HttpContext _httpContext;

    public AppUrlService(IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor)
    {
        _env = env;
        _httpContext = httpContextAccessor.HttpContext;
    }

    public string GetBaseUrl()
    {
        if (_httpContext is null)
            throw new ArgumentNullException(nameof(_httpContext));

        var request = _httpContext.Request;
        var finalPort = -1;

        // in dev, we append the port to make links still work (for when you're working against localhost)
        if (_env.IsDevelopment())
            finalPort = request.Host.Port != null ? request.Host.Port.Value : finalPort;

        var builder = new UriBuilder(request.Scheme, request.Host.Host, finalPort, request.PathBase);
        return builder.ToString();
    }

    public string ToAppAbsoluteUrl(string relativeUrl)
        => ToAbsoluteUrl(GetBaseUrl(), relativeUrl);

    private string ToAbsoluteUrl(string baseUrl, string relativeUrl)
    {
        // if you just convert both the base and the relative url to Uri objects, the `new Uri(baseUri, relativeUri)` ctor
        // does some pretty surprising things (e.g. drops the base path of the base Uri and replaces it with the relative path 
        // of the second). We have to build it manually, unfortunately.
        var finalBaseUrl = baseUrl.TrimEnd('/');

        if (relativeUrl.IsEmpty())
            return finalBaseUrl;

        return $"{finalBaseUrl}/{relativeUrl.TrimStart('/')}";
    }
}
