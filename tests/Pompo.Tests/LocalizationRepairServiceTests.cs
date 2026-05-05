using Pompo.Core.Localization;
using Pompo.Core.Project;

namespace Pompo.Tests;

public sealed class LocalizationRepairServiceTests
{
    [Fact]
    public void FillMissingValues_AddsSupportedLocalesAndPreservesUnsupportedLocales()
    {
        var tables = new List<StringTableDocument>
        {
            new(
                "dialogue",
                [
                    new StringTableEntry(
                        "line.hello",
                        new Dictionary<string, string>
                        {
                            ["ko"] = "안녕",
                            ["jp"] = "konnichiwa"
                        }),
                    new StringTableEntry(
                        "line.empty",
                        new Dictionary<string, string>())
                ])
        };

        var result = new LocalizationRepairService().FillMissingValues(tables, ["ko", "en"], "ko");

        Assert.Equal(3, result.FilledValueCount);
        Assert.Equal("안녕", tables[0].Entries[0].Values["en"]);
        Assert.Equal("konnichiwa", tables[0].Entries[0].Values["jp"]);
        Assert.Equal("line.empty", tables[0].Entries[1].Values["ko"]);
        Assert.Equal("line.empty", tables[0].Entries[1].Values["en"]);
    }

    [Fact]
    public void CreateReport_CountsEntriesMissingValuesAndUnsupportedValues()
    {
        var tables = new List<StringTableDocument>
        {
            new(
                "dialogue",
                [
                    new StringTableEntry(
                        "line.hello",
                        new Dictionary<string, string>
                        {
                            ["ko"] = "안녕",
                            ["jp"] = "konnichiwa"
                        }),
                    new StringTableEntry(
                        "line.ready",
                        new Dictionary<string, string>
                        {
                            ["ko"] = "준비",
                            ["en"] = "Ready"
                        })
                ])
        };

        var report = new LocalizationReportService().Create(tables, ["ko", "en"]);

        Assert.Equal(2, report.SupportedLocaleCount);
        Assert.Equal(1, report.StringTableCount);
        Assert.Equal(2, report.EntryCount);
        Assert.Equal(1, report.MissingValueCount);
        Assert.Equal(1, report.UnsupportedValueCount);
    }

    [Fact]
    public void AddSupportedLocale_AddsLocaleWithoutChangingExistingStringValues()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Locales",
            SupportedLocales = ["ko", "en"],
            StringTables =
            [
                new StringTableDocument(
                    "dialogue",
                    [
                        new StringTableEntry(
                            "line.hello",
                            new Dictionary<string, string>
                            {
                                ["ko"] = "안녕",
                                ["en"] = "Hello"
                            })
                    ])
            ]
        };

        new LocalizationProjectService().AddSupportedLocale(project, "ja");

        Assert.Equal(["ko", "en", "ja"], project.SupportedLocales);
        Assert.DoesNotContain("ja", project.StringTables.Single().Entries.Single().Values.Keys);
    }

    [Fact]
    public void DeleteSupportedLocale_RemovesLocaleAndMatchingStringValues()
    {
        var project = new PompoProjectDocument
        {
            ProjectName = "Locales",
            SupportedLocales = ["ko", "en", "ja"],
            StringTables =
            [
                new StringTableDocument(
                    "dialogue",
                    [
                        new StringTableEntry(
                            "line.hello",
                            new Dictionary<string, string>
                            {
                                ["ko"] = "안녕",
                                ["en"] = "Hello",
                                ["ja"] = "こんにちは"
                            })
                    ])
            ]
        };

        new LocalizationProjectService().DeleteSupportedLocale(project, "ja");

        Assert.Equal(["ko", "en"], project.SupportedLocales);
        Assert.DoesNotContain("ja", project.StringTables.Single().Entries.Single().Values.Keys);
    }
}
