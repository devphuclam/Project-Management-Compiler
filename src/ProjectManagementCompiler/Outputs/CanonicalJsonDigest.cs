using System.Security.Cryptography;
using System.Text;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Outputs;

public static class CanonicalJsonDigest
{
    public static string Compute(CanonicalProject project)
    {
        var semanticProject = project with
        {
            Sources = project.Sources
                .Select(source => source with { CapturedAtUtc = null })
                .ToArray(),
            ManagementEvidence = project.ManagementEvidence with
            {
                CapturedAtUtc = null
            },
            Analysis = null
        };
        var json = new CanonicalJsonSerializer().Serialize(semanticProject);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }
}
