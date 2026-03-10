using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Auth;
using TaskFlow.Application.Boards;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTaskFlowPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IBoardQueries, BoardQueries>();
        services.AddDbContext<TaskFlowDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("TaskFlow");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Set ConnectionStrings__TaskFlow before using TaskFlow persistence.");
            }

            options.UseSqlServer(connectionString, sql => sql.UseCompatibilityLevel(150));
        });

        return services;
    }
}
