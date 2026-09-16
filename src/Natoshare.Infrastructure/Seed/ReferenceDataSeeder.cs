using Microsoft.EntityFrameworkCore;
using Natoshare.Domain.Budgeting;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Seed;

// Makes sure the budget templates exist. Unlike DevDataSeeder, this runs in every
// environment (production too), because templates are real content the onboarding
// wizard needs, not a local dev convenience.
public static class ReferenceDataSeeder
{
    public static async Task SeedAsync(NatoshareDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (await dbContext.BudgetTemplates.AnyAsync(cancellationToken))
        {
            return;
        }

        var templates = new List<BudgetTemplate>
        {
            Template("Everyday", "Rent, feeding, transport, utilities, a subscription allowance, and investing.", 0,
                DefaultCategories.Items.Select(i => (i.Name, i.Kind, i.Percentage)).ToArray()),
            Template("50/30/20", "The classic split: needs, wants, and savings.", 1,
                [("Needs", CategoryKind.Standard, 50m), ("Wants", CategoryKind.Standard, 30m), ("Savings", CategoryKind.Standard, 20m)]),
            Template("60/20/20", "A bit more room for needs, still leaves space to save.", 2,
                [("Needs", CategoryKind.Standard, 60m), ("Wants", CategoryKind.Standard, 20m), ("Savings", CategoryKind.Standard, 20m)]),
            Template("Aggressive Saver", "For when you want to put away as much as you can.", 3,
                [
                    ("Rent", CategoryKind.FixedAccount, 20m),
                    ("Feeding", CategoryKind.Standard, 15m),
                    ("Transportation", CategoryKind.Standard, 10m),
                    ("Utility", CategoryKind.Standard, 5m),
                    ("Investment", CategoryKind.FixedAccount, 40m),
                    ("Flex", CategoryKind.Standard, 10m),
                ]),
            Template("Business Owner", "Room for the business itself, on top of personal spending.", 4,
                [
                    ("Rent", CategoryKind.FixedAccount, 15m),
                    ("Feeding", CategoryKind.Standard, 15m),
                    ("Transportation", CategoryKind.Standard, 10m),
                    ("Utility", CategoryKind.Standard, 5m),
                    ("Business Reinvestment", CategoryKind.FixedAccount, 30m),
                    ("Investment", CategoryKind.FixedAccount, 15m),
                    ("Flex", CategoryKind.Standard, 10m),
                ]),
        };

        dbContext.BudgetTemplates.AddRange(templates);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static BudgetTemplate Template(string name, string description, int sortOrder, (string Name, CategoryKind Kind, decimal Percentage)[] items)
    {
        var template = new BudgetTemplate
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Description = description,
            SortOrder = sortOrder,
        };

        template.Items = items.Select((item, index) => new BudgetTemplateItem
        {
            Id = Guid.CreateVersion7(),
            BudgetTemplateId = template.Id,
            Name = item.Name,
            Kind = item.Kind,
            Percentage = item.Percentage,
            SortOrder = index,
        }).ToList();

        return template;
    }
}
