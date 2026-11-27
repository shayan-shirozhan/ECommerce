using ECommerce.API.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.API.Payments;

public sealed class DemoPaymentEndpointsTests
{
    [Test]
    public async Task MapDemoPaymentEndpoints_should_register_only_the_two_authorized_post_routes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();

        await using var app = builder.Build();
        app.MapDemoPaymentEndpoints();

        IEndpointRouteBuilder endpointRouteBuilder = app;

        var routes = endpointRouteBuilder.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        Assert.That(routes, Has.Length.EqualTo(2));

        var initiateRoute = routes.Single(route =>
            route.RoutePattern.RawText == "/api/demo/payments/orders/{orderId:guid}/initiate");
        var completeRoute = routes.Single(route =>
            route.RoutePattern.RawText == "/api/demo/payments/{paymentId:guid}/complete");

        AssertAuthorizedPostRoute(initiateRoute);
        AssertAuthorizedPostRoute(completeRoute);
    }

    private static void AssertAuthorizedPostRoute(RouteEndpoint route)
    {
        Assert.Multiple(() =>
        {
            Assert.That(route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods,
                Does.Contain("POST"));
            Assert.That(route.Metadata.GetOrderedMetadata<IAuthorizeData>(), Is.Not.Empty);
        });
    }
}
