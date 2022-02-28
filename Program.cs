var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(options => {
    options.MaximumReceiveMessageSize = null;
});

var app = builder.Build();

app.UseFileServer();
app.MapHub<ChatHub>("/hub");
app.Run();