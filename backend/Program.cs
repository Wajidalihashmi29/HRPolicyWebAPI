var builder = WebApplication.CreateBuilder(args);
 
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173", "http://localhost:5174")
              .AllowAnyHeader()
              .AllowAnyMethod());
});
 
var app = builder.Build();
app.UseCors("AllowReact");

// Health check endpoint
app.MapGet("/", () => new { status = "API is running", timestamp = DateTime.UtcNow });

app.MapControllers();
app.Run();