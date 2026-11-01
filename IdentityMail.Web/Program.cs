using IdentityMail.Web.Context;
using IdentityMail.Web.CustomValidation;
using IdentityMail.Web.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<AppDbContext>(options=>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    options.UseSqlServer(connectionString);
});

//builder.Services.AddAuthentication().AddCookie(options =>
//{
//    options.LoginPath = "/Auth/Login";
//    options.LogoutPath = "Auth/Logout";
//    options.Cookie.Name = "IdentityMailCookie";

//});



builder.Services.AddIdentity<AppUser, AppRole>(options =>
    {
        options.User.RequireUniqueEmail = true;

    }).AddEntityFrameworkStores<AppDbContext>()
    .AddErrorDescriber<CustomErrorDescriber>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(config =>
{
    config.LoginPath = "/Auth/Login";
    config.LogoutPath = "/Auth/Logout";
    config.Cookie.Name = "IdentityMailCookie";
});


builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    string[] roleNames = { "Admin", "User" };
    foreach (var roleName in roleNames)
    {
        var exists = roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult();
        if (!exists)
        {
            roleManager.CreateAsync(new AppRole { Name = roleName }).GetAwaiter().GetResult();
        }
    }

    const string adminEmail = "admin@hmail.com";
    const string adminPassword = "Admin123!";
    var admin = userManager.FindByEmailAsync(adminEmail).GetAwaiter().GetResult();
    if (admin == null)
    {
        admin = new AppUser
        {
            Email = adminEmail,
            UserName = "admin",
            FirstName = "Admin",
            LastName = ""
        };
        userManager.CreateAsync(admin, adminPassword).GetAwaiter().GetResult();
        admin = userManager.FindByEmailAsync(adminEmail).GetAwaiter().GetResult();
    }
    if (admin != null)
    {
        if (admin.LastName == "HMail")
        {
            admin.LastName = "";
            userManager.UpdateAsync(admin).GetAwaiter().GetResult();
        }
        if (!userManager.IsInRoleAsync(admin, "User").GetAwaiter().GetResult())
        {
            userManager.AddToRoleAsync(admin, "User").GetAwaiter().GetResult();
        }
        if (!userManager.IsInRoleAsync(admin, "Admin").GetAwaiter().GetResult())
        {
            userManager.AddToRoleAsync(admin, "Admin").GetAwaiter().GetResult();
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
