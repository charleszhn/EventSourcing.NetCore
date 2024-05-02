using Core.Events;
using Microsoft.EntityFrameworkCore;
using Shipments.Packages.Events.External;
using Shipments.Packages.Requests;
using Shipments.Products;
using Shipments.Storage;

namespace Shipments.Packages;

public interface IPackageService
{
    Task<Package> SendPackage(SendPackage request, CancellationToken cancellationToken = default);
    Task DeliverPackage(DeliverPackage request, CancellationToken cancellationToken = default);
    Task<Package> GetById(Guid id, CancellationToken ct);
}

internal class PackageService(
    ShipmentsDbContext dbContext,
    IEventBus eventBus,
    IProductAvailabilityService productAvailabilityService)
    : IPackageService
{
    private readonly ShipmentsDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly IEventBus eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    private readonly IProductAvailabilityService productAvailabilityService = productAvailabilityService ??
                                                                              throw new ArgumentNullException(nameof(productAvailabilityService));

    private DbSet<Package> Packages => dbContext.Packages;

    public async Task<Package> GetById(Guid id, CancellationToken ct)
    {
        var package = await dbContext.Packages
            .Include(p => p.ProductItems)
            .SingleOrDefaultAsync(p => p.Id == id, ct);


        if (package == null)
            throw new InvalidOperationException($"Package with id {id} wasn't found");

        return package;
    }

    public async Task<Package> SendPackage(SendPackage request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.ProductItems?.Count == 0)
            throw new ArgumentException("It's not possible to send package with empty product items");

        if (!productAvailabilityService.IsEnoughOf(request.ProductItems!.ToArray()))
        {
            await Publish(new ProductWasOutOfStock(request.OrderId, DateTime.UtcNow), cancellationToken);
            throw new ArgumentOutOfRangeException(nameof(request.ProductItems), "Cannot send package - product was out of stock.");
        }

        var package = new Package
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            ProductItems = request.ProductItems.Select(pi =>
                new ProductItem {Id = Guid.NewGuid(), ProductId = pi.ProductId, Quantity = pi.Quantity}).ToList(),
            SentAt = DateTime.UtcNow
        };

        await Packages.AddAsync(package, cancellationToken);

        var @event = new PackageWasSent(package.Id,
            package.OrderId,
            package.ProductItems,
            package.SentAt);

        await SaveChangesAndPublish(@event, cancellationToken);

        return package;
    }

    public async Task DeliverPackage(DeliverPackage request, CancellationToken cancellationToken)
    {
        var package = await GetById(request.Id, cancellationToken);

        Packages.Update(package);

        await SaveChanges(cancellationToken);
    }

    private async Task SaveChangesAndPublish<TEvent>(TEvent @event, CancellationToken cancellationToken) where TEvent : notnull
    {
        await SaveChanges(cancellationToken);

        await Publish(@event, cancellationToken);
    }

    private async Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken) where TEvent : notnull
    {
        //TODO: metadata should be taken by event bus internally
        var eventEnvelope = new EventEnvelope<TEvent>(@event, new EventMetadata(Guid.NewGuid().ToString(), 0, 0, null));

        await eventBus.Publish(eventEnvelope, cancellationToken);
    }

    private Task SaveChanges(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
