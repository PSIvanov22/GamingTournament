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

    RoleManager<IdentityRole> roleManager =
        services.GetRequiredService<
            RoleManager<IdentityRole>
        >();

    UserManager<ApplicationUser> userManager =
        services.GetRequiredService<
            UserManager<ApplicationUser>
        >();

    string[] roles =
    {
        "Admin",
        "Player"
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

    List<ApplicationUser> users =
        await userManager.Users.ToListAsync();

    foreach (ApplicationUser user in users)
    {
        bool isAdmin =
            await userManager.IsInRoleAsync(
                user,
                "Admin"
            );

        bool isPlayer =
            await userManager.IsInRoleAsync(
                user,
                "Player"
            );

        if (!isAdmin && !isPlayer)
        {
            await userManager.AddToRoleAsync(
                user,
                "Player"
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