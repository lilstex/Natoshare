using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Natoshare.Application.Auth;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Ledger;
using Natoshare.Domain.Identity;

namespace Natoshare.Infrastructure.Seed;

// Makes sure the "User" and "Admin" roles exist, that there is one admin account
// ready to log in with, and one regular demo account with real onboarded data
// (categories, income, a few expenses), so a fresh `docker compose up` gives you
// something to actually look at instead of just an empty admin login. This is only
// meant for local development, the Api project only calls this when the
// environment is Development.
public static class DevDataSeeder
{
    private const string DemoEmail = "demo@natoshare.dev";
    private const string DemoPassword = "NatoshareDemo1";

    public static async Task SeedAsync(IServiceProvider services, string adminEmail, string adminPassword)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<User>>();

        foreach (var roleName in new[] { "User", "Admin" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName) { Id = Guid.CreateVersion7() });
            }
        }

        await SeedAdminAsync(userManager, adminEmail, adminPassword);
        await SeedDemoUserAsync(services, userManager);
    }

    private static async Task SeedAdminAsync(UserManager<User> userManager, string adminEmail, string adminPassword)
    {
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin is not null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var admin = new User
        {
            Id = Guid.CreateVersion7(),
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            DisplayName = "Natoshare Admin",
            TrialEndsAt = now.AddYears(10),
            CreatedAt = now,
        };

        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }

    // Goes through the real signup, onboarding and logging endpoints, the same way
    // an actual user would, instead of writing rows straight into the database.
    // That is the only way to end up with a ledger that is actually consistent
    // with itself, the same reason the rest of the app never takes a shortcut here.
    private static async Task SeedDemoUserAsync(IServiceProvider services, UserManager<User> userManager)
    {
        if (await userManager.FindByEmailAsync(DemoEmail) is not null)
        {
            return;
        }

        var authService = services.GetRequiredService<IAuthService>();
        var onboardingService = services.GetRequiredService<IOnboardingService>();
        var categoryService = services.GetRequiredService<ICategoryService>();
        var incomeService = services.GetRequiredService<IIncomeService>();
        var expenseService = services.GetRequiredService<IExpenseService>();

        var auth = await authService.SignupAsync(
            new SignupRequest(DemoEmail, DemoPassword, "Demo Account"), ip: null);
        var userId = auth.User.Id;

        var today = DateTimeOffset.UtcNow;
        await onboardingService.CompleteAsync(userId, new OnboardingCompleteRequest(
            new CurrencyInput("NGN", "₦"),
            "Africa/Lagos",
            "en-NG",
            450000m,
            new MonthInput(today.Year, today.Month),
            [
                new OnboardingCategoryInput("Rent", "FixedAccount", 30m, null, null),
                new OnboardingCategoryInput("Feeding", "Standard", 25m, null, null),
                new OnboardingCategoryInput("Transportation", "Standard", 15m, null, null),
                new OnboardingCategoryInput("Utility", "Standard", 10m, null, null),
                new OnboardingCategoryInput("Subscriptions", "Standard", 5m, null, null),
                new OnboardingCategoryInput("Savings", "Standard", 15m, null, null),
            ]));

        var categories = await categoryService.GetCategoriesAsync(userId, includeArchived: false);
        var feeding = categories.First(c => c.Name == "Feeding").Id;
        var transport = categories.First(c => c.Name == "Transportation").Id;
        var subscriptions = categories.First(c => c.Name == "Subscriptions").Id;

        // Onboarding only records the allocation percentages, it does not post an
        // actual transaction, this app never assumes money moved, it only ever
        // records what you tell it happened. "Allocatable" is the income type that
        // splits across categories by those percentages, matching this month's
        // real fixed income (see IncomeService.CreateAsync); "Flexible" income
        // (bonuses, side jobs) goes to the Flexible Pool instead, unsplit.
        await incomeService.CreateAsync(userId, new LogIncomeRequest("Allocatable", 450000m, "Salary", null));

        var expenses = new (decimal Amount, string Description, Guid CategoryId)[]
        {
            (18000m, "Weekly groceries", feeding),
            (6500m, "Rice and beans run", feeding),
            (4000m, "Bus fare, this week", transport),
            (3500m, "Ride-hailing to work", transport),
            (4500m, "Streaming subscription", subscriptions),
        };

        foreach (var expense in expenses)
        {
            await expenseService.CreateAsync(userId, new LogExpenseRequest(
                expense.Amount, expense.Description, "Category", expense.CategoryId, null, null, null));
        }
    }
}
