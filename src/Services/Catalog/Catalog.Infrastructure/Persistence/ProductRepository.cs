using Catalog.Core.Application.Abstractions;
using Catalog.Core.Domain;
using EShop.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

internal sealed class ProductRepository(CatalogDbContext db) : IProductRepository
{
    public Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return CatalogErrors.ConcurrencyConflict;
        }
    }
}
