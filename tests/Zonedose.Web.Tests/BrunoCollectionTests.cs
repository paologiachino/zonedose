using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Zonedose.Web.Tests;

/// <summary>
/// The Bruno collection (bruno/) has one request per API endpoint, no more, no
/// less, as in Sportlab. Endpoints come from the controllers of the Zonedose
/// modules (route attributes) plus the minimal API ones listed below. No
/// database needed.
/// </summary>
public sealed partial class BrunoCollectionTests
{
    /// <summary>Endpoints mapped with minimal APIs rather than controllers.</summary>
    private static readonly string[] MinimalApiEndpoints = ["GET /api/health"];

    [Fact]
    public void Every_api_endpoint_has_a_bruno_request_and_nothing_else()
    {
        var endpoints = CodeEndpoints().Concat(MinimalApiEndpoints).ToHashSet();
        var requests = BrunoRequests().ToHashSet();

        var missing = endpoints.Except(requests).Order().ToArray();
        var stale = requests.Except(endpoints).Order().ToArray();

        Assert.True(missing.Length == 0, "Endpoints without a Bruno request: " + string.Join(", ", missing));
        Assert.True(stale.Length == 0, "Bruno requests without an endpoint: " + string.Join(", ", stale));
    }

    private static IEnumerable<string> CodeEndpoints()
    {
        var assemblies = new[] { typeof(Zonedose.Accounts.Startup).Assembly, typeof(Zonedose.Platform.Startup).Assembly };
        foreach (var controller in assemblies.SelectMany(a => a.GetTypes()).Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            var prefix = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? "";
            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (action.GetCustomAttribute<ApiExplorerSettingsAttribute>()?.IgnoreApi == true)
                {
                    continue;
                }
                foreach (var http in action.GetCustomAttributes<HttpMethodAttribute>())
                {
                    var path = "/" + string.Join('/', new[] { prefix, http.Template }.Where(p => !string.IsNullOrEmpty(p)));
                    foreach (var method in http.HttpMethods)
                    {
                        yield return $"{method} {Normalize(path)}";
                    }
                }
            }
        }
    }

    private static IEnumerable<string> BrunoRequests()
    {
        var folder = Path.Combine(RepositoryRoot(), "bruno");
        foreach (var file in Directory.EnumerateFiles(folder, "*.yml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var method = MethodLine().Match(text);
            var url = UrlLine().Match(text);
            if (!method.Success || !url.Success)
            {
                continue; // collection, folder and environment files
            }
            var path = url.Groups[1].Value.Replace("{{baseUrl}}", "", StringComparison.Ordinal).Split('?')[0];
            yield return $"{method.Groups[1].Value.ToUpperInvariant()} {Normalize(path)}";
        }
    }

    /// <summary>Route parameters, {userId} or :user_id, all become {}.</summary>
    private static string Normalize(string path) => Parameter().Replace(path.TrimEnd('/'), "{}");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Zonedose.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [GeneratedRegex(@"^\s+method:\s*(\w+)\s*$", RegexOptions.Multiline)]
    private static partial Regex MethodLine();

    [GeneratedRegex(@"^\s+url:\s*""?([^""\n]+)""?\s*$", RegexOptions.Multiline)]
    private static partial Regex UrlLine();

    [GeneratedRegex(@"\{[^}]+\}|:[A-Za-z_]+")]
    private static partial Regex Parameter();
}
