using HealthTech.Data;
using HealthTech.IService;
using HealthTech.Models;
using HealthTech.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using HealthTech.Services;

var builder = WebApplication.CreateBuilder(args);

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "SmartDoc API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// PostgreSQL database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// JWT Authentication
var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"]);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

// Register services
builder.Services.AddScoped<IHealthAIService, HealthAIService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IQuizService, QuizService>();
builder.Services.AddScoped<IDietaryLifestyle, DietaryLifestyleService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartDoc API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Seed quiz categories
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    SeedQuizCategories(context);
}

app.Run();

// Seed method for quiz categories
void SeedQuizCategories(ApplicationDbContext context)
{
    if (!context.QuizCategories.Any())
    {
        var categories = new List<QuizCategory>
        {
            new QuizCategory { Name = "Anatomy", IsMedical = true },
            new QuizCategory { Name = "Physiology", IsMedical = true },
            new QuizCategory { Name = "Biochemistry", IsMedical = true },
            new QuizCategory { Name = "Pathology", IsMedical = true },
            new QuizCategory { Name = "Pharmacology", IsMedical = true },
            new QuizCategory { Name = "Clinical Chemistry", IsMedical = true },
            new QuizCategory { Name = "Hematology", IsMedical = true },
            new QuizCategory { Name = "Microbiology", IsMedical = true },
            new QuizCategory { Name = "Medicine", IsMedical = true },
            new QuizCategory { Name = "Surgery", IsMedical = true },
            new QuizCategory { Name = "Community Health", IsMedical = true },
            new QuizCategory { Name = "Prosthodontics", IsMedical = false },
            new QuizCategory { Name = "Orthodontics", IsMedical = false },
            new QuizCategory { Name = "Pedodontics", IsMedical = false },
            new QuizCategory { Name = "Conservative Dentistry", IsMedical = false },
            new QuizCategory { Name = "Oral Medicine", IsMedical = false },
            new QuizCategory { Name = "Oral Surgery", IsMedical = false },
            new QuizCategory { Name = "Periodontology", IsMedical = false },
            new QuizCategory { Name = "Oral Biology", IsMedical = false }
        };
        context.QuizCategories.AddRange(categories);
        context.SaveChanges();
    }
}