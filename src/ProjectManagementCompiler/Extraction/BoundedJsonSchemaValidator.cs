using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Extraction;

public sealed class BoundedJsonSchemaValidator
{
    private static readonly HashSet<string> SupportedKeywords =
    [
        "$schema", "$id", "$defs", "$ref", "title", "description", "type",
        "additionalProperties", "required", "properties", "items", "uniqueItems",
        "const", "enum", "oneOf", "allOf", "if", "then", "minimum", "multipleOf",
        "minLength", "minItems", "format", "pattern"
    ];

    public IReadOnlyList<ManifestDiagnostic> Validate(
        JsonElement instance,
        JsonElement schema,
        string sourcePath)
    {
        var diagnostics = new List<ManifestDiagnostic>();
        if (schema.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-CONTRACT-002",
                WarningSeverity.Error,
                "The selected schema must be a JSON object.",
                sourcePath,
                recommendedAction: "Use the supported Draft 2020-12 source schema."));
            return diagnostics;
        }

        var schemaDialect = ManifestCaptureSupport.GetString(schema, "$schema");
        if (!string.Equals(schemaDialect, "https://json-schema.org/draft/2020-12/schema", StringComparison.Ordinal))
        {
            diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                "PMC-CONTRACT-002",
                WarningSeverity.Error,
                $"Schema dialect '{schemaDialect ?? "<missing>"}' is not supported.",
                sourcePath,
                "$schema",
                recommendedAction: "Publish or select the bounded Draft 2020-12 schema supported by contract 0.1.0."));
            return diagnostics;
        }

        if (!CheckSupportedKeywords(schema, sourcePath, diagnostics, "$"))
        {
            return diagnostics;
        }

        ValidateNode(instance, schema, schema, sourcePath, "$", diagnostics, emitErrors: true);
        return diagnostics;
    }

    private static bool CheckSupportedKeywords(
        JsonElement schema,
        string sourcePath,
        ICollection<ManifestDiagnostic> diagnostics,
        string location)
    {
        foreach (var property in schema.EnumerateObject())
        {
            if (!SupportedKeywords.Contains(property.Name))
            {
                diagnostics.Add(ManifestCaptureSupport.Diagnostic(
                    "PMC-CONTRACT-002",
                    WarningSeverity.Error,
                    $"Schema keyword '{property.Name}' is outside the bounded supported subset.",
                    sourcePath,
                    property.Name,
                    recommendedAction: "Extend the explicit validator support table through a reviewed contract change."));
                return false;
            }
        }

        if (schema.TryGetProperty("$defs", out var definitions)
            && definitions.ValueKind == JsonValueKind.Object)
        {
            foreach (var definition in definitions.EnumerateObject())
            {
                if (!CheckSupportedKeywords(definition.Value, sourcePath, diagnostics, $"{location}/$defs/{definition.Name}"))
                {
                    return false;
                }
            }
        }

        foreach (var name in new[] { "properties", "items", "additionalProperties", "oneOf", "allOf", "if", "then" })
        {
            if (!schema.TryGetProperty(name, out var child))
            {
                continue;
            }

            if (name == "properties" && child.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in child.EnumerateObject())
                {
                    if (!CheckSupportedKeywords(property.Value, sourcePath, diagnostics, $"{location}/properties/{property.Name}"))
                    {
                        return false;
                    }
                }
            }
            else if (name is "oneOf" or "allOf" && child.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in child.EnumerateArray())
                {
                    if (!CheckSupportedKeywords(item, sourcePath, diagnostics, $"{location}/{name}"))
                    {
                        return false;
                    }
                }
            }
            else if (child.ValueKind == JsonValueKind.Object
                && !CheckSupportedKeywords(child, sourcePath, diagnostics, $"{location}/{name}"))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateNode(
        JsonElement instance,
        JsonElement schema,
        JsonElement rootSchema,
        string sourcePath,
        string instancePath,
        ICollection<ManifestDiagnostic> diagnostics,
        bool emitErrors)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var referenceValue = reference.GetString();
            if (referenceValue is null || !referenceValue.StartsWith("#/$defs/", StringComparison.Ordinal))
            {
                AddSchemaError(diagnostics, sourcePath, instancePath, "Only local $defs references are supported.", emitErrors);
                return false;
            }

            var key = referenceValue["#/$defs/".Length..];
            if (!rootSchema.TryGetProperty("$defs", out var definitions)
                || definitions.ValueKind != JsonValueKind.Object
                || !definitions.TryGetProperty(key, out var referencedSchema))
            {
                AddSchemaError(diagnostics, sourcePath, instancePath, $"Schema reference '{referenceValue}' cannot be resolved.", emitErrors);
                return false;
            }

            return ValidateNode(instance, referencedSchema, rootSchema, sourcePath, instancePath, diagnostics, emitErrors);
        }

        if (schema.TryGetProperty("type", out var type)
            && !MatchesType(instance, type))
        {
            AddSchemaError(diagnostics, sourcePath, instancePath, "The JSON value has an unsupported type.", emitErrors);
            return false;
        }

        var valid = true;
        if (schema.TryGetProperty("const", out var constant)
            && !JsonElement.DeepEquals(instance, constant))
        {
            AddSchemaError(diagnostics, sourcePath, instancePath, "The JSON value does not match the required constant.", emitErrors);
            valid = false;
        }

        if (schema.TryGetProperty("enum", out var enumeration)
            && (enumeration.ValueKind != JsonValueKind.Array
                || !enumeration.EnumerateArray().Any(candidate => JsonElement.DeepEquals(candidate, instance))))
        {
            AddSchemaError(diagnostics, sourcePath, instancePath, "The JSON value is outside the allowed enumeration.", emitErrors);
            valid = false;
        }

        if (schema.TryGetProperty("minLength", out var minLength)
            && instance.ValueKind == JsonValueKind.String
            && instance.GetString()!.Length < minLength.GetInt32())
        {
            AddSchemaError(diagnostics, sourcePath, instancePath, "The string is shorter than the required minimum length.", emitErrors);
            valid = false;
        }

        if (schema.TryGetProperty("minItems", out var minItems)
            && instance.ValueKind == JsonValueKind.Array
            && instance.GetArrayLength() < minItems.GetInt32())
        {
            AddSchemaError(diagnostics, sourcePath, instancePath, "The array has fewer items than required.", emitErrors);
            valid = false;
        }

        if (schema.TryGetProperty("minimum", out var minimum)
            && instance.ValueKind == JsonValueKind.Number
            && instance.GetDecimal() < minimum.GetDecimal())
        {
            AddSchemaError(diagnostics, sourcePath, instancePath, "The number is below the required minimum.", emitErrors);
            valid = false;
        }

        if (schema.TryGetProperty("multipleOf", out var multipleOf)
            && instance.ValueKind == JsonValueKind.Number
            && !IsMultipleOf(instance.GetDecimal(), multipleOf.GetDecimal()))
        {
            AddSchemaError(diagnostics, sourcePath, instancePath, "The number does not match the required multiple.", emitErrors);
            valid = false;
        }

        if (schema.TryGetProperty("pattern", out var pattern)
            && instance.ValueKind == JsonValueKind.String)
        {
            try
            {
                if (!Regex.IsMatch(instance.GetString()!, pattern.GetString() ?? string.Empty, RegexOptions.CultureInvariant))
                {
                    AddSchemaError(diagnostics, sourcePath, instancePath, "The string does not match the required pattern.", emitErrors);
                    valid = false;
                }
            }
            catch (ArgumentException)
            {
                AddSchemaError(diagnostics, sourcePath, instancePath, "The schema contains an invalid regular expression.", emitErrors);
                valid = false;
            }
        }

        if (schema.TryGetProperty("format", out var format)
            && !MatchesFormat(instance, format.GetString()))
        {
            AddSchemaError(diagnostics, sourcePath, instancePath, "The JSON string does not match the supported format.", emitErrors);
            valid = false;
        }

        if (instance.ValueKind == JsonValueKind.Object)
        {
            valid &= ValidateObject(instance, schema, rootSchema, sourcePath, instancePath, diagnostics, emitErrors);
        }

        if (instance.ValueKind == JsonValueKind.Array)
        {
            valid &= ValidateArray(instance, schema, rootSchema, sourcePath, instancePath, diagnostics, emitErrors);
        }

        if (schema.TryGetProperty("oneOf", out var oneOf)
            && oneOf.ValueKind == JsonValueKind.Array)
        {
            var matches = 0;
            foreach (var branch in oneOf.EnumerateArray())
            {
                var branchDiagnostics = new List<ManifestDiagnostic>();
                if (ValidateNode(instance, branch, rootSchema, sourcePath, instancePath, branchDiagnostics, emitErrors: false))
                {
                    matches++;
                }
            }

            if (matches != 1)
            {
                AddSchemaError(diagnostics, sourcePath, instancePath, "The JSON value must match exactly one schema branch.", emitErrors);
                valid = false;
            }
        }

        if (schema.TryGetProperty("allOf", out var allOf)
            && allOf.ValueKind == JsonValueKind.Array)
        {
            foreach (var branch in allOf.EnumerateArray())
            {
                valid &= ValidateNode(instance, branch, rootSchema, sourcePath, instancePath, diagnostics, emitErrors);
            }
        }

        if (schema.TryGetProperty("if", out var condition)
            && MatchesCondition(instance, condition, rootSchema))
        {
            if (schema.TryGetProperty("then", out var thenSchema))
            {
                valid &= ValidateNode(instance, thenSchema, rootSchema, sourcePath, instancePath, diagnostics, emitErrors);
            }
        }

        return valid;
    }

    private static bool ValidateObject(
        JsonElement instance,
        JsonElement schema,
        JsonElement rootSchema,
        string sourcePath,
        string instancePath,
        ICollection<ManifestDiagnostic> diagnostics,
        bool emitErrors)
    {
        var valid = true;
        var properties = schema.TryGetProperty("properties", out var propertySchema)
            && propertySchema.ValueKind == JsonValueKind.Object
            ? propertySchema
            : default;

        if (schema.TryGetProperty("required", out var required)
            && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var requiredProperty in required.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String))
            {
                var name = requiredProperty.GetString()!;
                if (!instance.TryGetProperty(name, out _))
                {
                    AddSchemaError(diagnostics, sourcePath, instancePath, $"Required property '{name}' is missing.", emitErrors);
                    valid = false;
                }
            }
        }

        foreach (var property in instance.EnumerateObject())
        {
            if (properties.ValueKind == JsonValueKind.Object
                && properties.TryGetProperty(property.Name, out var childSchema))
            {
                valid &= ValidateNode(property.Value, childSchema, rootSchema, sourcePath, $"{instancePath}/{property.Name}", diagnostics, emitErrors);
            }
            else if (schema.TryGetProperty("additionalProperties", out var additional)
                && additional.ValueKind == JsonValueKind.False)
            {
                AddSchemaError(diagnostics, sourcePath, instancePath, $"Property '{property.Name}' is not declared by the schema.", emitErrors);
                valid = false;
            }
        }

        return valid;
    }

    private static bool ValidateArray(
        JsonElement instance,
        JsonElement schema,
        JsonElement rootSchema,
        string sourcePath,
        string instancePath,
        ICollection<ManifestDiagnostic> diagnostics,
        bool emitErrors)
    {
        var valid = true;
        if (schema.TryGetProperty("uniqueItems", out var uniqueItems)
            && uniqueItems.ValueKind == JsonValueKind.True)
        {
            var values = instance.EnumerateArray().Select(item => item.GetRawText()).ToArray();
            if (values.Length != values.Distinct(StringComparer.Ordinal).Count())
            {
                AddSchemaError(diagnostics, sourcePath, instancePath, "The array contains duplicate items.", emitErrors);
                valid = false;
            }
        }

        if (schema.TryGetProperty("items", out var items))
        {
            foreach (var (item, index) in instance.EnumerateArray().Select((item, index) => (item, index)))
            {
                valid &= ValidateNode(item, items, rootSchema, sourcePath, $"{instancePath}/{index}", diagnostics, emitErrors);
            }
        }

        return valid;
    }

    private static bool MatchesCondition(JsonElement instance, JsonElement condition, JsonElement rootSchema)
    {
        if (condition.ValueKind != JsonValueKind.Object || instance.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (condition.TryGetProperty("properties", out var properties)
            && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                if (!instance.TryGetProperty(property.Name, out var value))
                {
                    return false;
                }

                var branchDiagnostics = new List<ManifestDiagnostic>();
                if (!ValidateNode(value, property.Value, rootSchema, string.Empty, string.Empty, branchDiagnostics, emitErrors: false))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool MatchesType(JsonElement instance, JsonElement type)
    {
        if (type.ValueKind == JsonValueKind.String)
        {
            return MatchesTypeName(instance, type.GetString());
        }

        return type.ValueKind == JsonValueKind.Array
            && type.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && MatchesTypeName(instance, item.GetString()));
    }

    private static bool MatchesTypeName(JsonElement instance, string? type) => type switch
    {
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        "string" => instance.ValueKind == JsonValueKind.String,
        "number" => instance.ValueKind == JsonValueKind.Number,
        "integer" => instance.ValueKind == JsonValueKind.Number && instance.TryGetInt64(out _),
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => instance.ValueKind == JsonValueKind.Null,
        _ => false
    };

    private static bool MatchesFormat(JsonElement instance, string? format)
    {
        if (instance.ValueKind != JsonValueKind.String)
        {
            return true;
        }

        var value = instance.GetString() ?? string.Empty;
        return format switch
        {
            null => true,
            "date" => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "date-time" => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
            "uri" => Uri.TryCreate(value, UriKind.Absolute, out _),
            _ => false
        };
    }

    private static bool IsMultipleOf(decimal value, decimal multiple)
    {
        if (multiple == 0)
        {
            return false;
        }

        var quotient = value / multiple;
        return quotient == decimal.Truncate(quotient);
    }

    private static void AddSchemaError(
        ICollection<ManifestDiagnostic> diagnostics,
        string sourcePath,
        string instancePath,
        string message,
        bool emitErrors)
    {
        if (!emitErrors)
        {
            return;
        }

        diagnostics.Add(ManifestCaptureSupport.Diagnostic(
            "PMC-SCHEMA-001",
            WarningSeverity.Error,
            $"{message} Location: {instancePath}.",
            sourcePath,
            recommendedAction: "Correct the source instance to match its declared schema."));
    }
}
