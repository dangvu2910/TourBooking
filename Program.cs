using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tourbooking.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
    await EnsureRolesAndAdminAsync(builder.Configuration, roleManager, userManager);
    await SeedToursAsync(dbContext);
}


app.Run();

static async Task EnsureRolesAndAdminAsync(
    IConfiguration configuration,
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager)
{
    var roles = new[] { "Admin", "User" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    var adminEmail = configuration["AdminUser:Email"];
    var adminPassword = configuration["AdminUser:Password"];
    if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
    {
        return;
    }

    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(adminUser, adminPassword);
        if (!createResult.Succeeded)
        {
            return;
        }
    }

    if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
    {
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }
}

static async Task SeedToursAsync(ApplicationDbContext dbContext)
{
    if (await dbContext.Tours.AnyAsync())
    {
        return;
    }

    var tours = new List<Tour>
    {
        new Tour
        {
            Name = "Sa Pa 3N2D",
            Location = "Lao Cai",
            Price = 3200000m,
            Description = "Kham pha Sa Pa, Fansipan va ban Cat Cat.",
            ImageUrl = "/images/hinh-anh-sapa-dep-thung-lung-hung-vi.jpg",
            CategoryId = 1
        },
        new Tour
        {
            Name = "Ha Long 2N1D",
            Location = "Quang Ninh",
            Price = 2500000m,
            Description = "Du thuyen tren vinh Ha Long, tham hang Sung Sot.",
            ImageUrl = "/images/df90d171-70a7-49f4-9725-290b24feb14f_halong.jpg",
            CategoryId = 1
        },
        new Tour
        {
            Name = "Da Nang 4N3D",
            Location = "Da Nang",
            Price = 4200000m,
            Description = "Ba Na Hills, Cau Vang va bai bien My Khe.",
            ImageUrl = "/images/92193f72-0b4b-4797-80d0-2cfa322aaf12_danang.jpg",
            CategoryId = 2
        },
        new Tour
        {
            Name = "Hoi An 2N1D",
            Location = "Quang Nam",
            Price = 1800000m,
            Description = "Pho co Hoi An, cho dem va song Hoai.",
            ImageUrl = "/images/839e713c-76f9-40d3-9764-758b56220ae0_hoian.webp",
            CategoryId = 2
        },
        new Tour
        {
            Name = "Hue 3N2D",
            Location = "Thua Thien Hue",
            Price = 2800000m,
            Description = "Kham pha Dai Noi, lang vua va am thuc Hue.",
            ImageUrl = "/images/1a85b0b7-62b8-41eb-ac0c-e64eb313df5b_hue.jpg",
            CategoryId = 2
        },
        new Tour
        {
            Name = "Quy Nhon 3N2D",
            Location = "Binh Dinh",
            Price = 3000000m,
            Description = "Ky Co, Eo Gio va thien duong bien Quy Nhon.",
            ImageUrl = "/images/3fc471e6-ab81-41a0-aec3-e670dd94c3a4_quynhon.jpg",
            CategoryId = 2
        },
        new Tour
        {
            Name = "Phu Quoc 4N3D",
            Location = "Kien Giang",
            Price = 5200000m,
            Description = "Bai Sao, Sunset Sanato va du ngoan dao.",
            ImageUrl = "/images/615d428f-d575-4362-9f21-fd32c6e44e88_phuquoc.webp",
            CategoryId = 3
        },
        new Tour
        {
            Name = "Nha Trang 3N2D",
            Location = "Khanh Hoa",
            Price = 3500000m,
            Description = "VinWonders, Hon Mun va tam bun.",
            ImageUrl = "/images/6637f545-5106-4d4a-8350-c3d31518c907_nhatrang.webp",
            CategoryId = 3
        },
        new Tour
        {
            Name = "Con Dao 3N2D",
            Location = "Ba Ria - Vung Tau",
            Price = 5500000m,
            Description = "Tham quan di tich va bien Con Dao.",
            ImageUrl = "/images/d229ab6e-8453-422a-aaa7-943097563bd6_condao.jpg",
            CategoryId = 3
        },
        new Tour
        {
            Name = "Vung Tau 2N1D",
            Location = "Ba Ria - Vung Tau",
            Price = 1500000m,
            Description = "Tuong Chua Kito, Hai dang va bai bien.",
            ImageUrl = "/images/a82908c6-9a7f-4ea0-94f2-d6c8c07c8e69_vungtau.jpg",
            CategoryId = 3
        },
        new Tour
        {
            Name = "Da Lat 3N2D",
            Location = "Lam Dong",
            Price = 2600000m,
            Description = "Ho Xuan Huong, vuon hoa va cafe cao nguyen.",
            ImageUrl = "/images/c853738e-77c6-4a2d-b0ae-ff9a5ff60b94_dalat.webp",
            CategoryId = 2
        },
        new Tour
        {
            Name = "Buon Ma Thuot 3N2D",
            Location = "Dak Lak",
            Price = 2900000m,
            Description = "Van hoa Tay Nguyen va thac Dray Nur.",
            ImageUrl = "/images/f5601ad1-5c70-4850-8826-b61f4c37c7ea_buonmathuat.webp",
            CategoryId = 2
        },
        new Tour
        {
            Name = "Ha Giang 4N3D",
            Location = "Ha Giang",
            Price = 3800000m,
            Description = "Cao nguyen da Dong Van va deo Ma Pi Leng.",
            ImageUrl = "/images/1f54d9c9-5508-4baf-a143-5ab31fa6073b_hagiang.jpg",
            CategoryId = 1
        },
        new Tour
        {
            Name = "Moc Chau 2N1D",
            Location = "Son La",
            Price = 1700000m,
            Description = "Doi che, mua hoa va nong truong.",
            ImageUrl = "/images/d3785924-8d64-4c9a-82d0-3cde250c3723_mocchau.jpg",
            CategoryId = 1
        },
        new Tour
        {
            Name = "Ta Nang - Phan Dung Trekking",
            Location = "Lam Dong",
            Price = 2400000m,
            Description = "Trekking doi co va cam trai qua dem.",
            ImageUrl = "/images/31a31140-e6a8-4664-a487-75c922deebec_trekkingtanang.webp",
            CategoryId = 1
        },
        new Tour
        {
            Name = "Binh Ba - Khanh Hoa",
            Location = "Khanh Hoa",
            Price = 2100000m,
            Description = "Dao Binh Ba va hai san tuoi song.",
            ImageUrl = "/images/9dc66f7c-2c48-4127-8d00-2cec5d80b4de_binhbakhanhhoa.jpg",
            CategoryId = 3
        },
        new Tour
        {
            Name = "Dao Ly Son 3N2D",
            Location = "Quang Ngai",
            Price = 3300000m,
            Description = "Cang To Vo, nui Thoi Loi va bien xanh.",
            ImageUrl = "/images/5201e709-4b24-46bc-a0aa-e87f460cec9a_daolyson.jpg",
            CategoryId = 3
        },
        new Tour
        {
            Name = "Mien Tay 2N1D",
            Location = "Can Tho",
            Price = 1600000m,
            Description = "Cho noi Cai Rang va vuon trai cay.",
            ImageUrl = "/images/8c18b822-a807-48e3-b471-ef035840c58c_mientay.jpeg",
            CategoryId = 3
        }
    };

    dbContext.Tours.AddRange(tours);
    await dbContext.SaveChangesAsync();
}
