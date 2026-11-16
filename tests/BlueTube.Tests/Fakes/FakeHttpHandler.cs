using System.Net;
using System.Text;

namespace BlueTube.Tests.Fakes;

/// <summary>Gives canned responses. Match: first route whose key is in the request URL.</summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly List<(string Match, HttpStatusCode Status, string Body)> _routes = [];
    public List<string> Requests { get; } = [];

    public FakeHttpHandler On(string urlContains, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes.Add((urlContains, status, json));
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var url = request.RequestUri!.ToString();
        Requests.Add(url);
        foreach (var (match, status, body) in _routes)
        {
            if (url.Contains(match, StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                });
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"error":{"message":"not found","errors":[{"reason":"notFound"}]}}"""),
        });
    }
}
