using FluentAssertions;
using Gearify.CatalogService.Application.Commands;
using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Gearify.CatalogService.UnitTests;

public class CreateProductCommandHandlerTests
{
    private readonly IProductRepository _repository;
    private readonly ILogger<CreateProductCommandHandler> _logger;
    private readonly CreateProductCommandHandler _handler;

    public CreateProductCommandHandlerTests()
    {
        _repository = Substitute.For<IProductRepository>();
        _logger = Substitute.For<ILogger<CreateProductCommandHandler>>();
        _handler = new CreateProductCommandHandler(_repository, _logger);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccess()
    {
        // Arrange
        var command = new CreateProductCommand(
            TenantId: "test-tenant",
            Sku: "TEST-SKU-001",
            Name: "Test Product",
            Description: "Test Description",
            Category: "test-category",
            Brand: "Test Brand",
            Price: 99.99m,
            CompareAtPrice: 149.99m,
            Tags: new List<string> { "test" },
            Attributes: new Dictionary<string, string> { { "color", "red" } }
        );

        _repository.CreateAsync(Arg.Any<Product>()).Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ProductId.Should().NotBeNullOrEmpty();
        await _repository.Received(1).CreateAsync(Arg.Any<Product>());
    }

    [Fact]
    public async Task Handle_RepositoryThrowsException_ReturnsFailure()
    {
        // Arrange
        var command = new CreateProductCommand(
            TenantId: "test-tenant",
            Sku: "TEST-SKU-001",
            Name: "Test Product",
            Description: "Test Description",
            Category: "test-category",
            Brand: "Test Brand",
            Price: 99.99m,
            CompareAtPrice: 149.99m,
            Tags: new List<string>(),
            Attributes: new Dictionary<string, string>()
        );

        _repository.CreateAsync(Arg.Any<Product>()).Returns(Task.FromException(new Exception("Database error")));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Database error");
    }
}
