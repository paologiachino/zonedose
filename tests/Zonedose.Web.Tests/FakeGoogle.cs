using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.Google;

namespace Zonedose.Web.Tests;

/// <summary>A Google account for the tests: synthetic, never a real person.</summary>
public sealed record GoogleUser(string Sub, string Email, bool EmailVerified = true, string? Name = "Test User")
{
    public static GoogleUser New(string? email = null) =>
        new("sub-" + Guid.NewGuid().ToString("N"), email ?? $"user-{Guid.NewGuid():N}@example.test");
}

/// <summary>
/// Stands in for Google's token and userinfo endpoints, as the Google handler's
/// backchannel: no test ever calls Google. A test registers an account and gets
/// an authorization code; the handler exchanges it, as with the real Google.
/// </summary>
public sealed class FakeGoogle : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, GoogleUser> _codes = new();

    public string Issue(GoogleUser user)
    {
        var code = "code-" + Guid.NewGuid().ToString("N");
        _codes[code] = user;
        return code;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.GetLeftPart(UriPartial.Path);
        if (url == GoogleDefaults.TokenEndpoint)
        {
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            var code = System.Web.HttpUtility.ParseQueryString(form)["code"];
            if (code is null || !_codes.ContainsKey(code))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new { error = "invalid_grant" }) };
            }
            // The access token is the code itself, so userinfo knows the account
            return Json(new { access_token = code, token_type = "Bearer", expires_in = 3600 });
        }
        if (url == GoogleDefaults.UserInformationEndpoint)
        {
            var token = request.Headers.Authorization?.Parameter;
            if (token is null || !_codes.TryGetValue(token, out var user))
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            return Json(new { sub = user.Sub, email = user.Email, email_verified = user.EmailVerified, name = user.Name });
        }
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
