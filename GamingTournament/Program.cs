using GamingTournament.Data;
using GamingTournament.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection"
    );

builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
        options.UseSqlite(connectionString)
);

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;

        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 6;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    IServiceProvider services = scope.ServiceProvider;

    // Създава базата и прилага всички миграции,
    // ако още не са приложени
    ApplicationDbContext context =
        services.GetRequiredService<
            ApplicationDbContext
        >();

    await context.Database.MigrateAsync();

    RoleManager<IdentityRole> roleManager =
        services.GetRequiredService<
            RoleManager<IdentityRole>
        >();

    UserManager<ApplicationUser> userManager =
        services.GetRequiredService<
            UserManager<ApplicationUser>
        >();

    // Играч е всеки влязъл потребител, който не е Admin,
    // затова роля "Player" не е нужна
    string[] roles =
    {
        "Admin"
    };

    foreach (string role in roles)
    {
        bool roleExists =
            await roleManager.RoleExistsAsync(role);

        if (!roleExists)
        {
            await roleManager.CreateAsync(
                new IdentityRole(role)
            );
        }
    }

    string adminEmail = "admin@gaming.com";
    string adminPassword = "Admin123!";

    ApplicationUser? adminUser =
        await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        IdentityResult result =
            await userManager.CreateAsync(
                adminUser,
                adminPassword
            );

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(
                adminUser,
                "Admin"
            );
        }
    }
    else
    {
        bool isAdmin =
            await userManager.IsInRoleAsync(
                adminUser,
                "Admin"
            );

        if (!isAdmin)
        {
            await userManager.AddToRoleAsync(
                adminUser,
                "Admin"
            );
        }
    }

}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern:
        "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.Run();