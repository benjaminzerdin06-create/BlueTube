using System.Net;
using System.Text;

namespace BlueTube.Tests.Fakes;

/// <summary>Gives canned responses. Match: first route whose key is in the request URL.</summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly List<(string Match, HttpStatusCode Status, string Body)> _routes = [];
    public List<string> Requests { get; } = [];
    public List<string> Bodies { get; } = [];
    public HttpRequestMessage? LastRequest { get; private set; }

    public FakeHttpHandler On(string urlContains, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes.Add((urlContains, status, json));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var url = request.RequestUri!.ToString();
        Requests.Add(url);
        LastRequest = request;
        if (request.Content is not null) Bodies.Add(await request.Content.ReadAsStringAsync(ct));

        foreach (var (match, status, body) in _routes)
        {
            if (url.Contains(match, StringComparison.Ordinal))
                return new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                };
        }
        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"error":{"message":"not found","errors":[{"reason":"notFound"}]}}"""),
        };
    }
}
