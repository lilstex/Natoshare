using FluentValidation;

namespace Natoshare.Application.Reports;

public class ExportReportRequestValidator : AbstractValidator<ExportReportRequest>
{
    public ExportReportRequestValidator()
    {
        RuleFor(x => x.Scope).Must(s => s is "month" or "range" or "annual").WithMessage("scope must be 'month', 'range' or 'annual'.");
        RuleFor(x => x.Format).Must(f => f is "csv" or "pdf").WithMessage("format must be 'csv' or 'pdf'.");

        RuleFor(x => x.Year).NotNull().WithMessage("year is required for scope 'month' or 'annual'.").When(x => x.Scope is "month" or "annual");
        RuleFor(x => x.Month).NotNull().WithMessage("month is required for scope 'month'.").When(x => x.Scope == "month");

        RuleFor(x => x.FromYear).NotNull().WithMessage("fromYear is required for scope 'range'.").When(x => x.Scope == "range");
        RuleFor(x => x.FromMonth).NotNull().WithMessage("fromMonth is required for scope 'range'.").When(x => x.Scope == "range");
        RuleFor(x => x.ToYear).NotNull().WithMessage("toYear is required for scope 'range'.").When(x => x.Scope == "range");
        RuleFor(x => x.ToMonth).NotNull().WithMessage("toMonth is required for scope 'range'.").When(x => x.Scope == "range");
    }
}
