using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Catalog.Application;

public sealed record GetProductQuery(Guid Id) : IQuery<Result<ProductDto>>;
