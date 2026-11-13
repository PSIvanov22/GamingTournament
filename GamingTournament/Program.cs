using GamingTournament.Data;
using GamingTournament.Middleware;
using GamingTournament.Models;
using GamingTournament.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Логване: конзола + файл logs/log-ГГГГММДД.txt (нов файл всеки ден,
// пази се последните 14). Всичко от Microsoft под Warning се скрива,
// иначе всяка SQL заявка влиза в лога
builder.Host.UseSerilog((hostContext, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(
                hostContext.HostingEnvironment.ContentRootPath,
                "logs",
                "log-.txt"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            outputTemplate:
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] " +
            "{Message:lj}{NewLine}{Exception}");
});

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

// Справки (Задание 7)
builder.Services.AddScoped<IReportService, ReportService>();

var app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    IServiceProvider services = scope.ServiceProvider;

    // Създава базата и таблиците от моделите
    // (миграциите са за SQL Server и не са съвместими със SQLite)
    ApplicationDbContext context =
        services.GetRequiredService<
            ApplicationDbContext
        >();

    await context.Database.EnsureCreatedAsync();

    // EnsureCreated не добавя нови таблици към вече съществуваща
    // база, затова журналът на грешките се създава отделно
    await DatabaseUpgrades.EnsureErrorLogsTableAsync(context);

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

    // Начални данни: отбори, играчи, треньори, заявки, мачове.
    // Само ако базата още няма отбори
    await SeedData.SeedAsync(context, userManager);
}

// Първият middleware: хваща всяка необработена грешка, записва я във
// файла и в базата и показва страница 500 вместо stack trace
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 404, 403 и т.н. без тяло -> /Home/HttpStatus?code=404
app.UseStatusCodePagesWithReExecute("/Home/HttpStatus", "?code={0}");

if (!app.Environment.IsDevelopment())
{
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