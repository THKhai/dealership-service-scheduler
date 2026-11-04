using Scheduler.Application.Domain.Entities;

namespace Scheduler.Application.Abstractions;

public interface ICustomerRepository
{
    /// <summary>Customers are matched by email (9.3).</summary>
    Task<Customer?> GetByEmailAsync(string email, CancellationToken ct);

    /// <summary>Registers a customer. If the email was registered concurrently, returns that customer instead.</summary>
    Task<Customer> AddAsync(Customer customer, CancellationToken ct);
}
