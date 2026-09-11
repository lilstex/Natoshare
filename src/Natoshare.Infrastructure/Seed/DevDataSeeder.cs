using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Natoshare.Domain.Identity;

namespace Natoshare.Infrastructure.Seed;

// Makes sure the "User" and "Admin" roles exist, and that there is one admin account
// ready to log in with on a fresh database. This is only meant for local development,
// the Api project only calls this when the environment is Development.
public static class DevDataSeeder
{
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
}
