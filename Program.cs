using TaskApp;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddSingleton<TaskStore>();

var app = builder.Build();
app.Services.GetRequiredService<TaskStore>().Initialize();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(error => error.Run(async context =>
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("Something went wrong. Please try again.");
    }));
}
app.UseStaticFiles();
app.MapRazorPages();
app.MapGet("/health", () => Results.Ok("Healthy"));
app.Run();