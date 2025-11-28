using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SftpApi.Data;
using SftpApi.Logger;
using SftpApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Logging.ClearProviders();
builder.Logging.AddProvider(new SimpleFileLoggerProvider(@"D:\Rohan\PublishForStudy\logs\myapp.log"));

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddTransient<SftpRetryService>();
builder.Services.AddTransient<SftpClientFactory>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHangfire(options =>
    options.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHangfireServer();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SFTP API V1");
        c.RoutePrefix = "swagger";
    });
}

//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire");

app.MapGet("/", context =>
{
    context.Response.Redirect("/SftpMvcCustom/Index");
    return Task.CompletedTask;
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=SftpMvcCustom}/{action=Index}/{id?}");

app.Run();
