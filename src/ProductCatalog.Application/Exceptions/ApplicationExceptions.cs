namespace ProductCatalog.Application.Exceptions;

public sealed class ProductNotFoundException(Guid id)
    : Exception($"Produto {id} não encontrado.");

public sealed class ProductConcurrencyException(Exception? innerException = null)
    : Exception("O produto foi alterado por outra operação. Consulte a versão atual e tente novamente.", innerException);
