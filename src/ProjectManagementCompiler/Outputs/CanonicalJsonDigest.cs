using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
            Analysis = null,
            ExecutionProposals = project.ExecutionProposals
                .Where(proposal => !proposal.Id.StartsWith("proposal-migrated-", StringComparison.OrdinalIgnoreCase))
                .ToArray(),
            Warnings = project.Warnings
                .Where(warning => !string.Equals(warning.Code, "PMC-MIGRATION-001", StringComparison.OrdinalIgnoreCase))
                .ToArray()
        };
        var json = new CanonicalJsonSerializer().Serialize(semanticProject);
        var node = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidDataException("Canonical JSON could not be represented as an object.");
        if (node["importMetadata"] is JsonObject metadata)
        {
            metadata.Remove("importedAtUtc");
        }

        json = node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }
}
