using FluentAssertions;
using Natoshare.Application.Reports;

namespace Natoshare.Application.Tests.Reports;

public class ExportReportRequestValidatorTests
{
    private readonly ExportReportRequestValidator _validator = new();

    [Fact]
    public void A_month_export_needs_a_year_and_a_month()
    {
        _validator.Validate(new ExportReportRequest("month", "csv", null, null, null, null, null, null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_complete_month_export_is_valid()
    {
        _validator.Validate(new ExportReportRequest("month", "csv", 2026, 9, null, null, null, null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_annual_export_only_needs_a_year()
    {
        _validator.Validate(new ExportReportRequest("annual", "pdf", 2026, null, null, null, null, null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_range_export_needs_all_four_range_fields()
    {
        _validator.Validate(new ExportReportRequest("range", "csv", null, null, 2026, 1, null, null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_complete_range_export_is_valid()
    {
        _validator.Validate(new ExportReportRequest("range", "csv", null, null, 2026, 1, 2026, 6)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_unknown_scope_is_rejected()
    {
        _validator.Validate(new ExportReportRequest("week", "csv", 2026, 9, null, null, null, null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_unknown_format_is_rejected()
    {
        _validator.Validate(new ExportReportRequest("month", "xlsx", 2026, 9, null, null, null, null)).IsValid.Should().BeFalse();
    }
}
