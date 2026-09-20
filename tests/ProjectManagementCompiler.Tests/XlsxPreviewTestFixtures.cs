using System.Collections;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;

namespace ProjectManagementCompiler.Tests;

internal static class XlsxPreviewTestFixtures
{
    public static byte[] ValidWorkbookBytes()
    {
        var cario = new CarioWorkbookModel
        {
            ProjectName = "Preview Project",
            Tasks =
            [
                new CarioTaskRow
                {
                    WorkItemType = "DeliveryCard",
                    TaskId = "P01-A",
                    PhaseId = "PH0",
                    WorkPackageId = "P01",
                    Title = "Prepare preview",
                    PlannedStart = new DateOnly(2026, 9, 18),
                    PlannedDeadline = new DateOnly(2026, 9, 21),
                    InitialState = "IN_PROGRESS",
                    PlannedEffortHours = 8,
                    SourceReference = "DOC-07:planning.md#P01-A"
                },
                new CarioTaskRow
                {
                    WorkItemType = "Milestone",
                    TaskId = "G-D0",
                    PhaseId = "PH0",
                    WorkPackageId = "P01",
                    Title = "Preview gate",
                    PlannedStart = new DateOnly(2026, 9, 21),
                    PlannedDeadline = new DateOnly(2026, 9, 21),
                    InitialState = "NOT_STARTED",
                    SourceReference = "DOC-07:planning.md#G-D0"
                }
            ],
            ProjectInfo =
            [
                new CarioProjectInfoRow { Key = "Project ID", Value = "PMC-PROJECT-001", DataState = DataState.Known },
                new CarioProjectInfoRow { Key = "Project name", Value = "Preview Project", DataState = DataState.Known },
                new CarioProjectInfoRow { Key = "Source ref", Value = "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4", DataState = DataState.Known }
            ]
        };

        var gantt = new GanttXlsxModel
        {
            ProjectName = "Preview Project",
            SnapshotScope = "Official source commit",
            SourceIdentity = "0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4",
            SnapshotId = "snapshot-preview-001",
            AsOfDate = new DateOnly(2026, 9, 19),
            Wbs = new WbsProjection
            {
                Root = new WbsNode
                {
                    Id = "PMC-PROJECT-001",
                    Name = "Preview Project",
                    Kind = WbsNodeKind.Project,
                    PlannedStart = new DateOnly(2026, 9, 18),
                    PlannedFinish = new DateOnly(2026, 9, 21),
                    Children =
                    [
                        new WbsNode
                        {
                            Id = "PH0",
                            Name = "Phase 0",
                            Kind = WbsNodeKind.Phase,
                            PlannedStart = new DateOnly(2026, 9, 18),
                            PlannedFinish = new DateOnly(2026, 9, 21),
                            Children =
                            [
                                new WbsNode
                                {
                                    Id = "P01",
                                    Name = "Package 01",
                                    Kind = WbsNodeKind.WorkPackage,
                                    PlannedStart = new DateOnly(2026, 9, 18),
                                    PlannedFinish = new DateOnly(2026, 9, 21),
                                    Children =
                                    [
                                        new WbsNode
                                        {
                                            Id = "P01-A",
                                            Name = "Prepare preview",
                                            Kind = WbsNodeKind.DeliveryCard,
                                            PlannedStart = new DateOnly(2026, 9, 18),
                                            PlannedFinish = new DateOnly(2026, 9, 21)
                                        },
                                        new WbsNode
                                        {
                                            Id = "G-D0",
                                            Name = "Preview gate",
                                            Kind = WbsNodeKind.Milestone,
                                            PlannedStart = new DateOnly(2026, 9, 21),
                                            PlannedFinish = new DateOnly(2026, 9, 21)
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            },
            Gantt = new GanttProjection
            {
                Items =
                [
                    new GanttItem
                    {
                        WorkItemId = "P01-A",
                        Name = "Prepare preview",
                        PhaseId = "PH0",
                        WorkPackageId = "P01",
                        ExecutionState = ExecutionState.InProgress,
                        HasExecutionEvidence = true,
                        LogicalRoles = ["LEAD"],
                        Lanes =
                        [
                            new GanttLaneEntry
                            {
                                WorkItemId = "P01-A",
                                Lane = GanttLane.Plan,
                                Start = new DateOnly(2026, 9, 18),
                                Finish = new DateOnly(2026, 9, 21),
                                Label = "PLAN"
                            },
                            new GanttLaneEntry
                            {
                                WorkItemId = "P01-A",
                                Lane = GanttLane.Actual,
                                Start = new DateOnly(2026, 9, 18),
                                IsOpenEnded = true,
                                ActualEffortHours = 4,
                                RemainingEffortHours = 4,
                                Label = "ACTUAL"
                            },
                            new GanttLaneEntry
                            {
                                WorkItemId = "P01-A",
                                Lane = GanttLane.Alert,
                                AlertCode = "AT_RISK",
                                Label = "At risk"
                            }
                        ]
                    }
                ],
                Milestones =
                [
                    new GanttMilestoneEntry
                    {
                        MilestoneId = "G-D0",
                        Name = "Preview gate",
                        Kind = MilestoneKind.Milestone,
                        ParentId = "P01",
                        PlannedDate = new DateOnly(2026, 9, 21)
                    }
                ]
            }
        };

        return new CarioXlsxExporter().ExportWithGantt(cario, gantt);
    }

    public static byte[] ReplaceEntry(byte[] source, string entryName, string replacement) =>
        RewriteArchive(source, (entry, content) =>
            string.Equals(entry, entryName, StringComparison.Ordinal)
                ? Encoding.UTF8.GetBytes(replacement)
                : content);

    public static byte[] RemoveEntry(byte[] source, string entryName)
    {
        using var input = new ZipArchive(new MemoryStream(source), ZipArchiveMode.Read, leaveOpen: false);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries.Where(entry => !string.Equals(entry.FullName, entryName, StringComparison.Ordinal)))
            {
                CopyEntry(entry, archive);
            }
        }

        return output.ToArray();
    }

    public static byte[] AddEntry(byte[] source, string entryName, string content)
    {
        using var input = new ZipArchive(new MemoryStream(source), ZipArchiveMode.Read, leaveOpen: false);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                CopyEntry(entry, archive);
            }

            var duplicate = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
            using var writer = new StreamWriter(duplicate.Open(), Encoding.UTF8);
            writer.Write(content);
        }

        return output.ToArray();
    }

    public static byte[] CorruptPackage() => [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0xFF, 0xFF];

    public static byte[] OversizedEntry(byte[] source) =>
        RewriteArchive(source, (entry, content) =>
            string.Equals(entry, "xl/worksheets/sheet7.xml", StringComparison.Ordinal)
                ? Encoding.UTF8.GetBytes(new string('x', 5 * 1024 * 1024))
                : content);

    public static PreviewImportView Import(byte[] source, string fileName = "preview.xlsx")
    {
        var importerType = Type.GetType("ProjectManagementCompiler.Outputs.XlsxPreviewImporter, ProjectManagementCompiler", throwOnError: false);
        TestAssert.True(importerType is not null, "The XLSX preview importer type must exist before the importer regression can pass.");
        var importer = Activator.CreateInstance(importerType!);
        TestAssert.True(importer is not null, "The XLSX preview importer must be constructible without external dependencies.");
        var method = importerType!.GetMethod("Import", [typeof(string), typeof(byte[])]);
        TestAssert.True(method is not null, "The XLSX preview importer must expose Import(fileName, bytes).");
        try
        {
            return new PreviewImportView(method!.Invoke(importer, [fileName, source]));
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static byte[] RewriteArchive(byte[] source, Func<string, byte[], byte[]> transform)
    {
        using var input = new ZipArchive(new MemoryStream(source), ZipArchiveMode.Read, leaveOpen: false);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                using var entryStream = entry.Open();
                using var content = new MemoryStream();
                entryStream.CopyTo(content);
                var rewritten = transform(entry.FullName, content.ToArray());
                var destination = archive.CreateEntry(entry.FullName, CompressionLevel.NoCompression);
                using var destinationStream = destination.Open();
                destinationStream.Write(rewritten);
            }
        }

        return output.ToArray();
    }

    private static void CopyEntry(ZipArchiveEntry source, ZipArchive target)
    {
        var destination = target.CreateEntry(source.FullName, CompressionLevel.NoCompression);
        using var input = source.Open();
        using var output = destination.Open();
        input.CopyTo(output);
    }

    internal sealed class PreviewImportView
    {
        private readonly object? result;

        public PreviewImportView(object? result) => this.result = result;

        public bool IsValid => Read<bool>("IsValid");

        public string DiagnosticsText => string.Join(
            " | ",
            ReadEnumerable("Diagnostics").Select(diagnostic =>
                ReadString(diagnostic, "Code") + ":" + ReadString(diagnostic, "Message")));

        public object Preview => ReadObject("Preview")
            ?? throw new InvalidOperationException("A valid preview result must include a preview. Diagnostics: " + DiagnosticsText);

        public T Read<T>(string propertyName)
        {
            var value = result?.GetType().GetProperty(propertyName)?.GetValue(result);
            return value is null ? default! : (T)value;
        }

        public IEnumerable<object> ReadEnumerable(string propertyName) => ReadEnumerable(result, propertyName);

        public static IEnumerable<object> ReadEnumerable(object? target, string propertyName) =>
            (target?.GetType().GetProperty(propertyName)?.GetValue(target) as IEnumerable)?.Cast<object>()
            ?? Array.Empty<object>();

        public object? ReadObject(string propertyName) => ReadObject(result, propertyName);

        public static object? ReadObject(object? target, string propertyName) =>
            target?.GetType().GetProperty(propertyName)?.GetValue(target);

        public static string ReadString(object? target, string propertyName) =>
            ReadObject(target, propertyName)?.ToString() ?? string.Empty;
    }
}
