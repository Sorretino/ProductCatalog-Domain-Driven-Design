using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Api.Contracts;
using ProductCatalog.Application.DTOs;
using ProductCatalog.Application.Features.Products.Commands;
using ProductCatalog.Application.Features.Products.Handlers;
using ProductCatalog.Application.Features.Products.Queries;

namespace ProductCatalog.Api.Controllers;

[ApiController]
[Route("api/products")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class ProductsController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "catalog.read")]
    [ProducesResponseType<PagedResult<ProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProductDto>>> List([FromServices] ListProductsHandler handler,
        CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] bool? isActive = null) =>
        Ok(await handler.HandleAsync(new ListProductsQuery(page, pageSize, search, isActive), cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "catalog.read")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> Get(Guid id, [FromServices] GetProductHandler handler,
        CancellationToken cancellationToken)
    {
        var product = await handler.HandleAsync(new GetProductQuery(id), cancellationToken);
        SetEtag(product.Version);
        return Ok(product);
    }

    [HttpPost]
    [Authorize(Policy = "catalog.write")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest request,
        [FromServices] CreateProductHandler handler, CancellationToken cancellationToken)
    {
        var product = await handler.HandleAsync(new CreateProductCommand(request.Name, request.Description,
            request.Price, request.IsActive), cancellationToken);
        SetEtag(product.Version);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "catalog.write")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult<ProductDto>> Update(Guid id, UpdateProductRequest request,
        [FromServices] UpdateProductHandler handler, CancellationToken cancellationToken,
        [FromHeader(Name = "If-Match")] string? ifMatch = null)
    {
        var product = await handler.HandleAsync(new UpdateProductCommand(id, request.Name, request.Description,
            request.Price, request.IsActive, ParseVersion(ifMatch)), cancellationToken);
        SetEtag(product.Version);
        return Ok(product);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "catalog.write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeleteProductHandler handler,
        CancellationToken cancellationToken, [FromHeader(Name = "If-Match")] string? ifMatch = null)
    {
        await handler.HandleAsync(new DeleteProductCommand(id, ParseVersion(ifMatch)), cancellationToken);
        return NoContent();
    }

    private void SetEtag(Guid version) => Response.Headers.ETag = $"\"{version:D}\"";

    private static Guid ParseVersion(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            throw new PreconditionException(StatusCodes.Status428PreconditionRequired,
                "Envie If-Match com o ETag obtido na consulta do produto.");
        }

        if (header.Length != 38 || header[0] != '"' || header[^1] != '"'
            || !Guid.TryParseExact(header[1..^1], "D", out var version) || version == Guid.Empty)
        {
            throw new PreconditionException(StatusCodes.Status400BadRequest,
                "If-Match deve conter um único ETag forte no formato UUID entre aspas.");
        }

        return version;
    }
}
